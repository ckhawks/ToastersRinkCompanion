using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Always-visible HUD showing currently active modifiers.
/// Bottom-left, 50px from bottom, expands upward.
/// </summary>
public static class ActiveModifiersHUD
{
    private static VisualElement _container;
    private static bool _isSetup;

    private static Color GetCategoryColor(string category)
    {
        foreach (var cat in ModifierRegistry.Categories)
        {
            if (cat.key == category && !string.IsNullOrEmpty(cat.color))
                return UIHelpers.ParseHexColor(cat.color);
        }
        return UIHelpers.TextSecondary;
    }

    private static void Setup()
    {
        if (_isSetup) return;

        var root = MonoBehaviourSingleton<UIManager>.Instance.RootVisualElement;
        if (root == null) return;

        _container = new VisualElement();
        _container.name = "ActiveModifiersHUD";
        _container.style.position = Position.Absolute;
        _container.style.flexDirection = FlexDirection.ColumnReverse;
        ApplyPosition();
        root.Add(_container);

        _isSetup = true;
    }

    private const float ScreenPadding = 16f;

    public static void ApplyPosition()
    {
        if (_container == null) return;
        var settings = Plugin.modSettings;
        int x = settings?.hudPositionX ?? 0;
        int y = settings?.hudPositionY ?? 95;

        // Position using left % with translate to center the container on that point
        // X: 0% = left edge, 50% = center, 100% = right edge
        _container.style.left = new StyleLength(new Length(x, LengthUnit.Percent));
        _container.style.right = StyleKeyword.Auto;
        _container.style.top = new StyleLength(new Length(y, LengthUnit.Percent));
        _container.style.bottom = StyleKeyword.Auto;

        // Alignment: left half aligns left, right half aligns right
        bool isRight = x > 50;
        _container.style.alignItems = isRight ? Align.FlexEnd : Align.FlexStart;

        // Stack direction: top half stacks down, bottom half stacks up
        bool isBottom = y > 50;
        _container.style.flexDirection = isBottom ? FlexDirection.ColumnReverse : FlexDirection.Column;

        // Use translate to anchor the container correctly:
        // X: lerp from 0% (left-aligned) to -100% (right-aligned)
        // Y: lerp from 0% (top-aligned) to -100% (bottom-aligned)
        float translateX = -x; // at 0% -> 0, at 50% -> -50%, at 100% -> -100%
        float translateY = -y;
        _container.style.translate = new Translate(
            new Length(translateX, LengthUnit.Percent),
            new Length(translateY, LengthUnit.Percent));
    }

    public static void Refresh()
    {
        if (!_isSetup) Setup();
        if (_container == null) return;

        _container.Clear();

        if (Plugin.modSettings != null && !Plugin.modSettings.showModifiersHUD) return;

        // Sort by category so same-category modifiers are grouped
        var sorted = new List<ActiveModifierEntry>(ModifierRegistry.ActiveModifiers);
        sorted.Sort((a, b) => string.Compare(a.category ?? "", b.category ?? "", System.StringComparison.Ordinal));

        foreach (var mod in sorted)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 1;

            // Category color dot
            Color catColor = GetCategoryColor(mod.category ?? "");
            var dot = new VisualElement();
            dot.style.width = 6;
            dot.style.height = 6;
            dot.style.borderTopLeftRadius = 3;
            dot.style.borderTopRightRadius = 3;
            dot.style.borderBottomLeftRadius = 3;
            dot.style.borderBottomRightRadius = 3;
            dot.style.backgroundColor = new StyleColor(catColor);
            dot.style.marginRight = 6;
            dot.style.flexShrink = 0;
            row.Add(dot);

            string text = mod.name;

            // Append parameter values for SetValue modifiers
            if (mod.parameters != null && mod.parameters.Count > 0)
            {
                var vals = new List<string>();
                foreach (var kvp in mod.parameters)
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                        vals.Add(kvp.Value);
                }
                if (vals.Count > 0)
                    text += " " + string.Join(" ", vals);
            }

            var label = new Label(text);
            label.style.fontSize = 15;
            label.style.color = new StyleColor(catColor);
            row.Add(label);

            _container.Add(row);
        }

        // Hint at bottom
        if (ModifierRegistry.ActiveModifiers.Count > 0 || ModifierRegistry.Modifiers.Count > 0)
        {
            string key = Plugin.modSettings?.panelKeybind?.ToUpper() ?? "F3";
            var hint = new Label($"Press <b>{key}</b> to open menu");
            hint.style.fontSize = 14;
            hint.style.color = new StyleColor(new Color(0.45f, 0.45f, 0.45f));
            hint.style.marginTop = 6;
            _container.Insert(0, hint); // Insert at index 0 = bottom of ColumnReverse
        }
    }

    public static void Clear()
    {
        if (_container != null)
        {
            _container.Clear();
        }
    }

    public static void Destroy()
    {
        if (_container != null)
        {
            _container.RemoveFromHierarchy();
            _container = null;
        }
        _isSetup = false;
    }
}
