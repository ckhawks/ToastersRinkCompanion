using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Home tab showing card links to other tabs.
/// </summary>
public static class HomeTab
{
    private static readonly List<CardInfo> Cards = new()
    {
        new() { Title = "Actions", Description = "Puck spawning, cones, passer, goalie training", TabName = "Actions", Color = new Color(0.3f, 0.8f, 0.4f) },
        new() { Title = "Modifiers", Description = "Browse and vote on game modifiers", TabName = "Modifiers", Color = UIHelpers.AccentBlue },
        new() { Title = "Players", Description = "View connected players", TabName = "Players", Color = new Color(0.2f, 0.7f, 0.8f) },
        new() { Title = "Servers", Description = "Browse and join Toaster's Rink servers", TabName = "Servers", Color = new Color(0.9f, 0.6f, 0.1f) },
        new() { Title = "Admin", Description = "Kick, ban, jail, and server management", TabName = "Admin", Color = new Color(0.9f, 0.2f, 0.2f), AdminOnly = true },
        new() { Title = "Settings", Description = "Configure keybinds and preferences", TabName = "Settings", Color = new Color(0.6f, 0.6f, 0.6f) },
    };

    private class CardInfo
    {
        public string Title;
        public string Description;
        public string TabName;
        public Color Color;
        public bool AdminOnly;
    }

    public static void BuildContent(VisualElement parent)
    {
        var container = new VisualElement();
        container.style.flexGrow = 1;
        container.style.paddingLeft = 16;
        container.style.paddingRight = 20;
        container.style.paddingTop = 12;
        container.style.paddingBottom = 12;
        parent.Add(container);

        // Welcome text
        var welcome = new Label("Welcome to Toaster's Rink");
        welcome.style.fontSize = 20;
        welcome.style.unityFontStyleAndWeight = FontStyle.Bold;
        welcome.style.color = UIHelpers.TextPrimary;
        welcome.style.marginBottom = 4;
        container.Add(welcome);

        // Active modifiers summary
        int activeCount = ModifierRegistry.ActiveModifiers.Count;
        string summary = activeCount > 0
            ? $"{activeCount} modifier{(activeCount != 1 ? "s" : "")} active"
            : "No modifiers active";
        var summaryLabel = new Label(summary);
        summaryLabel.style.fontSize = 13;
        summaryLabel.style.color = activeCount > 0
            ? new StyleColor(UIHelpers.ActiveGreen)
            : new StyleColor(UIHelpers.TextMuted);
        summaryLabel.style.marginBottom = 20;
        container.Add(summaryLabel);

        // Card grid
        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.SpaceBetween;
        container.Add(grid);

        foreach (var card in Cards)
        {
            if (card.AdminOnly && !ModifierRegistry.IsAdmin) continue;
            BuildCard(grid, card);
        }

        // About section
        BuildAboutSection(container);
    }

    private static void BuildAboutSection(VisualElement parent)
    {
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        sep.style.marginTop = 8;
        sep.style.marginBottom = 12;
        parent.Add(sep);

        var desc = new Label(
            "Toaster's Rink is a community Puck server featuring game modifiers, " +
            "training tools, collectibles, and more. Powered by PuckStats.");
        desc.style.fontSize = 13;
        desc.style.color = new StyleColor(UIHelpers.TextSecondary);
        desc.style.whiteSpace = WhiteSpace.Normal;
        desc.style.marginBottom = 12;
        parent.Add(desc);

        var linksRow = new VisualElement();
        linksRow.style.flexDirection = FlexDirection.Row;
        linksRow.style.flexWrap = Wrap.Wrap;
        parent.Add(linksRow);

        BuildLinkButton(linksRow, "Discord", "https://discord.gg/4eYYQtcGGz", new Color(0.34f, 0.40f, 0.95f));
        BuildLinkButton(linksRow, "PuckStats", "https://puckstats.io/", new Color(0.27f, 0.54f, 0.96f));
        BuildLinkButton(linksRow, "Rules", "https://puckstats.io/rules", UIHelpers.TextSecondary);
        BuildLinkButton(linksRow, "Donate", "https://ko-fi.com/stellaric", new Color(1f, 0.35f, 0.45f));
        BuildLinkButton(linksRow, "EIS Discord", "https://discord.gg/swDnyXFChu", new Color(0.2f, 0.7f, 0.8f));
    }

    private static void BuildLinkButton(VisualElement parent, string label, string url, Color color)
    {
        var btn = new Button(() => Application.OpenURL(url));
        btn.text = label;
        btn.style.fontSize = 12;
        btn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        btn.style.color = new StyleColor(color);
        btn.style.paddingLeft = 12;
        btn.style.paddingRight = 12;
        btn.style.paddingTop = 6;
        btn.style.paddingBottom = 6;
        btn.style.marginRight = 8;
        btn.style.marginBottom = 6;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;
        btn.style.borderTopWidth = 1;
        btn.style.borderBottomWidth = 1;
        btn.style.borderLeftWidth = 1;
        btn.style.borderRightWidth = 1;
        btn.style.borderTopColor = new StyleColor(new Color(color.r, color.g, color.b, 0.3f));
        btn.style.borderBottomColor = new StyleColor(new Color(color.r, color.g, color.b, 0.3f));
        btn.style.borderLeftColor = new StyleColor(new Color(color.r, color.g, color.b, 0.3f));
        btn.style.borderRightColor = new StyleColor(new Color(color.r, color.g, color.b, 0.3f));

        // Hover: invert colors
        btn.RegisterCallback<MouseEnterEvent>(evt =>
        {
            btn.style.backgroundColor = new StyleColor(color);
            btn.style.color = new StyleColor(UIHelpers.BgDark);
        });
        btn.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            btn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
            btn.style.color = new StyleColor(color);
        });
        parent.Add(btn);
    }

    private static void BuildCard(VisualElement parent, CardInfo info)
    {
        var card = new VisualElement();
        card.style.width = new StyleLength(new Length(48, LengthUnit.Percent));
        card.style.marginBottom = 12;
        card.style.paddingLeft = 16;
        card.style.paddingRight = 16;
        card.style.paddingTop = 14;
        card.style.paddingBottom = 14;
        card.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        card.style.borderLeftWidth = 3;
        card.style.borderLeftColor = new StyleColor(info.Color);

        // Hover effect
        card.RegisterCallback<MouseEnterEvent>(evt =>
        {
            card.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        });
        card.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            card.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        });

        // Click navigates to tab
        card.RegisterCallback<ClickEvent>(evt =>
        {
            ModifierPanelUI.SwitchToTabByName(info.TabName);
        });

        var title = new Label(info.Title);
        title.style.fontSize = 16;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new StyleColor(info.Color);
        title.style.marginBottom = 4;
        title.pickingMode = PickingMode.Ignore;
        card.Add(title);

        var desc = new Label(info.Description);
        desc.style.fontSize = 12;
        desc.style.color = new StyleColor(UIHelpers.TextSecondary);
        desc.style.whiteSpace = WhiteSpace.Normal;
        desc.pickingMode = PickingMode.Ignore;
        card.Add(desc);

        parent.Add(card);
    }
}
