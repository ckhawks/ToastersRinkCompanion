using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Players tab showing connected players with profile links.
/// </summary>
public static class PlayersTab
{
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

        // Sort: by team then number
        // Blue first, then Red, then others
        var sorted = players
            .OrderBy(p => p.Team == PlayerTeam.Blue ? 0 : p.Team == PlayerTeam.Red ? 1 : 2)
            .ThenBy(p => p.Number.Value).ToList();

        PlayerTeam lastTeam = (PlayerTeam)(-1);
        foreach (var player in sorted)
        {
            if (player.Team != lastTeam)
            {
                lastTeam = player.Team;
                bool isBlue = player.Team == PlayerTeam.Blue;
                var teamLabel = new Label(isBlue ? "Blue" : player.Team == PlayerTeam.Red ? "Red" : player.Team.ToString());
                teamLabel.style.fontSize = 14;
                teamLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                teamLabel.style.color = isBlue
                    ? new StyleColor(new Color(0.3f, 0.5f, 1f))
                    : new StyleColor(new Color(0.9f, 0.2f, 0.2f));
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
        Color teamColor = player.Team == PlayerTeam.Blue
            ? new Color(0.3f, 0.5f, 1f)
            : new Color(0.9f, 0.2f, 0.2f);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 12;
        row.style.paddingRight = 8;
        row.style.paddingTop = 6;
        row.style.paddingBottom = 6;
        row.style.marginBottom = 2;
        row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        row.style.borderTopLeftRadius = 4;
        row.style.borderTopRightRadius = 4;
        row.style.borderBottomLeftRadius = 4;
        row.style.borderBottomRightRadius = 4;
        parent.Add(row);

        // Team color dot
        var dot = new VisualElement();
        dot.style.width = 8;
        dot.style.height = 8;
        dot.style.borderTopLeftRadius = 4;
        dot.style.borderTopRightRadius = 4;
        dot.style.borderBottomLeftRadius = 4;
        dot.style.borderBottomRightRadius = 4;
        dot.style.backgroundColor = new StyleColor(teamColor);
        dot.style.marginRight = 10;
        row.Add(dot);

        // Number
        var numberLabel = new Label($"#{number}");
        numberLabel.style.fontSize = 14;
        numberLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        numberLabel.style.minWidth = 35;
        row.Add(numberLabel);

        // Name
        var nameLabel = new Label(username);
        nameLabel.style.fontSize = 14;
        nameLabel.style.color = UIHelpers.TextPrimary;
        nameLabel.style.flexGrow = 1;
        row.Add(nameLabel);

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
        BuildSmallButton(row, "Open Steam Profile", () =>
        {
            Application.OpenURL($"https://steamcommunity.com/profiles/{steamId}");
        });

        BuildSmallButton(row, "PuckStats", () =>
        {
            Application.OpenURL($"https://puckstats.io/player/{steamId}");
        });

        BuildSmallButton(row, "Copy Steam ID", () =>
        {
            GUIUtility.systemCopyBuffer = steamId;
            Plugin.AddLocalChatMessage($"<size=14><i>Copied Steam ID: {steamId}</i></size>");
        });
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
