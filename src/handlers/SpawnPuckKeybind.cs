using System.Reflection;
using HarmonyLib;
using ToastersRinkCompanion.modifiers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToastersRinkCompanion.handlers;

public static class SpawnPuckKeybind
{
    private static float pressCooldown = 0.25f;

    private static bool IsKeyPressed(string keyName)
    {
        if (Keyboard.current == null || string.IsNullOrEmpty(keyName)) return false;
        return keyName.ToLower() switch
        {
            "f1" => Keyboard.current.f1Key.wasPressedThisFrame,
            "f2" => Keyboard.current.f2Key.wasPressedThisFrame,
            "f3" => Keyboard.current.f3Key.wasPressedThisFrame,
            "f4" => Keyboard.current.f4Key.wasPressedThisFrame,
            "f5" => Keyboard.current.f5Key.wasPressedThisFrame,
            "f6" => Keyboard.current.f6Key.wasPressedThisFrame,
            "f7" => Keyboard.current.f7Key.wasPressedThisFrame,
            "f8" => Keyboard.current.f8Key.wasPressedThisFrame,
            "f9" => Keyboard.current.f9Key.wasPressedThisFrame,
            "f10" => Keyboard.current.f10Key.wasPressedThisFrame,
            "f11" => Keyboard.current.f11Key.wasPressedThisFrame,
            "f12" => Keyboard.current.f12Key.wasPressedThisFrame,
            _ => false,
        };
    }

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
            if (!MessagingHandler.connectedToToastersRink) return;

            if (_isFocusedField == null)
            {
                Plugin.LogError($"could not locate isFocused field");
                return;
            }

            bool isFocusedChat = (bool)_isFocusedField.GetValue(MonoBehaviourSingleton<UIManager>.Instance.Chat);
            if (isFocusedChat) return;

            if (Plugin.spawnPuckAction.WasPressedThisFrame())
            {
                if (Time.time - lastPressTimeSpawnPuck > pressCooldown){
                    lastPressTimeSpawnPuck = Time.time;
                    NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/s", false, false);
                }
            }

            // Configurable keybinds
            if (Keyboard.current != null)
            {
                var settings = Plugin.modSettings;
                bool voteYes = IsKeyPressed(settings.voteYesKeybind);
                bool voteNo = IsKeyPressed(settings.voteNoKeybind);
                if (voteYes || voteNo)
                {
                    if (ModifierRegistry.CurrentVote != null && Time.time - lastPressTimeVote > pressCooldown)
                    {
                        lastPressTimeVote = Time.time;
                        ModifierMessaging.SendCastVote(voteYes);
                    }
                }

                // Panel toggle
                if (IsKeyPressed(settings.panelKeybind))
                {
                    ModifierPanelUI.Toggle();
                }

                // ESC — close modifier panel if open
                if (Keyboard.current.escapeKey.wasPressedThisFrame && ModifierPanelUI.IsVisible)
                {
                    ModifierPanelUI.Hide();
                }
            }
        }
    }
}