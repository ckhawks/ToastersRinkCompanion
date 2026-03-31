using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Admin tab for player management: kick, ban, jail, messaging.
/// </summary>
public static class AdminTab
{
    // Track confirmation state: button hash -> timestamp of first click
    private static readonly Dictionary<string, float> _pendingConfirms = new();
    private const float ConfirmTimeout = 3f;

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

        var header = new Label("Admin");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = UIHelpers.TextPrimary;
        header.style.marginBottom = 16;
        content.Add(header);

        if (!ModifierRegistry.IsAdmin)
        {
            var noAccess = new Label("You do not have admin access on this server.");
            noAccess.style.color = UIHelpers.TextMuted;
            noAccess.style.fontSize = 14;
            content.Add(noAccess);
            return;
        }

        // Send message
        BuildSection(content, "Server Message");
        BuildMessageRow(content);

        // Player actions
        BuildSection(content, "Player Actions");

        // Shared reason field
        var reasonRow = new VisualElement();
        reasonRow.style.flexDirection = FlexDirection.Row;
        reasonRow.style.alignItems = Align.Center;
        reasonRow.style.marginBottom = 10;
        content.Add(reasonRow);

        var reasonLabel = new Label("Reason:");
        reasonLabel.style.fontSize = 13;
        reasonLabel.style.color = UIHelpers.TextSecondary;
        reasonLabel.style.marginRight = 8;
        reasonRow.Add(reasonLabel);

