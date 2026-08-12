using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Client half of the puck-block bind. Sends one message per key edge; the server
/// owns every rule about whether the block is actually allowed.
///
/// Edge-triggered on purpose -- there is no per-frame traffic while held.
/// </summary>
public static class PuckBlockInput
{
    public const string InputMessageType = "puck_block_input";

    private static bool _isDown;

    /// <summary>
    /// Whether the server currently has the puck-block modifier on, mirrored from the
    /// modifier state broadcast. Without this the bind fires on every server: the
    /// message goes nowhere, but the shield still flashes and times out, which reads
    /// as the ability being broken rather than absent.
    /// </summary>
    private static bool _modifierEnabled;

    public static void SetModifierEnabled(bool enabled)
    {
        if (_modifierEnabled == enabled) return;

        _modifierEnabled = enabled;

        // Turned off mid-hold: drop the hold so neither side is left thinking we are
        // still blocking.
        if (!enabled) ForceRelease();
    }

    static readonly FieldInfo _isFocusedField = typeof(UIView)
        .GetField("isFocused", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void RegisterHandlers()
    {
        // Disconnecting mid-hold would otherwise leave _isDown stuck true, and the next
        // press after reconnecting would look like no edge at all -- the bind would
        // silently do nothing until you pressed and released once.
        EventManager.AddEventListener("Event_OnClientDisconnected",
            new Action<Dictionary<string, object>>(_ => _isDown = false));
    }

    [Serializable]
    public class PuckBlockInputPayload
    {
        public bool down;
    }

    /// <summary>
    /// Releases the bind without waiting for a key-up. Used when the window loses
    /// input focus or the client disconnects, so a dropped release cannot leave the
    /// player blocking forever.
    /// </summary>
    public static void ForceRelease()
    {
        if (!_isDown) return;

        SetDown(false);
    }

    /// <summary>Single edge path, so the indicator can never disagree with what we sent.</summary>
    private static void SetDown(bool down)
    {
        _isDown = down;
        PuckBlockIndicator.OnLocalInput(down);
        Send(down);
    }

    private static void Send(bool down)
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient) return;

            JsonMessageRouter.SendMessage(
                InputMessageType,
                NetworkManager.ServerClientId,
                new PuckBlockInputPayload { down = down });
        }
        catch (Exception e)
        {
            Plugin.LogError($"PuckBlockInput send failed: {e.Message}");
        }
    }

    [HarmonyPatch(typeof(PlayerInput), "Update")]
    public static class PuckBlockInputUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerInput __instance)
        {
            try
            {
                if (NetworkManager.Singleton == null) return;
                if (__instance.OwnerClientId != NetworkManager.Singleton.LocalClientId) return;
                if (!MessagingHandler.connectedToToastersRink) return;
                if (!_modifierEnabled) return;
                if (Plugin.puckBlockAction == null) return;

                // Typing in chat must not hold the block down.
                bool chatFocused = false;
                if (_isFocusedField != null)
                {
                    var chat = MonoBehaviourSingleton<UIManager>.Instance?.Chat;
                    if (chat != null) chatFocused = (bool)_isFocusedField.GetValue(chat);
                }

                if (chatFocused || ToastersRinkCompanion.modifiers.ModifierPanelUI.IsVisible)
                {
                    ForceRelease();
                    return;
                }

                bool pressed = Plugin.puckBlockAction.IsPressed();

                if (pressed == _isDown) return;

                SetDown(pressed);
            }
            catch (Exception e)
            {
                Plugin.LogError($"PuckBlockInput update failed: {e.Message}");
            }
        }
    }
}
