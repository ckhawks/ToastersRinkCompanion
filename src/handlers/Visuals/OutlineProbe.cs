using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Read-only diagnostic for the Linework outline stack. Run <c>/outlineprobe</c> in
/// chat; it writes a full report to the temp directory and prints the path locally.
///
/// This exists to answer four questions before any brace-outline work starts:
///
/// <list type="number">
/// <item>Which <c>ScriptableRendererFeature</c>s are present on the game's
/// <c>universalRendererData</c>, and which of them are Linework features.</item>
/// <item>What is already in each feature's <c>settings.outlines</c> list, and whether
/// the puck outline runs in shared-color mode (which would make adding an entry to
/// that same settings object change how the puck outline looks).</item>
/// <item>Which URP rendering-layer bits are already in use, so we can claim a free
/// one for per-player outline toggling.</item>
/// <item>How many <c>Renderer</c>s a single player body actually has, since the mask
/// must be set on every one of them and that count sets the per-toggle cost.</item>
/// </list>
///
/// Everything here is reflection-based on purpose: the probe must never fail to
/// compile or throw into the game loop because a Linework or URP type moved.
/// Nothing is mutated.
/// </summary>
public static class OutlineProbe
{
    private const string Command = "/outlineprobe";
    private const string ReportFileName = "toaster-outline-probe.txt";

    /// <summary>Cap on how many scene renderers we sample for mask usage.</summary>
    private const int RendererScanLimit = 20000;

