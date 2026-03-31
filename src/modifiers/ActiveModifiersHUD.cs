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
        _container.style.left = 16;
        _container.style.bottom = 50;
        _container.style.flexDirection = FlexDirection.ColumnReverse;
        _container.style.alignItems = Align.FlexStart;
        root.Add(_container);

        _isSetup = true;
    }

    public static void Refresh()
    {
        if (!_isSetup) Setup();
        if (_container == null) return;

        _container.Clear();

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
