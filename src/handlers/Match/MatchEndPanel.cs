using System;
using System.Collections.Generic;
using System.Reflection;
using ToastersRinkCompanion.modifiers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Modal overlay shown at match end displaying the Three Stars of the Match,
/// the final score, and a link to the PuckStats.io match page.
///
/// Input handling mirrors <see cref="ModifierPanelUI"/>:
/// - Opens automatically when <see cref="MatchStarsStore.OnStarsChanged"/> fires with fresh stars.
/// - Closes on the X button, clicking the backdrop, or pressing Escape.
/// - While visible, suppresses the game's keybinds via GlobalStateManager.interactingViews
///   and blocks the pause action on the same-frame Escape press so the pause menu
///   doesn't pop open as the user is closing the panel.
/// - When the server sends the real matchId asynchronously (after the websocket upload
///   confirms), the PuckStats.io button is updated in place via <see cref="MatchStarsStore.OnMatchIdResolved"/>.
/// </summary>
public static class MatchEndPanel
{
    private static VisualElement _overlay;
    private static VisualElement _panel;
    private static Button _puckStatsButton;
    private static bool _isVisible;

    // Tracks whether the user dismissed this particular star payload so we don't
    // re-open the panel when UpdateMatchId re-broadcasts the same stars.
    private static long _dismissedFinishedAt = -1;
    // Tracks the timestamp of the stars the user has chosen to suppress (X/Esc).
    // Fresh stars from the NEXT match will still auto-open the panel.

    // Same same-frame-ESC suppression pattern as ModifierPanelUI.
    private static int _hideFrame = -1;

    public static bool IsVisible => _isVisible;

    /// <summary>
    /// Called once at startup from MessagingHandler.Setup() to subscribe to store events.
    /// </summary>
    public static void RegisterEvents()
    {
        MatchStarsStore.OnStarsChanged += OnStarsChanged;
        MatchStarsStore.OnMatchIdResolved += OnMatchIdResolved;
    }

    /// <summary>
    /// Called from SpawnPuckKeybind's input loop so Escape can close the panel.
    /// Mirrors the ModifierPanelUI.ShouldBlockPauseAction() guard.
    /// </summary>
    public static bool ShouldBlockPauseAction()
    {
        return _isVisible || _hideFrame == Time.frameCount;
    }

    private static void OnStarsChanged()
    {
        if (!MatchStarsStore.HasStars) return;

        // Late-join sync: the match already ended before this client connected.
        // Still apply the data for chat/scoreboard/glow, but don't pop open the panel —
        // the stats/result are stale and intrusive at that moment.
        if (MatchStarsStore.LastApplyWasLateJoinSync)
        {
            // If somehow already open (shouldn't be), refresh in place.
            if (_isVisible) Rebuild();
            // Treat late-join sync as "already dismissed" so an async matchId
            // update broadcast (which also has isLateJoinSync=true in the resend)
            // never surprises them either.
            _dismissedFinishedAt = MatchStarsStore.Current?.finishedAtUnix ?? 0;
            return;
        }

        long finished = MatchStarsStore.Current?.finishedAtUnix ?? 0;

        // Suppress the auto-open if the user already dismissed THIS set of stars.
        // UpdateMatchId rebroadcasts the same stars with the id filled in; we
        // shouldn't re-open in that case — OnMatchIdResolved handles the in-place refresh.
        if (_dismissedFinishedAt == finished && finished != 0)
        {
            // Still update in-place if already showing
            if (_isVisible) Rebuild();
            return;
        }

        if (_isVisible)
        {
            Rebuild();
        }
        else
        {
            Show();
        }
    }

    private static void OnMatchIdResolved(int matchId)
    {
        if (!_isVisible || _panel == null) return;
        UpdatePuckStatsButton(matchId);
    }

    // ---------------------------------------------------------------
    // Show / Hide
    // ---------------------------------------------------------------

