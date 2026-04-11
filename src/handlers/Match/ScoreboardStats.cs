using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Adds a single "SOG/SV%" column to the in-game tab scoreboard that shows either
/// shots-on-goal (skaters) or save percentage (goalies). Values come from the
/// server-synced PlayerStatsStore.
/// </summary>
public static class ScoreboardStats
{
    // Wrapper column VisualElement name
    private const string StatWrapperName = "TRC_StatWrapper";
    // Header variant — different name so we can find each independently
    private const string StatHeaderWrapperName = "TRC_StatHeaderWrapper";
    private const string StarLabelName = "TRC_StarLabel";

    private static bool _headersInjected;
    private static bool _hierarchyDumped;

    public static void ResetHeaders()
    {
        _headersInjected = false;
        _hierarchyDumped = false;
    }

    /// <summary>
    /// Re-style all players on the scoreboard so our stat labels update.
    /// </summary>
    public static void RefreshAllPlayers()
    {
        try
        {
            var scoreboard = Object.FindObjectOfType<UIScoreboard>();
            if (scoreboard == null) return;

            var mapField = AccessTools.Field(typeof(UIScoreboard), "playerVisualElementMap");
            if (mapField == null) return;
            var map = mapField.GetValue(scoreboard) as Dictionary<Player, VisualElement>;
            if (map == null) return;

            var players = new List<Player>(map.Keys);
            foreach (var player in players)
            {
                scoreboard.StylePlayer(player);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScoreboardStats] Error refreshing all players: {e}");
        }
    }

    /// <summary>
    /// Walk the entire scoreboard tree and dump hierarchy info to the Puck log.
    /// </summary>
    private static void DumpHierarchy(UIScoreboard instance)
    {
        if (_hierarchyDumped) return;
        _hierarchyDumped = true;

        try
        {
            var scoreboardField = AccessTools.Field(typeof(UIScoreboard), "scoreboard");
            var scoreboard = scoreboardField?.GetValue(instance) as VisualElement;
            if (scoreboard == null) return;

            var root = scoreboard;
            while (root.parent != null) root = root.parent;

            var sb = new StringBuilder();
            sb.AppendLine("[ScoreboardStats][DUMP] Scoreboard tree (from root):");
            DumpElement(root, 0, sb);
            Debug.Log(sb.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScoreboardStats][DUMP] Error: {e}");
        }
    }

    private static void DumpElement(VisualElement el, int depth, StringBuilder sb)
    {
        if (el == null || depth > 30) return;
        string indent = new string(' ', depth * 2);
        string type = el.GetType().Name;
        string name = string.IsNullOrEmpty(el.name) ? "" : $" name='{el.name}'";
        string text = "";
        if (el is TextElement te && !string.IsNullOrEmpty(te.text))
        {
            string safe = te.text.Length > 40 ? te.text.Substring(0, 40) + "..." : te.text;
            text = $" text='{safe}'";
        }

        var classes = el.GetClasses();
        string classList = "";
        foreach (var c in classes) classList += "." + c;
        if (!string.IsNullOrEmpty(classList)) classList = " class=" + classList;

        string size = $" w={el.resolvedStyle.width:F0} h={el.resolvedStyle.height:F0} fs={el.resolvedStyle.fontSize:F0}";

        sb.AppendLine($"{indent}{type}{name}{classList}{text}{size}");

        for (int i = 0; i < el.childCount; i++)
            DumpElement(el[i], depth + 1, sb);
    }

    /// <summary>
    /// Copy width/layout properties from a reference wrapper onto ours.
    /// The vanilla column wrappers get their widths from USS selectors matching
    /// by name (e.g. "#Points"), which our custom-named clone can't inherit.
    /// </summary>
    private static void SyncWrapperLayout(VisualElement clone, VisualElement reference)
    {
        if (clone == null || reference == null) return;
        var rs = reference.resolvedStyle;
        if (rs.width > 0.5f)
        {
            clone.style.width = rs.width;
            clone.style.minWidth = rs.width;
        }
    }

