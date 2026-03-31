using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Shared UI styling helpers for the modifier panel system.
/// </summary>
public static class UIHelpers
{
    // Common colors
    public static readonly Color ActiveGreen = new(0.3f, 0.8f, 0.4f);
    public static readonly Color ErrorRed = new(0.9f, 0.1f, 0.1f);
    public static readonly Color AccentBlue = new(0.4f, 0.7f, 1f);
    public static readonly Color TextPrimary = Color.white;
    public static readonly Color TextSecondary = new(0.5f, 0.5f, 0.5f);
    public static readonly Color TextMuted = new(0.45f, 0.45f, 0.45f);
    public static readonly Color BgDark = new(0.12f, 0.12f, 0.12f);
    public static readonly Color BgRow = new(0.15f, 0.15f, 0.15f);
    public static readonly Color BgButton = new(0.2f, 0.2f, 0.2f);
    public static readonly Color BgButtonDisabled = new(0.15f, 0.15f, 0.15f);
    public static readonly Color BorderGray = new(0.35f, 0.35f, 0.35f);
    public static readonly Color BorderDark = new(0.3f, 0.3f, 0.3f);

    public static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return TextSecondary;
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length != 6) return TextSecondary;
        float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        return new Color(r, g, b);
    }

    public static void SetBorder(VisualElement el, float width, Color color)
    {
        el.style.borderTopWidth = width;
        el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width;
        el.style.borderRightWidth = width;
        el.style.borderTopColor = new StyleColor(color);
        el.style.borderBottomColor = new StyleColor(color);
        el.style.borderLeftColor = new StyleColor(color);
        el.style.borderRightColor = new StyleColor(color);
    }

    public static void StyleInputField(VisualElement parent)
    {
        var inputField = parent.Q(className: "unity-base-text-field__input");
        if (inputField == null) return;
        inputField.style.backgroundColor = new StyleColor(BgRow);
        inputField.style.color = TextPrimary;
        inputField.style.fontSize = 14;
        SetBorder(inputField, 1, BorderGray);
    }

    /// <summary>
    /// Style a PopupField dropdown with dark theme, border, arrow indicator, and popover styling.
    /// </summary>
    public static void StyleDropdown(VisualElement dropdown, int minWidth = 100, int maxWidth = 160)
    {
        dropdown.style.minWidth = minWidth;
        dropdown.style.maxWidth = maxWidth;
        dropdown.style.height = 26;
        dropdown.style.marginRight = 8;
        dropdown.style.paddingLeft = 8;
        dropdown.style.overflow = Overflow.Hidden;
        dropdown.style.backgroundColor = new StyleColor(BgRow);
        dropdown.style.color = TextPrimary;
        dropdown.style.fontSize = 14;
        SetBorder(dropdown, 1, BorderGray);

        dropdown.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            var input = dropdown.Q(className: "unity-base-popup-field__input");
            if (input == null) return;

            input.style.flexDirection = FlexDirection.Row;
            input.style.justifyContent = Justify.SpaceBetween;
            input.style.alignItems = Align.Center;

            var textLabel = input.Q<Label>(className: "unity-text-element");
            if (textLabel != null)
            {
                textLabel.style.fontSize = 14;
                textLabel.style.color = TextPrimary;
            }

            var arrow = new Label("\u25BC");
            arrow.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            arrow.style.fontSize = 8;
            arrow.style.marginLeft = 4;
            arrow.style.marginRight = 4;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.pickingMode = PickingMode.Ignore;
            input.Add(arrow);
        });

        dropdown.RegisterCallback<MouseDownEvent>(evt =>
        {
            dropdown.schedule.Execute(() => StylePopover(dropdown)).ExecuteLater(2);
        });
    }

    /// <summary>
    /// Style the popover dropdown menu that appears when a PopupField is clicked.
    /// </summary>
    public static void StylePopover(VisualElement popupField)
    {
        var root = popupField.panel?.visualTree;
        if (root == null) return;

        var dropdownEl = root.Q(className: "unity-base-dropdown");
        if (dropdownEl == null) return;

        var containerInner = dropdownEl.Q(className: "unity-base-dropdown__container-inner");
        if (containerInner != null)
        {
            containerInner.style.backgroundColor = new StyleColor(new Color(BgDark.r, BgDark.g, BgDark.b, 0.95f));
            SetBorder(containerInner, 1, BorderDark);
        }

        var items = dropdownEl.Query(className: "unity-base-dropdown__item").ToList();
        foreach (var item in items)
        {
            item.style.backgroundColor = new StyleColor(BgRow);
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
            item.style.paddingTop = 3;
            item.style.paddingBottom = 3;
            item.style.paddingLeft = 10;
            item.style.paddingRight = 10;
            item.style.minHeight = 22;

            item.RegisterCallback<MouseEnterEvent>(evt2 =>
            {
                item.style.backgroundColor = new StyleColor(BorderDark);
                var l = item.Q<Label>(className: "unity-base-dropdown__label");
                if (l != null) l.style.color = TextPrimary;
            });
            item.RegisterCallback<MouseLeaveEvent>(evt2 =>
            {
                item.style.backgroundColor = new StyleColor(BgRow);
                var l = item.Q<Label>(className: "unity-base-dropdown__label");
                if (l != null) l.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.85f));
            });

            var label = item.Q<Label>(className: "unity-base-dropdown__label");
            if (label != null)
            {
                label.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.85f));
                label.style.fontSize = 12;
            }
        }
    }
}
