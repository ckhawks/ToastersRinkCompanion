using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Players tab showing connected players with profile links, position, and mod info.
/// </summary>
public static class PlayersTab
{
    private static readonly HashSet<string> _expandedPlayers = new();
    private static VisualElement _activeTooltip;

    /// <summary>
    /// Rebuild stats inside already-expanded accordions without touching the rest of the tab.
    /// Called when a stats delta arrives so numbers update live.
    /// </summary>
    public static void RefreshExpandedStats(VisualElement root)
    {
        if (root == null) return;
        foreach (var steamId in _expandedPlayers)
        {
            var panel = root.Q<VisualElement>($"stats-panel-{steamId}");
            if (panel == null) continue;
            panel.Clear();
            BuildPlayerStats(panel, steamId);
        }
    }

    public static void BuildContent(VisualElement parent)
    {
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        parent.Add(scrollView);

        var content = scrollView.contentContainer;
        content.style.paddingLeft = 16;
        content.style.paddingRight = 20;
        content.style.paddingTop = 12;
        content.style.paddingBottom = 12;

        var header = new Label("Players");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = UIHelpers.TextPrimary;
        header.style.marginBottom = 12;
        content.Add(header);

        var players = PlayerManager.Instance.GetPlayers();
        if (players == null || players.Count == 0)
        {
            var empty = new Label("No players connected.");
            empty.style.color = UIHelpers.TextMuted;
            empty.style.fontSize = 14;
            content.Add(empty);
            return;
        }

        var sorted = players
            .OrderBy(p => p.Team == PlayerTeam.Blue ? 0 : p.Team == PlayerTeam.Red ? 1 : p.Team == PlayerTeam.Spectator ? 3 : 2)
            .ThenBy(p => p.Number.Value).ToList();

        PlayerTeam lastTeam = (PlayerTeam)(-1);
        foreach (var player in sorted)
        {
            if (player.Team != lastTeam)
            {
                lastTeam = player.Team;
                string teamName;
                Color teamLabelColor;

                if (player.Team == PlayerTeam.Blue)
                {
                    teamName = "Blue";
                    teamLabelColor = new Color(0.3f, 0.5f, 1f);
                }
                else if (player.Team == PlayerTeam.Red)
                {
                    teamName = "Red";
                    teamLabelColor = new Color(0.9f, 0.2f, 0.2f);
                }
                else if (player.Team == PlayerTeam.Spectator)
                {
                    teamName = "Spectators";
                    teamLabelColor = new Color(0.5f, 0.5f, 0.5f);
                }
                else
                {
                    teamName = player.Team.ToString();
                    teamLabelColor = new Color(0.5f, 0.5f, 0.5f);
                }

                var teamLabel = new Label(teamName);
                teamLabel.style.fontSize = 14;
                teamLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                teamLabel.style.color = new StyleColor(teamLabelColor);
                teamLabel.style.marginTop = 8;
                teamLabel.style.marginBottom = 4;
                content.Add(teamLabel);
            }

            BuildPlayerRow(content, player);
        }
    }

