using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Builds UI controls from modifier ArgSchemas.
/// Returns a container with the controls and a method to extract parameter values.
/// </summary>
public static class ModifierControlFactory
{
    public class ControlResult
    {
        public VisualElement Container;
        public Func<Dictionary<string, string>> GetValues;
    }

    public static ControlResult Build(ArgSchemaEntry[] schemas)
    {
        if (schemas == null || schemas.Length == 0)
            return null;

        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        container.style.alignItems = Align.Center;

        var valueGetters = new List<Func<KeyValuePair<string, string>>>();

        foreach (var schema in schemas)
        {
            switch (schema.controlType)
            {
                case "Slider":
                    BuildSlider(container, schema, valueGetters);
                    break;
                case "Dropdown":
                    BuildDropdown(container, schema, valueGetters);
                    break;
                case "PlayerPicker":
                    BuildPlayerPicker(container, schema, valueGetters);
                    break;
                case "IntField":
                    BuildIntField(container, schema, valueGetters);
                    break;
                case "TeamPicker":
                    BuildTeamPicker(container, schema, valueGetters);
                    break;
            }
        }

        return new ControlResult
        {
            Container = container,
            GetValues = () =>
            {
                var dict = new Dictionary<string, string>();
                foreach (var getter in valueGetters)
                {
                    var kvp = getter();
                    if (!string.IsNullOrEmpty(kvp.Key))
                        dict[kvp.Key] = kvp.Value;
                }
                return dict;
            }
        };
    }

    private static void StyleDropdown(VisualElement dropdown, int minWidth = 100, int maxWidth = 160)
        => UIHelpers.StyleDropdown(dropdown, minWidth, maxWidth);

    private static void BuildSlider(VisualElement parent, ArgSchemaEntry schema,
        List<Func<KeyValuePair<string, string>>> getters)
    {
        var slider = new Slider();
        slider.lowValue = schema.minValue;
        slider.highValue = schema.maxValue;
        slider.value = schema.defaultValue;
        slider.direction = SliderDirection.Horizontal;
        slider.showInputField = true;
        slider.style.minWidth = 140;
        slider.style.maxWidth = 180;
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

        // Reset button
        var resetBtn = new Button(() => slider.value = schema.defaultValue);
        resetBtn.text = "\u21BA";
        resetBtn.style.fontSize = 14;
        resetBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        resetBtn.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        resetBtn.style.paddingLeft = 4;
        resetBtn.style.paddingRight = 4;
        resetBtn.style.paddingTop = 1;
        resetBtn.style.paddingBottom = 1;
        resetBtn.style.borderTopLeftRadius = 0;
        resetBtn.style.borderTopRightRadius = 0;
        resetBtn.style.borderBottomLeftRadius = 0;
        resetBtn.style.borderBottomRightRadius = 0;
        resetBtn.style.marginRight = 4;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.Add(slider);
        row.Add(resetBtn);
        parent.Add(row);

        getters.Add(() => new KeyValuePair<string, string>(
            schema.name, slider.value.ToString(CultureInfo.InvariantCulture)));
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }

    private static void BuildDropdown(VisualElement parent, ArgSchemaEntry schema,
        List<Func<KeyValuePair<string, string>>> getters)
    {
        if (schema.allowedValues == null || schema.allowedValues.Length == 0) return;

        // Map display (capitalized) -> raw value
        var displayToRaw = new Dictionary<string, string>();
        var choices = new List<string>();
        foreach (var val in schema.allowedValues)
        {
            string display = Capitalize(val);
            choices.Add(display);
            displayToRaw[display] = val;
        }

        var dropdown = new PopupField<string>(choices, 0);
        StyleDropdown(dropdown);
        parent.Add(dropdown);

        getters.Add(() => new KeyValuePair<string, string>(
            schema.name, displayToRaw.ContainsKey(dropdown.value) ? displayToRaw[dropdown.value] : dropdown.value));
    }

