using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Client-side detection of the single goalie and [SG] tag on the tab scoreboard.
/// Mirrors the server logic: exactly one goalie spawned = single goalie.
/// </summary>
public static class SingleGoalieTag
{
    /// <summary>
    /// Returns the single goalie player if exactly one non-replay goalie is spawned,
    /// the DisableSingleGoalie modifier is not active, and we're on a Toaster's Rink server.
    /// </summary>
    public static Player GetSingleGoalie()
    {
        if (!MessagingHandler.connectedToToastersRink) return null;

        // Check if the DisableSingleGoalie modifier is active
        if (modifiers.ModifierRegistry.ActiveModifiers.Exists(m => m.key == "disablesinglegoalie"))
            return null;

        var players = PlayerManager.Instance?.GetSpawnedPlayers();
        if (players == null) return null;

        Player foundGoalie = null;
        int goalieCount = 0;

        foreach (var player in players)
        {
            if (player == null) continue;
            if (player.IsReplay.Value) continue;
            if (player.Role == PlayerRole.Goalie)
            {
                goalieCount++;
                foundGoalie = player;
                if (goalieCount > 1) return null;
            }
        }

        return goalieCount == 1 ? foundGoalie : null;
    }

    /// <summary>
    /// Append " [SG]" to the username label on the tab scoreboard for the single goalie.
    /// </summary>
    [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.StylePlayer))]
    public static class ScoreboardStylePlayerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIScoreboard __instance, Player player)
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                var mapField = AccessTools.Field(typeof(UIScoreboard), "playerVisualElementMap");
                if (mapField == null) return;
                var map = mapField.GetValue(__instance) as System.Collections.Generic.Dictionary<Player, VisualElement>;
                if (map == null || !map.ContainsKey(player)) return;

                var playerEl = map[player].Q<VisualElement>("Player");
                if (playerEl == null) return;

                Label usernameLabel = playerEl.Q<Label>("UsernameLabel");
                if (usernameLabel == null) return;

                // Remove any previous SG label we injected
                var existingSg = playerEl.Q<Label>("TRC_SGLabel");
                existingSg?.RemoveFromHierarchy();

                Player singleGoalie = GetSingleGoalie();
                if (singleGoalie != null && singleGoalie == player)
                {
                    // Clone UsernameLabel's USS classes so font weight/line-height match
                    var sgLabel = new Label("SG");
                    sgLabel.name = "TRC_SGLabel";
                    foreach (var cls in usernameLabel.GetClasses())
                        sgLabel.AddToClassList(cls);
                    sgLabel.style.color = new StyleColor(new Color(1f, 0.8f, 0f)); // gold
                    sgLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    sgLabel.style.marginLeft = 16;

                    // Font size: 2pt smaller than the username. Read from resolvedStyle
                    // (valid once the scoreboard has been rendered at least once).
                    float refFontSize = usernameLabel.resolvedStyle.fontSize;
                    if (refFontSize > 2f)
                        sgLabel.style.fontSize = refFontSize - 2f;

                    // UsernameLabel may be nested inside a sub-container; insert as a
                    // sibling in its own parent so the SG badge lands right next to it.
                    var parent = usernameLabel.parent;
                    if (parent != null)
                    {
                        int idx = parent.IndexOf(usernameLabel);
                        parent.Insert(idx + 1, sgLabel);
                    }
                    else
                    {
                        playerEl.Add(sgLabel);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SingleGoalieTag] Error in StylePlayer postfix: {e}");
            }
        }
    }
}
