using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Builds the "Modifiers" tab content for the panel.
/// Shows modifier categories with collapsible sections.
/// </summary>
public static class ModifierListTab
{
    // Fallback color for unknown categories
    private static readonly Color DefaultCategoryColor = UIHelpers.TextSecondary;

    // Track which categories are collapsed
    private static readonly HashSet<string> _collapsedCategories = new();

    public static void BuildContent(VisualElement parent)
    {
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        parent.Add(scrollView);

        // Padding on the content container so scrollbar stays outside
        var contentContainer = scrollView.contentContainer;
        contentContainer.style.paddingLeft = 12;
        contentContainer.style.paddingRight = 16;
        contentContainer.style.paddingTop = 8;
        contentContainer.style.paddingBottom = 8;

        var modifiers = ModifierRegistry.Modifiers;
        if (modifiers == null || modifiers.Count == 0)
        {
            var emptyLabel = new Label("No modifiers received from server. Connect to a Toaster's Rink server.");
            emptyLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            emptyLabel.style.fontSize = 14;
            emptyLabel.style.paddingTop = 20;
            emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            scrollView.Add(emptyLabel);
            return;
        }

        // Group by category
        var grouped = modifiers.Values
            .GroupBy(m => m.category ?? "Gameplay")
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.name).ToList());

        // Active modifier keys for quick lookup
        var activeKeys = new HashSet<string>(
            ModifierRegistry.ActiveModifiers.Select(a => a.key));

        // Active section at top for quick disable
        if (activeKeys.Count > 0)
            BuildActiveSection(scrollView, modifiers, activeKeys);

        var serverCategories = ModifierRegistry.Categories;
        var shownKeys = new HashSet<string>();

        foreach (var cat in serverCategories)
        {
            if (!grouped.ContainsKey(cat.key)) continue;
            var mods = grouped[cat.key];
            shownKeys.Add(cat.key);
            BuildCategorySection(scrollView, cat.key, cat.label, UIHelpers.ParseHexColor(cat.color), mods, activeKeys);
        }

        // Any modifiers in categories not defined by server
        foreach (var kvp in grouped)
        {
            if (shownKeys.Contains(kvp.Key)) continue;
            BuildCategorySection(scrollView, kvp.Key, kvp.Key, DefaultCategoryColor, kvp.Value, activeKeys);
        }
    }

    private static void BuildActiveSection(VisualElement parent,
        Dictionary<string, ModifierRegistryEntry> modifiers, HashSet<string> activeKeys)
    {
        // Header
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.paddingTop = 4;
        headerRow.style.paddingBottom = 6;

        var colorBar = new VisualElement();
        colorBar.style.width = 4;
        colorBar.style.height = 18;
        colorBar.style.backgroundColor = new StyleColor(UIHelpers.TextSecondary);
        colorBar.style.borderTopLeftRadius = 2;
        colorBar.style.borderBottomLeftRadius = 2;
        colorBar.style.marginRight = 8;
        headerRow.Add(colorBar);

        var headerLabel = new Label("Active");
        headerLabel.style.color = UIHelpers.TextPrimary;
        headerLabel.style.fontSize = 16;
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerRow.Add(headerLabel);

        var countLabel = new Label($"  ({activeKeys.Count})");
        countLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        countLabel.style.fontSize = 13;
        headerRow.Add(countLabel);

        parent.Add(headerRow);

        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        sep.style.marginBottom = 4;
        parent.Add(sep);

        // Reuse standard modifier rows
        var container = new VisualElement();
        container.style.borderLeftWidth = 3;
        container.style.borderLeftColor = new StyleColor(UIHelpers.TextSecondary);
        container.style.marginLeft = 6;
        container.style.paddingLeft = 8;
        container.style.marginBottom = 4;
        parent.Add(container);

        foreach (var active in ModifierRegistry.ActiveModifiers)
        {
            if (!modifiers.TryGetValue(active.key, out var mod)) continue;
            BuildModifierRow(container, mod, true);
        }

        // Divider below active section
        var bottomSep = new VisualElement();
        bottomSep.style.height = 1;
        bottomSep.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        bottomSep.style.marginBottom = 8;
        parent.Add(bottomSep);
    }

    private static void BuildCategorySection(VisualElement parent, string catKey,
        string catLabel, Color catColor, List<ModifierRegistryEntry> mods,
        HashSet<string> activeKeys)
    {
        bool collapsed = _collapsedCategories.Contains(catKey);
        int activeCount = mods.Count(m => activeKeys.Contains(m.key));

        // Category header
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.paddingTop = 10;
        headerRow.style.paddingBottom = 6;

        // Color bar
        var colorBar = new VisualElement();
        colorBar.style.width = 4;
        colorBar.style.height = 18;
        colorBar.style.backgroundColor = new StyleColor(catColor);
        colorBar.style.borderTopLeftRadius = 2;
        colorBar.style.borderBottomLeftRadius = 2;
        colorBar.style.marginRight = 8;
        headerRow.Add(colorBar);

        // Collapse arrow (unicode triangles)
        var arrow = new Label(collapsed ? "\u25B6" : "\u25BC");
        arrow.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        arrow.style.fontSize = 10;
        arrow.style.marginRight = 6;
        arrow.style.minWidth = 12;
        headerRow.Add(arrow);

        var headerLabel = new Label(catLabel);
        headerLabel.style.color = Color.white;
        headerLabel.style.fontSize = 16;
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerRow.Add(headerLabel);

        var countLabel = new Label($"  ({mods.Count})");
        countLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        countLabel.style.fontSize = 13;
        headerRow.Add(countLabel);

        if (activeCount > 0)
        {
            var activeLabel = new Label($"  {activeCount} active");
            activeLabel.style.color = new StyleColor(new Color(0.3f, 0.8f, 0.4f));
            activeLabel.style.fontSize = 12;
            headerRow.Add(activeLabel);
        }

        // Click to collapse/expand
        headerRow.RegisterCallback<ClickEvent>(evt =>
        {
            if (_collapsedCategories.Contains(catKey))
                _collapsedCategories.Remove(catKey);
            else
                _collapsedCategories.Add(catKey);
            ModifierPanelUI.RefreshCurrentTab();
        });

        parent.Add(headerRow);

        // Separator line
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        sep.style.marginBottom = 4;
        parent.Add(sep);

        if (collapsed) return;

        // Modifier rows with category-colored left border
        var modsContainer = new VisualElement();
        modsContainer.style.borderLeftWidth = 3;
        modsContainer.style.borderLeftColor = new StyleColor(catColor);
        modsContainer.style.marginLeft = 6;
        modsContainer.style.paddingLeft = 8;
        modsContainer.style.marginBottom = 4;
        parent.Add(modsContainer);

        foreach (var mod in mods)
        {
            bool isActive = activeKeys.Contains(mod.key);
            BuildModifierRow(modsContainer, mod, isActive);
        }
    }

    private static void BuildModifierRow(VisualElement parent, ModifierRegistryEntry mod, bool isActive)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 16;
        row.style.paddingRight = 8;
        row.style.paddingTop = 5;
        row.style.paddingBottom = 5;
        row.style.marginBottom = 2;
        row.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        row.style.borderTopLeftRadius = 4;
        row.style.borderTopRightRadius = 4;
        row.style.borderBottomLeftRadius = 4;
        row.style.borderBottomRightRadius = 4;
        parent.Add(row);

        // Active indicator dot
        var dot = new VisualElement();
        dot.style.width = 8;
        dot.style.height = 8;
        dot.style.borderTopLeftRadius = 4;
        dot.style.borderTopRightRadius = 4;
        dot.style.borderBottomLeftRadius = 4;
        dot.style.borderBottomRightRadius = 4;
        dot.style.marginRight = 10;
        dot.style.backgroundColor = isActive
            ? new StyleColor(new Color(0.3f, 0.8f, 0.4f))
            : new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        row.Add(dot);

        // Info column (name + description)
        var infoCol = new VisualElement();
        infoCol.style.flexGrow = 1;
        infoCol.style.flexShrink = 1;
        row.Add(infoCol);

        var nameLabel = new Label(mod.name);
        nameLabel.style.color = isActive ? new StyleColor(Color.white) : new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        nameLabel.style.fontSize = 14;
        nameLabel.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
        infoCol.Add(nameLabel);

        if (!string.IsNullOrEmpty(mod.description))
        {
            var descLabel = new Label(mod.description);
            descLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            descLabel.style.fontSize = 11;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            infoCol.Add(descLabel);
        }

        // Active parameter display
        if (isActive)
        {
            var activeEntry = ModifierRegistry.ActiveModifiers.FirstOrDefault(a => a.key == mod.key);
            if (activeEntry?.parameters != null && activeEntry.parameters.Count > 0)
            {
                var paramText = string.Join(", ", activeEntry.parameters.Select(p => $"{p.Key}: {p.Value}"));
                var paramLabel = new Label(paramText);
                paramLabel.style.color = new StyleColor(new Color(0.4f, 0.7f, 1f));
                paramLabel.style.fontSize = 11;
                infoCol.Add(paramLabel);
            }
        }

        // Controls row (arg controls + buttons inline)
        var controlsRow = new VisualElement();
        controlsRow.style.flexDirection = FlexDirection.Row;
        controlsRow.style.alignItems = Align.Center;
        controlsRow.style.flexShrink = 0;
        row.Add(controlsRow);

        // Arg controls (if any)
        ModifierControlFactory.ControlResult controlResult = null;
        if (mod.argSchemas != null && mod.argSchemas.Length > 0)
        {
            controlResult = ModifierControlFactory.Build(mod.argSchemas);
            if (controlResult != null)
                controlsRow.Add(controlResult.Container);
        }

        // Vote button
        bool voteActive = ModifierRegistry.CurrentVote != null && ModifierRegistry.CurrentVote.Result == null;
        string voteLabel;
        if (mod.type == "Toggle")
            voteLabel = isActive ? "Start Vote to Disable" : "Start Vote to Enable";
        else
            voteLabel = "Start Vote";
        var voteButton = new Button(() =>
        {
            var parameters = controlResult?.GetValues() ?? new Dictionary<string, string>();
            ModifierMessaging.SendVoteRequest(mod.key, parameters, false);
            ModifierPanelUI.Hide();
        });
        voteButton.text = voteLabel;
        StyleButton(voteButton, voteActive ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.2f, 0.2f, 0.2f));
        voteButton.style.marginRight = 4;
        if (voteActive)
        {
            voteButton.SetEnabled(false);
            voteButton.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            UIHelpers.SetBorder(voteButton, 1, UIHelpers.BorderDark);
        }
        controlsRow.Add(voteButton);

        // Force button (admin only)
        if (ModifierRegistry.IsAdmin)
        {
            var forceButton = new Button(() =>
            {
                var parameters = controlResult?.GetValues() ?? new Dictionary<string, string>();
                ModifierMessaging.SendVoteRequest(mod.key, parameters, true);
            });
            forceButton.text = "Force";
            StyleButton(forceButton, new Color(0.6f, 0.15f, 0.15f));
            controlsRow.Add(forceButton);
        }
    }

    private static void StyleButton(Button btn, Color bgColor)
    {
        btn.style.fontSize = 14;
        btn.style.backgroundColor = new StyleColor(bgColor);
        btn.style.color = Color.white;
        btn.style.paddingLeft = 10;
        btn.style.paddingRight = 10;
        btn.style.paddingTop = 4;
        btn.style.paddingBottom = 4;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;
    }
}