    /// <summary>
    /// Register a GeometryChangedEvent handler on the reference wrapper so our clone
    /// updates its width the same frame the vanilla layout resolves — no lag.
    /// Uses a WeakReference to avoid pinning the clone if it gets detached.
    /// </summary>
    private static void RegisterLayoutSync(VisualElement clone, VisualElement reference)
    {
        if (clone == null || reference == null) return;

        // Sync immediately in case layout is already resolved
        SyncWrapperLayout(clone, reference);

        var cloneRef = new System.WeakReference(clone);
        EventCallback<GeometryChangedEvent> handler = null;
        handler = evt =>
        {
            var c = cloneRef.Target as VisualElement;
            if (c == null || c.panel == null)
            {
                reference.UnregisterCallback(handler);
                return;
            }
            SyncWrapperLayout(c, reference);
        };
        reference.RegisterCallback<GeometryChangedEvent>(handler);
    }

    /// <summary>
    /// Inject the "SOG/SV%" column header into Content > Header.
    /// </summary>
    private static void EnsureHeaders(UIScoreboard instance)
    {
        if (_headersInjected) return;

        try
        {
            var scoreboardField = AccessTools.Field(typeof(UIScoreboard), "scoreboard");
            var scoreboard = scoreboardField?.GetValue(instance) as VisualElement;
            if (scoreboard == null) return;

            var content = scoreboard.Q<VisualElement>("Content");
            if (content == null) return;

            // Find the column header row (the Header element that is a direct child of Content)
            VisualElement headerRow = null;
            for (int i = 0; i < content.childCount; i++)
            {
                if (content[i].name == "Header")
                {
                    headerRow = content[i];
                    break;
                }
            }

            if (headerRow == null) return;

            if (headerRow.Q(StatHeaderWrapperName) != null)
            {
                _headersInjected = true;
                return;
            }

            var pointsWrapper = headerRow.Q<VisualElement>("Points");
            var pingWrapper = headerRow.Q<VisualElement>("Ping");
            if (pointsWrapper == null) return;

            var statHeader = CloneColumnWrapper(pointsWrapper, StatHeaderWrapperName, "SOG/SV%");

            int insertIndex = pingWrapper != null
                ? headerRow.IndexOf(pingWrapper)
                : headerRow.IndexOf(pointsWrapper) + 1;

            headerRow.Insert(insertIndex, statHeader);

            // Hook layout sync so our header tracks the Points reference width instantly
            RegisterLayoutSync(statHeader, pointsWrapper);

            _headersInjected = true;
            Debug.Log($"[ScoreboardStats] Injected SOG/SV% header at index {insertIndex}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScoreboardStats] Error injecting column header: {e}");
        }
    }

    /// <summary>
    /// Clone a column wrapper (e.g. "Points" which contains "PointsLabel"):
    /// returns a new VisualElement with the same USS classes (plus any wrappers),
    /// containing a Label with the same USS classes as the inner label.
    /// </summary>
    private static VisualElement CloneColumnWrapper(VisualElement reference, string wrapperName, string labelText)
    {
        var wrapper = new VisualElement();
        wrapper.name = wrapperName;
        foreach (var cls in reference.GetClasses())
            wrapper.AddToClassList(cls);

        // Find the inner label to copy its classes
        Label innerReference = null;
        for (int i = 0; i < reference.childCount; i++)
        {
            if (reference[i] is Label l)
            {
                innerReference = l;
                break;
            }
        }

        var innerLabel = new Label(labelText);
        if (innerReference != null)
        {
            foreach (var cls in innerReference.GetClasses())
                innerLabel.AddToClassList(cls);
        }
        wrapper.Add(innerLabel);

        return wrapper;
    }

