using System.Collections.Generic;
using System.Linq;
using ToastersRinkCompanion.extras;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Actions tab for puck spawning, training tools, game management.
/// Shows current server state for toggles.
/// </summary>
public static class TrainingTab
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

        var header = new Label("Actions");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = UIHelpers.TextPrimary;
        header.style.marginBottom = 16;
        content.Add(header);

        // Two column layout
        var columns = new VisualElement();
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.flexWrap = Wrap.NoWrap;
        content.Add(columns);

        var leftCol = new VisualElement();
        leftCol.style.flexGrow = 1;
        leftCol.style.flexBasis = 0;
        leftCol.style.marginRight = 8;
        columns.Add(leftCol);

        var rightCol = new VisualElement();
        rightCol.style.flexGrow = 1;
        rightCol.style.flexBasis = 0;
        rightCol.style.marginLeft = 8;
        columns.Add(rightCol);

        // Left column: Puck Management + Game
        BuildSection(leftCol, "Puck Management");
        BuildCommandRow(leftCol, "Spawn Puck", "Summon a puck onto the ice", "/s", "/s");
        BuildCommandRow(leftCol, "Spawn 3 Pucks", null, "/s 3", "/s 3");
        BuildCommandRow(leftCol, "Spawn 5 Pucks", null, "/s 5", "/s 5");
        BuildCommandRow(leftCol, "Bring Pucks", "Bring nearby pucks to your position", "/bp", "/bp");
        BuildCommandRow(leftCol, "Bring All Pucks", "Bring all pucks on the ice to you", "/bp a", "/bp a");
        BuildCommandRow(leftCol, "Reset Pucks", "Move all pucks back to center ice", "/rp", "/rp");
        BuildCommandRow(leftCol, "Empty Pucks", "Remove all pucks from the ice", "/ep", "/ep");

        BuildSection(leftCol, "Game");
        BuildTimeRow(leftCol);
        BuildToggleRow(leftCol, "Autoclean", "Automatically clean pucks that enter the net", "/autoclean",
            ServerState.AutocleanEnabled, "/autoclean");

        // Right column: Training Tools + Team
        BuildSection(rightCol, "Training Tools");
        BuildToggleRow(rightCol, "Passer", "Shoots pucks at you for passing practice", "/passer",
            ServerState.PasserEnabled, "/passer");
        BuildToggleRow(rightCol, "Cones", "Place training cones on the ice", "/cones",
            ServerState.ConesEnabled, "/cones");
        BuildToggleRow(rightCol, "Goalie Trainer", "Shoots pucks at the net for goalie practice", "/goalie",
            ServerState.BlueGoalieEnabled || ServerState.RedGoalieEnabled,
            "/goalie",
            ServerState.BlueGoalieEnabled && ServerState.RedGoalieEnabled ? "Both sides"
                : ServerState.BlueGoalieEnabled ? "Blue side"
                : ServerState.RedGoalieEnabled ? "Red side" : null);
        BuildDummyRow(rightCol);
        BuildToggleRow(rightCol, "Puck on String", "Attach the puck to your stick", "/string",
            ServerState.PuckOnStringPlayerCount > 0, "/string",
            ServerState.PuckOnStringPlayerCount > 0 ? $"{ServerState.PuckOnStringPlayerCount} active" : null);
        BuildWatchPucksOfRow(rightCol);

        BuildSection(rightCol, "Drill Snapshots");
        BuildDrillRow(rightCol);

        BuildSection(rightCol, "Team Names");
        BuildTeamNameRow(rightCol);
    }

    private static void BuildSection(VisualElement parent, string title)
    {
        var label = new Label(title);
        label.style.fontSize = 15;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = new StyleColor(UIHelpers.ActiveGreen);
        label.style.marginTop = 8;
        label.style.marginBottom = 6;
        parent.Add(label);

        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        sep.style.marginBottom = 6;
        parent.Add(sep);
    }

    private static void BuildCommandRow(VisualElement parent, string name, string description,
        string command, string cmdLabel)
    {
        var row = MakeRow(parent);

        var infoCol = new VisualElement();
        infoCol.style.flexGrow = 1;
        row.Add(infoCol);

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        infoCol.Add(nameRow);

        var nameLabel = new Label(name);
        nameLabel.style.fontSize = 13;
        nameLabel.style.color = UIHelpers.TextPrimary;
        nameRow.Add(nameLabel);

        if (!string.IsNullOrEmpty(cmdLabel))
        {
            var cmdText = new Label(cmdLabel);
            cmdText.style.fontSize = 10;
            cmdText.style.color = new StyleColor(UIHelpers.TextMuted);
            cmdText.style.marginLeft = 8;
            nameRow.Add(cmdText);
        }

        if (!string.IsNullOrEmpty(description))
        {
            var descLabel = new Label(description);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            infoCol.Add(descLabel);
        }

        row.Add(MakeRunButton(command));
    }

    private static void BuildToggleRow(VisualElement parent, string name, string description,
        string command, bool isEnabled, string cmdLabel, string extraInfo = null)
    {
        var row = MakeRow(parent);

        // Status dot
        var dot = new VisualElement();
        dot.style.width = 8;
        dot.style.height = 8;
        dot.style.borderTopLeftRadius = 4;
        dot.style.borderTopRightRadius = 4;
        dot.style.borderBottomLeftRadius = 4;
        dot.style.borderBottomRightRadius = 4;
        dot.style.backgroundColor = isEnabled
            ? new StyleColor(UIHelpers.ActiveGreen)
            : new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dot.style.marginRight = 8;
        row.Add(dot);

        var infoCol = new VisualElement();
        infoCol.style.flexGrow = 1;
        row.Add(infoCol);

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        infoCol.Add(nameRow);

        var nameLabel = new Label(name);
        nameLabel.style.fontSize = 13;
        nameLabel.style.color = isEnabled ? UIHelpers.TextPrimary : new StyleColor(UIHelpers.TextSecondary);
        nameRow.Add(nameLabel);

        if (!string.IsNullOrEmpty(cmdLabel))
        {
            var cmdText = new Label(cmdLabel);
            cmdText.style.fontSize = 10;
            cmdText.style.color = new StyleColor(UIHelpers.TextMuted);
            cmdText.style.marginLeft = 8;
            nameRow.Add(cmdText);
        }

        if (!string.IsNullOrEmpty(description))
        {
            var descLabel = new Label(description);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            infoCol.Add(descLabel);
        }

        if (extraInfo != null)
        {
            var infoLabel = new Label(extraInfo);
            infoLabel.style.fontSize = 11;
            infoLabel.style.color = new StyleColor(UIHelpers.AccentBlue);
            infoCol.Add(infoLabel);
        }

        var btn = MakeRunButton(command);
        btn.text = isEnabled ? "Disable" : "Enable";
        row.Add(btn);
    }

    private static void BuildDummyRow(VisualElement parent)
    {
        var row = MakeRow(parent);

        // Status dot
        var anyEnabled = ServerState.BlueDummyEnabled || ServerState.RedDummyEnabled;
        var dot = new VisualElement();
        dot.style.width = 8;
        dot.style.height = 8;
        dot.style.borderTopLeftRadius = 4;
        dot.style.borderTopRightRadius = 4;
        dot.style.borderBottomLeftRadius = 4;
        dot.style.borderBottomRightRadius = 4;
        dot.style.backgroundColor = anyEnabled
            ? new StyleColor(UIHelpers.ActiveGreen)
            : new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dot.style.marginRight = 8;
        row.Add(dot);

        var infoCol = new VisualElement();
        infoCol.style.flexGrow = 1;
        row.Add(infoCol);

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        infoCol.Add(nameRow);

        var nameLabel = new Label("Dummy");
        nameLabel.style.fontSize = 13;
        nameLabel.style.color = anyEnabled ? UIHelpers.TextPrimary : new StyleColor(UIHelpers.TextSecondary);
        nameRow.Add(nameLabel);

        var cmdText = new Label("/dummy");
        cmdText.style.fontSize = 10;
        cmdText.style.color = new StyleColor(UIHelpers.TextMuted);
        cmdText.style.marginLeft = 8;
        nameRow.Add(cmdText);

        var descLabel = new Label("Spawns a skating dummy player");
        descLabel.style.fontSize = 11;
        descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        infoCol.Add(descLabel);

        // Blue toggle button
        var blueBtn = new Button(() =>
        {
            NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/dummy blue", false, false);
            ModifierPanelUI.Hide();
        });
        blueBtn.text = ServerState.BlueDummyEnabled ? "Blue ✓" : "Blue";
        blueBtn.style.fontSize = 12;
        blueBtn.style.backgroundColor = ServerState.BlueDummyEnabled
            ? new StyleColor(new Color(0.2f, 0.35f, 0.7f))
            : new StyleColor(UIHelpers.BgButton);
        blueBtn.style.color = UIHelpers.TextPrimary;
        blueBtn.style.paddingLeft = 8;
        blueBtn.style.paddingRight = 8;
        blueBtn.style.paddingTop = 3;
        blueBtn.style.paddingBottom = 3;
        blueBtn.style.marginRight = 4;
        blueBtn.style.borderTopLeftRadius = 0;
        blueBtn.style.borderTopRightRadius = 0;
        blueBtn.style.borderBottomLeftRadius = 0;
        blueBtn.style.borderBottomRightRadius = 0;
        row.Add(blueBtn);

        // Red toggle button
        var redBtn = new Button(() =>
        {
            NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/dummy red", false, false);
            ModifierPanelUI.Hide();
        });
        redBtn.text = ServerState.RedDummyEnabled ? "Red ✓" : "Red";
        redBtn.style.fontSize = 12;
        redBtn.style.backgroundColor = ServerState.RedDummyEnabled
            ? new StyleColor(new Color(0.7f, 0.15f, 0.15f))
            : new StyleColor(UIHelpers.BgButton);
        redBtn.style.color = UIHelpers.TextPrimary;
        redBtn.style.paddingLeft = 8;
        redBtn.style.paddingRight = 8;
        redBtn.style.paddingTop = 3;
        redBtn.style.paddingBottom = 3;
        redBtn.style.borderTopLeftRadius = 0;
        redBtn.style.borderTopRightRadius = 0;
        redBtn.style.borderBottomLeftRadius = 0;
        redBtn.style.borderBottomRightRadius = 0;
        row.Add(redBtn);
    }

    private static void BuildTimeRow(VisualElement parent)
    {
        var row = MakeRow(parent);

        var infoCol = new VisualElement();
        infoCol.style.flexGrow = 1;
        row.Add(infoCol);

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        infoCol.Add(nameRow);

        var nameLabel = new Label("Set Time");
        nameLabel.style.fontSize = 13;
        nameLabel.style.color = UIHelpers.TextPrimary;
        nameRow.Add(nameLabel);

        var cmdText = new Label("/time <seconds>");
        cmdText.style.fontSize = 10;
        cmdText.style.color = new StyleColor(UIHelpers.TextMuted);
        cmdText.style.marginLeft = 8;
        nameRow.Add(cmdText);

        var descLabel = new Label("Set remaining time in the current period");
        descLabel.style.fontSize = 11;
        descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        infoCol.Add(descLabel);

        var field = new TextField();
        field.value = "300";
        field.style.minWidth = 50;
        field.style.maxWidth = 60;
        field.style.marginRight = 4;
        field.RegisterCallback<AttachToPanelEvent>(evt => UIHelpers.StyleInputField(field));
        row.Add(field);

        var btn = new Button(() =>
        {
            NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(
                $"/time {field.value}", false, false);
            ModifierPanelUI.Hide();
        });
        btn.text = "Set";
        StyleSmallButton(btn);
        row.Add(btn);
    }

    private static void BuildTeamNameRow(VisualElement parent)
    {
        // Show current names
        var currentRow = new VisualElement();
        currentRow.style.flexDirection = FlexDirection.Row;
        currentRow.style.marginBottom = 6;
        parent.Add(currentRow);

        var blueNameLabel = new Label($"Blue: {ServerState.BlueTeamName}");
        blueNameLabel.style.fontSize = 12;
        blueNameLabel.style.color = new StyleColor(new Color(0.3f, 0.5f, 1f));
        blueNameLabel.style.marginRight = 16;
        currentRow.Add(blueNameLabel);

        var redNameLabel = new Label($"Red: {ServerState.RedTeamName}");
        redNameLabel.style.fontSize = 12;
        redNameLabel.style.color = new StyleColor(new Color(0.9f, 0.2f, 0.2f));
        currentRow.Add(redNameLabel);

        // Command hint
        var cmdHint = new Label("/setteamname <blue/red> <name>");
        cmdHint.style.fontSize = 10;
        cmdHint.style.color = new StyleColor(UIHelpers.TextMuted);
        cmdHint.style.marginBottom = 4;
        parent.Add(cmdHint);

        // Input row
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 4;
        parent.Add(row);

        var field = new TextField();
        field.value = "";
        field.style.flexGrow = 1;
        field.style.marginRight = 6;
        field.RegisterCallback<AttachToPanelEvent>(evt => UIHelpers.StyleInputField(field));
        row.Add(field);

        var blueBtn = new Button(() =>
        {
            if (!string.IsNullOrWhiteSpace(field.value))
                NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(
                    $"/setteamname blue {field.value}", false, false);
        });
        blueBtn.text = "Set Blue";
        blueBtn.style.fontSize = 12;
        blueBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.35f, 0.7f));
        blueBtn.style.color = UIHelpers.TextPrimary;
        blueBtn.style.paddingLeft = 8;
        blueBtn.style.paddingRight = 8;
        blueBtn.style.paddingTop = 3;
        blueBtn.style.paddingBottom = 3;
        blueBtn.style.marginRight = 4;
        blueBtn.style.borderTopLeftRadius = 0;
        blueBtn.style.borderTopRightRadius = 0;
        blueBtn.style.borderBottomLeftRadius = 0;
        blueBtn.style.borderBottomRightRadius = 0;
        row.Add(blueBtn);

        var redBtn = new Button(() =>
        {
            if (!string.IsNullOrWhiteSpace(field.value))
                NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(
                    $"/setteamname red {field.value}", false, false);
        });
        redBtn.text = "Set Red";
        redBtn.style.fontSize = 12;
        redBtn.style.backgroundColor = new StyleColor(new Color(0.7f, 0.15f, 0.15f));
        redBtn.style.color = UIHelpers.TextPrimary;
        redBtn.style.paddingLeft = 8;
        redBtn.style.paddingRight = 8;
        redBtn.style.paddingTop = 3;
        redBtn.style.paddingBottom = 3;
        redBtn.style.borderTopLeftRadius = 0;
        redBtn.style.borderTopRightRadius = 0;
        redBtn.style.borderBottomLeftRadius = 0;
        redBtn.style.borderBottomRightRadius = 0;
        row.Add(redBtn);
    }

    private static void BuildDrillRow(VisualElement parent)
    {
        var descLabel = new Label("Save your position + puck state, then reload it to repeat drills. Warmup only.");
        descLabel.style.fontSize = 11;
        descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.marginBottom = 4;
        parent.Add(descLabel);

        var row = MakeRow(parent);

        var saveBtn = new Button(() =>
        {
            NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/drill save", false, false);
            ModifierPanelUI.Hide();
        });
        saveBtn.text = "Save";
        StyleSmallButton(saveBtn);
        row.Add(saveBtn);

        var saveHint = new Label(SettingsTab.GetKeyDisplayName(Plugin.modSettings.drillSaveKeybind));
        saveHint.style.fontSize = 10;
        saveHint.style.color = new StyleColor(UIHelpers.TextMuted);
        saveHint.style.marginLeft = 4;
        saveHint.style.marginRight = 12;
        row.Add(saveHint);

        var loadBtn = new Button(() =>
        {
            NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/drill load", false, false);
            ModifierPanelUI.Hide();
        });
        loadBtn.text = "Load";
        StyleSmallButton(loadBtn);
        row.Add(loadBtn);

        var loadHint = new Label(SettingsTab.GetKeyDisplayName(Plugin.modSettings.drillLoadKeybind));
        loadHint.style.fontSize = 10;
        loadHint.style.color = new StyleColor(UIHelpers.TextMuted);
        loadHint.style.marginLeft = 4;
        row.Add(loadHint);
    }

    private static void BuildWatchPucksOfRow(VisualElement parent)
    {
        var isActive = WatchPucksOf.target != null;
        var row = MakeRow(parent);

        // Status dot
        var dot = new VisualElement();
        dot.style.width = 8;
        dot.style.height = 8;
        dot.style.borderTopLeftRadius = 4;
        dot.style.borderTopRightRadius = 4;
        dot.style.borderBottomLeftRadius = 4;
        dot.style.borderBottomRightRadius = 4;
        dot.style.backgroundColor = isActive
            ? new StyleColor(UIHelpers.ActiveGreen)
            : new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dot.style.marginRight = 8;
        row.Add(dot);

        var infoCol = new VisualElement();
        infoCol.style.flexGrow = 1;
        row.Add(infoCol);

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        infoCol.Add(nameRow);

        var nameLabel = new Label("Watch Pucks Of");
        nameLabel.style.fontSize = 13;
        nameLabel.style.color = isActive ? UIHelpers.TextPrimary : new StyleColor(UIHelpers.TextSecondary);
        nameRow.Add(nameLabel);

        var cmdText = new Label("/watchpucksof");
        cmdText.style.fontSize = 10;
        cmdText.style.color = new StyleColor(UIHelpers.TextMuted);
        cmdText.style.marginLeft = 8;
        nameRow.Add(cmdText);

        var descLabel = new Label("Follow another player's puck with your camera");
        descLabel.style.fontSize = 11;
        descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        infoCol.Add(descLabel);

        if (isActive)
        {
            var activeLabel = new Label($"Watching {WatchPucksOf.target.Username.Value}");
            activeLabel.style.fontSize = 11;
            activeLabel.style.color = new StyleColor(UIHelpers.AccentBlue);
            infoCol.Add(activeLabel);

            var disableBtn = new Button(() =>
            {
                NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/watchpucksof", false, false);
                ModifierPanelUI.Hide();
            });
            disableBtn.text = "Disable";
            StyleSmallButton(disableBtn);
            row.Add(disableBtn);
        }
        else
        {
            var players = PlayerManager.Instance.GetPlayers();
            var localPlayer = PlayerManager.Instance.GetLocalPlayer();
            var choices = new List<string>();
            var playerMap = new Dictionary<string, Player>();

            if (players != null)
            {
                foreach (var player in players.OrderBy(p => p.Number.Value))
                {
                    if (player == localPlayer) continue;
                    var display = $"#{player.Number.Value} {player.Username.Value}";
                    choices.Add(display);
                    playerMap[display] = player;
                }
            }

            if (choices.Count > 0)
            {
                var dropdown = new PopupField<string>(choices, 0);
                UIHelpers.StyleDropdown(dropdown, 120, 180);
                row.Add(dropdown);

                var watchBtn = new Button(() =>
                {
                    if (playerMap.TryGetValue(dropdown.value, out var selected))
                    {
                        NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(
                            $"/watchpucksof {selected.Number.Value}", false, false);
                        ModifierPanelUI.Hide();
                    }
                });
                watchBtn.text = "Watch";
                StyleSmallButton(watchBtn);
                row.Add(watchBtn);
            }
            else
            {
                var noPlayers = new Label("No players");
                noPlayers.style.fontSize = 11;
                noPlayers.style.color = new StyleColor(UIHelpers.TextMuted);
                row.Add(noPlayers);
            }
        }
    }

    private static VisualElement MakeRow(VisualElement parent)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 10;
        row.style.paddingRight = 6;
        row.style.paddingTop = 4;
        row.style.paddingBottom = 4;
        row.style.marginBottom = 2;
        row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        row.style.borderTopLeftRadius = 4;
        row.style.borderTopRightRadius = 4;
        row.style.borderBottomLeftRadius = 4;
        row.style.borderBottomRightRadius = 4;
        parent.Add(row);
        return row;
    }

    private static Button MakeRunButton(string command)
    {
        var btn = new Button(() =>
        {
            NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(command, false, false);
            ModifierPanelUI.Hide();
        });
        btn.text = "Run";
        StyleSmallButton(btn);
        return btn;
    }

    private static void StyleSmallButton(Button btn)
    {
        btn.style.fontSize = 12;
        btn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        btn.style.color = UIHelpers.TextPrimary;
        btn.style.paddingLeft = 10;
        btn.style.paddingRight = 10;
        btn.style.paddingTop = 3;
        btn.style.paddingBottom = 3;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;
    }
}
