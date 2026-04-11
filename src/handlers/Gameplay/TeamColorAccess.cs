using System;
using System.Reflection;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Soft dependency wrapper around <c>ToasterReskinLoader.ToasterReskinLoaderAPI</c>.
/// If TRL isn't loaded we silently fall back to hardcoded defaults that match TRL's
/// own defaults (documented at <c>src/ToasterReskinLoaderAPI.cs</c>).
///
/// All access goes through reflection so this assembly doesn't need a compile-time
/// reference to TRL — the user's TRL install is optional.
/// </summary>
internal static class TeamColorAccess
{
    // TRL's defaults (same values as DefaultBlue / DefaultRed in TRL's API).
    private static readonly Color DefaultBlue = new Color(0.231f, 0.510f, 0.965f, 1f);
    private static readonly Color DefaultRed  = new Color(0.820f, 0.200f, 0.200f, 1f);

    private static bool _resolved;
    private static Type _apiType;
    private static PropertyInfo _enabledProp;
    private static PropertyInfo _blueProp;
    private static PropertyInfo _redProp;
    private static EventInfo _changedEvent;

    /// <summary>
    /// Fires when TRL notifies that team color settings changed.
    /// Only invoked if TRL is present.
    /// </summary>
    public static event Action OnTeamColorsChanged;

    public static bool IsAvailable
    {
        get
        {
            Resolve();
            return _apiType != null;
        }
    }

    public static bool TeamColorsEnabled
    {
        get
        {
            Resolve();
            if (_enabledProp == null) return false;
            try { return (bool)_enabledProp.GetValue(null); }
            catch { return false; }
        }
    }

    public static Color BlueTeamColor
    {
        get
        {
            Resolve();
            if (_blueProp == null) return DefaultBlue;
            try { return (Color)_blueProp.GetValue(null); }
            catch { return DefaultBlue; }
        }
    }

    public static Color RedTeamColor
    {
        get
        {
            Resolve();
            if (_redProp == null) return DefaultRed;
            try { return (Color)_redProp.GetValue(null); }
            catch { return DefaultRed; }
        }
    }

    // ---------------------------------------------------------------
    // Reflection setup
    // ---------------------------------------------------------------

    private static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;

        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _apiType = asm.GetType("ToasterReskinLoader.ToasterReskinLoaderAPI");
                if (_apiType != null) break;
            }

            if (_apiType == null)
            {
                Plugin.Log("[TeamColorAccess] ToasterReskinLoader not found — using default team colors");
                return;
            }

            _enabledProp = _apiType.GetProperty("TeamColorsEnabled", BindingFlags.Public | BindingFlags.Static);
            _blueProp    = _apiType.GetProperty("BlueTeamColor",    BindingFlags.Public | BindingFlags.Static);
            _redProp     = _apiType.GetProperty("RedTeamColor",     BindingFlags.Public | BindingFlags.Static);
            _changedEvent = _apiType.GetEvent("OnTeamColorsChanged", BindingFlags.Public | BindingFlags.Static);

            // Subscribe via a MethodInfo so we don't need a strongly-typed Action.
            if (_changedEvent != null)
            {
                var methodInfo = typeof(TeamColorAccess).GetMethod(
                    nameof(HandleTrlColorsChanged), BindingFlags.NonPublic | BindingFlags.Static);
                var handler = Delegate.CreateDelegate(_changedEvent.EventHandlerType, methodInfo);
                _changedEvent.AddEventHandler(null, handler);
            }

            Plugin.Log($"[TeamColorAccess] TRL API bound (enabled={TeamColorsEnabled})");
        }
        catch (Exception e)
        {
            Plugin.LogError($"[TeamColorAccess] Reflection setup failed: {e}");
            _apiType = null;
        }
    }

    private static void HandleTrlColorsChanged()
    {
        try { OnTeamColorsChanged?.Invoke(); }
        catch (Exception e) { Plugin.LogError($"[TeamColorAccess] OnTeamColorsChanged handler error: {e}"); }
    }
}
