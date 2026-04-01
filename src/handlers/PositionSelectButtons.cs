using System.Linq;
using HarmonyLib;
using ToastersRinkCompanion.modifiers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Injects quick-join buttons into the game's position select screen (UIPositionSelect).
/// Adds "Blue Skater", "Blue Goalie", "Red Skater", "Red Goalie" buttons that send
/// chat commands to join the corresponding team/position.
/// </summary>
public static class PositionSelectButtons
{
    private static VisualElement _buttonContainer;
    private static bool _injected;

    private static bool IsMMOActive()
    {
        return ModifierRegistry.ActiveModifiers.Any(m => m.key == "mmo");
    }

    private static readonly Color BlueColor = new(0.2f, 0.4f, 0.8f);
    private static readonly Color BlueDarkColor = new(0.15f, 0.3f, 0.6f);
    private static readonly Color RedColor = new(0.8f, 0.15f, 0.15f);
    private static readonly Color RedDarkColor = new(0.6f, 0.1f, 0.1f);

    /// <summary>
    /// Patch UIView.Show — when the instance is UIPositionSelect, inject our buttons.
    /// </summary>
    [HarmonyPatch(typeof(UIView), nameof(UIView.Show))]
    public static class UIViewShowPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIView __instance)
        {
            if (__instance is not UIPositionSelect) return;
            if (!MessagingHandler.connectedToToastersRink) return;
            if (!IsMMOActive()) return;

            // Inject after a short delay to let the game finish building its UI
            var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
            if (root == null) return;

            root.schedule.Execute(() => InjectButtons(root)).ExecuteLater(50);
        }
    }

    [HarmonyPatch(typeof(UIView), nameof(UIView.Hide))]
    public static class UIViewHidePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIView __instance)
        {
            if (__instance is not UIPositionSelect) return;
            RemoveButtons();
        }
    }

    private static void InjectButtons(VisualElement root)
    {
        // Remove old container if it exists
        RemoveButtons();

        _buttonContainer = new VisualElement();
        _buttonContainer.name = "QuickJoinButtons";
        _buttonContainer.style.position = Position.Absolute;
        _buttonContainer.style.bottom = 80;
        _buttonContainer.style.left = 0;
        _buttonContainer.style.right = 0;
        _buttonContainer.style.flexDirection = FlexDirection.Row;
        _buttonContainer.style.justifyContent = Justify.Center;
        _buttonContainer.style.alignItems = Align.Center;
        root.Add(_buttonContainer);

        BuildJoinButton("Blue Skater", BlueColor, "/blue");
        BuildJoinButton("Blue Goalie", BlueDarkColor, "/blue g");
        BuildJoinButton("Red Skater", RedColor, "/red");
        BuildJoinButton("Red Goalie", RedDarkColor, "/red g");

        _injected = true;
    }

    private static void BuildJoinButton(string label, Color bgColor, string command)
    {
        var btn = new Button(() =>
        {
            NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage(command, false, false);
        });
        btn.text = label;
        btn.style.fontSize = 14;
        btn.style.backgroundColor = new StyleColor(bgColor);
        btn.style.color = Color.white;
        btn.style.paddingLeft = 20;
        btn.style.paddingRight = 20;
        btn.style.paddingTop = 10;
        btn.style.paddingBottom = 10;
        btn.style.marginLeft = 6;
        btn.style.marginRight = 6;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;
        btn.style.borderTopWidth = 1;
        btn.style.borderBottomWidth = 1;
        btn.style.borderLeftWidth = 1;
        btn.style.borderRightWidth = 1;
        btn.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
        btn.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
        btn.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
        btn.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
        _buttonContainer.Add(btn);
    }

    private static void RemoveButtons()
    {
        if (_buttonContainer != null)
        {
            _buttonContainer.RemoveFromHierarchy();
            _buttonContainer = null;
        }
        _injected = false;
    }
}
