using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Settings tab for the panel. Allows changing keybinds.
/// </summary>
public static class SettingsTab
{
    private static readonly Dictionary<string, string> KeyDisplayNames = new()
    {
        { "f1", "F1" }, { "f2", "F2" }, { "f3", "F3" }, { "f4", "F4" },
        { "f5", "F5" }, { "f6", "F6" }, { "f7", "F7" }, { "f8", "F8" },
        { "f9", "F9" }, { "f10", "F10" }, { "f11", "F11" }, { "f12", "F12" },
    };

    private static readonly string[] KeyOptions =
    {
        "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12"
    };

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

        BuildTextSettingRow(scrollView, "Spawn Puck Bind", settings.spawnPuckKeybind,
            "InputSystem path, e.g. <keyboard>/g", val =>
        {
            settings.spawnPuckKeybind = val;
            settings.Save();
            Plugin.spawnPuckAction.Disable();
            Plugin.spawnPuckAction = new UnityEngine.InputSystem.InputAction(binding: val);
            Plugin.spawnPuckAction.Enable();
        });

        BuildKeybindRow(scrollView, "Vote Yes", settings.voteYesKeybind, val =>
        {
            settings.voteYesKeybind = val;
            settings.Save();
        });

        BuildKeybindRow(scrollView, "Vote No", settings.voteNoKeybind, val =>
        {
            settings.voteNoKeybind = val;
            settings.Save();
        });

        BuildKeybindRow(scrollView, "Open Panel", settings.panelKeybind, val =>
        {
            settings.panelKeybind = val;
            settings.Save();
            ActiveModifiersHUD.Refresh();
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

        var choices = new List<string>(KeyOptions);
        int defaultIndex = choices.IndexOf(currentValue);
        if (defaultIndex < 0) defaultIndex = 0;

        var dropdown = new PopupField<string>(choices, defaultIndex,
            val => KeyDisplayNames.ContainsKey(val) ? KeyDisplayNames[val] : val.ToUpper(),
            val => KeyDisplayNames.ContainsKey(val) ? KeyDisplayNames[val] : val.ToUpper());
        StyleDropdown(dropdown, 80, 100);

        dropdown.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        row.Add(dropdown);
    }

    private static void BuildTextSettingRow(VisualElement parent, string label, string currentValue,
        string tooltip, System.Action<string> onChanged)
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

        var field = new TextField();
        field.value = currentValue;
        field.style.minWidth = 160;
        field.style.maxWidth = 200;

        field.RegisterCallback<AttachToPanelEvent>(evt => UIHelpers.StyleInputField(field));

        field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        row.Add(field);
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
        row.Add(toggle);
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

    private static void StyleDropdown(VisualElement dropdown, int minWidth = 100, int maxWidth = 160)
        => UIHelpers.StyleDropdown(dropdown, minWidth, maxWidth);
}
