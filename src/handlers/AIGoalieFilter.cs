using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Client-side filtering of AI goalie bots from the scoreboard, player list, and player count.
/// AI goalies are identified by their username prefix "TotBot".
/// </summary>
public static class AIGoalieFilter
{
    private const string BotNamePrefix = "TotBot";

    /// <summary>
    /// Debug: log when any Player is added to PlayerManager to see if replay copies arrive.
    /// </summary>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.AddPlayer))]
    public static class DebugPlayerAddedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player player)
        {
            try
            {
                string name = player.Username.Value.ToString();
                if (name.StartsWith(BotNamePrefix))
                {
                    Debug.Log($"[AIGoalieFilter] AddPlayer: {name} clientId={player.OwnerClientId} isReplay={player.IsReplay.Value} isCharSpawned={player.IsCharacterSpawned}");
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Debug: log when any PlayerBody spawns for a TotBot.
    /// </summary>
    [HarmonyPatch(typeof(Player), "OnNetworkPostSpawn")]
    public static class DebugPlayerPostSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            try
            {
                string name = __instance.Username.Value.ToString();
                if (name.StartsWith(BotNamePrefix))
                {
                    Debug.Log($"[AIGoalieFilter] OnNetworkPostSpawn: {name} clientId={__instance.OwnerClientId} isReplay={__instance.IsReplay.Value}");
                }
            }
            catch { }
        }
    }

    public static bool IsAIGoalie(Player player)
    {
        if (player == null) return false;
        try
        {
            // Don't filter replay copies - they need to be visible during replays
            if (player.IsReplay.Value) return false;
            string username = player.Username.Value.ToString();
            return username.StartsWith(BotNamePrefix);
        }
        catch { return false; }
    }

    /// <summary>
    /// Prevent AI goalies from being added to the tab scoreboard.
    /// </summary>
    [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.AddPlayer))]
    public static class HideFromScoreboardPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player player)
        {
            return !IsAIGoalie(player);
        }
    }

    /// <summary>
    /// Exclude AI goalies from PlayerManager.GetPlayers() on the client side.
    /// This fixes the player count in the scoreboard header and the companion PlayersTab.
    /// </summary>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.GetPlayers))]
    public static class ExcludeFromPlayerListPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref List<Player> __result, bool includeReplay)
        {
            int before = __result.Count;
            __result = __result.Where(p => !IsAIGoalie(p)).ToList();
            int after = __result.Count;
            if (before != after && includeReplay)
            {
                Debug.Log($"[AIGoalieFilter] GetPlayers(includeReplay={includeReplay}): filtered {before - after} AI goalies, {after} remaining");
            }
        }
    }

    /// <summary>
    /// Exclude AI goalies from PlayerManager.GetSpawnedPlayers() on the client side.
    /// </summary>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.GetSpawnedPlayers))]
    public static class ExcludeFromSpawnedPlayerListPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref List<Player> __result)
        {
            __result = __result.Where(p => !IsAIGoalie(p)).ToList();
        }
    }
}
