using System;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Discord
{
    [HarmonyPatch(typeof(UIChat), nameof(UIChat.Client_SendClientChatMessage))]
    public static class UIChatClientSendClientChatMessage
    {
        [HarmonyPrefix]
        public static bool Prefix(UIChat __instance, string message, bool useTeamChat)
        {
            if (!MessagingHandler.connectedToToastersRink) return true;
            
            if (message.StartsWith("/discord", StringComparison.OrdinalIgnoreCase))
            {
                Application.OpenURL("http://discord.puckstats.io/");
                __instance.AddChatMessage($"Opened the link to Toaster's Rink Discord in your browser!");
                return false;
            }

            return true;
        }
    }
}