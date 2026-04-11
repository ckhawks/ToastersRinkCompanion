using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Suppresses the black "camera" overlay ONLY during single goalie team switches.
/// The server sends a "singlegoalie_switch" message before the switch, which sets
/// a short suppression window on the client.
/// </summary>
public static class SuppressCameraOverlay
{
    private static bool _suppressing;
    private static float _suppressUntil;

    public static void RegisterHandlers()
    {
        // The server sends an empty envelope; we just need the ping, not the payload.
        JsonMessageRouter.RegisterHandler("singlegoalie_switch",
            (_, _) => BeginSuppression());
    }

    /// <summary>
    /// Called by the messaging system when the server signals a single goalie switch
    /// is about to happen for the local player.
    /// </summary>
    public static void BeginSuppression()
    {
        _suppressing = true;
        _suppressUntil = Time.time + 1f; // 1 second window
    }

    [HarmonyPatch(typeof(UIOverlayManager), nameof(UIOverlayManager.ShowOverlay))]
    public static class ShowOverlayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string identifier)
        {
            if (identifier != "camera") return true;

            if (_suppressing)
            {
                if (Time.time > _suppressUntil)
                    _suppressing = false;
                else
                    return false; // suppress the overlay
            }

            return true;
        }
    }
}
