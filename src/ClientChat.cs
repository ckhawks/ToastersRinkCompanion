using HarmonyLib;
using ToastersRinkCompanion.collectibles;
using ToastersRinkCompanion.handlers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToastersRinkCompanion;

public static class ClientChat
{
    public static SpectatorCamera SpectatorCamera;
    public static PlayerCamera PlayerCamera;
    
    [HarmonyPatch(typeof(UIChat), nameof(UIChat.Client_SendClientChatMessage))]
    public class UIChatPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(UIChat __instance, string message, bool useTeamChat)
        {
            string[] args = message.Split(' ');
            
            if (message.ToLower().StartsWith("/setkeybind"))
            {
                if (args.Length < 3)
                {
                    __instance.AddChatMessage($"Please what you would like to set a keybind for, and what the key is. /setkeybind spawnpuck q");
                    return false;
                }

                if (args[1].ToLower() != "spawnpuck")
                {
                    __instance.AddChatMessage($"At this moment, you can only customize the keybind of spawnpuck. /setkeybind spawnpuck q");
                    return false;
                }
                
                string newBindingPath = $"<keyboard>/{args[2].ToLower()}";
                Plugin.modSettings.spawnPuckKeybind = newBindingPath;
                Plugin.modSettings.Save();
                __instance.AddChatMessage($"Your keybind for spawnpuck has been set to {Plugin.modSettings.spawnPuckKeybind}. If this is not a valid input action, it will not work.");
                Plugin.spawnPuckAction.Disable();
                if (Plugin.spawnPuckAction.bindings.Count > 0)
                {
                    // Change the first binding
                    Plugin.spawnPuckAction.ChangeBinding(0).To(binding: new InputBinding(newBindingPath));
                }
                else
                {
                    // If no binding exists, add one
                    Plugin.spawnPuckAction.AddBinding(newBindingPath);
                }
                
                Plugin.spawnPuckAction.Enable();
                
                return false;
            }
            
            // else if (message.ToLower().StartsWith("/fuckgoals"))
            // {
            //     FuckGoals.FuckGoalsNow();
            // }
            // else if (message.ToLower().StartsWith("/collectible"))
            // {
            //     Player player = PlayerManager.Instance.GetLocalPlayer();
            //     OldCollectibleRenderer.ShowCollectiblePrototype(player);
            //     return false;
            // } 
            // else if (message.ToLower().StartsWith($"/opencase"))
            // {
            //     Player player = PlayerManager.Instance.GetLocalPlayer();
            //     Opening.PlayOpeningForAt(player.Stick.transform.position, player);
            // }
            else if (message.ToLower().StartsWith("/logcamera"))
            {
                if (PlayerCamera != null)
                {
                    
                    Camera playerCamComponent = PlayerCamera.GetComponent<Camera>();
                    Plugin.Log($"Playercamera {playerCamComponent.cullingMatrix}");
                }
                else
                {
                    Plugin.Log($"playerCamera is null");
                }

                if (SpectatorCamera != null)
                {
                    Camera specCamComponent = SpectatorCamera.GetComponent<Camera>();
                    Plugin.Log($"Speccam {specCamComponent.cullingMatrix}");
                }
                else
                {
                    Plugin.Log("SpectatorCamera is null");
                }
                
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SpectatorCamera), "OnNetworkPostSpawn")]
    public class SpectatorCameraPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SpectatorCamera __instance)
        {
            SpectatorCamera = __instance;
        }
    }
    
    [HarmonyPatch(typeof(PlayerCamera), "OnNetworkPostSpawn")]
    public class PlayerCameraPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerCamera __instance)
        {
            PlayerCamera = __instance;
        }
    }
}