    public static void Show()
    {
        if (Application.isBatchMode) return;
        if (!MatchStarsStore.HasStars) return;

        var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
        if (root == null) return;

        _overlay?.RemoveFromHierarchy();
        Build();
        root.Add(_overlay);

        _isVisible = true;
        SetGameInputSuppressed(true);
        GlobalStateManager.SetUIState(new Dictionary<string, object> { { "isMouseRequired", true } });
    }

    public static void Hide()
    {
        if (_overlay != null)
        {
            _overlay.RemoveFromHierarchy();
            _overlay = null;
            _panel = null;
            _puckStatsButton = null;
        }
        _isVisible = false;

        // Remember that the user dismissed this particular payload so the
        // async matchId rebroadcast doesn't re-open us.
        if (MatchStarsStore.Current != null)
            _dismissedFinishedAt = MatchStarsStore.Current.finishedAtUnix;

        // Only suppress the same-frame pause action if the pause menu
        // wasn't already open — otherwise the user needs Esc to close it.
        try
        {
            var pauseMenu = MonoBehaviourSingleton<UIManager>.Instance?.PauseMenu;
            if (pauseMenu == null || !pauseMenu.IsVisible)
                _hideFrame = Time.frameCount;
        }
        catch { _hideFrame = Time.frameCount; }

        SetGameInputSuppressed(false);

        if (!AnyGameViewRequiresMouse())
        {
            GlobalStateManager.SetUIState(new Dictionary<string, object> { { "isMouseRequired", false } });
        }
    }

    /// <summary>
    /// Rebuild content in place (used when a new payload arrives while the panel is already showing).
    /// </summary>
    private static void Rebuild()
    {
        if (_overlay == null) return;
        var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
        if (root == null) return;

        _overlay.RemoveFromHierarchy();
        Build();
        root.Add(_overlay);
    }

    // ---------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------

