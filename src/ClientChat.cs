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
    
    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.Client_SendChatMessage))]
    public class UIChatPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ChatManager __instance, string content, bool isQuickChat, bool isTeamChat)
        {
            // Plugin.Log($"UIChatPatch");
            string[] args = content.Split(' ');
            // Plugin.Log($"setkeybindpre");
            if (content.ToLower().StartsWith("/setkeybind"))
            {
                if (args.Length < 3)
                {
                    Plugin.AddLocalChatMessage($"Please write what you would like to set a keybind for, and what the key is. /setkeybind spawnpuck q");
                    return false;
                }

                if (args[1].ToLower() != "spawnpuck")
                {
                    Plugin.AddLocalChatMessage($"At this moment, you can only customize the keybind of spawnpuck. /setkeybind spawnpuck q");
                    return false;
                }
                
                string newBindingPath = $"<keyboard>/{args[2].ToLower()}";
                Plugin.modSettings.spawnPuckKeybind = newBindingPath;
                Plugin.modSettings.Save();
                Plugin.AddLocalChatMessage($"Your keybind for spawnpuck has been set to {Plugin.modSettings.spawnPuckKeybind}. If this is not a valid input action, it will not work.");
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
            if (content.ToLower().StartsWith("/watchpucksof"))
            {
                Plugin.Log($"args length {args.Length}");

                // if they already had a target, turn it off no matter what they entered.
                if (WatchPucksOf.target != null)
                {
                    Plugin.AddLocalChatMessage($"<size=18><color=green><b>WATCH PUCKS OF</b></color>  Disabled.");
                    WatchPucksOf.target = null;
                    return false;
                }

                // if they entered short thing, and there is no target already, provide help info
                if (args.Length <= 1 && WatchPucksOf.target == null)
                {
                    Plugin.AddLocalChatMessage($"<s>-></s> <size=16><color=red>Please write who you want to watch the pucks of. Usage: /watchpucksof <name/number></color></size>");
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
                                    Plugin.AddLocalChatMessage(
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
                    Plugin.AddLocalChatMessage(
                        $"<s>-></s> <size=16><color=red>Could not find a user to watch pucks of for <b>{nameArg}</b>.</color></size>");
                    return false;
                }

                WatchPucksOf.target = targetPlayer;
                Plugin.AddLocalChatMessage($"<size=18><color=green><b>WATCH PUCKS OF</b></color>  Watching pucks for <b>{targetPlayer.Username.Value.ToString()}</b>...");
                return false;
            }
            
            // else if (content.ToLower().StartsWith("/fuckgoals"))
            // {
            //     FuckGoals.FuckGoalsNow();
            // }
            // else if (content.ToLower().StartsWith("/collectible"))
            // {
            //     Player player = PlayerManager.Instance.GetLocalPlayer();
            //     OldCollectibleRenderer.ShowCollectiblePrototype(player);
            //     return false;
            // } 
            // else if (content.ToLower().StartsWith($"/opencase"))
            // {
            //     Player player = PlayerManager.Instance.GetLocalPlayer();
            //     Opening.PlayOpeningForAt(player.Stick.transform.position, player);
            // }
            else if (content.ToLower().StartsWith("/logcamera"))
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

    // DEBUG: Log all chat messages being added to trace missing leave messages
    // Also replaces server-sent placeholders with client keybind display names
    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.AddChatMessage))]
    public class AddChatMessageDebugPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ChatMessage chatMessage)
        {
            Plugin.Log($"[ChatDebug] AddChatMessage: IsSystem={chatMessage.IsSystem}, Content='{chatMessage.Content}', Username='{chatMessage.Username}'");

            // Suppress juggle messages when the setting is off
            if (!Plugin.modSettings.showJuggleNotifications
                && chatMessage.Content.Length > 0
                && chatMessage.Content.ToString().Contains("<b>JUGGLE</b>"))
                return false;

            if (chatMessage.IsSystem && chatMessage.Content.Length > 0)
            {
                string content = chatMessage.Content.ToString();
                string replaced = ReplacePlaceholders(content);
                if (replaced != content)
                    chatMessage.Content = replaced;
            }

            return true;
        }

        private static string ReplacePlaceholders(string content)
        {
            if (content.Contains("%modifierMenuKeybind%"))
            {
                string displayName = GetKeyDisplayName(Plugin.modSettings.panelKeybind);
                content = content.Replace("%modifierMenuKeybind%", displayName);
            }
            if (content.Contains("%spawnPuckKeybind%"))
            {
                string displayName = GetKeyDisplayName(Plugin.modSettings.spawnPuckKeybind);
                content = content.Replace("%spawnPuckKeybind%", displayName);
            }
            return content;
        }

        private static string GetKeyDisplayName(string keybind)
        {
            // Handle InputSystem paths like "<keyboard>/g"
            if (keybind.Contains("/"))
            {
                string key = keybind.Substring(keybind.LastIndexOf('/') + 1);
                return key.ToUpper();
            }
            // Handle simple names like "f3"
            return keybind.ToUpper();
        }
    }

    // DEBUG: Log when players despawn (which is when leave messages should appear)
    [HarmonyPatch(typeof(Player), "OnNetworkDespawn")]
    public class PlayerDespawnDebugPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            try
            {
                Plugin.Log($"[ChatDebug] Player.OnNetworkDespawn: Username='{__instance.Username.Value}', SteamId='{__instance.SteamId.Value}', ClientId={__instance.OwnerClientId}");
            }
            catch (Exception e)
            {
                Plugin.Log($"[ChatDebug] Player.OnNetworkDespawn: (could not read player data: {e.Message})");
            }
        }
    }

    // DEBUG: Log when the ServerManagerController fires the disconnect event on the client
    [HarmonyPatch(typeof(ServerManagerController), "Event_Everyone_OnClientDisconnected")]
    public class ClientDisconnectEventDebugPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Dictionary<string, object> message)
        {
            if (message == null)
            {
                Plugin.Log($"[ChatDebug] Event_Everyone_OnClientDisconnected: message is null");
                return;
            }

            string info = "";
            foreach (var kvp in message)
                info += $"{kvp.Key}={kvp.Value}, ";
            Plugin.Log($"[ChatDebug] Event_Everyone_OnClientDisconnected: {info}");
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