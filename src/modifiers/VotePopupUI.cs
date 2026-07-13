using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Vote popup that appears during active votes.
/// Bottom-center, shows modifier info, timer, vote counts, F1/F2 buttons.
/// </summary>
public static class VotePopupUI
{
    private static VisualElement _container;
    private static Label _titleLabel;
    private static Label _descriptionLabel;
    private static Label _paramsLabel;
    private static Label _initiatorLabel;
    private static ProgressBar _timerBar;
    private static VisualElement _voteBar;
    private static VisualElement _voteBarYes;
    private static VisualElement _voteBarNo;
    private static VisualElement _voteBarTick;
    private static Label _voteCounts;
    private static Button _yesButton;
    private static Button _noButton;
    private static Label _resultLabel;
    private static bool _isSetup;
    private static bool _isVisible;
    private static bool _isPreview;
    private static Coroutine _hideCoroutine;

    private static void Setup()
    {
        if (_isSetup) return;

        var root = MonoBehaviourSingleton<UIManager>.Instance.RootVisualElement;
        if (root == null) return;

        _container = new VisualElement();
        _container.name = "VotePopup";
        _container.style.position = Position.Absolute;
        _container.style.width = 420;
        _container.style.paddingLeft = 16;
        _container.style.paddingRight = 16;
        _container.style.paddingTop = 12;
        _container.style.paddingBottom = 12;
        _container.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.85f));
        _container.style.borderTopLeftRadius = 8;
        _container.style.borderTopRightRadius = 8;
        _container.style.borderBottomLeftRadius = 8;
        _container.style.borderBottomRightRadius = 8;
        _container.style.flexDirection = FlexDirection.Column;
        _container.style.alignItems = Align.Center;
        _container.style.display = DisplayStyle.None;
        root.Add(_container);
        ApplyPosition();

        _titleLabel = new Label("Vote: ...");
        _titleLabel.style.fontSize = 18;
        _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _titleLabel.style.color = Color.white;
        _titleLabel.style.marginBottom = 4;
        _container.Add(_titleLabel);

        _descriptionLabel = new Label("");
        _descriptionLabel.style.fontSize = 13;
        _descriptionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        _descriptionLabel.style.marginBottom = 4;
        _descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        _container.Add(_descriptionLabel);

        _paramsLabel = new Label("");
        _paramsLabel.style.fontSize = 14;
        _paramsLabel.style.color = new Color(0.4f, 0.7f, 1f);
        _paramsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _paramsLabel.style.marginBottom = 4;
        _paramsLabel.style.display = DisplayStyle.None;
        _container.Add(_paramsLabel);

        _initiatorLabel = new Label("Started by: ...");
        _initiatorLabel.style.fontSize = 12;
        _initiatorLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        _initiatorLabel.style.marginBottom = 8;
        _container.Add(_initiatorLabel);

        // Timer bar
        _timerBar = new ProgressBar();
        _timerBar.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _timerBar.style.height = 10;
        _timerBar.lowValue = 0f;
        _timerBar.highValue = 1f;
        _timerBar.value = 0f;
        _timerBar.style.marginBottom = 8;
        _container.Add(_timerBar);

        // Style the progress bar background
        var progressBg = _timerBar.Q<VisualElement>(className: "unity-progress-bar__background");
        if (progressBg != null)
        {
            progressBg.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            progressBg.style.borderTopLeftRadius = 4;
            progressBg.style.borderTopRightRadius = 4;
            progressBg.style.borderBottomLeftRadius = 4;
            progressBg.style.borderBottomRightRadius = 4;
        }

        // Style the progress bar fill
        var progressFill = _timerBar.Q<VisualElement>(className: "unity-progress-bar__progress");
        if (progressFill != null)
        {
            progressFill.style.backgroundColor = new StyleColor(new Color(0.4f, 0.7f, 1f));
            progressFill.style.borderTopLeftRadius = 4;
            progressFill.style.borderTopRightRadius = 4;
            progressFill.style.borderBottomLeftRadius = 4;
            progressFill.style.borderBottomRightRadius = 4;
        }

        // Vote bar
        _voteBar = new VisualElement();
        _voteBar.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _voteBar.style.height = 20;
        _voteBar.style.marginBottom = 6;
        _voteBar.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        _voteBar.style.borderTopLeftRadius = 4;
        _voteBar.style.borderTopRightRadius = 4;
        _voteBar.style.borderBottomLeftRadius = 4;
        _voteBar.style.borderBottomRightRadius = 4;
        _voteBar.style.overflow = Overflow.Hidden;
        _container.Add(_voteBar);

        // Green yes fill (from left)
        _voteBarYes = new VisualElement();
        _voteBarYes.style.position = Position.Absolute;
        _voteBarYes.style.left = 0;
        _voteBarYes.style.top = 0;
        _voteBarYes.style.bottom = 0;
        _voteBarYes.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
        _voteBarYes.style.backgroundColor = new StyleColor(new Color(0.03f, 0.7f, 0.34f));
        _voteBar.Add(_voteBarYes);

        // Red no fill (from right)
        _voteBarNo = new VisualElement();
        _voteBarNo.style.position = Position.Absolute;
        _voteBarNo.style.right = 0;
        _voteBarNo.style.top = 0;
        _voteBarNo.style.bottom = 0;
        _voteBarNo.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
        _voteBarNo.style.backgroundColor = new StyleColor(new Color(0.9f, 0.1f, 0.1f));
        _voteBar.Add(_voteBarNo);

        // White tick mark at required-votes threshold
        _voteBarTick = new VisualElement();
        _voteBarTick.style.position = Position.Absolute;
        _voteBarTick.style.top = 0;
        _voteBarTick.style.bottom = 0;
        _voteBarTick.style.width = 2;
        _voteBarTick.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
        _voteBarTick.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.7f));
        _voteBar.Add(_voteBarTick);

        _voteCounts = new Label("Yes: 0 / No: 0 (need 0)");
        _voteCounts.style.fontSize = 13;
        _voteCounts.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.75f));
        _voteCounts.style.marginBottom = 8;
        _container.Add(_voteCounts);

        // Button row
        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.Center;
        _container.Add(buttonRow);

        _yesButton = new Button(() => ModifierMessaging.SendCastVote(true));
        _yesButton.text = $"[{SettingsTab.GetKeyDisplayName(Plugin.modSettings.voteYesKeybind)}] Yes";
        _yesButton.style.backgroundColor = new StyleColor(new Color(0.03f, 0.7f, 0.34f));
        _yesButton.style.color = Color.white;
        _yesButton.style.fontSize = 15;
        _yesButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        _yesButton.style.paddingLeft = 20;
        _yesButton.style.paddingRight = 20;
        _yesButton.style.paddingTop = 6;
        _yesButton.style.paddingBottom = 6;
        _yesButton.style.marginRight = 12;
        _yesButton.style.borderTopLeftRadius = 0;
        _yesButton.style.borderTopRightRadius = 0;
        _yesButton.style.borderBottomLeftRadius = 0;
        _yesButton.style.borderBottomRightRadius = 0;
        buttonRow.Add(_yesButton);

        _noButton = new Button(() => ModifierMessaging.SendCastVote(false));
        _noButton.text = $"[{SettingsTab.GetKeyDisplayName(Plugin.modSettings.voteNoKeybind)}] No";
        _noButton.style.backgroundColor = new StyleColor(new Color(0.9f, 0.1f, 0.1f));
        _noButton.style.color = Color.white;
        _noButton.style.fontSize = 15;
        _noButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        _noButton.style.paddingLeft = 20;
        _noButton.style.paddingRight = 20;
        _noButton.style.paddingTop = 6;
        _noButton.style.paddingBottom = 6;
        _noButton.style.borderTopLeftRadius = 0;
        _noButton.style.borderTopRightRadius = 0;
        _noButton.style.borderBottomLeftRadius = 0;
        _noButton.style.borderBottomRightRadius = 0;
        buttonRow.Add(_noButton);

        // Result label (hidden until vote ends)
        _resultLabel = new Label("");
        _resultLabel.style.fontSize = 20;
        _resultLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _resultLabel.style.marginTop = 8;
        _resultLabel.style.display = DisplayStyle.None;
        _container.Add(_resultLabel);

        // Make the popup pass mouse events through to whatever is behind it (e.g. the
        // F3 panel's sliders) so it never blocks interaction. Only the vote buttons
        // opt back in, and only while a real vote is active (see Show/ShowPreview).
        SetPickingRecursive(_container, PickingMode.Ignore);

        _isSetup = true;
    }

    private static void SetPickingRecursive(VisualElement element, PickingMode mode)
    {
        element.pickingMode = mode;
        foreach (var child in element.Children())
            SetPickingRecursive(child, mode);
    }

    private const float ScreenPadding = 16f;

    /// <summary>
    /// Positions the popup using percentage-based coordinates from settings,
    /// anchored from the nearest edge so it stays on-screen. Mirrors
    /// <see cref="ActiveModifiersHUD.ApplyPosition"/>.
    /// </summary>
    public static void ApplyPosition()
    {
        if (_container == null) return;
        var settings = Plugin.modSettings;
        int x = settings?.votePositionX ?? 50;
        int y = settings?.votePositionY ?? 85;

        var root = _container.parent;
        float parentW = root?.resolvedStyle.width ?? 1920f;
        float parentH = root?.resolvedStyle.height ?? 1080f;
        float safeW = parentW - ScreenPadding * 2f;
        float safeH = parentH - ScreenPadding * 2f;

        float pixelX = ScreenPadding + (x / 100f) * safeW;
        float pixelY = ScreenPadding + (y / 100f) * safeH;

        // Anchor the box's top-left, then translate by the same percentage so the box
        // stays fully on-screen: x=0 left-aligned, x=50 centered, x=100 right-aligned.
        _container.style.left = pixelX;
        _container.style.right = StyleKeyword.Auto;
        _container.style.top = pixelY;
        _container.style.bottom = StyleKeyword.Auto;
        _container.style.alignSelf = new StyleEnum<Align>(StyleKeyword.Auto);
        _container.style.translate = new Translate(
            new Length(-x, LengthUnit.Percent),
            new Length(-y, LengthUnit.Percent));
    }

    public static void Show()
    {
        if (!_isSetup) Setup();
        if (_container == null) return;

        // Cancel pending hide
        if (_hideCoroutine != null && MonoBehaviourSingleton<UIManager>.Instance.Chat is MonoBehaviour mb)
        {
            mb.StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        // A real vote overrides any active positioning preview.
        _isPreview = false;

        _container.style.display = DisplayStyle.Flex;
        _container.BringToFront();
        ApplyPosition();
        _resultLabel.style.display = DisplayStyle.None;
        _initiatorLabel.style.display = DisplayStyle.Flex;
        _yesButton.text = $"[{SettingsTab.GetKeyDisplayName(Plugin.modSettings.voteYesKeybind)}] Yes";
        _noButton.text = $"[{SettingsTab.GetKeyDisplayName(Plugin.modSettings.voteNoKeybind)}] No";
        _yesButton.SetEnabled(true);
        _noButton.SetEnabled(true);
        // Buttons become clickable for a real vote (the rest stays pass-through).
        _yesButton.pickingMode = PickingMode.Position;
        _noButton.pickingMode = PickingMode.Position;
        _isVisible = true;

        var vote = ModifierRegistry.CurrentVote;
        if (vote != null)
            _timerBar.highValue = Mathf.Max(vote.InitialSoftSeconds, 1f);

        UpdateDisplay();
    }

    public static void UpdateDisplay()
    {
        if (!_isSetup || !_isVisible) return;

        var vote = ModifierRegistry.CurrentVote;
        if (vote == null) return;

        // Only show Enable/Disable for Toggle modifiers
        string prefix = "";
        if (ModifierRegistry.Modifiers.TryGetValue(vote.ModifierKey, out var modEntry) && modEntry.type == "Toggle")
            prefix = vote.IsDisabling ? "Disable " : "Enable ";
        _titleLabel.text = $"Vote: {prefix}{vote.ModifierName}";
        _descriptionLabel.text = vote.Description ?? "";

        // Show vote parameters (e.g. "gravity: low", "team: home, score: 5")
        if (vote.Parameters != null && vote.Parameters.Count > 0)
        {
            var paramParts = new System.Collections.Generic.List<string>();
            foreach (var kvp in vote.Parameters)
                if (!string.IsNullOrEmpty(kvp.Value))
                    paramParts.Add(FormatParamValue(vote, kvp.Key, kvp.Value));
            _paramsLabel.text = string.Join(", ", paramParts);
            _paramsLabel.style.display = paramParts.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
        else
        {
            _paramsLabel.style.display = DisplayStyle.None;
        }

        _initiatorLabel.text = string.IsNullOrEmpty(vote.InitiatorName)
            ? "" : $"Started by: {vote.InitiatorName}";
        _initiatorLabel.style.display = string.IsNullOrEmpty(vote.InitiatorName)
            ? DisplayStyle.None : DisplayStyle.Flex;

        if (vote.SoftSecondsRemaining > _timerBar.highValue)
            _timerBar.highValue = vote.SoftSecondsRemaining;
        _timerBar.value = vote.SoftSecondsRemaining;

        // Vote bar
        int total = Mathf.Max(vote.TotalPlayers, 1);
        float yesPercent = Mathf.Clamp01((float)vote.YesCount / total) * 100f;
        float noPercent = Mathf.Clamp01((float)vote.NoCount / total) * 100f;
        float tickPercent = Mathf.Clamp01((float)vote.RequiredVotes / total) * 100f;
        _voteBarYes.style.width = new StyleLength(new Length(yesPercent, LengthUnit.Percent));
        _voteBarNo.style.width = new StyleLength(new Length(noPercent, LengthUnit.Percent));
        _voteBarTick.style.left = new StyleLength(new Length(tickPercent, LengthUnit.Percent));

        _voteCounts.text = $"Yes: {vote.YesCount} / No: {vote.NoCount} (need {vote.RequiredVotes})";
    }

    /// <summary>
    /// Formats a vote parameter value for display. PlayerPicker parameters arrive
    /// as a raw client ID (or username); resolve them to "#number Name".
    /// </summary>
    private static string FormatParamValue(VoteState vote, string key, string value)
    {
        // Look up the arg schema to see if this parameter targets a player
        if (ModifierRegistry.Modifiers.TryGetValue(vote.ModifierKey, out var modEntry) && modEntry.argSchemas != null)
        {
            foreach (var schema in modEntry.argSchemas)
            {
                if (schema.name != key || schema.controlType != "PlayerPicker") continue;

                var player = ResolvePlayer(value);
                if (player != null)
                    return $"#{player.Number.Value} {player.Username.Value}";
                break;
            }
        }

        return value;
    }

    /// <summary>
    /// Resolves a player from a parameter value that may be a client ID or username.
    /// </summary>
    private static Player ResolvePlayer(string value)
    {
        var pm = PlayerManager.Instance;
        if (pm == null) return null;

        // Try client ID first (server broadcasts kick target as a client ID)
        if (ulong.TryParse(value, out var clientId))
        {
            var byId = pm.GetPlayerByClientId(clientId);
            if (byId != null) return byId;
        }

        // Fall back to username match
        foreach (var player in pm.GetPlayers())
            if (player.Username.Value.ToString() == value)
                return player;

        return null;
    }

    public static void ShowResult(string result)
    {
        if (!_isSetup) return;

        // Snap timer to 0 so the bar visually empties
        _timerBar.value = 0;

        _yesButton.SetEnabled(false);
        _noButton.SetEnabled(false);
        _resultLabel.style.display = DisplayStyle.Flex;

        switch (result)
        {
            case "passed":
                _resultLabel.text = "PASSED";
                _resultLabel.style.color = new Color(0.03f, 0.7f, 0.34f);
                break;
            case "failed":
                _resultLabel.text = "FAILED";
                _resultLabel.style.color = new Color(0.9f, 0.1f, 0.1f);
                break;
            case "timed_out":
                _resultLabel.text = "TIMED OUT";
                _resultLabel.style.color = Color.white;
                break;
            case "overridden":
                _resultLabel.text = "OVERRIDDEN";
                _resultLabel.style.color = new Color(0.6f, 0.3f, 0.8f);
                break;
        }

        // Auto-hide after 3 seconds
        if (MonoBehaviourSingleton<UIManager>.Instance.Chat is MonoBehaviour mb)
        {
            if (_hideCoroutine != null) mb.StopCoroutine(_hideCoroutine);
            _hideCoroutine = mb.StartCoroutine(HideAfterDelay(3f));
        }
    }

    private static IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
        ModifierRegistry.CurrentVote = null;
    }

    public static void Hide()
    {
        if (_container != null)
        {
            _container.style.display = DisplayStyle.None;
        }
        _isVisible = false;
    }

    /// <summary>
    /// Shows a non-interactive placeholder popup so the position sliders in Settings
    /// can be previewed when no real vote is active. Skipped if a vote is live.
    /// </summary>
    public static void ShowPreview()
    {
        if (!_isSetup) Setup();
        if (_container == null) return;
        if (_isVisible) return; // don't clobber a live vote

        _isPreview = true;

        _titleLabel.text = "Vote: Example Modifier";
        _descriptionLabel.text = "Preview — adjust the sliders to reposition";
        _paramsLabel.style.display = DisplayStyle.None;
        _initiatorLabel.style.display = DisplayStyle.None;

        _timerBar.highValue = 1f;
        _timerBar.value = 0.6f;
        _voteBarYes.style.width = new StyleLength(new Length(60f, LengthUnit.Percent));
        _voteBarNo.style.width = new StyleLength(new Length(20f, LengthUnit.Percent));
        _voteBarTick.style.left = new StyleLength(new Length(50f, LengthUnit.Percent));
        _voteCounts.text = "Yes: 3 / No: 1 (need 4)";

        _yesButton.SetEnabled(false);
        _noButton.SetEnabled(false);
        _resultLabel.style.display = DisplayStyle.None;

        // Fully pass-through: the preview must never block the F3 sliders behind it.
        _yesButton.pickingMode = PickingMode.Ignore;
        _noButton.pickingMode = PickingMode.Ignore;

        _container.style.display = DisplayStyle.Flex;
        _container.BringToFront();
        ApplyPosition();
    }

    /// <summary>
    /// Hides the positioning preview. No-op if a real vote is showing.
    /// </summary>
    public static void HidePreview()
    {
        if (!_isPreview) return;
        _isPreview = false;
        if (_container != null)
            _container.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Called each frame to tick down the timer locally.
    /// </summary>
    public static void Tick()
    {
        if (!_isVisible || ModifierRegistry.CurrentVote == null) return;

        var vote = ModifierRegistry.CurrentVote;
        if (vote.Result != null) return;

        vote.SoftSecondsRemaining -= Time.deltaTime;
        vote.HardSecondsRemaining -= Time.deltaTime;

        if (vote.SoftSecondsRemaining > 0)
        {
            _timerBar.value = vote.SoftSecondsRemaining;
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
        _isVisible = false;
    }
}