    private static void BuildPlayerRow(VisualElement parent, Player player)
    {
        string username = player.Username.Value.ToString();
        string steamId = player.SteamId.Value.ToString();
        int number = player.Number.Value;
        string position = player.PlayerPosition != null ? player.PlayerPosition.Name : "";

        Color teamColor;
        if (player.Team == PlayerTeam.Blue)
            teamColor = new Color(0.3f, 0.5f, 1f);
        else if (player.Team == PlayerTeam.Red)
            teamColor = new Color(0.9f, 0.2f, 0.2f);
        else
            teamColor = new Color(0.5f, 0.5f, 0.5f);

        var container = new VisualElement();
        container.style.marginBottom = 2;
        parent.Add(container);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 10;
        row.style.paddingRight = 8;
        row.style.paddingTop = 4;
        row.style.paddingBottom = 4;
        row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        row.style.borderTopLeftRadius = 4;
        row.style.borderTopRightRadius = 4;
        row.style.borderBottomLeftRadius = 4;
        row.style.borderBottomRightRadius = 4;
        container.Add(row);

        // Expand arrow (placeholder for future accordion use)
        bool isExpanded = _expandedPlayers.Contains(steamId);

        var arrow = new Label(isExpanded ? "\u25BC" : "\u25B6");
        arrow.style.fontSize = 12;
        arrow.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        arrow.style.minWidth = 14;
        arrow.style.marginRight = 6;
        arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(arrow);

        // Player avatar with team-colored border
        var avatarContainer = new VisualElement();
        avatarContainer.style.width = 24;
        avatarContainer.style.height = 24;
        avatarContainer.style.marginRight = 8;
        avatarContainer.style.borderTopLeftRadius = 12;
        avatarContainer.style.borderTopRightRadius = 12;
        avatarContainer.style.borderBottomLeftRadius = 12;
        avatarContainer.style.borderBottomRightRadius = 12;
        avatarContainer.style.borderTopWidth = 1;
        avatarContainer.style.borderBottomWidth = 1;
        avatarContainer.style.borderLeftWidth = 1;
        avatarContainer.style.borderRightWidth = 1;
        avatarContainer.style.borderTopColor = new StyleColor(teamColor);
        avatarContainer.style.borderBottomColor = new StyleColor(teamColor);
        avatarContainer.style.borderLeftColor = new StyleColor(teamColor);
        avatarContainer.style.borderRightColor = new StyleColor(teamColor);
        avatarContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        row.Add(avatarContainer);

        // Fetch Steam avatar
        try
        {
            var cSteamId = new Steamworks.CSteamID(ulong.Parse(steamId));
            Steamworks.SteamFriends.RequestUserInformation(cSteamId, false);
            Texture2D avatar = SteamIntegrationManager.GetAvatar(steamId, AvatarSize.Small);
            if (avatar != null)
                avatarContainer.style.backgroundImage = new StyleBackground(avatar);
        }
        catch { /* Steam not available */ }

        // Position label
        if (!string.IsNullOrEmpty(position))
        {
            var posLabel = new Label(position);
            posLabel.style.fontSize = 11;
            posLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            posLabel.style.minWidth = 22;
            posLabel.style.marginRight = 4;
            posLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(posLabel);
        }

        // Number
        var numberLabel = new Label($"#{number}");
        numberLabel.style.fontSize = 14;
        numberLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        numberLabel.style.minWidth = 35;
        row.Add(numberLabel);

        // Name + EIS pills (left-aligned together, takes remaining space)
        var nameCol = new VisualElement();
        nameCol.style.flexGrow = 1;
        nameCol.style.flexShrink = 1;
        row.Add(nameCol);

        // Top row: name + pills inline
        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        nameCol.Add(nameRow);

        // Single goalie badge
        var singleGoalie = handlers.SingleGoalieTag.GetSingleGoalie();
        if (singleGoalie != null && singleGoalie == player)
        {
            var sgBadge = new Label("SG");
            sgBadge.style.fontSize = 9;
            sgBadge.style.color = new StyleColor(new Color(1f, 0.8f, 0f)); // gold
            sgBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            sgBadge.style.marginRight = 6;
            sgBadge.style.paddingLeft = 4;
            sgBadge.style.paddingRight = 4;
            sgBadge.style.paddingTop = 1;
            sgBadge.style.paddingBottom = 1;
            sgBadge.style.backgroundColor = new StyleColor(new Color(1f, 0.8f, 0f, 0.15f));
            sgBadge.style.borderTopLeftRadius = 3;
            sgBadge.style.borderTopRightRadius = 3;
            sgBadge.style.borderBottomLeftRadius = 3;
            sgBadge.style.borderBottomRightRadius = 3;
            nameRow.Add(sgBadge);
        }

        // Donor badge
        if (ChatFormatting.IsDonor(steamId))
        {
            var donorBadge = new Label("DONOR");
            donorBadge.style.fontSize = 9;
            donorBadge.style.color = new StyleColor(new Color(0.28f, 0.50f, 0.90f)); // #487fe6
            donorBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            donorBadge.style.marginRight = 6;
            donorBadge.style.paddingLeft = 4;
            donorBadge.style.paddingRight = 4;
            donorBadge.style.paddingTop = 1;
            donorBadge.style.paddingBottom = 1;
            donorBadge.style.backgroundColor = new StyleColor(new Color(0.28f, 0.50f, 0.90f, 0.15f));
            donorBadge.style.borderTopLeftRadius = 3;
            donorBadge.style.borderTopRightRadius = 3;
            donorBadge.style.borderBottomLeftRadius = 3;
            donorBadge.style.borderBottomRightRadius = 3;
            nameRow.Add(donorBadge);
        }

        var nameLabel = new Label(username);
        nameLabel.style.fontSize = 14;
        nameLabel.style.color = UIHelpers.TextPrimary;
        nameRow.Add(nameLabel);

        // EIS team pills
        var eisTeams = ChatFormatting.GetPlayerTeams(steamId);
        if (eisTeams != null && eisTeams.Length > 0)
        {
            EISTeamData.EnsureFetched();
            foreach (var teamEntry in eisTeams)
            {
                var pill = BuildTeamPill(teamEntry);
                pill.style.marginLeft = 6;
                nameRow.Add(pill);
            }
        }

        // Mod count badge with hover popover
        var modInfo = PlayerModStore.GetMods(steamId);
        bool hasMods = modInfo != null && (modInfo.modNames.Length > 0 || modInfo.localModCount > 0);

        if (hasMods)
        {
            int totalMods = modInfo.modNames.Length + modInfo.localModCount;
            var modBadge = new Label($"{totalMods} mod{(totalMods != 1 ? "s" : "")}");
            modBadge.style.fontSize = 11;
            modBadge.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            modBadge.style.marginRight = 8;
            modBadge.style.paddingLeft = 4;
            modBadge.style.paddingRight = 4;
            modBadge.style.paddingTop = 1;
            modBadge.style.paddingBottom = 1;
            modBadge.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
            modBadge.style.borderTopLeftRadius = 3;
            modBadge.style.borderTopRightRadius = 3;
            modBadge.style.borderBottomLeftRadius = 3;
            modBadge.style.borderBottomRightRadius = 3;
            row.Add(modBadge);

            modBadge.RegisterCallback<MouseEnterEvent>(evt =>
            {
                modBadge.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
                modBadge.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                ShowModTooltip(modBadge, modInfo);
            });
            modBadge.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                modBadge.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                modBadge.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
                HideModTooltip();
            });
        }

        // Ping
        var pingLabel = new Label($"{player.Ping.Value}ms");
        pingLabel.style.fontSize = 12;
        pingLabel.style.color = player.Ping.Value > 100
            ? new StyleColor(new Color(0.9f, 0.6f, 0.1f))
            : new StyleColor(UIHelpers.TextSecondary);
        pingLabel.style.minWidth = 45;
        pingLabel.style.marginRight = 8;
        pingLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(pingLabel);

        // Action buttons
        BuildSmallButton(row, "Steam", () =>
        {
            Application.OpenURL($"https://steamcommunity.com/profiles/{steamId}");
        });

        BuildSmallButton(row, "PuckStats", () =>
        {
            Application.OpenURL($"https://puckstats.io/player/{steamId}");
        });

        BuildSmallButton(row, "Copy ID", () =>
        {
            GUIUtility.systemCopyBuffer = steamId;
            Plugin.AddLocalChatMessage($"<size=14><i>Copied Steam ID: {steamId}</i></size>");
        });

        // Click row to toggle accordion
        row.RegisterCallback<ClickEvent>(evt =>
        {
            if (_expandedPlayers.Contains(steamId))
                _expandedPlayers.Remove(steamId);
            else
                _expandedPlayers.Add(steamId);
            ModifierPanelUI.RefreshCurrentTab();
        });

        // Expanded section with stats
        if (isExpanded)
        {
            var expandedPanel = new VisualElement();
            expandedPanel.name = $"stats-panel-{steamId}";
            expandedPanel.style.paddingLeft = 34;
            expandedPanel.style.paddingRight = 12;
            expandedPanel.style.paddingTop = 6;
            expandedPanel.style.paddingBottom = 8;
            expandedPanel.style.backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.13f));
            expandedPanel.style.borderBottomLeftRadius = 4;
            expandedPanel.style.borderBottomRightRadius = 4;
            expandedPanel.style.borderLeftWidth = 3;
            expandedPanel.style.borderLeftColor = new StyleColor(teamColor);
            container.Add(expandedPanel);

            BuildPlayerStats(expandedPanel, steamId);
        }
    }

    private static void BuildPlayerStats(VisualElement parent, string steamId)
    {
        var stats = PlayerStatsStore.GetStats(steamId);
        if (stats == null)
        {
            var noStats = new Label("No match stats available.");
            noStats.style.fontSize = 12;
            noStats.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            parent.Add(noStats);
            return;
        }

        // -- Scoring / Shooting --
        BuildGroupLabel(parent, "Scoring / Shooting");
        var scoringRow = new VisualElement();
        scoringRow.style.flexDirection = FlexDirection.Row;
        scoringRow.style.flexWrap = Wrap.Wrap;
        scoringRow.style.marginBottom = 4;
        parent.Add(scoringRow);

        BuildStatCell(scoringRow, "Goals", stats.goals.ToString());
        BuildStatCell(scoringRow, "Assists", stats.assists.ToString());
        BuildStatCell(scoringRow, "Points", (stats.goals + stats.assists).ToString());
        BuildStatCell(scoringRow, "+/-", FormatPlusMinus(stats.plusMinus));
        if (stats.ownGoals > 0)
            BuildStatCell(scoringRow, "Own Goals", stats.ownGoals.ToString());
        BuildStatCell(scoringRow, "SOG", stats.shots.ToString());
        if (stats.shots > 0)
        {
            float shPct = (float)stats.goals / stats.shots * 100f;
            BuildStatCell(scoringRow, "SH%", $"{shPct:F0}%");
        }
        else
        {
            BuildStatCell(scoringRow, "SH%", "N/A");
        }

        // -- Puck Work --
        BuildGroupLabel(parent, "Puck Work");
        var puckWorkRow = new VisualElement();
        puckWorkRow.style.flexDirection = FlexDirection.Row;
        puckWorkRow.style.flexWrap = Wrap.Wrap;
        puckWorkRow.style.marginBottom = 4;
        parent.Add(puckWorkRow);

        BuildStatCell(puckWorkRow, "Touches", stats.touches.ToString());
        BuildStatCell(puckWorkRow, "Possessions", stats.possessions.ToString());
        BuildStatCell(puckWorkRow, "Poss. Time", FormatTime(stats.possessionSeconds));
        BuildStatCell(puckWorkRow, "Passes", stats.passes.ToString());
        BuildStatCell(puckWorkRow, "Pass Recv", stats.passesReceived.ToString());
        BuildStatCell(puckWorkRow, "Hits", stats.tacklesGiven.ToString());
        BuildStatCell(puckWorkRow, "Hits Taken", stats.tacklesReceived.ToString());
        BuildStatCell(puckWorkRow, "Takeaways", stats.takeaways.ToString());
        BuildStatCell(puckWorkRow, "Turnovers", stats.turnovers.ToString());
        BuildStatCell(puckWorkRow, "Faceoffs", stats.faceoffTotal > 0
            ? $"{stats.faceoffWins}/{stats.faceoffTotal}"
            : "0");

        // -- Goalie / Defense (only show if player has faced shots or blocked) --
        if (stats.shotsFaced > 0 || stats.blocks > 0)
        {
            BuildGroupLabel(parent, "Goalie / Defense");
            var defenseRow = new VisualElement();
            defenseRow.style.flexDirection = FlexDirection.Row;
            defenseRow.style.flexWrap = Wrap.Wrap;
            defenseRow.style.marginBottom = 4;
            parent.Add(defenseRow);

            if (stats.shotsFaced > 0)
            {
                float svPct = (float)stats.saves / stats.shotsFaced * 100f;
                BuildStatCell(defenseRow, "Saves", stats.saves.ToString());
                BuildStatCell(defenseRow, "SV%", $"{svPct:F1}%");
                BuildStatCell(defenseRow, "Shots Faced", stats.shotsFaced.ToString());
                BuildStatCell(defenseRow, "Stick Saves", stats.savesByStick.ToString());
                BuildStatCell(defenseRow, "Body Saves", stats.savesByBody.ToString());
                if (stats.savesHomePlate > 0)
                    BuildStatCell(defenseRow, "HP Saves", stats.savesHomePlate.ToString());
            }

            BuildStatCell(defenseRow, "Blocks", stats.blocks.ToString());
        }

        // -- Ice Time / Movement --
        BuildGroupLabel(parent, "Ice Time / Movement");
        var timeRow = new VisualElement();
        timeRow.style.flexDirection = FlexDirection.Row;
        timeRow.style.flexWrap = Wrap.Wrap;
        timeRow.style.marginBottom = 4;
        parent.Add(timeRow);

        BuildStatCell(timeRow, "Ice Time", FormatTime(stats.onIceSeconds));

        // "As Goalie" = real goalie role + SingleGoalie target time.
        // (Server keeps them as separate fields so puckstats.io can store them distinctly,
        //  but for the player-facing display the intuitive bucket is the sum.)
        int goalieSeconds = stats.asGoalieSeconds + stats.asSingleGoalieSeconds;
        int skaterSeconds = stats.onIceSeconds - goalieSeconds;
        if (skaterSeconds > 0 && goalieSeconds > 0)
            BuildStatCell(timeRow, "As Skater", FormatTime(skaterSeconds));
        if (goalieSeconds > 0)
            BuildStatCell(timeRow, "As Goalie", FormatTime(goalieSeconds));

        BuildStatCell(timeRow, "Distance", $"{stats.totalDistanceTravelled:F0}u");
        BuildStatCell(timeRow, "Avg Speed", $"{stats.averageSpeed:F1}");
        BuildStatCell(timeRow, "Jumps", stats.jumps.ToString());
        BuildStatCell(timeRow, "Air Time", FormatTime(stats.airborneSeconds));
        BuildStatCell(timeRow, "Slides", stats.slides.ToString());
        BuildStatCell(timeRow, "Juggles", stats.juggles.ToString());
        BuildStatCell(timeRow, "Revolutions", $"{stats.totalRevolutions:F1}");

        // Team participation bar (only show if player switched teams mid-match)
        if (stats.onBlueSeconds > 0 && stats.onRedSeconds > 0)
        {
            int totalTeamSeconds = stats.onBlueSeconds + stats.onRedSeconds;
            var teamBarContainer = new VisualElement();
            teamBarContainer.style.marginTop = 4;
            teamBarContainer.style.marginBottom = 4;
            parent.Add(teamBarContainer);

            float bluePct = (float)stats.onBlueSeconds / totalTeamSeconds * 100f;
            float redPct = 100f - bluePct;

            var teamLabel = new Label($"Team: {bluePct:F0}% Blue / {redPct:F0}% Red");
            teamLabel.style.fontSize = 10;
            teamLabel.style.color = new StyleColor(UIHelpers.TextMuted);
            teamLabel.style.marginBottom = 2;
            teamBarContainer.Add(teamLabel);

            var barRow = new VisualElement();
            barRow.style.flexDirection = FlexDirection.Row;
            barRow.style.height = 6;
            barRow.style.maxWidth = 150;
            barRow.style.borderTopLeftRadius = 3;
            barRow.style.borderTopRightRadius = 3;
            barRow.style.borderBottomLeftRadius = 3;
            barRow.style.borderBottomRightRadius = 3;
            barRow.style.overflow = Overflow.Hidden;
            teamBarContainer.Add(barRow);

            var blueBar = new VisualElement();
            blueBar.style.flexGrow = bluePct;
            blueBar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.4f, 0.9f));
            barRow.Add(blueBar);

            var redBar = new VisualElement();
            redBar.style.flexGrow = redPct;
            redBar.style.backgroundColor = new StyleColor(new Color(0.85f, 0.15f, 0.15f));
            barRow.Add(redBar);
        }
    }

    private static void BuildGroupLabel(VisualElement parent, string text)
    {
        var label = new Label(text);
        label.style.fontSize = 10;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = new StyleColor(UIHelpers.TextMuted);
        label.style.marginTop = 4;
        label.style.marginBottom = 2;
        label.style.letterSpacing = 1;
        parent.Add(label);
    }

    private static void BuildStatCell(VisualElement parent, string label, string value)
    {
        var cell = new VisualElement();
        cell.style.minWidth = 80;
        cell.style.marginRight = 12;
        cell.style.marginBottom = 2;
        parent.Add(cell);

        var valLabel = new Label(value);
        valLabel.style.fontSize = 14;
        valLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        valLabel.style.color = UIHelpers.TextPrimary;
        cell.Add(valLabel);

        var nameLabel = new Label(label);
        nameLabel.style.fontSize = 10;
        nameLabel.style.color = new StyleColor(UIHelpers.TextMuted);
        cell.Add(nameLabel);
    }

    private static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 60) return $"{totalSeconds}s";
        int min = totalSeconds / 60;
        int sec = totalSeconds % 60;
        return $"{min}:{sec:D2}";
    }

    private static string FormatPlusMinus(int value)
    {
        if (value > 0) return $"+{value}";
        if (value < 0) return value.ToString();
        return "0";
    }

    private static void ShowModTooltip(VisualElement anchor, PlayerModStore.PlayerModEntry modInfo)
    {
        HideModTooltip();

        var tooltip = new VisualElement();
        tooltip.style.position = Position.Absolute;
        tooltip.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.95f));
        tooltip.style.paddingLeft = 10;
        tooltip.style.paddingRight = 10;
        tooltip.style.paddingTop = 8;
        tooltip.style.paddingBottom = 8;
        tooltip.style.borderTopLeftRadius = 4;
        tooltip.style.borderTopRightRadius = 4;
        tooltip.style.borderBottomLeftRadius = 4;
        tooltip.style.borderBottomRightRadius = 4;
        UIHelpers.SetBorder(tooltip, 1, new Color(0.3f, 0.3f, 0.3f));
        tooltip.style.minWidth = 180;

        foreach (string modName in modInfo.modNames)
        {
            var lineLabel = new Label(modName);
            lineLabel.style.fontSize = 12;
            lineLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            lineLabel.style.marginBottom = 2;
            lineLabel.style.whiteSpace = WhiteSpace.Normal;
            tooltip.Add(lineLabel);
        }

        if (modInfo.localModCount > 0)
        {
            var localLabel = new Label($"{modInfo.localModCount} local mod{(modInfo.localModCount != 1 ? "s" : "")}");
            localLabel.style.fontSize = 12;
            localLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            tooltip.Add(localLabel);
        }

        // Add to the panel root so it's not clipped by scroll/row
        var panelRoot = anchor.panel.visualTree;
        panelRoot.Add(tooltip);
        _activeTooltip = tooltip;

        // Position below the badge
        anchor.schedule.Execute(() =>
        {
            var badgeRect = anchor.worldBound;
            tooltip.style.left = badgeRect.x;
            tooltip.style.top = badgeRect.yMax + 4;
        });
    }

    private static void HideModTooltip()
    {
        if (_activeTooltip != null)
        {
            _activeTooltip.RemoveFromHierarchy();
            _activeTooltip = null;
        }
    }

    private static VisualElement BuildTeamPill(ChatFormatting.TeamEntry teamEntry)
    {
        Color teamColor = UIHelpers.ParseHexColor(teamEntry.hexColor);
        var eisTeam = EISTeamData.GetTeamByAcronym(teamEntry.acronym);
        string displayName = eisTeam?.name ?? teamEntry.acronym;

        var pill = new VisualElement();
        pill.style.flexDirection = FlexDirection.Row;
        pill.style.alignItems = Align.Center;
        pill.style.backgroundColor = new StyleColor(new Color(teamColor.r * 0.3f, teamColor.g * 0.3f, teamColor.b * 0.3f, 0.8f));
        pill.style.paddingLeft = 4;
        pill.style.paddingRight = 6;
        pill.style.paddingTop = 2;
        pill.style.paddingBottom = 2;
        pill.style.borderTopLeftRadius = 8;
        pill.style.borderTopRightRadius = 8;
        pill.style.borderBottomLeftRadius = 8;
        pill.style.borderBottomRightRadius = 8;
        pill.style.borderTopWidth = 1;
        pill.style.borderBottomWidth = 1;
        pill.style.borderLeftWidth = 1;
        pill.style.borderRightWidth = 1;
        pill.style.borderTopColor = new StyleColor(new Color(teamColor.r, teamColor.g, teamColor.b, 0.4f));
        pill.style.borderBottomColor = new StyleColor(new Color(teamColor.r, teamColor.g, teamColor.b, 0.4f));
        pill.style.borderLeftColor = new StyleColor(new Color(teamColor.r, teamColor.g, teamColor.b, 0.4f));
        pill.style.borderRightColor = new StyleColor(new Color(teamColor.r, teamColor.g, teamColor.b, 0.4f));

        // Logo placeholder - will be filled when loaded
        var logoEl = new VisualElement();
        logoEl.style.width = 14;
        logoEl.style.height = 14;
        logoEl.style.marginRight = 4;
        logoEl.style.borderTopLeftRadius = 7;
        logoEl.style.borderTopRightRadius = 7;
        logoEl.style.borderBottomLeftRadius = 7;
        logoEl.style.borderBottomRightRadius = 7;
        pill.Add(logoEl);

        // Proactively fetch logo and update element when ready
        EISTeamData.GetLogoAsync(teamEntry.acronym, tex =>
        {
            if (tex != null)
                logoEl.style.backgroundImage = new StyleBackground(tex);
            else
                logoEl.style.display = DisplayStyle.None; // hide placeholder if no logo
        });

        var label = new Label(displayName);
        label.style.fontSize = 10;
        label.style.color = new StyleColor(teamColor);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        pill.Add(label);

        return pill;
    }

    private static void BuildSmallButton(VisualElement parent, string text, System.Action onClick)
    {
        var btn = new Button(onClick);
        btn.text = text;
        btn.style.fontSize = 11;
        btn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        btn.style.color = UIHelpers.TextPrimary;
        btn.style.paddingLeft = 6;
        btn.style.paddingRight = 6;
        btn.style.paddingTop = 2;
        btn.style.paddingBottom = 2;
        btn.style.marginLeft = 3;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;
        parent.Add(btn);
    }
}
