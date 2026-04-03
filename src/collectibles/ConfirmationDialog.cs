using System;
using ToastersRinkCompanion.modifiers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.collectibles;

public static class ConfirmationDialog
{
    private static VisualElement _overlay;

    public static void Show(string title, string message, string confirmText, Color confirmColor, Action onConfirm)
    {
        Hide(); // Clear any existing dialog

        var root = ModifierPanelUI.GetPanelRoot();
        if (root == null) return;

        // Overlay
        _overlay = new VisualElement();
        _overlay.style.position = Position.Absolute;
        _overlay.style.top = 0;
        _overlay.style.bottom = 0;
        _overlay.style.left = 0;
        _overlay.style.right = 0;
        _overlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.6f));
        _overlay.style.justifyContent = Justify.Center;
        _overlay.style.alignItems = Align.Center;

        // Click overlay to cancel
        _overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _overlay) Hide();
        });

        // Dialog card
        var card = new VisualElement();
        card.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f));
        card.style.width = Length.Percent(60);
        card.style.maxWidth = 400;
        card.style.paddingTop = 16;
        card.style.paddingBottom = 16;
        card.style.paddingLeft = 20;
        card.style.paddingRight = 20;
        card.style.borderTopLeftRadius = 6;
        card.style.borderTopRightRadius = 6;
        card.style.borderBottomLeftRadius = 6;
        card.style.borderBottomRightRadius = 6;
        UIHelpers.SetBorder(card, 1, UIHelpers.BorderGray);
        _overlay.Add(card);

        // Title
        var titleLabel = new Label(title);
        titleLabel.style.fontSize = 18;
        titleLabel.style.color = UIHelpers.TextPrimary;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 10;
        card.Add(titleLabel);

        // Message
        var messageLabel = new Label(message);
        messageLabel.style.fontSize = 14;
        messageLabel.style.color = UIHelpers.TextSecondary;
        messageLabel.style.whiteSpace = WhiteSpace.Normal;
        messageLabel.style.marginBottom = 16;
        card.Add(messageLabel);

        // Button row
        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.FlexEnd;
        card.Add(buttonRow);

        // Cancel button
        var cancelBtn = new Button(() => Hide());
        cancelBtn.text = "Cancel";
        cancelBtn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        cancelBtn.style.color = UIHelpers.TextSecondary;
        cancelBtn.style.fontSize = 14;
        cancelBtn.style.paddingTop = 6;
        cancelBtn.style.paddingBottom = 6;
        cancelBtn.style.paddingLeft = 16;
        cancelBtn.style.paddingRight = 16;
        cancelBtn.style.marginRight = 8;
        cancelBtn.style.borderTopLeftRadius = 4;
        cancelBtn.style.borderTopRightRadius = 4;
        cancelBtn.style.borderBottomLeftRadius = 4;
        cancelBtn.style.borderBottomRightRadius = 4;
        UIHelpers.SetBorder(cancelBtn, 1, UIHelpers.BorderGray);
        buttonRow.Add(cancelBtn);

        // Confirm button
        var confirmBtn = new Button(() =>
        {
            Hide();
            onConfirm?.Invoke();
        });
        confirmBtn.text = confirmText;
        confirmBtn.style.backgroundColor = new StyleColor(new Color(confirmColor.r, confirmColor.g, confirmColor.b, 0.2f));
        confirmBtn.style.color = new StyleColor(confirmColor);
        confirmBtn.style.fontSize = 14;
        confirmBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        confirmBtn.style.paddingTop = 6;
        confirmBtn.style.paddingBottom = 6;
        confirmBtn.style.paddingLeft = 16;
        confirmBtn.style.paddingRight = 16;
        confirmBtn.style.borderTopLeftRadius = 4;
        confirmBtn.style.borderTopRightRadius = 4;
        confirmBtn.style.borderBottomLeftRadius = 4;
        confirmBtn.style.borderBottomRightRadius = 4;
        UIHelpers.SetBorder(confirmBtn, 1, confirmColor);
        buttonRow.Add(confirmBtn);

        root.Add(_overlay);
    }

    public static void Hide()
    {
        _overlay?.RemoveFromHierarchy();
        _overlay = null;
    }
}
