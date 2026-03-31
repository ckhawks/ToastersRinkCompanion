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
        container.style.paddingLeft = 24;
        container.style.paddingRight = 24;
        container.style.paddingTop = 20;
        container.style.paddingBottom = 20;
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
