using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion;

// Lightweight, non-interactive on-screen toast banner (UI Toolkit). Shows a short
// message near the top of the screen for a few seconds, then removes itself. Unlike
// UIPopup it does NOT capture the cursor or block input (PickingMode.Ignore), so it's
// safe to fire during gameplay / a server migration.
public static class Toast
{
    // The in-game UI root is reached the same way UIPopup does it: reflect the private
    // gameTimeLabel off UIGameState and walk up to its containing panel.
    static readonly FieldInfo _gameTimeLabelField = typeof(UIGameState)
        .GetField("gameTimeLabel", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Show(string message, float durationSeconds = 4f)
    {
        if (Application.isBatchMode) return;

        var host = MonoBehaviourSingleton<UIManager>.Instance?.GameState;
        if (host == null)
        {
            Plugin.LogError("Cannot show toast, UIGameState is null.");
            return;
        }
        host.StartCoroutine(ShowCoroutine(message, durationSeconds));
    }

    private static IEnumerator ShowCoroutine(string message, float durationSeconds)
    {
        VisualElement root = GetRoot();
        if (root == null)
        {
            Plugin.LogError("Cannot show toast, UI root is null.");
            yield break;
        }

        // Full-width, top-aligned, non-interactive wrapper.
        var wrapper = new VisualElement();
        wrapper.pickingMode = PickingMode.Ignore;
        wrapper.style.position = Position.Absolute;
        wrapper.style.left = 0;
        wrapper.style.right = 0;
        wrapper.style.top = 0;
        wrapper.style.alignItems = Align.Center;
        wrapper.style.justifyContent = Justify.FlexStart;

        var card = new VisualElement();
        card.pickingMode = PickingMode.Ignore;
        card.style.marginTop = 90;
        card.style.paddingTop = 12;
        card.style.paddingBottom = 12;
        card.style.paddingLeft = 22;
        card.style.paddingRight = 22;
        card.style.backgroundColor = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 0.92f));
        SetBorderRadius(card, 8);
        SetBorder(card, 2, new Color(0.88f, 0.55f, 0.04f)); // orange accent
        wrapper.Add(card);

        var label = new Label(message);
        label.pickingMode = PickingMode.Ignore;
        label.style.color = Color.white;
        label.style.fontSize = 18;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.Normal;
        card.Add(label);

        root.Add(wrapper);

        yield return new WaitForSeconds(durationSeconds);

        if (wrapper.parent != null)
            wrapper.RemoveFromHierarchy();
    }

    private static VisualElement GetRoot()
    {
        if (_gameTimeLabelField == null) return null;
        var host = MonoBehaviourSingleton<UIManager>.Instance?.GameState;
        if (host == null) return null;
        var gameTimeLabel = (Label)_gameTimeLabelField.GetValue(host);
        if (gameTimeLabel?.parent?.parent?.parent == null) return null;
        return gameTimeLabel.parent.parent.parent;
    }

    private static void SetBorderRadius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r;
        e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r;
        e.style.borderBottomRightRadius = r;
    }

    private static void SetBorder(VisualElement e, float w, Color c)
    {
        e.style.borderTopWidth = w;
        e.style.borderBottomWidth = w;
        e.style.borderLeftWidth = w;
        e.style.borderRightWidth = w;
        var color = new StyleColor(c);
        e.style.borderTopColor = color;
        e.style.borderBottomColor = color;
        e.style.borderLeftColor = color;
        e.style.borderRightColor = color;
    }
}
