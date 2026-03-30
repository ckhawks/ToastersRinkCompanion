using System;
using System.Collections.Generic;
using HarmonyLib;
using ToastersRinkCompanion.collectibles;
using ToastersRinkCompanion.extras;
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
            // Plugin.Log($"UIChatPatch");
            string[] args = message.Split(' ');
            // Plugin.Log($"setkeybindpre");
            if (message.ToLower().StartsWith("/setkeybind"))
            {
                if (args.Length < 3)
                {
                    __instance.AddChatMessage($"Please write what you would like to set a keybind for, and what the key is. /setkeybind spawnpuck q");
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

            // Plugin.Log($"Message: {message}");
            if (message.ToLower().StartsWith("/watchpucksof"))
            {
                Plugin.Log($"args length {args.Length}");

                // if they already had a target, turn it off no matter what they entered.
                if (WatchPucksOf.target != null)
                {
                    __instance.AddChatMessage($"<size=18><color=green><b>WATCH PUCKS OF</b></color>  Disabled.");
                    WatchPucksOf.target = null;
                    return false;
                }

                // if they entered short thing, and there is no target already, provide help info
                if (args.Length <= 1 && WatchPucksOf.target == null)
                {
                    __instance.AddChatMessage($"<s>-></s> <size=16><color=red>Please write who you want to watch the pucks of. Usage: /watchpucksof <name/number></color></size>");
                    return false;
                }
                
                // try to search by the player number
                int playerNumber = -1;
                Player targetPlayer = null;
                if (int.TryParse(args[1], out playerNumber))
                {
                    targetPlayer = PlayerManager.Instance.GetPlayerByNumber(playerNumber);
                    
                    // if we found one player, let's make sure there isn't another one
                    if (targetPlayer != null)
                    {
                        List<Player> players = PlayerManager.Instance.GetPlayers();
                        bool foundPlayer = false;
                        foreach (Player player in players)
                        {
                            if (player.Number.Value == playerNumber)
                            {
                                if (foundPlayer)
                                {
                                    __instance.AddChatMessage(
                                        $"<s>-></s> <size=16><color=red>There were multiple users found with the <b>number {playerNumber}</b>.</color></size>");
                                    return false;
                                }
                                foundPlayer = true;
                            }
                        }
                    }
                }

                // try to search by the name
                string nameArg = "";
                if (targetPlayer == null)
                {
                    nameArg = string.Join(" ", args[new Range(1, args.Length)]);
                    targetPlayer = PlayerManager.Instance.GetPlayerByUsername(nameArg);
                }
                
                // if no player found
                if (targetPlayer == null)
                {
                    __instance.AddChatMessage(
                        $"<s>-></s> <size=16><color=red>Could not find a user to watch pucks of for <b>{nameArg}</b>.</color></size>");
                    return false;
                }

                WatchPucksOf.target = targetPlayer;
                __instance.AddChatMessage($"<size=18><color=green><b>WATCH PUCKS OF</b></color>  Watching pucks for <b>{targetPlayer.Username.Value.ToString()}</b>...");
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