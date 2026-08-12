using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Linework.SoftOutline;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Outline = Linework.SoftOutline.Outline;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Draws the outline on players who are holding the puck-block bind.
///
/// The selection mechanism is URP rendering layers, not per-object registration.
/// We add our own <c>SoftOutline</c> renderer feature whose single Outline entry
/// matches one rendering-layer bit, then tagging a player is just setting that bit
/// on their body renderers -- allocation-free, and only on state change.
///
/// Two findings from the probe shape this:
///
/// <list type="bullet">
/// <item>No rendering-layer bit is globally free: the stick tape renderers have all
/// 32 bits set. They live on the "Stick" physics layer though, so the Outline's
/// layerMask excludes exactly that layer and our bit is effectively ours. Note the
/// co-filter cannot be narrowed to "Player Mesh": a body's renderers span layers 0
/// and 9, and filtering to 9 alone drops the whole head group and draws nothing.</item>
/// <item>Outline <i>style</i> (intensity, blend mode, blur) lives on the shared
/// settings object, only color and occlusion are per-entry. So we stand up our own
/// feature with its own settings rather than adding an entry to the puck outline's,
/// which would have changed how the puck outline looks.</item>
/// </list>
/// </summary>
public static class PuckBlockOutline
{
    public const string StateMessageType = "puck_block_state";

    /// <summary>
    /// The rendering-layer bit we claim. Only bit 0 ("Default") is a named layer in
    /// this project, and every player renderer sits at exactly 0x1, so bit 1 is
    /// unused on the objects we filter down to.
    /// </summary>
    private const int OutlineLayerBit = 1;

    private const uint OutlineLayerMask = 1u << OutlineLayerBit;

    /// <summary>
    /// Fallback ability color, used before settings load. Deliberately not a team color
    /// -- see the design notes. The live value comes from settings so players can pick
    /// something that reads for them against the ice.
    /// </summary>
    private static readonly Color DefaultOutlineColor = new(1f, 0.86f, 0.2f, 1f);

    /// <summary>The configured color, shared with the local player's shield.</summary>
    public static Color AbilityColor
    {
        get
        {
            string hex = Plugin.modSettings?.puckBlockColor;
            if (string.IsNullOrEmpty(hex)) return DefaultOutlineColor;

            var color = modifiers.UIHelpers.ParseHexColor(hex);
            return new Color(color.r, color.g, color.b, 1f);
        }
    }

    /// <summary>
    /// The live Outline entry. Held so a color change can be pushed onto the running
    /// feature -- it is only built once, so rebuilding is not an option.
    /// </summary>
    private static Outline _outlineEntry;

    /// <summary>The live settings object, which owns sharedColor.</summary>
    private static SoftOutlineSettings _outlineSettings;

    private static bool _featureInstalled;
    private static ScriptableRendererFeature _feature;

    /// <summary>Renderers we have tagged, per client, so release is exact.</summary>
    private static readonly Dictionary<ulong, List<Renderer>> _tagged = new();

