using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private static bool _bypassFilter = false;

    /// <summary>
    /// Debug: log when any Player is added to PlayerManager to see if replay copies arrive.
    /// </summary>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.AddPlayer))]
    public static class DebugPlayerAddedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player player)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
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
            if (!MessagingHandler.connectedToToastersRink) return;
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

    // Grey robot skin color for AI goalie heads
    private static readonly Color RobotSkinColor = new Color(0.55f, 0.57f, 0.60f);
    // Eye colors per team
    private static readonly Color RedEyeColor = new Color(1.0f, 0.15f, 0.1f);
    private static readonly Color BlueEyeColor = new Color(0.1f, 0.4f, 1.0f);

    // Cached reflection access to PlayerHead's private headgear list
    private static readonly FieldInfo HeadgearListField =
        typeof(PlayerHead).GetField("headgear", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Apply robot head/eye colors and headgear to an AI goalie's PlayerHead.
    /// Shared by both the ApplyCustomizations postfix and the TRL ApplyHeadColors postfix.
    /// </summary>
    public static void ApplyRobotAppearance(PlayerHead playerHead, Player player)
    {
        if (playerHead == null || player == null) return;

        Color eyeColor = player.Team == PlayerTeam.Red ? RedEyeColor : BlueEyeColor;

        var renderers = playerHead.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            string rendererName = renderer.name.ToLower();

            if (rendererName == "head")
            {
                SetRendererColor(renderer, RobotSkinColor);
            }
            else if (rendererName == "eyes")
            {
                SetRendererColor(renderer, eyeColor);
                // Enable emission for a glowing effect
                foreach (var mat in renderer.materials)
                {
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", eyeColor * 1.5f);
                    if (mat.HasProperty("_EmissiveColor"))
                        mat.SetColor("_EmissiveColor", eyeColor * 1.5f);
                }
            }
            else if (rendererName == "cage")
            {
                // Hide the cage so the robot face/eyes are visible
                renderer.enabled = false;
            }
        }
    }

    private static void SetRendererColor(MeshRenderer renderer, Color color)
    {
        foreach (var mat in renderer.materials)
        {
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("baseColorFactor"))
                mat.SetColor("baseColorFactor", color);
        }
    }

    /// <summary>
    /// Activate a goalie headgear on the PlayerHead if none is currently active.
    /// Uses reflection to access the private headgear list and finds the first
    /// goalie-compatible headgear to enable.
    /// </summary>
    private static void ApplyGoalieHeadgear(PlayerHead playerHead)
    {
        if (HeadgearListField == null)
        {
            Debug.LogWarning("[AIGoalieFilter] HeadgearListField reflection failed");
            return;
        }

        var headgearList = HeadgearListField.GetValue(playerHead) as List<Headgear>;
        if (headgearList == null || headgearList.Count == 0)
        {
            Debug.Log($"[AIGoalieFilter] Headgear list is null or empty (count={headgearList?.Count ?? -1})");
            return;
        }

        Debug.Log($"[AIGoalieFilter] Headgear list has {headgearList.Count} entries:");
        foreach (var h in headgearList)
        {
            Debug.Log($"[AIGoalieFilter]   ID={h.ID} role={h.Role} active={h.GameObject?.activeSelf} goalie={h.IsForRole(PlayerRole.Goalie)}");
        }

        // Check if any headgear is already active
        bool anyActive = headgearList.Any(h => h.GameObject != null && h.GameObject.activeSelf);
        if (anyActive)
        {
            Debug.Log("[AIGoalieFilter] A headgear is already active, skipping");
            return;
        }

        // Find the first goalie-compatible headgear and activate it
        Headgear goalieHeadgear = headgearList.FirstOrDefault(h =>
            h.GameObject != null && h.IsForRole(PlayerRole.Goalie));

        if (goalieHeadgear != null)
        {
            goalieHeadgear.GameObject.SetActive(true);
            Debug.Log($"[AIGoalieFilter] Activated goalie headgear ID={goalieHeadgear.ID}");
        }
        else
        {
            Debug.LogWarning("[AIGoalieFilter] No goalie-compatible headgear found in list");
        }
    }

    /// <summary>
    /// Apply robot appearance + headgear to AI goalies when their body spawns.
    /// </summary>
    [HarmonyPatch(typeof(PlayerBody), nameof(PlayerBody.ApplyCustomizations))]
    public static class ApplyRobotAppearancePatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerBody __instance)
        {
            // No connectedToToastersRink gate here — the bot spawns before the
            // server handshake completes, so the flag is still false. The TotBot
            // name prefix is unique enough to be safe on any server.
            try
            {
                Player player = __instance.Player;
                if (player == null) return;

                string name = player.Username.Value.ToString();
                if (!name.StartsWith(BotNamePrefix)) return;

                PlayerHead playerHead = __instance.PlayerMesh?.PlayerHead;
                if (playerHead == null)
                {
                    Debug.Log($"[AIGoalieFilter] PlayerHead null for {name}, skipping appearance");
                    return;
                }

                ApplyGoalieHeadgear(playerHead);
                ApplyRobotAppearance(playerHead, player);

                Debug.Log($"[AIGoalieFilter] Applied robot appearance to {name} (team={player.Team})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIGoalieFilter] Error applying robot appearance: {e}");
            }
        }
    }

    /// <summary>
    /// Try to patch TRL's GenderSwapper.ApplyHeadColors so our robot colors
    /// are re-applied after TRL sets skin tones (including async API callbacks).
    /// Call this during plugin init after attribute-based patches are applied.
    /// Safe to call if TRL is not installed — it will simply no-op.
    /// </summary>
    public static void TryPatchTRLHeadColors(Harmony harmony)
    {
        try
        {
            // Find TRL's GenderSwapper type across all loaded assemblies
            System.Type genderSwapperType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                genderSwapperType = asm.GetType("ToasterReskinLoader.swappers.GenderSwapper");
                if (genderSwapperType != null) break;
            }

            if (genderSwapperType == null)
            {
                Debug.Log("[AIGoalieFilter] TRL not found, skipping ApplyHeadColors patch");
                return;
            }

            var applyHeadColors = genderSwapperType.GetMethod("ApplyHeadColors",
                BindingFlags.Public | BindingFlags.Static);
            if (applyHeadColors == null)
            {
                Debug.LogWarning("[AIGoalieFilter] TRL GenderSwapper found but ApplyHeadColors method missing");
                return;
            }

            var postfix = typeof(AIGoalieFilter).GetMethod(nameof(ApplyHeadColorsPostfix),
                BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(applyHeadColors, postfix: new HarmonyMethod(postfix));
            Debug.Log("[AIGoalieFilter] Patched TRL GenderSwapper.ApplyHeadColors to preserve robot appearance");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIGoalieFilter] Failed to patch TRL ApplyHeadColors: {e}");
        }
    }

    /// <summary>
    /// Harmony postfix for TRL's GenderSwapper.ApplyHeadColors.
    /// Re-applies robot colors after TRL has set its skin tone.
    /// </summary>
    public static void ApplyHeadColorsPostfix(PlayerHead playerHead)
    {
        try
        {
            if (playerHead == null) return;

            // Walk up the hierarchy to find the Player
            PlayerBody playerBody = playerHead.GetComponentInParent<PlayerBody>();
            Player player = playerBody?.Player;
            if (player == null) return;

            string name = player.Username.Value.ToString();
            if (!name.StartsWith(BotNamePrefix)) return;

            ApplyRobotAppearance(playerHead, player);
            Debug.Log($"[AIGoalieFilter] Re-applied robot appearance after TRL for {name}");
        }
        catch { }
    }

    public static bool IsAIGoalie(Player player)
    {
        if (_bypassFilter) return false;
        if (!MessagingHandler.connectedToToastersRink) return false;
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
    /// Retroactively remove any AI goalies that were added to the scoreboard
    /// before we detected this is a Toaster's Rink server.
    /// Call this right after connectedToToastersRink becomes true.
    /// </summary>
    public static void RemoveExistingAIGoalies()
    {
        try
        {
            var scoreboard = UnityEngine.Object.FindObjectOfType<UIScoreboard>();
            if (scoreboard == null) return;

            // Temporarily bypass our own filter so GetPlayers returns the bots
            List<Player> players;
            _bypassFilter = true;
            try { players = PlayerManager.Instance.GetPlayers(true); }
            finally { _bypassFilter = false; }

            foreach (var player in players)
            {
                if (player == null) continue;
                try
                {
                    if (player.IsReplay.Value) continue;
                    string username = player.Username.Value.ToString();
                    if (username.StartsWith(BotNamePrefix))
                    {
                        scoreboard.RemovePlayer(player);
                        Debug.Log($"[AIGoalieFilter] Retroactively removed {username} from scoreboard");
                    }
                }
                catch { }
            }

            // Update the player count in the scoreboard header
            scoreboard.StyleServer(
                NetworkBehaviourSingleton<ServerManager>.Instance.Server.Value,
                PlayerManager.Instance.GetPlayers(false).Count);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIGoalieFilter] Error removing existing AI goalies: {e}");
        }
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
