using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Client-side filtering of AI goalie bots from the scoreboard, player list, and player count.
/// AI goalies are identified by their username prefix "TotBot".
/// </summary>
public static class AIGoalieFilter
{
    private const string BotNamePrefix = "TotBot";

    public static bool IsAIGoalie(Player player)
    {
        if (player == null) return false;
        try
        {
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
        public static void Postfix(ref List<Player> __result)
        {
            __result = __result.Where(p => !IsAIGoalie(p)).ToList();
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