        var reasonField = new TextField();
        reasonField.value = "";
        reasonField.style.flexGrow = 1;
        reasonField.RegisterCallback<AttachToPanelEvent>(evt => UIHelpers.StyleInputField(reasonField));
        reasonRow.Add(reasonField);

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
            .OrderBy(p => p.Team == PlayerTeam.Blue ? 0 : p.Team == PlayerTeam.Red ? 1 : 2)
            .ThenBy(p => p.Number.Value).ToList();
        foreach (var player in sorted)
        {
            BuildPlayerActionRow(content, player, reasonField);
        }
    }

    private static void BuildSection(VisualElement parent, string title)
    {
        var label = new Label(title);
        label.style.fontSize = 15;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = new StyleColor(new Color(0.9f, 0.2f, 0.2f));
        label.style.marginTop = 8;
        label.style.marginBottom = 6;
        parent.Add(label);

        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        sep.style.marginBottom = 6;
        parent.Add(sep);
    }

    private static void BuildMessageRow(VisualElement parent)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 12;
        parent.Add(row);

        var field = new TextField();
        field.value = "";
        field.style.flexGrow = 1;
        field.style.marginRight = 8;
        field.RegisterCallback<AttachToPanelEvent>(evt => UIHelpers.StyleInputField(field));
        row.Add(field);

        var sendBtn = new Button(() =>
        {
            if (!string.IsNullOrWhiteSpace(field.value))
            {
                NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(field.value, false, false);
                field.value = "";
            }
        });
        sendBtn.text = "Send";
        sendBtn.style.fontSize = 14;
        sendBtn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        sendBtn.style.color = UIHelpers.TextPrimary;
        sendBtn.style.paddingLeft = 12;
        sendBtn.style.paddingRight = 12;
        sendBtn.style.paddingTop = 4;
        sendBtn.style.paddingBottom = 4;
        sendBtn.style.borderTopLeftRadius = 0;
        sendBtn.style.borderTopRightRadius = 0;
        sendBtn.style.borderBottomLeftRadius = 0;
        sendBtn.style.borderBottomRightRadius = 0;
        row.Add(sendBtn);
    }

    private static void BuildPlayerActionRow(VisualElement parent, Player player, TextField reasonField)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 12;
        row.style.paddingRight = 8;
        row.style.paddingTop = 5;
        row.style.paddingBottom = 5;
        row.style.marginBottom = 2;
        row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        row.style.borderTopLeftRadius = 4;
        row.style.borderTopRightRadius = 4;
        row.style.borderBottomLeftRadius = 4;
        row.style.borderBottomRightRadius = 4;
        parent.Add(row);

        // Team dot
        Color teamColor = player.Team == PlayerTeam.Blue
            ? new Color(0.3f, 0.5f, 1f)
            : new Color(0.9f, 0.2f, 0.2f);
        var dot = new VisualElement();
        dot.style.width = 8;
        dot.style.height = 8;
        dot.style.borderTopLeftRadius = 4;
        dot.style.borderTopRightRadius = 4;
        dot.style.borderBottomLeftRadius = 4;
        dot.style.borderBottomRightRadius = 4;
        dot.style.backgroundColor = new StyleColor(teamColor);
        dot.style.marginRight = 8;
        row.Add(dot);

        // Player info
        string username = player.Username.Value.ToString();
        int number = player.Number.Value;
        var nameLabel = new Label($"#{number} {username}");
        nameLabel.style.fontSize = 14;
        nameLabel.style.color = UIHelpers.TextPrimary;
        nameLabel.style.flexGrow = 1;
        row.Add(nameLabel);

        // Action buttons with double-click confirmation
        string reason() => string.IsNullOrWhiteSpace(reasonField.value) ? "" : $" {reasonField.value}";
        BuildConfirmButton(row, "Kick", $"/kick {number}{reason()}", new Color(0.8f, 0.3f, 0.1f), $"kick_{number}", reasonField);
        BuildConfirmButton(row, "Ban", $"/ban {username}{reason()}", new Color(0.7f, 0.1f, 0.1f), $"ban_{username}", reasonField);
        BuildConfirmButton(row, "Jail", $"/jail {number}", new Color(0.5f, 0.3f, 0.7f), $"jail_{number}", null);
        BuildConfirmButton(row, "Jail Up", $"/jailup {number}", new Color(0.4f, 0.25f, 0.6f), $"jailup_{number}", null);
    }

    private static void BuildConfirmButton(VisualElement parent, string text, string command,
        Color bgColor, string confirmKey, TextField reasonField)
    {
        var btn = new Button();
        btn.text = text;
        btn.style.fontSize = 12;
        btn.style.backgroundColor = new StyleColor(bgColor);
        btn.style.color = UIHelpers.TextPrimary;
        btn.style.paddingLeft = 8;
        btn.style.paddingRight = 8;
        btn.style.paddingTop = 3;
        btn.style.paddingBottom = 3;
        btn.style.marginLeft = 4;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;

        btn.RegisterCallback<ClickEvent>(evt =>
        {
            float now = Time.time;

            if (_pendingConfirms.TryGetValue(confirmKey, out float firstClickTime)
                && now - firstClickTime < ConfirmTimeout)
            {
                // Second click — execute
                _pendingConfirms.Remove(confirmKey);
                // Build command with current reason value
                string cmd = command;
                if (reasonField != null && !string.IsNullOrWhiteSpace(reasonField.value))
                {
                    // Re-append reason at execution time
                    string baseCmd = cmd.Contains(" ") ? cmd : cmd;
                    cmd = $"{baseCmd} {reasonField.value}";
                }
                NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(cmd, false, false);
                btn.text = text;
                btn.style.backgroundColor = new StyleColor(bgColor);
                ModifierPanelUI.RefreshCurrentTab();
            }
            else
            {
                // First click — show confirm state
                _pendingConfirms[confirmKey] = now;
                btn.text = $"Confirm?";
                btn.style.backgroundColor = new StyleColor(new Color(0.9f, 0.7f, 0.1f));
                btn.style.color = new StyleColor(Color.black);

                // Reset after timeout
                btn.schedule.Execute(() =>
                {
                    if (_pendingConfirms.ContainsKey(confirmKey)
                        && Time.time - _pendingConfirms[confirmKey] >= ConfirmTimeout)
                    {
                        _pendingConfirms.Remove(confirmKey);
                        btn.text = text;
                        btn.style.backgroundColor = new StyleColor(bgColor);
                        btn.style.color = UIHelpers.TextPrimary;
                    }
                }).ExecuteLater((long)(ConfirmTimeout * 1000));
            }
        });

        parent.Add(btn);
    }
}
