using System.Reflection;
using HarmonyLib;
using ToastersRinkCompanion.modifiers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToastersRinkCompanion.handlers;

public static class SpawnPuckKeybind
{
    private static float pressCooldown = 0.25f;

    static readonly FieldInfo _isFocusedField = typeof(UIView)
        .GetField("isFocused",
            BindingFlags.Instance | BindingFlags.NonPublic);


    [HarmonyPatch(typeof(PlayerInput), "Update")]
    public static class PlayerInputUpdatePatch
    {
        private static float lastPressTimeSpawnPuck = 0f;
        private static float lastPressTimeVote = 0f;

        [HarmonyPostfix]
        public static void Postfix(PlayerInput __instance)
        {
            // Only process for local player — with multiple players, Update runs for each PlayerInput
            if (__instance.OwnerClientId != Unity.Netcode.NetworkManager.Singleton.LocalClientId) return;
            if (!MessagingHandler.connectedToToastersRink) return;

            if (_isFocusedField == null)
            {
                Plugin.LogError($"could not locate isFocused field");
                return;
            }

            bool isFocusedChat = (bool)_isFocusedField.GetValue(MonoBehaviourSingleton<UIManager>.Instance.Chat);

            // When panel is open or chat focused, only process panel toggle/ESC
            if (isFocusedChat || ModifierPanelUI.IsVisible)
            {
                if (Keyboard.current != null)
                {
                    // Keybind listening mode — capture the next key press
                    if (SettingsTab.IsListening)
                    {
                        if (Keyboard.current.escapeKey.wasPressedThisFrame)
                        {
                            SettingsTab.CancelListening();
                        }
                        else if (Keyboard.current.anyKey.wasPressedThisFrame)
                        {
                            foreach (var key in Keyboard.current.allKeys)
                            {
                                if (key.wasPressedThisFrame)
                                {
                                    SettingsTab.ApplyListening($"<keyboard>/{key.name}");
                                    break;
                                }
                            }
                        }
                        return;
                    }

                    if (Plugin.panelAction.WasPressedThisFrame())
                        ModifierPanelUI.Toggle();
                    if (Keyboard.current.escapeKey.wasPressedThisFrame && ModifierPanelUI.IsVisible)
                        ModifierPanelUI.Hide();
                }
                return;
            }

            if (Plugin.spawnPuckAction.WasPressedThisFrame())
            {
                if (Time.time - lastPressTimeSpawnPuck > pressCooldown){
                    lastPressTimeSpawnPuck = Time.time;
                    NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/s", false, false);
                }
            }

            // Configurable keybinds
            {
                bool voteYes = Plugin.voteYesAction.WasPressedThisFrame();
                bool voteNo = Plugin.voteNoAction.WasPressedThisFrame();
                if (voteYes || voteNo)
                {
                    if (ModifierRegistry.CurrentVote != null && Time.time - lastPressTimeVote > pressCooldown)
                    {
                        lastPressTimeVote = Time.time;
                        ModifierMessaging.SendCastVote(voteYes);
                    }
                }

                // Panel toggle
                if (Plugin.panelAction.WasPressedThisFrame())
                {
                    ModifierPanelUI.Toggle();
                }

                // ESC — close modifier panel if open
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && ModifierPanelUI.IsVisible)
                {
                    ModifierPanelUI.Hide();
                }
            }
        }
    }
}