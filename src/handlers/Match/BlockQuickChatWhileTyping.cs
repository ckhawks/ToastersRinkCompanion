using HarmonyLib;
using ToastersRinkCompanion.modifiers;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// The game fires a quick chat (ChatManager.Client_QuickChatAction) whenever a
/// digit key bound to "Quick Chat 1-5" is pressed and UIState.IsInteracting is
/// false. Our modifier panel uses UI Toolkit TextField/IntegerField inputs, and
/// focusing one of those can rebuild the game's interacting-views list (dropping
/// the chat view our panel injects to suppress input), so typing a number into a
/// text field leaks through and sends a quick chat. Block quick chat while our
/// panel is open or any text input is focused.
///
/// Both the vanilla keys (1-5) and the Quick Chat Plus mod's extra keys (6-0)
/// route through Client_QuickChatAction, so this single guard covers both. Quick
/// Chat Plus also prefixes Client_QuickChatAction and always returns false; a
/// prefix returning false skips the original AND any not-yet-run prefixes, so we
/// run at Priority.First to ensure our guard executes before theirs cancels.
/// </summary>
public static class BlockQuickChatWhileTyping
{
    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.Client_QuickChatAction))]
    public static class QuickChatActionPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            // Skip the original (no quick chat) when the player is typing in our UI.
            return !ModifierPanelUI.IsVisible && !IsTextInputFocused();
        }
    }

    /// <summary>
    /// True when a UI Toolkit text-editing element (TextField, IntegerField,
    /// FloatField, or their inner "unity-base-text-field__input" element) holds
    /// focus in the game's root visual element.
    /// </summary>
    private static bool IsTextInputFocused()
    {
        var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
        var focused = root?.focusController?.focusedElement as VisualElement;
        while (focused != null)
        {
            if (focused is TextField || focused is IntegerField || focused is FloatField)
                return true;
            if (focused.ClassListContains("unity-base-text-field__input"))
                return true;
            focused = focused.hierarchy.parent;
        }
        return false;
    }
}
