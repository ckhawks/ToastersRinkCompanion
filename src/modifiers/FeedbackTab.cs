using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Feedback/Report tab for submitting bug reports, feature requests, and general feedback.
/// </summary>
public static class FeedbackTab
{
    private static readonly List<string> Categories = new()
    {
        "Bug Report",
        "Feature Request",
        "Player Report",
        "General Feedback",
        "Other"
    };

    private static string _selectedCategory = "Bug Report";
    private static string _descriptionText = "";
    private static Label _statusLabel;
    private static Button _submitButton;

    // Client-side rate limiting: track submission timestamps
    private static readonly List<float> _submissionTimestamps = new();
    private const int MaxSubmissions = 3;
    private const float RateLimitWindowSeconds = 15 * 60; // 15 minutes

    // Server response status
    private static string _lastStatus = "";
    private static Color _lastStatusColor = UIHelpers.TextMuted;

    public static void BuildContent(VisualElement parent)
    {
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        parent.Add(scrollView);

        var content = scrollView.contentContainer;
        content.style.paddingLeft = 16;
        content.style.paddingRight = 20;
        content.style.paddingTop = 12;
        content.style.paddingBottom = 12;

        var header = new Label("Feedback & Reports");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = UIHelpers.TextPrimary;
        header.style.marginBottom = 4;
        content.Add(header);

        var subtitle = new Label("Submit bug reports, feature requests, or general feedback.");
        subtitle.style.fontSize = 13;
        subtitle.style.color = UIHelpers.TextMuted;
        subtitle.style.marginBottom = 16;
        content.Add(subtitle);

        if (!MessagingHandler.connectedToToastersRink)
        {
            var notConnected = new Label("You must be connected to a Toaster's Rink server to submit feedback.");
            notConnected.style.color = UIHelpers.TextMuted;
            notConnected.style.fontSize = 14;
            content.Add(notConnected);
            return;
        }

        // Category row
        var categoryRow = new VisualElement();
        categoryRow.style.flexDirection = FlexDirection.Row;
        categoryRow.style.alignItems = Align.Center;
        categoryRow.style.marginBottom = 12;
        content.Add(categoryRow);

        var categoryLabel = new Label("Category:");
        categoryLabel.style.fontSize = 13;
        categoryLabel.style.color = UIHelpers.TextSecondary;
        categoryLabel.style.marginRight = 8;
        categoryLabel.style.minWidth = 70;
        categoryRow.Add(categoryLabel);

        var categoryDropdown = new PopupField<string>(Categories, Categories.IndexOf(_selectedCategory));
        categoryDropdown.RegisterValueChangedCallback(evt => _selectedCategory = evt.newValue);
        UIHelpers.StyleDropdown(categoryDropdown, 160, 220);
        categoryRow.Add(categoryDropdown);

        // Description label
        var descLabel = new Label("Description:");
        descLabel.style.fontSize = 13;
        descLabel.style.color = UIHelpers.TextSecondary;
        descLabel.style.marginBottom = 4;
        content.Add(descLabel);

        // Description text area
        var descField = new TextField();
        descField.multiline = true;
        descField.value = _descriptionText;
        descField.style.minHeight = 120;
        descField.style.marginBottom = 12;
        descField.RegisterValueChangedCallback(evt => _descriptionText = evt.newValue);
        descField.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            var inputField = descField.Q(className: "unity-base-text-field__input");
            if (inputField == null) return;
            inputField.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
            inputField.style.color = UIHelpers.TextPrimary;
            inputField.style.fontSize = 14;
            inputField.style.whiteSpace = WhiteSpace.Normal;
            inputField.style.unityTextAlign = TextAnchor.UpperLeft;
            inputField.style.minHeight = 120;
            UIHelpers.SetBorder(inputField, 1, UIHelpers.BorderGray);
        });
        content.Add(descField);

        // Character count
        var charCount = new Label($"{_descriptionText.Length}/1000");
        charCount.style.fontSize = 11;
        charCount.style.color = UIHelpers.TextMuted;
        charCount.style.unityTextAlign = TextAnchor.MiddleRight;
        charCount.style.marginBottom = 12;
        descField.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue.Length > 1000)
            {
                descField.SetValueWithoutNotify(evt.newValue.Substring(0, 1000));
                _descriptionText = descField.value;
            }
            charCount.text = $"{descField.value.Length}/1000";
        });
        content.Add(charCount);

        // Submit button row
        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.alignItems = Align.Center;
        buttonRow.style.marginBottom = 8;
        content.Add(buttonRow);

        _submitButton = new Button(OnSubmitClicked);
        _submitButton.text = "Submit Feedback";
        _submitButton.style.fontSize = 14;
        _submitButton.style.paddingLeft = 20;
        _submitButton.style.paddingRight = 20;
        _submitButton.style.paddingTop = 8;
        _submitButton.style.paddingBottom = 8;
        _submitButton.style.backgroundColor = new StyleColor(UIHelpers.AccentBlue);
        _submitButton.style.color = UIHelpers.TextPrimary;
        _submitButton.style.borderTopLeftRadius = 4;
        _submitButton.style.borderTopRightRadius = 4;
        _submitButton.style.borderBottomLeftRadius = 4;
        _submitButton.style.borderBottomRightRadius = 4;
        UIHelpers.SetBorder(_submitButton, 0, Color.clear);
        buttonRow.Add(_submitButton);

        // Status label
        _statusLabel = new Label(_lastStatus);
        _statusLabel.style.fontSize = 13;
        _statusLabel.style.color = new StyleColor(_lastStatusColor);
        _statusLabel.style.marginTop = 8;
        content.Add(_statusLabel);

    }

    private static void OnSubmitClicked()
    {
        if (string.IsNullOrWhiteSpace(_descriptionText))
        {
            SetStatus("Please enter a description.", UIHelpers.ErrorRed);
            return;
        }

        if (_descriptionText.Trim().Length < 10)
        {
            SetStatus("Description must be at least 10 characters.", UIHelpers.ErrorRed);
            return;
        }

        if (!IsWithinRateLimit())
        {
            SetStatus("Rate limit reached. Please wait before submitting again.", UIHelpers.ErrorRed);
            return;
        }

        // Record submission time
        _submissionTimestamps.Add(Time.realtimeSinceStartup);

        // Send to server
        JsonMessageRouter.SendMessage("feedback_submit", 0, new FeedbackSubmitPayload
        {
            category = _selectedCategory,
            description = _descriptionText.Trim()
        });

        SetStatus("Submitting...", UIHelpers.AccentBlue);

        if (_submitButton != null)
        {
            _submitButton.SetEnabled(false);
            _submitButton.style.backgroundColor = new StyleColor(UIHelpers.BgButtonDisabled);
        }
    }

    public static void HandleSubmitResult(string status, string message)
    {
        if (status == "ok")
        {
            _descriptionText = "";
            SetStatus(message ?? "Feedback submitted! Thank you.", UIHelpers.ActiveGreen);
        }
        else
        {
            SetStatus(message ?? "Failed to submit feedback.", UIHelpers.ErrorRed);
        }

        ModifierPanelUI.RefreshCurrentTab();
    }

    private static void SetStatus(string text, Color color)
    {
        _lastStatus = text;
        _lastStatusColor = color;
        if (_statusLabel != null)
        {
            _statusLabel.text = text;
            _statusLabel.style.color = new StyleColor(color);
        }
    }

    private static bool IsWithinRateLimit()
    {
        PruneOldTimestamps();
        return _submissionTimestamps.Count < MaxSubmissions;
    }

    private static int GetRemainingSubmissions()
    {
        PruneOldTimestamps();
        return Math.Max(0, MaxSubmissions - _submissionTimestamps.Count);
    }

    private static void PruneOldTimestamps()
    {
        float cutoff = Time.realtimeSinceStartup - RateLimitWindowSeconds;
        _submissionTimestamps.RemoveAll(t => t < cutoff);
    }

    public static void Clear()
    {
        _descriptionText = "";
        _lastStatus = "";
        _lastStatusColor = UIHelpers.TextMuted;
        _statusLabel = null;
        _submitButton = null;
    }

    [Serializable]
    public class FeedbackSubmitPayload
    {
        public string category;
        public string description;
    }
}
