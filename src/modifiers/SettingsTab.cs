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
        scrollView.style.paddingLeft = 20;
        scrollView.style.paddingRight = 20;
        scrollView.style.paddingTop = 16;
        scrollView.style.paddingBottom = 16;
        parent.Add(scrollView);

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

    private static void StyleDropdown(VisualElement dropdown, int minWidth = 100, int maxWidth = 160)
        => UIHelpers.StyleDropdown(dropdown, minWidth, maxWidth);
}
