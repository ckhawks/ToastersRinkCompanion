using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Settings tab for the panel. Allows changing keybinds.
/// </summary>
public static class SettingsTab
{
    private static Button _listeningButton = null;
    private static System.Action<string> _listeningCallback = null;
    private static string _listeningPreviousText = null;

    public static bool IsListening => _listeningButton != null;

    public static void CancelListening()
    {
        if (_listeningButton == null) return;
        _listeningButton.text = _listeningPreviousText;
        _listeningButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        _listeningButton = null;
        _listeningCallback = null;
        _listeningPreviousText = null;
    }

    public static void ApplyListening(string bindingPath)
    {
        if (_listeningButton == null) return;
        _listeningButton.text = GetKeyDisplayName(bindingPath);
        _listeningButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        _listeningCallback?.Invoke(bindingPath);
        _listeningButton = null;
        _listeningCallback = null;
        _listeningPreviousText = null;
    }

    public static string GetKeyDisplayName(string bindingPath)
    {
        if (string.IsNullOrEmpty(bindingPath)) return "None";
        int slashIndex = bindingPath.LastIndexOf('/');
        string keyName = slashIndex >= 0 ? bindingPath.Substring(slashIndex + 1) : bindingPath;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < keyName.Length; i++)
        {
            char c = keyName[i];
            if (i == 0)
                sb.Append(char.ToUpper(c));
            else if (char.IsUpper(c))
            {
                sb.Append(' ');
                sb.Append(c);
            }
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    public static void BuildContent(VisualElement parent)
    {
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        parent.Add(scrollView);

        var contentContainer = scrollView.contentContainer;
        contentContainer.style.paddingLeft = 16;
        contentContainer.style.paddingRight = 20;
        contentContainer.style.paddingTop = 12;
        contentContainer.style.paddingBottom = 12;

        // Section header
        var header = new Label("Keybinds");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = Color.white;
        header.style.marginBottom = 12;
        scrollView.Add(header);

        var settings = Plugin.modSettings;

        BuildKeybindRow(scrollView, "Spawn Puck Bind", settings.spawnPuckKeybind, val =>
        {
            settings.spawnPuckKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.spawnPuckAction, val);
        });

        BuildKeybindRow(scrollView, "Vote Yes", settings.voteYesKeybind, val =>
        {
            settings.voteYesKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.voteYesAction, val);
        });

