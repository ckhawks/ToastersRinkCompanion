using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class SpawnPuckKeybind
{
    private static float pressCooldown = 0.25f;
    
    static readonly FieldInfo _isFocusedField = typeof(UIComponent<UIChat>)
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
            
            bool isFocusedChat = (bool)_isFocusedField.GetValue(UIChat.Instance);
            if (isFocusedChat) return;
            
            if (Plugin.spawnPuckAction.WasPressedThisFrame())
            {
                if (Time.time - lastPressTimeSpawnPuck > pressCooldown){
                    lastPressTimeSpawnPuck = Time.time;
                    UIChat.Instance.Client_SendClientChatMessage("/s", false);
                }
            }
        }
    }
}