    [Serializable]
    public class PuckBlockStatePayload
    {
        public ulong clientId;
        public bool blocking;
    }

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<PuckBlockStatePayload>(StateMessageType,
            (_, payload) => Apply(payload));
    }

    // ---------------------------------------------------------------- applying

    private static void Apply(PuckBlockStatePayload payload)
    {
        if (payload == null) return;

        try
        {
            // Your own body is never outlined (see GetBodyRenderers), so the pill above
            // the stamina bar is the only feedback you get for your own block.
            if (NetworkManager.Singleton != null &&
                payload.clientId == NetworkManager.Singleton.LocalClientId)
            {
                PuckBlockIndicator.OnServerState(payload.blocking);
            }

            EnsureFeature();

            if (payload.blocking) Tag(payload.clientId);
            else Untag(payload.clientId);
        }
        catch (Exception e)
        {
            Plugin.LogError($"PuckBlockOutline apply failed: {e.Message}");
        }
    }

    private static void Tag(ulong clientId)
    {
        Untag(clientId);

        var renderers = GetBodyRenderers(clientId);
        if (renderers.Count == 0)
        {
            Plugin.LogWarning($"PuckBlockOutline: no renderers to tag for client {clientId}.");
            return;
        }

        foreach (var r in renderers)
            r.renderingLayerMask |= OutlineLayerMask;

        _tagged[clientId] = renderers;
    }

    private static void Untag(ulong clientId)
    {
        if (!_tagged.TryGetValue(clientId, out var renderers)) return;

        foreach (var r in renderers)
            if (r != null) r.renderingLayerMask &= ~OutlineLayerMask;

        _tagged.Remove(clientId);
    }

    /// <summary>
    /// Pushes the configured color onto the running feature. The Outline entry holds
    /// the color itself, so this takes effect on the next frame with no rebuild --
    /// including on a player who is mid-block while you change it.
    /// </summary>
    public static void RefreshColor()
    {
        if (_outlineEntry == null) return;

        try
        {
            var color = AbilityColor;

            SetField(_outlineEntry, "color", color);
            if (_outlineSettings != null) SetField(_outlineSettings, "sharedColor", color);

            // Linework rebuilds its materials off this notification. Without it the
            // fields change but the running pass keeps drawing the old color.
            NotifySettingsChanged();
        }
        catch (Exception e)
        {
            Plugin.LogError($"PuckBlockOutline could not apply color: {e.Message}");
        }
    }

    /// <summary>Drops every tag. Used on disconnect so nothing is left glowing.</summary>
    public static void ClearAll()
    {
        foreach (var clientId in _tagged.Keys.ToList())
            Untag(clientId);

        _tagged.Clear();
    }

    /// <summary>
    /// The body renderers for a client, minus the nameplate text. "Number" and
    /// "Username" sit on the Player Mesh layer too, so a blanket tag would outline
    /// the floating text above the player.
    /// </summary>
    private static List<Renderer> GetBodyRenderers(ulong clientId)
    {
        var result = new List<Renderer>();

        try
        {
            var player = PlayerManager.Instance?.GetPlayerByClientId(clientId);
            if (player?.PlayerBody?.PlayerMesh == null) return result;

            // The local player is skipped: their own body clips the first-person
            // camera, the same reason StarPlayerGlow leaves it alone.
            if (NetworkManager.Singleton != null &&
                clientId == NetworkManager.Singleton.LocalClientId) return result;

            foreach (var r in player.PlayerBody.PlayerMesh.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (IsNameplate(r)) continue;
                result.Add(r);
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"PuckBlockOutline could not collect renderers: {e.Message}");
        }

        return result;
    }

    private static bool IsNameplate(Renderer r)
    {
        string name = r.gameObject.name;
        if (name == "Number" || name == "Username") return true;

        // TMP renders through a MeshRenderer as well, so check for the text component.
        return r.GetComponent<TMPro.TMP_Text>() != null;
    }

    // ---------------------------------------------------------- feature install

    /// <summary>
    /// Stands up our SoftOutline feature once. The shaders are copied off the
    /// game's existing SoftOutline instance -- a runtime-constructed feature has no
    /// serialized ShaderResources of its own, and that is the only thing it lacks.
    /// </summary>
    private static void EnsureFeature()
    {
        if (_featureInstalled && _feature != null) return;

        var ppm = Object.FindFirstObjectByType<PostProcessing>();
        if (ppm == null) return;

        var rendererDataField = typeof(PostProcessing)
            .GetField("universalRendererData", BindingFlags.Instance | BindingFlags.NonPublic);

        if (!(rendererDataField?.GetValue(ppm) is UniversalRendererData rendererData))
        {
            Plugin.LogWarning("PuckBlockOutline: universalRendererData unavailable.");
            return;
        }

        // Already installed from a previous match in this process.
        var existingOurs = rendererData.rendererFeatures
            .FirstOrDefault(f => f != null && f.name == "TR Puck Block Outline");

        if (existingOurs != null)
        {
            _feature = existingOurs;
            _featureInstalled = true;
            return;
        }

        var donor = rendererData.rendererFeatures.FirstOrDefault(f => f is SoftOutline);
        if (donor == null)
        {
            Plugin.LogWarning("PuckBlockOutline: no SoftOutline feature to copy shaders from.");
            return;
        }

        var feature = ScriptableObject.CreateInstance<SoftOutline>();
        feature.name = "TR Puck Block Outline";

        // Shaders: the one thing CreateInstance cannot populate.
        var shadersField = typeof(SoftOutline)
            .GetField("shaders", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (shadersField == null || shadersField.GetValue(donor) == null)
        {
            Plugin.LogWarning("PuckBlockOutline: could not read donor ShaderResources.");
            Object.Destroy(feature);
            return;
        }

        shadersField.SetValue(feature, shadersField.GetValue(donor));

        var settings = BuildSettings(donor);
        if (settings == null)
        {
            Object.Destroy(feature);
            return;
        }

        var settingsField = typeof(SoftOutline)
            .GetField("settings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        settingsField?.SetValue(feature, settings);

        try
        {
            feature.Create();
        }
        catch (Exception e)
        {
            Plugin.LogError($"PuckBlockOutline: feature.Create() threw: {e.Message}");
        }

        feature.SetActive(true);
        rendererData.rendererFeatures.Add(feature);

        try
        {
            rendererData.SetDirty();
        }
        catch (Exception e)
        {
            Plugin.LogWarning($"PuckBlockOutline: SetDirty threw: {e.Message}");
        }

        _feature = feature;
        _featureInstalled = true;

        Plugin.Log($"PuckBlockOutline: installed outline feature on rendering-layer bit {OutlineLayerBit}.");
    }

    /// <summary>
    /// Our own settings object, so the puck outline's intensity and blend mode stay
    /// untouched. Style values are tuned for a player-sized silhouette rather than a
    /// puck: the puck runs additive at intensity 22.7, which would be blinding here.
    /// </summary>
    private static SoftOutlineSettings BuildSettings(ScriptableRendererFeature donor)
    {
        var settings = ScriptableObject.CreateInstance<SoftOutlineSettings>();

        SetField(settings, "injectionPoint", GetField(GetField(donor, "settings"), "injectionPoint"));
        SetField(settings, "type", GetField(GetField(donor, "settings"), "type"));
        SetField(settings, "hardness", 1f);
        SetField(settings, "intensity", 4f);
        SetField(settings, "kernelSize", 3);
        SetField(settings, "blurSpread", 1f);
        SetField(settings, "blurPasses", 2);
        SetField(settings, "gap", 0f);
        SetField(settings, "scaleWithResolution", true);

        // Set alongside the per-entry color below. Which of the two the shader actually
        // samples depends on disableColor, and the game ships no active entry to learn
        // the semantics from -- the first attempt set only the entry color and rendered
        // in this object's default green. Writing both is unambiguous.
        SetField(settings, "sharedColor", AbilityColor);

        var outline = ScriptableObject.CreateInstance<Outline>();
        outline.name = "TR Puck Block Outline Entry";

        SetField(outline, "isActive", true);
        SetField(outline, "disableColor", false);
        SetField(outline, "color", AbilityColor);
        SetField(outline, "layerMask", (LayerMask)BuildLayerMask());
        SetRenderingLayer(outline, OutlineLayerMask);

        _outlineEntry = outline;
        _outlineSettings = settings;

        var outlinesField = typeof(SoftOutlineSettings)
            .GetField("outlines", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (outlinesField == null)
        {
            Plugin.LogWarning("PuckBlockOutline: no outlines field on settings.");
            return null;
        }

        outlinesField.SetValue(settings, new List<Outline> { outline });
        return settings;
    }

    /// <summary>
    /// Fires the settings object's OnSettingsChanged callback, which is how Linework is
    /// told to rebuild materials. Best effort: if the hook is absent the color still
    /// applies on the next feature rebuild.
    /// </summary>
    private static void NotifySettingsChanged()
    {
        if (_outlineSettings == null) return;

        try
        {
            var field = typeof(SoftOutlineSettings)
                .GetField("OnSettingsChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            (field?.GetValue(_outlineSettings) as Action)?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.LogWarning($"PuckBlockOutline: settings-changed notify failed: {e.Message}");
        }
    }

    /// <summary>
    /// The physics-layer co-filter that runs alongside the rendering-layer bit.
    ///
    /// This was originally "Player Mesh" only, which drew nothing: a player body's
    /// renderers are spread across physics layers, and the ones under PlayerHead sit on
    /// Default, so the filter rejected almost everything we tagged.
    ///
    /// The bit still needs a co-filter, because the stick tape renderers have all 32
    /// rendering-layer bits set and would otherwise be outlined permanently. They live
    /// on "Stick", so excluding exactly that layer is enough, and it is the narrowest
    /// exclusion that keeps bit 1 ours.
    /// </summary>
    private static int BuildLayerMask()
    {
        int mask = ~0;

        int stickLayer = LayerMask.NameToLayer("Stick");
        if (stickLayer >= 0) mask &= ~(1 << stickLayer);

        return mask;
    }

    /// <summary>
    /// RenderingLayerMask is a wrapper struct; go through its implicit uint
    /// conversion rather than assuming a layout.
    /// </summary>
    private static void SetRenderingLayer(object target, uint mask)
    {
        var field = target.GetType()
            .GetField("RenderingLayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null) return;

        try
        {
            var op = field.FieldType.GetMethod("op_Implicit", new[] { typeof(uint) });
            object value = op != null ? op.Invoke(null, new object[] { mask }) : mask;
            field.SetValue(target, value);
        }
        catch (Exception e)
        {
            Plugin.LogWarning($"PuckBlockOutline: could not set RenderingLayer: {e.Message}");
        }
    }

    private static object GetField(object target, string name)
    {
        if (target == null) return null;

        var field = target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return field?.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        if (target == null || value == null) return;

        var field = target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        try
        {
            field?.SetValue(target, value);
        }
        catch (Exception e)
        {
            Plugin.LogWarning($"PuckBlockOutline: could not set {name}: {e.Message}");
        }
    }
}