    private static void Build()
    {
        // Full-screen overlay (backdrop dim + click-outside-to-close)
        _overlay = new VisualElement();
        _overlay.name = "MatchEndOverlay";
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0;
        _overlay.style.top = 0;
        _overlay.style.right = 0;
        _overlay.style.bottom = 0;
        _overlay.style.alignItems = Align.Center;
        _overlay.style.justifyContent = Justify.Center;
        _overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));

        _overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _overlay) Hide();
        });

        _panel = new VisualElement();
        _panel.name = "MatchEndPanel";
        _panel.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
        _panel.style.maxWidth = 700;
        _panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 0.97f));
        _panel.style.borderTopLeftRadius = 8;
        _panel.style.borderTopRightRadius = 8;
        _panel.style.borderBottomLeftRadius = 8;
        _panel.style.borderBottomRightRadius = 8;
        _panel.style.flexDirection = FlexDirection.Column;
        _panel.style.overflow = Overflow.Hidden;
        _overlay.Add(_panel);

        BuildHeader();
        BuildBody();
        BuildFooter();
    }

    private static void BuildHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.paddingLeft = 20;
        header.style.paddingRight = 12;
        header.style.paddingTop = 12;
        header.style.paddingBottom = 12;
        header.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
        _panel.Add(header);

        var title = new Label("Stars of the Match");
        title.style.fontSize = 22;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = Color.white;
        header.Add(title);

        var closeButton = new Button(Hide);
        closeButton.text = "X";
        closeButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        closeButton.style.color = Color.white;
        closeButton.style.fontSize = 16;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.paddingLeft = 10;
        closeButton.style.paddingRight = 10;
        closeButton.style.paddingTop = 4;
        closeButton.style.paddingBottom = 4;
        closeButton.style.borderTopLeftRadius = 0;
        closeButton.style.borderTopRightRadius = 0;
        closeButton.style.borderBottomLeftRadius = 0;
        closeButton.style.borderBottomRightRadius = 0;
        header.Add(closeButton);
    }

    private static void BuildBody()
    {
        var body = new VisualElement();
        body.style.paddingLeft = 24;
        body.style.paddingRight = 24;
        body.style.paddingTop = 0;
        body.style.paddingBottom = 8;
        _panel.Add(body);

        // Winning team banner (full-width, team-colored)
        BuildWinnerBanner(body);

        // Final score row
        var scoreRow = new VisualElement();
        scoreRow.style.flexDirection = FlexDirection.Row;
        scoreRow.style.justifyContent = Justify.Center;
        scoreRow.style.alignItems = Align.Center;
        scoreRow.style.marginTop = 14;
        scoreRow.style.marginBottom = 16;
        body.Add(scoreRow);

        var blueScore = new Label($"BLUE {MatchStarsStore.BlueScore}");
        blueScore.style.fontSize = 20;
        blueScore.style.unityFontStyleAndWeight = FontStyle.Bold;
        blueScore.style.color = new StyleColor(new Color32(0x4F, 0x9C, 0xFF, 0xFF));
        blueScore.style.marginRight = 16;
        scoreRow.Add(blueScore);

        var sep = new Label("-");
        sep.style.fontSize = 20;
        sep.style.color = new StyleColor(UIHelpers.TextMuted);
        sep.style.marginRight = 16;
        scoreRow.Add(sep);

        var redScore = new Label($"{MatchStarsStore.RedScore} RED");
        redScore.style.fontSize = 20;
        redScore.style.unityFontStyleAndWeight = FontStyle.Bold;
        redScore.style.color = new StyleColor(new Color32(0xFF, 0x5C, 0x5C, 0xFF));
        scoreRow.Add(redScore);

        // Star rows
        var stars = MatchStarsStore.GetOrderedStars();
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
                BuildStarRow(body, stars[i]);
        }

        // Category leaders (one line per category, lesser recognition than stars)
        BuildLeadersSection(body);
    }

    private static void BuildWinnerBanner(VisualElement parent)
    {
        string winningTeam = MatchStarsStore.WinningTeam ?? "None";

        Color bannerColor;
        string bannerText;
        switch (winningTeam)
        {
            case "Blue":
                bannerColor = new Color32(0x4F, 0x9C, 0xFF, 0xFF);
                bannerText = "BLUE WINS";
                break;
            case "Red":
                bannerColor = new Color32(0xFF, 0x5C, 0x5C, 0xFF);
                bannerText = "RED WINS";
                break;
            default:
                bannerColor = new Color32(0x88, 0x88, 0x88, 0xFF);
                bannerText = "DRAW";
                break;
        }

        var banner = new VisualElement();
        banner.style.marginLeft = -24; // overshoot the body padding so banner is edge-to-edge
        banner.style.marginRight = -24;
        banner.style.marginTop = 0;
        banner.style.paddingTop = 12;
        banner.style.paddingBottom = 12;
        banner.style.backgroundColor = new StyleColor(new Color(bannerColor.r, bannerColor.g, bannerColor.b, 0.22f));
        banner.style.borderBottomWidth = 3;
        banner.style.borderBottomColor = new StyleColor(bannerColor);
        banner.style.alignItems = Align.Center;
        banner.style.justifyContent = Justify.Center;
        parent.Add(banner);

        var bannerLabel = new Label(bannerText);
        bannerLabel.style.fontSize = 24;
        bannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        bannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        bannerLabel.style.color = new StyleColor(bannerColor);
        banner.Add(bannerLabel);
    }

    private static void BuildLeadersSection(VisualElement parent)
    {
        var leaders = MatchStarsStore.Leaders;
        if (leaders == null || leaders.Length == 0) return;

        var header = new Label("Category Leaders");
        header.style.fontSize = 13;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = new StyleColor(UIHelpers.TextMuted);
        header.style.marginTop = 12;
        header.style.marginBottom = 6;
        parent.Add(header);

        foreach (var leader in leaders)
        {
            if (leader == null) continue;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.marginBottom = 2;
            parent.Add(row);

            var categoryLabel = new Label(leader.category);
            categoryLabel.style.fontSize = 12;
            categoryLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
            categoryLabel.style.minWidth = 110;
            row.Add(categoryLabel);

            var playerLabel = new Label($"#{leader.number} {leader.username}");
            playerLabel.style.fontSize = 12;
            playerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            playerLabel.style.color = new StyleColor(UIHelpers.TextPrimary);
            playerLabel.style.flexGrow = 1;
            playerLabel.style.marginLeft = 8;
            row.Add(playerLabel);

            var valueLabel = new Label(leader.value.ToString());
            valueLabel.style.fontSize = 12;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = new StyleColor(UIHelpers.TextPrimary);
            valueLabel.style.marginLeft = 8;
            row.Add(valueLabel);
        }
    }

    private static void BuildStarRow(VisualElement parent, MatchStarsStore.StarEntry star)
    {
        Color starColor = star.starRank switch
        {
            1 => new Color32(0xFF, 0xD7, 0x00, 0xFF), // gold
            2 => new Color32(0xED, 0xED, 0xED, 0xFF), // silver (brighter, near-white)
            _ => new Color32(0xCD, 0x7F, 0x32, 0xFF), // bronze
        };

        string rankLabel = star.starRank switch
        {
            1 => "1st Star",
            2 => "2nd Star",
            _ => "3rd Star",
        };

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 12;
        row.style.paddingRight = 12;
        row.style.paddingTop = 10;
        row.style.paddingBottom = 10;
        row.style.marginBottom = 8;
        row.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
        row.style.borderLeftWidth = 4;
        row.style.borderLeftColor = new StyleColor(starColor);
        parent.Add(row);

        var starIcon = new Label("\u2605");
        starIcon.style.fontSize = 28;
        starIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
        starIcon.style.color = new StyleColor(starColor);
        starIcon.style.minWidth = 36;
        starIcon.style.marginRight = 10;
        row.Add(starIcon);

        var info = new VisualElement();
        info.style.flexGrow = 1;
        info.style.flexDirection = FlexDirection.Column;
        row.Add(info);

        var topLine = new Label($"{rankLabel} \u2014 #{star.number} {star.username}");
        topLine.style.fontSize = 16;
        topLine.style.unityFontStyleAndWeight = FontStyle.Bold;
        topLine.style.color = new StyleColor(UIHelpers.TextPrimary);
        info.Add(topLine);

        var bottomLine = new Label(string.IsNullOrEmpty(star.statLine) ? "0 pts" : star.statLine);
        bottomLine.style.fontSize = 13;
        bottomLine.style.color = new StyleColor(UIHelpers.TextSecondary);
        info.Add(bottomLine);

        var pointsLabel = new Label($"{star.points:F0} pts");
        pointsLabel.style.fontSize = 13;
        pointsLabel.style.color = new StyleColor(UIHelpers.TextMuted);
        pointsLabel.style.marginLeft = 8;
        row.Add(pointsLabel);
    }

    private static void BuildFooter()
    {
        var footer = new VisualElement();
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.alignItems = Align.Center;
        footer.style.paddingLeft = 20;
        footer.style.paddingRight = 20;
        footer.style.paddingTop = 12;
        footer.style.paddingBottom = 12;
        footer.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
        _panel.Add(footer);

        _puckStatsButton = new Button(OnPuckStatsClicked);
        StylePuckStatsButton(_puckStatsButton);
        footer.Add(_puckStatsButton);
        UpdatePuckStatsButton(MatchStarsStore.MatchId);

        var closeButton = new Button(Hide);
        closeButton.text = "Close";
        closeButton.style.fontSize = 14;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        closeButton.style.color = new StyleColor(UIHelpers.TextPrimary);
        closeButton.style.paddingLeft = 20;
        closeButton.style.paddingRight = 20;
        closeButton.style.paddingTop = 6;
        closeButton.style.paddingBottom = 6;
        closeButton.style.borderTopLeftRadius = 4;
        closeButton.style.borderTopRightRadius = 4;
        closeButton.style.borderBottomLeftRadius = 4;
        closeButton.style.borderBottomRightRadius = 4;
        UIHelpers.SetBorder(closeButton, 1, UIHelpers.BorderGray);
        footer.Add(closeButton);
    }

    private static void StylePuckStatsButton(Button button)
    {
        button.style.fontSize = 13;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.paddingLeft = 14;
        button.style.paddingRight = 14;
        button.style.paddingTop = 6;
        button.style.paddingBottom = 6;
        button.style.borderTopLeftRadius = 4;
        button.style.borderTopRightRadius = 4;
        button.style.borderBottomLeftRadius = 4;
        button.style.borderBottomRightRadius = 4;
    }

    private static void UpdatePuckStatsButton(int matchId)
    {
        if (_puckStatsButton == null) return;

        if (matchId > 0)
        {
            _puckStatsButton.text = $"View on PuckStats.io (#{matchId})";
            _puckStatsButton.style.backgroundColor = new StyleColor(new Color(0.95f, 0.55f, 0.1f));
            _puckStatsButton.style.color = new StyleColor(UIHelpers.BgDark);
            _puckStatsButton.SetEnabled(true);
        }
        else
        {
            _puckStatsButton.text = "Uploading stats\u2026";
            _puckStatsButton.style.backgroundColor = new StyleColor(UIHelpers.BgButtonDisabled);
            _puckStatsButton.style.color = new StyleColor(UIHelpers.TextMuted);
            _puckStatsButton.SetEnabled(false);
        }
    }

    private static void OnPuckStatsClicked()
    {
        int id = MatchStarsStore.MatchId;
        if (id <= 0) return;
        Application.OpenURL($"https://puckstats.io/match/{id}");
    }

    // ---------------------------------------------------------------
    // Game input suppression (same pattern as ModifierPanelUI)
    // ---------------------------------------------------------------

    private static void SetGameInputSuppressed(bool suppressed)
    {
        try
        {
            if (suppressed)
            {
                var chat = MonoBehaviourSingleton<UIManager>.Instance?.Chat;
                if (chat != null)
                {
                    var views = new List<UIView> { chat };
                    GlobalStateManager.SetUIState(new Dictionary<string, object>
                    {
                        { "interactingViews", views }
                    });
                }
            }
            else
            {
                var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
                var viewsField = typeof(UIManager).GetField("views", BindingFlags.NonPublic | BindingFlags.Instance);
                var gameViews = viewsField?.GetValue(uiManager) as List<UIView>;
                var rebuilt = new List<UIView>();
                if (gameViews != null)
                {
                    foreach (var v in gameViews)
                    {
                        if ((v.VisibilityIsInteractive && v.IsVisible) ||
                            (v.FocusIsInteractive && v.IsFocused))
                        {
                            rebuilt.Add(v);
                        }
                    }
                }
                GlobalStateManager.SetUIState(new Dictionary<string, object>
                {
                    { "interactingViews", rebuilt }
                });
            }
        }
        catch { /* not available yet */ }
    }

    private static bool AnyGameViewRequiresMouse()
    {
        try
        {
            var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
            if (uiManager == null) return false;

            var viewsField = typeof(UIManager).GetField("views", BindingFlags.NonPublic | BindingFlags.Instance);
            if (viewsField == null) return false;

            var views = viewsField.GetValue(uiManager) as List<UIView>;
            if (views == null) return false;

            foreach (var view in views)
            {
                if ((view.VisibilityRequiresMouse && view.IsVisible) ||
                    (view.FocusRequiresMouse && view.IsFocused))
                {
                    return true;
                }
            }
        }
        catch { /* reflection failed, safe to fall through */ }

        return false;
    }
}
