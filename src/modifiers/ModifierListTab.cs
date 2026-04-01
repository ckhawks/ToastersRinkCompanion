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

    private static string _searchFilter = "";

    private static ScrollView _scrollView;

    public static void BuildContent(VisualElement parent)
    {
        // Top bar: title + flavor badge on left, filter on right
        var topBar = new VisualElement();
        topBar.style.flexDirection = FlexDirection.Row;
        topBar.style.alignItems = Align.Center;
        topBar.style.paddingLeft = 12;
        topBar.style.paddingRight = 16;
        topBar.style.paddingTop = 8;
        topBar.style.paddingBottom = 4;
        parent.Add(topBar);

        var titleLabel = new Label("Modifiers");
        titleLabel.style.fontSize = 18;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.color = UIHelpers.TextPrimary;
        titleLabel.style.marginRight = 10;
        topBar.Add(titleLabel);

        // Server flavor badge
        string flavor = ModifierRegistry.ServerFlavor;
        if (!string.IsNullOrEmpty(flavor))
        {
            var flavorBadge = new Label(flavor);
            flavorBadge.style.fontSize = 12;
            flavorBadge.style.paddingLeft = 6;
            flavorBadge.style.paddingRight = 6;
            flavorBadge.style.paddingTop = 2;
            flavorBadge.style.paddingBottom = 2;
            flavorBadge.style.borderTopLeftRadius = 3;
            flavorBadge.style.borderTopRightRadius = 3;
            flavorBadge.style.borderBottomLeftRadius = 3;
            flavorBadge.style.borderBottomRightRadius = 3;

            Color flavorColor = flavor.ToLower() switch
            {
                "chaos" => new Color(0.9f, 0.55f, 0.1f),
                "standard" => new Color(0.0f, 0.44f, 0.78f),
                "training" => new Color(0.35f, 0.76f, 0.32f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
            flavorBadge.style.backgroundColor = new StyleColor(new Color(flavorColor.r * 0.3f, flavorColor.g * 0.3f, flavorColor.b * 0.3f));
            flavorBadge.style.color = new StyleColor(flavorColor);
            topBar.Add(flavorBadge);
        }

        // Spacer
        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        topBar.Add(spacer);

        // Filter
        var filterLabel = new Label("Filter");
        filterLabel.style.fontSize = 13;
        filterLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        filterLabel.style.marginRight = 8;
        topBar.Add(filterLabel);

        var searchField = new TextField();
        searchField.value = _searchFilter;
        searchField.style.minWidth = 160;
        searchField.style.maxWidth = 220;
        searchField.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            UIHelpers.StyleInputField(searchField);
            var input = searchField.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.fontSize = 14;
                input.style.paddingTop = 4;
                input.style.paddingBottom = 4;
            }
        });
        searchField.RegisterValueChangedCallback(evt =>
        {
            _searchFilter = evt.newValue;
            RebuildScrollContent();
        });
        topBar.Add(searchField);

        _scrollView = new ScrollView(ScrollViewMode.Vertical);
        _scrollView.style.flexGrow = 1;
        parent.Add(_scrollView);

        // Padding on the content container so scrollbar stays outside
        var contentContainer = _scrollView.contentContainer;
        contentContainer.style.paddingLeft = 16;
        contentContainer.style.paddingRight = 20;
        contentContainer.style.paddingTop = 12;
        contentContainer.style.paddingBottom = 12;

        RebuildScrollContent();
    }

    private static void RebuildScrollContent()
    {
        if (_scrollView == null) return;
        _scrollView.Clear();

        var modifiers = ModifierRegistry.Modifiers;
        if (modifiers == null || modifiers.Count == 0)
        {
            var emptyLabel = new Label("No modifiers received from server. Connect to a Toaster's Rink server.");
            emptyLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            emptyLabel.style.fontSize = 14;
            emptyLabel.style.paddingTop = 20;
            emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _scrollView.Add(emptyLabel);
            return;
        }

        // Split into available and unavailable, applying search filter
        var allMods = modifiers.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            var filter = _searchFilter.Trim();
            allMods = allMods.Where(m =>
                (m.name != null && m.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                (m.description != null && m.description.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0));
        }

        var availableMods = allMods.Where(m => m.availableOnFlavor).ToList();
        var unavailableMods = allMods.Where(m => !m.availableOnFlavor).ToList();

        var grouped = availableMods
            .GroupBy(m => m.category ?? "Gameplay")
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.name).ToList());

        // Active modifier keys for quick lookup
        var activeKeys = new HashSet<string>(
            ModifierRegistry.ActiveModifiers.Select(a => a.key));

        // Active section at top for quick disable
        if (activeKeys.Count > 0)
            BuildActiveSection(_scrollView, modifiers, activeKeys);

        var serverCategories = ModifierRegistry.Categories;
        var shownKeys = new HashSet<string>();

        foreach (var cat in serverCategories)
        {
            if (!grouped.ContainsKey(cat.key)) continue;
            var mods = grouped[cat.key];
            shownKeys.Add(cat.key);
            BuildCategorySection(_scrollView, cat.key, cat.label, UIHelpers.ParseHexColor(cat.color), mods, activeKeys);
        }

        // Any modifiers in categories not defined by server
        foreach (var kvp in grouped)
        {
            if (shownKeys.Contains(kvp.Key)) continue;
            BuildCategorySection(_scrollView, kvp.Key, kvp.Key, DefaultCategoryColor, kvp.Value, activeKeys);
        }

        // Unavailable modifiers section
        if (unavailableMods.Count > 0)
        {
            var unavailableGrouped = unavailableMods
                .GroupBy(m => m.category ?? "Gameplay")
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.name).ToList());

            // Divider
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            divider.style.marginTop = 16;
            divider.style.marginBottom = 8;
            _scrollView.Add(divider);

            var unavailTitle = new Label("Unavailable Modifiers");
            unavailTitle.style.fontSize = 16;
            unavailTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            unavailTitle.style.color = new StyleColor(new Color(0.45f, 0.45f, 0.45f));
            unavailTitle.style.marginBottom = 4;
            unavailTitle.style.paddingLeft = 4;
            _scrollView.Add(unavailTitle);

            var noticeRow = new VisualElement();
            noticeRow.style.flexDirection = FlexDirection.Row;
            noticeRow.style.alignItems = Align.Center;
            noticeRow.style.marginBottom = 8;
            noticeRow.style.paddingLeft = 4;
            _scrollView.Add(noticeRow);

            var noticeLabel = new Label("Try a Chaos server for access to all modifiers!");
            noticeLabel.style.fontSize = 12;
            noticeLabel.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            noticeLabel.style.marginRight = 8;
            noticeRow.Add(noticeLabel);

            var serversBtn = new Button(() => ModifierPanelUI.SwitchToTabByName("Servers"));
            serversBtn.text = "Browse Servers";
            serversBtn.style.fontSize = 11;
            serversBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            serversBtn.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            serversBtn.style.paddingLeft = 8;
            serversBtn.style.paddingRight = 8;
            serversBtn.style.paddingTop = 2;
            serversBtn.style.paddingBottom = 2;
            serversBtn.style.borderTopLeftRadius = 3;
            serversBtn.style.borderTopRightRadius = 3;
            serversBtn.style.borderBottomLeftRadius = 3;
            serversBtn.style.borderBottomRightRadius = 3;
            noticeRow.Add(serversBtn);

            var unavailableShownKeys = new HashSet<string>();
            foreach (var cat in serverCategories)
            {
                if (!unavailableGrouped.ContainsKey(cat.key)) continue;
                unavailableShownKeys.Add(cat.key);
                BuildCategorySection(_scrollView, "unavail_" + cat.key, cat.label,
                    DimColor(UIHelpers.ParseHexColor(cat.color)), unavailableGrouped[cat.key], activeKeys, true);
            }

            foreach (var kvp in unavailableGrouped)
            {
                if (unavailableShownKeys.Contains(kvp.Key)) continue;
                BuildCategorySection(_scrollView, "unavail_" + kvp.Key, kvp.Key,
                    DimColor(DefaultCategoryColor), kvp.Value, activeKeys, true);
            }
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

    private static Color DimColor(Color c)
    {
        return new Color(c.r * 0.4f, c.g * 0.4f, c.b * 0.4f);
    }

    private static void BuildCategorySection(VisualElement parent, string catKey,
        string catLabel, Color catColor, List<ModifierRegistryEntry> mods,
        HashSet<string> activeKeys, bool dimmed = false)
    {
        bool collapsed = _collapsedCategories.Contains(catKey);
        int activeCount = mods.Count(m => activeKeys.Contains(m.key));

        // Category header
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.paddingTop = 10;
        headerRow.style.paddingBottom = 6;

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
            BuildModifierRow(modsContainer, mod, isActive, dimmed);
        }
    }

    private static void BuildModifierRow(VisualElement parent, ModifierRegistryEntry mod, bool isActive, bool dimmed = false)
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
        nameLabel.style.color = dimmed
            ? new StyleColor(new Color(0.4f, 0.4f, 0.4f))
            : isActive ? new StyleColor(Color.white) : new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        nameLabel.style.fontSize = 14;
        nameLabel.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
        infoCol.Add(nameLabel);

        if (!string.IsNullOrEmpty(mod.description))
        {
            var descLabel = new Label(mod.description);
            descLabel.style.color = dimmed
                ? new StyleColor(new Color(0.3f, 0.3f, 0.3f))
                : new StyleColor(new Color(0.5f, 0.5f, 0.5f));
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