    [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.StylePlayer))]
    public static class StylePlayerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIScoreboard __instance, Player player)
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                DumpHierarchy(__instance);
                EnsureHeaders(__instance);

                var mapField = AccessTools.Field(typeof(UIScoreboard), "playerVisualElementMap");
                if (mapField == null) return;
                var map = mapField.GetValue(__instance) as Dictionary<Player, VisualElement>;
                if (map == null || !map.ContainsKey(player)) return;

                var playerEl = map[player].Q<VisualElement>("Player");
                if (playerEl == null) return;

                var pointsWrapper = playerEl.Q<VisualElement>("Points");
                var pingWrapper = playerEl.Q<VisualElement>("Ping");

                // Star badge
                var starLabel = playerEl.Q<Label>(StarLabelName);
                if (starLabel == null)
                {
                    starLabel = CreateStarLabel();
                    playerEl.Insert(0, starLabel);
                }

                int starRank = modifiers.MatchStarsStore.RankBySteamId(player.SteamId.Value.ToString());
                switch (starRank)
                {
                    case 1:
                        starLabel.text = "\u2605";
                        starLabel.style.color = new StyleColor(new Color32(0xFF, 0xD7, 0x00, 0xFF));
                        break;
                    case 2:
                        starLabel.text = "\u2605";
                        starLabel.style.color = new StyleColor(new Color32(0xED, 0xED, 0xED, 0xFF));
                        break;
                    case 3:
                        starLabel.text = "\u2605";
                        starLabel.style.color = new StyleColor(new Color32(0xCD, 0x7F, 0x32, 0xFF));
                        break;
                    default:
                        starLabel.text = "";
                        break;
                }

                // Create the stat wrapper column if missing
                var statWrapper = playerEl.Q<VisualElement>(StatWrapperName);

                if (statWrapper == null && pointsWrapper != null)
                {
                    statWrapper = CloneColumnWrapper(pointsWrapper, StatWrapperName, "");

                    int insertIndex = pingWrapper != null
                        ? playerEl.IndexOf(pingWrapper)
                        : playerEl.IndexOf(pointsWrapper) + 1;

                    playerEl.Insert(insertIndex, statWrapper);

                    // Hook layout sync so our column tracks the Points reference width instantly
                    RegisterLayoutSync(statWrapper, pointsWrapper);
                }

                if (statWrapper == null) return;

                // Get inner label
                Label statLabel = null;
                for (int i = 0; i < statWrapper.childCount; i++)
                    if (statWrapper[i] is Label sl) { statLabel = sl; break; }
                if (statLabel == null) return;

                // Populate value based on current role
                bool isOnTeam = player.Team == PlayerTeam.Blue || player.Team == PlayerTeam.Red;
                if (!isOnTeam)
                {
                    statLabel.text = "";
                    return;
                }

                string steamId = player.SteamId.Value.ToString();
                var stats = modifiers.PlayerStatsStore.GetStats(steamId);

                if (stats == null)
                {
                    statLabel.text = "";
                    return;
                }

                if (player.Role == PlayerRole.Goalie)
                {
                    // Goalie: save percentage
                    if (stats.shotsFaced > 0)
                    {
                        float svPct = (float)stats.saves / stats.shotsFaced * 100f;
                        statLabel.text = $"{svPct:F0}%";
                    }
                    else
                    {
                        statLabel.text = "-";
                    }
                }
                else
                {
                    // Skater: shots on goal
                    statLabel.text = stats.shots.ToString();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ScoreboardStats] Error in StylePlayer postfix: {e}");
            }
        }

        private static Label CreateStarLabel()
        {
            var label = new Label("");
            label.name = StarLabelName;
            label.style.fontSize = 16;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.minWidth = 18;
            label.style.width = 18;
            label.style.paddingLeft = 2;
            label.style.paddingRight = 2;
            return label;
        }
    }

    /// <summary>
    /// After the scoreboard becomes visible and lays out, sync the header column
    /// width from the Points reference so the header aligns with the player rows.
    /// This is called from a geometry-changed event handler we set up on the header.
    /// </summary>
    public static void SyncHeaderWidth(UIScoreboard instance)
    {
        try
        {
            var scoreboardField = AccessTools.Field(typeof(UIScoreboard), "scoreboard");
            var scoreboard = scoreboardField?.GetValue(instance) as VisualElement;
            if (scoreboard == null) return;

            var content = scoreboard.Q<VisualElement>("Content");
            if (content == null) return;

            VisualElement headerRow = null;
            for (int i = 0; i < content.childCount; i++)
                if (content[i].name == "Header") { headerRow = content[i]; break; }
            if (headerRow == null) return;

            var pointsRef = headerRow.Q<VisualElement>("Points");
            var ourHeader = headerRow.Q<VisualElement>(StatHeaderWrapperName);
            SyncWrapperLayout(ourHeader, pointsRef);
        }
        catch { }
    }
}