        BuildKeybindRow(scrollView, "Vote No", settings.voteNoKeybind, val =>
        {
            settings.voteNoKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.voteNoAction, val);
        });

        BuildKeybindRow(scrollView, "Open Panel", settings.panelKeybind, val =>
        {
            settings.panelKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.panelAction, val);
            ActiveModifiersHUD.Refresh();
        });

        BuildKeybindRow(scrollView, "Drill Save", settings.drillSaveKeybind, val =>
        {
            settings.drillSaveKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.drillSaveAction, val);
        });

        BuildKeybindRow(scrollView, "Drill Load", settings.drillLoadKeybind, val =>
        {
            settings.drillLoadKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.drillLoadAction, val);
        });

        BuildKeybindRow(scrollView, "Open Previous Case", settings.openCaseKeybind, val =>
        {
            settings.openCaseKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.openCaseAction, val);
        });

        BuildKeybindRow(scrollView, "Puck Body Block", settings.puckBlockKeybind, val =>
        {
            settings.puckBlockKeybind = val;
            settings.Save();
            Plugin.RecreateAction(ref Plugin.puckBlockAction, val);
            // Rebinding mid-hold would strand the held state on both ends.
            handlers.PuckBlockInput.ForceRelease();
        });

        BuildColorRow(scrollView, "Puck Block Color", settings.puckBlockColor, val =>
        {
            settings.puckBlockColor = val;
            settings.Save();
            handlers.PuckBlockOutline.RefreshColor();
        });

        // Display section
        var displayHeader = new Label("Display");
        displayHeader.style.fontSize = 18;
        displayHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        displayHeader.style.color = Color.white;
        displayHeader.style.marginTop = 20;
        displayHeader.style.marginBottom = 12;
        scrollView.Add(displayHeader);

        BuildToggleRow(scrollView, "Show Modifiers HUD", settings.showModifiersHUD, val =>
        {
            settings.showModifiersHUD = val;
            settings.Save();
            if (val) ActiveModifiersHUD.Refresh();
            else ActiveModifiersHUD.Clear();
        });

        BuildToggleRow(scrollView, "Show Objects on Minimap", settings.showMinimapObjects, val =>
        {
            settings.showMinimapObjects = val;
            settings.Save();
            if (!val) handlers.MinimapObjects.Clear();
        });

        BuildToggleRow(scrollView, "Show Juggle Notifications", settings.showJuggleNotifications, val =>
        {
            settings.showJuggleNotifications = val;
            settings.Save();
        });

        // HUD Position
        var posHeader = new Label("Modifiers List HUD Position");
        posHeader.style.fontSize = 18;
        posHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        posHeader.style.color = Color.white;
        posHeader.style.marginTop = 20;
        posHeader.style.marginBottom = 12;
        scrollView.Add(posHeader);

        BuildSliderRow(scrollView, "Horizontal", settings.hudPositionX, 0, 100, val =>
        {
            settings.hudPositionX = val;
            settings.Save();
            ActiveModifiersHUD.ApplyPosition();
        });

        BuildSliderRow(scrollView, "Vertical", settings.hudPositionY, 0, 100, val =>
        {
            settings.hudPositionY = val;
            settings.Save();
            ActiveModifiersHUD.ApplyPosition();
        });

        // Vote Popup Position
        var votePosHeader = new Label("Vote Popup Position");
        votePosHeader.style.fontSize = 18;
        votePosHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        votePosHeader.style.color = Color.white;
        votePosHeader.style.marginTop = 20;
        votePosHeader.style.marginBottom = 12;
        scrollView.Add(votePosHeader);

        BuildSliderRow(scrollView, "Horizontal", settings.votePositionX, 0, 100, val =>
        {
            settings.votePositionX = val;
            settings.Save();
            VotePopupUI.ApplyPosition();
        });

        BuildSliderRow(scrollView, "Vertical", settings.votePositionY, 0, 100, val =>
        {
            settings.votePositionY = val;
            settings.Save();
            VotePopupUI.ApplyPosition();
        });

        // Show a live preview of the vote popup so these sliders can be positioned
        // even when no vote is active. Hidden again when leaving the tab/panel.
        VotePopupUI.ShowPreview();

        // Note
        var note = new Label("Changes take effect immediately. Keybinds are saved to config.");
        note.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        note.style.fontSize = 11;
        note.style.marginTop = 16;
        note.style.whiteSpace = WhiteSpace.Normal;
        scrollView.Add(note);
    }

    private static void BuildKeybindRow(VisualElement parent, string label, string currentValue,
        System.Action<string> onChanged)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 8;
        row.style.paddingLeft = 8;
        row.style.paddingRight = 8;
        row.style.paddingTop = 6;
        row.style.paddingBottom = 6;
        row.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        parent.Add(row);

        var labelEl = new Label(label);
        labelEl.style.color = Color.white;
        labelEl.style.fontSize = 14;
        labelEl.style.flexGrow = 1;
        row.Add(labelEl);

        var bindButton = new Button();
        bindButton.text = GetKeyDisplayName(currentValue);
        bindButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        bindButton.style.color = Color.white;
        bindButton.style.fontSize = 13;
        bindButton.style.minWidth = 100;
        bindButton.style.paddingLeft = 12;
        bindButton.style.paddingRight = 12;
        bindButton.style.paddingTop = 4;
        bindButton.style.paddingBottom = 4;
        bindButton.style.borderTopLeftRadius = 4;
        bindButton.style.borderTopRightRadius = 4;
        bindButton.style.borderBottomLeftRadius = 4;
        bindButton.style.borderBottomRightRadius = 4;

        bindButton.RegisterCallback<ClickEvent>(evt =>
        {
            CancelListening();
            _listeningButton = bindButton;
            _listeningCallback = onChanged;
            _listeningPreviousText = bindButton.text;
            bindButton.text = "Press a key...";
            bindButton.style.backgroundColor = new StyleColor(new Color(0.8f, 0.5f, 0.1f));
        });

        row.Add(bindButton);
    }

    private static void BuildToggleRow(VisualElement parent, string label, bool currentValue,
        System.Action<bool> onChanged)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 8;
        row.style.paddingLeft = 8;
        row.style.paddingRight = 8;
        row.style.paddingTop = 6;
        row.style.paddingBottom = 6;
        row.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        parent.Add(row);

        var labelEl = new Label(label);
        labelEl.style.color = Color.white;
        labelEl.style.fontSize = 14;
        labelEl.style.flexGrow = 1;
        row.Add(labelEl);

        var toggle = new Toggle();
        toggle.value = currentValue;
        toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        toggle.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            var input = toggle.Q(className: "unity-toggle__input");
            if (input != null)
                input.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
        });
        row.Add(toggle);
    }

    /// <summary>
    /// Swatch picker. Deliberately a fixed set rather than a hex field: these are read
    /// at a distance against white ice, and a free-entry field invites picking
    /// something that vanishes against it.
    /// </summary>
    private static readonly string[] SwatchColors =
    {
        "FFDB33",
        "FF8C1A",
        "FF3B30",
        "FF4FA3",
        "B45CFF",
        "33D6FF",
        "3BE06B",
        "FFFFFF",
    };

    private static void BuildColorRow(VisualElement parent, string label, string currentValue,
        System.Action<string> onChanged)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 8;
        row.style.paddingLeft = 8;
        row.style.paddingRight = 8;
        row.style.paddingTop = 6;
        row.style.paddingBottom = 6;
        row.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        parent.Add(row);

        var labelEl = new Label(label);
        labelEl.style.color = Color.white;
        labelEl.style.fontSize = 14;
        labelEl.style.flexGrow = 1;
        row.Add(labelEl);

        var swatches = new List<Button>();
        string selected = string.IsNullOrEmpty(currentValue) ? SwatchColors[0] : currentValue;

        void Highlight()
        {
            for (int i = 0; i < swatches.Count; i++)
            {
                bool isSelected = string.Equals(SwatchColors[i], selected,
                    System.StringComparison.OrdinalIgnoreCase);

                var border = isSelected ? Color.white : new Color(0f, 0f, 0f, 0.5f);
                swatches[i].style.borderTopColor = border;
                swatches[i].style.borderBottomColor = border;
                swatches[i].style.borderLeftColor = border;
                swatches[i].style.borderRightColor = border;
            }
        }

        foreach (string hex in SwatchColors)
        {
            string value = hex;

            var swatch = new Button(() =>
            {
                selected = value;
                Highlight();
                onChanged(value);
            });

            swatch.text = string.Empty;
            swatch.style.width = 24;
            swatch.style.height = 24;
            swatch.style.marginLeft = 4;
            swatch.style.marginRight = 0;
            swatch.style.marginTop = 0;
            swatch.style.marginBottom = 0;
            swatch.style.paddingLeft = 0;
            swatch.style.paddingRight = 0;
            swatch.style.backgroundColor = UIHelpers.ParseHexColor(value);
            swatch.style.borderTopWidth = 2;
            swatch.style.borderBottomWidth = 2;
            swatch.style.borderLeftWidth = 2;
            swatch.style.borderRightWidth = 2;
            swatch.style.borderTopLeftRadius = 4;
            swatch.style.borderTopRightRadius = 4;
            swatch.style.borderBottomLeftRadius = 4;
            swatch.style.borderBottomRightRadius = 4;

            swatches.Add(swatch);
            row.Add(swatch);
        }

        Highlight();
    }

    private static void BuildSliderRow(VisualElement parent, string label, int currentValue,
        int min, int max, System.Action<int> onChanged)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 8;
        row.style.paddingLeft = 8;
        row.style.paddingRight = 8;
        row.style.paddingTop = 6;
        row.style.paddingBottom = 6;
        row.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        parent.Add(row);

        var labelEl = new Label(label);
        labelEl.style.color = Color.white;
        labelEl.style.fontSize = 14;
        labelEl.style.minWidth = 80;
        row.Add(labelEl);

        var slider = new Slider();
        slider.lowValue = min;
        slider.highValue = max;
        slider.value = currentValue;
        slider.direction = SliderDirection.Horizontal;
        slider.showInputField = true;
        slider.style.minWidth = 140;
        slider.style.maxWidth = 200;
        slider.style.marginRight = 8;
        slider.style.fontSize = 12;
        slider.style.overflow = Overflow.Hidden;

        slider.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            var dragger = slider.Q(className: "unity-base-slider__dragger-border");
            if (dragger != null)
                dragger.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
                tracker.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));

            UIHelpers.StyleInputField(slider);
        });
        row.Add(slider);

        slider.RegisterValueChangedCallback(evt =>
        {
            int intVal = Mathf.RoundToInt(evt.newValue);
            onChanged(intVal);
        });
    }

}