    private static void BuildPlayerPicker(VisualElement parent, ArgSchemaEntry schema,
        List<Func<KeyValuePair<string, string>>> getters)
    {
        var players = PlayerManager.Instance.GetPlayers();

        // Build display strings with number + name
        var choices = new List<string>();
        var playerMap = new Dictionary<string, string>(); // display -> username

        foreach (var player in players)
        {
            string username = player.Username.Value.ToString();
            int number = player.Number.Value;
            string display = $"#{number} {username}";
            choices.Add(display);
            playerMap[display] = username;
        }

        if (choices.Count == 0)
        {
            choices.Add("(no players)");
            playerMap["(no players)"] = "";
        }

        // Default to local player for optional pickers (e.g. SingleGoalie)
        int defaultIndex = 0;
        if (!schema.isRequired && choices.Count > 0)
        {
            ulong localId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
            var localPlayer = PlayerManager.Instance.GetPlayerByClientId(localId);
            if (localPlayer != null)
            {
                string localDisplay = $"#{localPlayer.Number.Value} {localPlayer.Username.Value}";
                int idx = choices.IndexOf(localDisplay);
                if (idx >= 0) defaultIndex = idx;
            }
        }

        var dropdown = new PopupField<string>(choices, defaultIndex);
        StyleDropdown(dropdown, 180, 240);

        // Custom formatters to show team color dot
        dropdown.formatListItemCallback = item =>
        {
            // Find the player for this item to get team color
            return item; // text formatting only — dots added via makeItem if needed
        };
        dropdown.formatSelectedValueCallback = item => item;

        parent.Add(dropdown);

        // Add team color dots next to the dropdown
        // We do this by wrapping in a container with a colored dot
        var wrapper = new VisualElement();
        wrapper.style.flexDirection = FlexDirection.Row;
        wrapper.style.alignItems = Align.Center;
        wrapper.style.marginRight = 8;

        // Move dropdown into wrapper
        dropdown.RemoveFromHierarchy();
        wrapper.Add(dropdown);

        // Team dot that updates with selection
        var teamDot = new VisualElement();
        teamDot.style.width = 8;
        teamDot.style.height = 8;
        teamDot.style.borderTopLeftRadius = 4;
        teamDot.style.borderTopRightRadius = 4;
        teamDot.style.borderBottomLeftRadius = 4;
        teamDot.style.borderBottomRightRadius = 4;
        teamDot.style.marginLeft = 4;

        UpdateTeamDot(teamDot, dropdown.value, players);
        dropdown.RegisterValueChangedCallback(evt => UpdateTeamDot(teamDot, evt.newValue, players));

        wrapper.Add(teamDot);
        parent.Add(wrapper);

        getters.Add(() =>
        {
            string val = playerMap.ContainsKey(dropdown.value) ? playerMap[dropdown.value] : dropdown.value;
            return new KeyValuePair<string, string>(schema.name, val);
        });
    }

    private static void UpdateTeamDot(VisualElement dot, string displayValue,
        System.Collections.Generic.IReadOnlyList<Player> players)
    {
        if (displayValue == "(none)" || displayValue == "(no players)")
        {
            dot.style.display = DisplayStyle.None;
            return;
        }

        dot.style.display = DisplayStyle.Flex;

        // Find player by matching "#N Name" format
        foreach (var player in players)
        {
            string username = player.Username.Value.ToString();
            int number = player.Number.Value;
            if (displayValue == $"#{number} {username}")
            {
                // Team 0 = home (typically blue-ish), Team 1 = away (typically red-ish)
                Color teamColor = player.Team == PlayerTeam.Blue
                    ? new Color(0.3f, 0.5f, 1f)
                    : new Color(0.9f, 0.2f, 0.2f);
                dot.style.backgroundColor = new StyleColor(teamColor);
                return;
            }
        }

        dot.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
    }

    private static void BuildIntField(VisualElement parent, ArgSchemaEntry schema,
        List<Func<KeyValuePair<string, string>>> getters)
    {
        var field = new TextField();
        field.value = ((int)schema.defaultValue).ToString();
        field.style.minWidth = 50;
        field.style.maxWidth = 70;
        field.style.marginRight = 8;

        field.RegisterCallback<AttachToPanelEvent>(evt => UIHelpers.StyleInputField(field));

        parent.Add(field);

        getters.Add(() => new KeyValuePair<string, string>(schema.name, field.value));
    }

    // Team color mapping for known team identifiers
    private static readonly Dictionary<string, Color> TeamColors = new()
    {
        { "red", new Color(0.9f, 0.2f, 0.2f) },
        { "blue", new Color(0.3f, 0.5f, 1f) },
        { "home", new Color(0.3f, 0.5f, 1f) },
        { "away", new Color(0.9f, 0.2f, 0.2f) },
    };

    private static void BuildTeamPicker(VisualElement parent, ArgSchemaEntry schema,
        List<Func<KeyValuePair<string, string>>> getters)
    {
        if (schema.allowedValues == null || schema.allowedValues.Length == 0) return;

        var displayToRaw = new Dictionary<string, string>();
        var choices = new List<string>();
        foreach (var val in schema.allowedValues)
        {
            string display = Capitalize(val);
            choices.Add(display);
            displayToRaw[display] = val;
        }

        var dropdown = new PopupField<string>(choices, 0);
        StyleDropdown(dropdown, 90, 130);

        // Color the text based on team value
        void UpdateTeamColor()
        {
            string raw = displayToRaw.ContainsKey(dropdown.value) ? displayToRaw[dropdown.value] : dropdown.value;
            var label = dropdown.Q<Label>(className: "unity-text-element");
            if (label != null && TeamColors.TryGetValue(raw.ToLower(), out var color))
                label.style.color = new StyleColor(color);
        }
        dropdown.RegisterValueChangedCallback(evt => UpdateTeamColor());
        dropdown.schedule.Execute(UpdateTeamColor);

        parent.Add(dropdown);

        getters.Add(() => new KeyValuePair<string, string>(
            schema.name, displayToRaw.ContainsKey(dropdown.value) ? displayToRaw[dropdown.value] : dropdown.value));
    }
}