    /// <summary>Cap on how many PlayerMesh instances we describe in detail.</summary>
    private const int PlayerMeshDetailLimit = 2;

    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.Client_SendChatMessage))]
    public static class OutlineProbeCommandPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string content)
        {
            if (content == null) return true;
            if (!content.Trim().ToLowerInvariant().StartsWith(Command)) return true;

            Run();
            return false;
        }
    }

    /// <summary>
    /// Builds the report and writes it out. Never throws: any failure is reported
    /// into local chat instead.
    /// </summary>
    public static void Run()
    {
        var sb = new StringBuilder();

        try
        {
            sb.AppendLine("=== Toaster outline probe ===");
            sb.AppendLine($"Unity: {Application.unityVersion}");
            sb.AppendLine($"Scene renderer scan limit: {RendererScanLimit}");
            sb.AppendLine();

            AppendRendererFeatures(sb);
            sb.AppendLine();
            AppendRenderingLayerNames(sb);
            sb.AppendLine();
            AppendSceneMaskUsage(sb);
            sb.AppendLine();
            AppendPhysicsLayerUsage(sb);
            sb.AppendLine();
            AppendPlayerMeshBreakdown(sb);
            sb.AppendLine();
            AppendRuntimeFeatureMutability(sb);
            sb.AppendLine();
            AppendLiveRendererFeatures(sb);
            sb.AppendLine();
            AppendTaggedRenderers(sb);
        }
        catch (Exception e)
        {
            sb.AppendLine();
            sb.AppendLine($"PROBE ABORTED: {e}");
        }

        WriteReport(sb.ToString());
    }

    // ------------------------------------------------------------ live renderer

    /// <summary>
    /// The list on UniversalRendererData is only the recipe. URP builds a
    /// ScriptableRenderer from it and caches that, so a feature appended at runtime
    /// shows up in the recipe whether or not the renderer actually picked it up.
    /// This walks the live renderer hanging off the camera to tell the two apart.
    /// </summary>
    private static void AppendLiveRendererFeatures(StringBuilder sb)
    {
        sb.AppendLine("--- live renderer features (what is actually executing) ---");

        try
        {
            var camera = Camera.main;
            if (camera == null)
            {
                foreach (var c in Camera.allCameras)
                {
                    if (c != null && c.isActiveAndEnabled) { camera = c; break; }
                }
            }

            if (camera == null)
            {
                sb.AppendLine("no active camera found.");
                return;
            }

            sb.AppendLine($"camera: {camera.name}");

            object cameraData = null;
            foreach (var component in camera.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component.GetType().Name != "UniversalAdditionalCameraData") continue;
                cameraData = component;
                break;
            }

            if (cameraData == null)
            {
                sb.AppendLine("no UniversalAdditionalCameraData on the camera.");
                return;
            }

            var rendererProperty = cameraData.GetType()
                .GetProperty("scriptableRenderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            object renderer = rendererProperty?.GetValue(cameraData);
            if (renderer == null)
            {
                sb.AppendLine("scriptableRenderer unavailable.");
                return;
            }

            sb.AppendLine($"renderer: {renderer.GetType().Name}");

            // The backing list has moved between URP versions, so try both shapes.
            object features =
                renderer.GetType()
                    .GetProperty("rendererFeatures", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(renderer)
                ?? renderer.GetType()
                    .GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(renderer);

            if (!(features is IEnumerable list))
            {
                sb.AppendLine("could not read the renderer's feature list.");
                return;
            }

            bool foundOurs = false;
            int index = 0;

            foreach (var feature in list)
            {
                if (feature == null)
                {
                    sb.AppendLine($"  [{index++}] null");
                    continue;
                }

                string name = (feature as UnityEngine.Object)?.name ?? "(unnamed)";
                bool isActive = feature is ScriptableRendererFeature srf && srf.isActive;

                sb.AppendLine($"  [{index++}] name=\"{name}\" type={feature.GetType().Name} isActive={isActive}");

                if (name == "TR Puck Block Outline") foundOurs = true;
            }

            sb.AppendLine(foundOurs
                ? "VERDICT: our feature IS in the live renderer."
                : "VERDICT: our feature is NOT in the live renderer -- the renderer was "
                  + "built before we appended it and SetDirty did not force a rebuild.");
        }
        catch (Exception e)
        {
            sb.AppendLine($"live renderer probe failed: {e.Message}");
        }
    }

    /// <summary>
    /// Counts player renderers currently carrying the outline bit. Run this while
    /// someone is holding the block to see whether tagging landed at all.
    /// </summary>
    private static void AppendTaggedRenderers(StringBuilder sb)
    {
        sb.AppendLine("--- renderers currently carrying the outline bit ---");

        const uint outlineBit = 1u << 1;

        try
        {
            int tagged = 0;
            int total = 0;
            var owners = new List<string>();

            foreach (var mesh in UnityEngine.Object.FindObjectsByType<PlayerMesh>(FindObjectsSortMode.None))
            {
                if (mesh == null) continue;

                foreach (var r in mesh.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;

                    total++;
                    if ((r.renderingLayerMask & outlineBit) == 0) continue;

                    tagged++;
                    if (owners.Count < 8) owners.Add($"{mesh.name}/{r.gameObject.name} layer={r.gameObject.layer}");
                }
            }

            sb.AppendLine($"player renderers: {total}, carrying bit 1: {tagged}");
            foreach (var owner in owners) sb.AppendLine($"  {owner}");

            if (tagged == 0)
                sb.AppendLine("NOTE: nothing tagged right now. Run this while a player is blocking.");
        }
        catch (Exception e)
        {
            sb.AppendLine($"tag scan failed: {e.Message}");
        }
    }

    // ---------------------------------------------------------------- features

    /// <summary>
    /// Walks PostProcessing.universalRendererData.rendererFeatures and dumps each
    /// feature, its settings object, and any outline entries it holds.
    /// </summary>
    private static void AppendRendererFeatures(StringBuilder sb)
    {
        sb.AppendLine("--- renderer features ---");

        var ppmType = FindGameType("PostProcessing");
        if (ppmType == null)
        {
            sb.AppendLine("PostProcessing type not found.");
            return;
        }

        var ppm = FindFirstOfType(ppmType);
        if (ppm == null)
        {
            sb.AppendLine("No PostProcessing instance in the scene (are you in a match?).");
            return;
        }

        var rendererData = GetMember(ppm, "universalRendererData");
        if (rendererData == null)
        {
            sb.AppendLine("universalRendererData is null.");
            return;
        }

        sb.AppendLine($"universalRendererData: {Describe(rendererData)}");

        if (!(GetMember(rendererData, "rendererFeatures") is IEnumerable features))
        {
            sb.AppendLine("rendererFeatures is not enumerable.");
            return;
        }

        int index = 0;
        foreach (var feature in features)
        {
            if (feature == null)
            {
                sb.AppendLine($"[{index++}] <null feature>");
                continue;
            }

            var name = GetMember(feature, "name");
            var isActive = GetMember(feature, "isActive");
            sb.AppendLine($"[{index}] name=\"{name}\" type={feature.GetType().FullName} isActive={isActive}");

            AppendFeatureSettings(sb, feature);
            sb.AppendLine();
            index++;
        }

        if (index == 0) sb.AppendLine("(no renderer features)");
    }

    /// <summary>
    /// Dumps a feature's settings object: every scalar field, then each entry in an
    /// "outlines" list if one is present.
    /// </summary>
    private static void AppendFeatureSettings(StringBuilder sb, object feature)
    {
        var settings = GetMember(feature, "settings");
        if (settings == null)
        {
            sb.AppendLine("      (no settings field)");
            return;
        }

        sb.AppendLine($"      settings type: {settings.GetType().FullName}");

        foreach (var line in DescribeScalarFields(settings))
            sb.AppendLine($"        {line}");

        if (!(GetMember(settings, "outlines") is IEnumerable outlines))
        {
            sb.AppendLine("        (no outlines list)");
            return;
        }

        int outlineIndex = 0;
        foreach (var outline in outlines)
        {
            if (outline == null)
            {
                sb.AppendLine($"        outline[{outlineIndex++}] = null");
                continue;
            }

            sb.AppendLine($"        outline[{outlineIndex}] ({outline.GetType().Name})");
            foreach (var line in DescribeScalarFields(outline))
                sb.AppendLine($"          {line}");

            outlineIndex++;
        }

        if (outlineIndex == 0) sb.AppendLine("        (outlines list is empty)");
    }

    // ------------------------------------------------------------ layer names

    /// <summary>
    /// Unity 6 exposes the project's named rendering layers. If the API is present,
    /// this tells us directly which bits are spoken for by name.
    /// </summary>
    private static void AppendRenderingLayerNames(StringBuilder sb)
    {
        sb.AppendLine("--- defined rendering layer names ---");

        // The type moved between Unity versions; try the known homes before giving up.
        var maskType = FindTypeAnywhere("UnityEngine.Rendering.RenderingLayerMask")
                       ?? FindTypeAnywhere("UnityEngine.RenderingLayerMask")
                       ?? FindGameType("RenderingLayerMask");

        if (maskType == null)
        {
            sb.AppendLine("RenderingLayerMask type not found.");
            return;
        }

        sb.AppendLine($"(resolved {maskType.FullName} in {maskType.Assembly.GetName().Name})");

        var getNames = maskType.GetMethod(
            "GetDefinedRenderingLayerNames",
            BindingFlags.Public | BindingFlags.Static);

        if (getNames == null)
        {
            sb.AppendLine("GetDefinedRenderingLayerNames not available on this Unity version.");
            return;
        }

        try
        {
            if (getNames.Invoke(null, null) is string[] names)
            {
                for (int i = 0; i < names.Length; i++)
                    sb.AppendLine($"  bit {i} (1 << {i} = {1u << i}): \"{names[i]}\"");
            }
        }
        catch (Exception e)
        {
            sb.AppendLine($"  failed: {e.Message}");
        }
    }

    // ------------------------------------------------------------- mask usage

    /// <summary>
    /// Scans scene renderers and groups them by renderingLayerMask, so we can see
    /// which bits are actually in use versus merely defined.
    /// </summary>
    private static void AppendSceneMaskUsage(StringBuilder sb)
    {
        sb.AppendLine("--- renderingLayerMask usage across scene renderers ---");

        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (renderers == null || renderers.Length == 0)
        {
            sb.AppendLine("(no renderers found)");
            return;
        }

        sb.AppendLine($"total renderers: {renderers.Length}");

        var byMask = new Dictionary<uint, List<string>>();
        int scanned = 0;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (scanned++ >= RendererScanLimit) break;

            uint mask = r.renderingLayerMask;
            if (!byMask.TryGetValue(mask, out var examples))
            {
                examples = new List<string>();
                byMask[mask] = examples;
            }

            if (examples.Count < 3) examples.Add(r.gameObject.name);
        }

        foreach (var pair in byMask.OrderByDescending(p => p.Value.Count))
        {
            sb.AppendLine(
                $"  mask 0x{pair.Key:X8} (bits: {BitList(pair.Key)}) " +
                $"examples: {string.Join(", ", pair.Value)}");
        }

        uint union = 0;
        foreach (var mask in byMask.Keys) union |= mask;

        sb.AppendLine($"  union of all masks in use: 0x{union:X8} (bits: {BitList(union)})");
        sb.AppendLine($"  lowest free bit: {LowestFreeBit(union)}");
    }

    // ------------------------------------------------------------ player mesh

    /// <summary>
    /// Counts the renderers on a player body. Puck player models are not rigged --
    /// the body is a pile of individual MeshRenderers -- so this number is the
    /// per-toggle write count for the rendering-layer approach.
    /// </summary>
    private static void AppendPlayerMeshBreakdown(StringBuilder sb)
    {
        sb.AppendLine("--- player mesh renderer breakdown ---");

        var meshType = FindGameType("PlayerMesh");
        if (meshType == null)
        {
            sb.AppendLine("PlayerMesh type not found.");
            return;
        }

        var meshes = UnityEngine.Object.FindObjectsByType(
            meshType,
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (meshes == null || meshes.Length == 0)
        {
            sb.AppendLine("No PlayerMesh instances (are you in a match with players spawned?).");
            return;
        }

        sb.AppendLine($"PlayerMesh instances in scene: {meshes.Length}");

        int described = 0;
        foreach (var mesh in meshes)
        {
            if (described++ >= PlayerMeshDetailLimit) break;
            if (!(mesh is Component component)) continue;

            var all = component.GetComponentsInChildren<Renderer>(true);
            sb.AppendLine($"  \"{component.gameObject.name}\": {all.Length} renderers in children");

            var distinct = all
                .Where(r => r != null)
                .Select(r => r.renderingLayerMask)
                .Distinct()
                .ToList();

            sb.AppendLine($"    distinct masks: {string.Join(", ", distinct.Select(m => $"0x{m:X8}"))}");

            // The three body parts the brace ability actually cares about.
            foreach (var part in new[] { "PlayerHead", "PlayerTorso", "PlayerGroin" })
            {
                var partObj = GetMember(mesh, part);
                var partTransform = AsTransform(partObj);

                if (partTransform == null)
                {
                    sb.AppendLine($"    {part}: not resolvable");
                    continue;
                }

                var partRenderers = partTransform.GetComponentsInChildren<Renderer>(true);
                sb.AppendLine($"    {part}: {partRenderers.Length} renderers");
            }
        }
    }

    // --------------------------------------------------- physics layer usage

    /// <summary>
    /// Groups scene renderers by their GameObject physics layer. An Outline entry
    /// filters on BOTH RenderingLayer and layerMask, so if player bodies sit on a
    /// layer that the all-bits-set stick renderers do not share, the layerMask acts
    /// as the co-filter that makes a non-free rendering-layer bit usable anyway.
    /// </summary>
    private static void AppendPhysicsLayerUsage(StringBuilder sb)
    {
        sb.AppendLine("--- physics layer usage across scene renderers ---");

        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (renderers == null || renderers.Length == 0)
        {
            sb.AppendLine("(no renderers found)");
            return;
        }

        var byLayer = new Dictionary<int, List<string>>();
        var counts = new Dictionary<int, int>();
        int scanned = 0;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (scanned++ >= RendererScanLimit) break;

            int layer = r.gameObject.layer;

            if (!byLayer.TryGetValue(layer, out var examples))
            {
                examples = new List<string>();
                byLayer[layer] = examples;
                counts[layer] = 0;
            }

            counts[layer]++;
            if (examples.Count < 4) examples.Add(r.gameObject.name);
        }

        foreach (var pair in byLayer.OrderByDescending(p => counts[p.Key]))
        {
            string layerName = LayerMask.LayerToName(pair.Key);
            if (string.IsNullOrEmpty(layerName)) layerName = "<unnamed>";

            sb.AppendLine(
                $"  layer {pair.Key} (mask 0x{1 << pair.Key:X}) \"{layerName}\" " +
                $"count={counts[pair.Key]} examples: {string.Join(", ", pair.Value)}");
        }
    }

    // ------------------------------------------- runtime feature mutability

    /// <summary>
    /// Reports whether a second SoftOutline feature could be created and appended at
    /// runtime: is the rendererFeatures list mutable, can we reach the existing
    /// feature's ShaderResources to copy, and does a rebuild hook exist. Reports
    /// only -- nothing here mutates the renderer.
    /// </summary>
    private static void AppendRuntimeFeatureMutability(StringBuilder sb)
    {
        sb.AppendLine("--- runtime feature mutability ---");

        var ppmType = FindGameType("PostProcessing");
        var ppm = ppmType != null ? FindFirstOfType(ppmType) : null;
        var rendererData = ppm != null ? GetMember(ppm, "universalRendererData") : null;

        if (rendererData == null)
        {
            sb.AppendLine("universalRendererData unavailable.");
            return;
        }

        var featuresObj = GetMember(rendererData, "rendererFeatures");
        sb.AppendLine($"rendererFeatures type: {featuresObj?.GetType().FullName ?? "null"}");
        sb.AppendLine($"  is IList: {featuresObj is IList}");
        if (featuresObj is IList list)
            sb.AppendLine($"  IsReadOnly: {list.IsReadOnly}  Count: {list.Count}");

        // Rebuild hooks that would force URP to re-read the feature list.
        foreach (var methodName in new[] { "SetDirty", "OnValidate", "ReleaseRenderPasses", "Create" })
        {
            var m = rendererData.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            sb.AppendLine($"  rendererData.{methodName}: {(m == null ? "absent" : "present (" + (m.IsPublic ? "public" : "non-public") + ")")}");
        }

        // The SoftOutline instance we would clone shaders from.
        var soft = FindFeatureByTypeName(featuresObj, "SoftOutline");
        if (soft == null)
        {
            sb.AppendLine("  no SoftOutline feature found to clone from.");
            return;
        }

        var shaders = GetMember(soft, "shaders");
        sb.AppendLine($"  SoftOutline.shaders: {(shaders == null ? "null" : shaders.GetType().FullName)}");

        if (shaders != null)
        {
            foreach (var line in DescribeScalarFields(shaders))
                sb.AppendLine($"    {line}");

            // Shader references are what a runtime-created feature would otherwise lack.
            var shaderFields = shaders.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var f in shaderFields)
            {
                var value = f.GetValue(shaders) as Shader;
                sb.AppendLine($"    shader {f.Name}: {(value == null ? "null" : value.name)}");
            }
        }

        var createMethod = soft.GetType().GetMethod(
            "Create",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        sb.AppendLine($"  SoftOutline.Create: {(createMethod == null ? "absent" : "present")}");
    }

    private static object FindFeatureByTypeName(object featuresObj, string typeShortName)
    {
        if (!(featuresObj is IEnumerable features)) return null;

        foreach (var f in features)
            if (f != null && f.GetType().Name == typeShortName) return f;

        return null;
    }

    // --------------------------------------------------------------- plumbing

    /// <summary>
    /// Formats every field whose value is worth printing inline (scalars, enums,
    /// colors, masks). Reference-typed fields are summarised by type only.
    /// </summary>
    private static IEnumerable<string> DescribeScalarFields(object obj)
    {
        FieldInfo[] fields;
        try
        {
            fields = obj.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        catch
        {
            yield break;
        }

        foreach (var f in fields)
        {
            object value;
            try
            {
                value = f.GetValue(obj);
            }
            catch
            {
                continue;
            }

            if (value == null)
            {
                yield return $"{f.Name} = null";
                continue;
            }

            var t = value.GetType();

            if (t.IsPrimitive || t.IsEnum || value is string || value is Color || value is LayerMask)
            {
                yield return $"{f.Name} ({t.Name}) = {FormatValue(value)}";
                continue;
            }

            // RenderingLayerMask and similar wrapper structs: print the numeric value
            // if we can find one, since that is what we need to compare bits against.
            if (t.IsValueType && !(value is Vector2) && !(value is Vector3) && !(value is Vector4))
            {
                var inner = GetMember(value, "value") ?? GetMember(value, "m_Bits");
                yield return inner != null
                    ? $"{f.Name} ({t.Name}) = {inner}"
                    : $"{f.Name} ({t.Name}) = {value}";
                continue;
            }

            yield return $"{f.Name} : {t.Name}";
        }
    }

    private static string FormatValue(object value)
    {
        if (value is LayerMask lm) return $"{lm.value} (0x{lm.value:X})";
        if (value is Color c) return $"RGBA({c.r:F3}, {c.g:F3}, {c.b:F3}, {c.a:F3})";
        return value.ToString();
    }

    /// <summary>Lists the set bit indices of a mask, e.g. "0, 3, 17".</summary>
    private static string BitList(uint mask)
    {
        var bits = new List<int>();
        for (int i = 0; i < 32; i++)
            if ((mask & (1u << i)) != 0) bits.Add(i);

        return bits.Count == 0 ? "none" : string.Join(", ", bits);
    }

    private static string LowestFreeBit(uint used)
    {
        for (int i = 0; i < 32; i++)
            if ((used & (1u << i)) == 0) return $"{i} (1 << {i} = 0x{1u << i:X})";

        return "none free";
    }

    /// <summary>Field-then-property lookup, public and non-public, instance only.</summary>
    private static object GetMember(object obj, string name)
    {
        if (obj == null) return null;

        var type = obj.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        try
        {
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(obj);

            var prop = type.GetProperty(name, flags);
            if (prop != null && prop.CanRead) return prop.GetValue(obj, null);
        }
        catch
        {
            // Fall through: a member that throws on read is not worth the report.
        }

        return null;
    }

    /// <summary>Coerces a GameObject/Component/Transform-ish value to a Transform.</summary>
    private static Transform AsTransform(object value)
    {
        if (value is Transform t) return t;
        if (value is GameObject go) return go.transform;
        if (value is Component c) return c.transform;
        return null;
    }

    private static string Describe(object obj)
    {
        if (obj == null) return "null";
        var name = GetMember(obj, "name");
        return name != null
            ? $"\"{name}\" ({obj.GetType().Name})"
            : obj.GetType().FullName;
    }

    private static UnityEngine.Object FindFirstOfType(Type type)
    {
        try
        {
            return UnityEngine.Object.FindFirstObjectByType(type, FindObjectsInactive.Include);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Resolves a game type by short name across all loaded assemblies.</summary>
    private static Type FindGameType(string shortName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var t in types)
                if (t.Name == shortName) return t;
        }

        return null;
    }

    private static Type FindTypeAnywhere(string fullName)
    {
        var direct = Type.GetType(fullName);
        if (direct != null) return direct;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            catch
            {
                // Skip assemblies that refuse reflection.
            }
        }

        return null;
    }

    /// <summary>
    /// Writes the report to the temp directory so it never lands in the game or
    /// project folders, and echoes the path into local chat.
    /// </summary>
    private static void WriteReport(string report)
    {
        string path = null;

        try
        {
            path = Path.Combine(Path.GetTempPath(), ReportFileName);
            File.WriteAllText(path, report);
        }
        catch (Exception e)
        {
            Plugin.LogError($"OutlineProbe: could not write report: {e.Message}");
            path = null;
        }

        // The log always gets the full text, so the report survives even if the
        // file write failed.
        Plugin.Log("OutlineProbe report follows:");
        Plugin.Log(report);

        Plugin.AddLocalChatMessage(path != null
            ? $"<color=green><b>OUTLINE PROBE</b></color>  Report written to <b>{path}</b>"
            : "<color=orange><b>OUTLINE PROBE</b></color>  File write failed; report is in the game log.");
    }
}
