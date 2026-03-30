using System.Reflection;
using HarmonyLib;
using UnityEngine;

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
        }
    }
}