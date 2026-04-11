using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Main panel UI with a tab system. Centered on screen.
/// F3 to toggle. Future tabs: Players, Admin, Server Info, etc.
/// </summary>
public static class ModifierPanelUI
{
    private static VisualElement _overlay;
    private static VisualElement _panel;
    private static VisualElement _tabBar;
    private static VisualElement _contentArea;
    private static Label _versionLabel;
    private static bool _isSetup;
    private static bool _isVisible;

    private static readonly List<TabDefinition> _tabs = new();
    private static int _activeTabIndex = -1;

    public static VisualElement GetPanelRoot() => _panel;

    /// <summary>
    /// Suppresses the game's own keybinds (P for position select, T for chat, etc.)
    /// by setting interactingViews in GlobalStateManager.UIState.
    /// The game checks UIState.IsInteracting (InteractingViews.Count > 0) before
    /// processing keyboard shortcuts.
    /// </summary>
    private static void SetGameInputSuppressed(bool suppressed)
    {
        try
        {
            if (suppressed)
            {
                // Add the chat UIView to interactingViews so IsInteracting returns true
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
                // Rebuild the interacting views list the same way the game's
                // CheckInteraction() does, so we don't clobber views (like
                // the pause menu) that are legitimately interactive right now.
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

    public class TabDefinition
    {
        public string Name;
        public Button TabButton;
        public Action<VisualElement> BuildContent;
    }

    public static bool IsVisible => _isVisible;

    public static void Toggle()
    {
        if (_isVisible) Hide();
        else Show();
    }

    public static void Show()
    {
        if (!_isSetup) Setup();
        if (_overlay == null) return;

        _overlay.style.display = DisplayStyle.Flex;
        _isVisible = true;

        // Update version label
        if (_versionLabel != null)
        {
            string sv = MessagingHandler.serverVersion;
            string cv = Plugin.MOD_VERSION;
            _versionLabel.text = string.IsNullOrEmpty(sv) ? $"Companion {cv}" : $"Server {sv} \u2014 Companion {cv}";
        }

        // Rebuild active tab content
        if (_activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
            SwitchToTab(_activeTabIndex);

        SetGameInputSuppressed(true);
        GlobalStateManager.SetUIState(new Dictionary<string, object> { { "isMouseRequired", true } });
    }

    public static void Hide()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;
        _isVisible = false;

        // Only block the pause action on this frame if the pause menu
        // wasn't already open — otherwise the user needs Esc to close it.
        try
        {
            var pauseMenu = MonoBehaviourSingleton<UIManager>.Instance?.PauseMenu;
            if (pauseMenu == null || !pauseMenu.IsVisible)
                _hideFrame = Time.frameCount;
        }
        catch { _hideFrame = Time.frameCount; }

        SetGameInputSuppressed(false);

        // Instead of unconditionally hiding the cursor, ask the game to re-evaluate
        // whether any of its own UIViews still need it (e.g. scoreboard, settings, chat).
        // This prevents stealing the cursor from other open UI when closing our panel.
        if (!AnyGameViewRequiresMouse())
        {
            GlobalStateManager.SetUIState(new Dictionary<string, object> { { "isMouseRequired", false } });
        }
    }

    /// <summary>
    /// The frame number on which Hide() was last called (without the pause menu open).
    /// Used by the pause-action patch to suppress the game opening
    /// the pause menu on the same Escape press that closed our panel.
    /// </summary>
    private static int _hideFrame = -1;

    /// <summary>
    /// Returns true if our panel is open or was closed this frame
    /// (while the pause menu wasn't already open), meaning the game's
    /// pause action should be suppressed.
    /// </summary>
    public static bool ShouldBlockPauseAction()
    {
        return _isVisible || _hideFrame == Time.frameCount;
    }

    /// <summary>
    /// Checks whether any of the game's own UIViews currently require the mouse cursor.
    /// Mirrors the logic in UIManager.CheckMouseRequirement().
    /// </summary>
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

    private static void Setup()
    {
        if (_isSetup) return;

        var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
        if (root == null) return;

        // Full-screen overlay (blocks clicks behind panel)
        _overlay = new VisualElement();
        _overlay.name = "ModifierPanelOverlay";
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0;
        _overlay.style.top = 0;
        _overlay.style.right = 0;
        _overlay.style.bottom = 0;
        _overlay.style.alignItems = Align.Center;
        _overlay.style.justifyContent = Justify.Center;
        _overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.5f));
        _overlay.style.display = DisplayStyle.None;
        root.Add(_overlay);

        // Close when clicking the overlay background
        _overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _overlay) Hide();
        });

        // Main panel
        _panel = new VisualElement();
        _panel.name = "ModifierPanel";
        _panel.style.width = new StyleLength(new Length(45, LengthUnit.Percent));
        _panel.style.height = new StyleLength(new Length(70, LengthUnit.Percent));
        _panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 0.97f));
        _panel.style.borderTopLeftRadius = 8;
        _panel.style.borderTopRightRadius = 8;
        _panel.style.borderBottomLeftRadius = 8;
        _panel.style.borderBottomRightRadius = 8;
        _panel.style.flexDirection = FlexDirection.Column;
        _panel.style.overflow = Overflow.Hidden;
        _overlay.Add(_panel);

        // Header
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.paddingLeft = 16;
        header.style.paddingRight = 12;
        header.style.paddingTop = 10;
        header.style.paddingBottom = 10;
        header.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
        _panel.Add(header);

        var titleRow = new VisualElement();
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.style.alignItems = Align.FlexEnd;
        header.Add(titleRow);

        var title = new Label("Toaster's Rink");
        title.style.fontSize = 22;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = Color.white;
        titleRow.Add(title);

        _versionLabel = new Label("");
        _versionLabel.style.fontSize = 11;
        _versionLabel.style.color = new StyleColor(new Color(0.45f, 0.45f, 0.45f));
        _versionLabel.style.marginLeft = 24;
        _versionLabel.style.marginBottom = 3;
        titleRow.Add(_versionLabel);

        var closeButton = new Button(() => Hide());
        closeButton.text = "X";
        closeButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        closeButton.style.color = Color.white;
        closeButton.style.fontSize = 16;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.paddingLeft = 8;
        closeButton.style.paddingRight = 8;
        closeButton.style.paddingTop = 4;
        closeButton.style.paddingBottom = 4;
        closeButton.style.borderTopLeftRadius = 0;
        closeButton.style.borderTopRightRadius = 0;
        closeButton.style.borderBottomLeftRadius = 0;
        closeButton.style.borderBottomRightRadius = 0;
        header.Add(closeButton);

        // Tab bar
        _tabBar = new VisualElement();
        _tabBar.style.flexDirection = FlexDirection.Row;
        _tabBar.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
        _tabBar.style.paddingLeft = 8;
        _tabBar.style.paddingTop = 4;
        _tabBar.style.paddingBottom = 0;
        _panel.Add(_tabBar);

        // Content area
        _contentArea = new VisualElement();
        _contentArea.style.flexGrow = 1;
        _contentArea.style.overflow = Overflow.Hidden;
        _panel.Add(_contentArea);

        // Register built-in tabs
        RegisterTab("Home", HomeTab.BuildContent);
        RegisterTab("Actions", TrainingTab.BuildContent);
        RegisterTab("Modifiers", ModifierListTab.BuildContent);
        RegisterTab("Players", PlayersTab.BuildContent);
        if (ModifierRegistry.IsAdmin)
            RegisterTab("Admin", AdminTab.BuildContent);
        RegisterTab("Servers", ServersTab.BuildContent);
        RegisterTab("Donors", DonorsTab.BuildContent);
        RegisterTab("Collectibles", ToastersRinkCompanion.collectibles.CollectiblesTab.BuildContent);
        RegisterTab("Feedback", FeedbackTab.BuildContent);
        RegisterTab("Settings", SettingsTab.BuildContent);

        _isSetup = true;

        // Default to first tab
        if (_tabs.Count > 0)
            SwitchToTab(0);
    }

    public static void RegisterTab(string name, Action<VisualElement> buildContent)
    {
        int tabIndex = _tabs.Count;

        var tabButton = new Button(() => SwitchToTab(tabIndex));
        tabButton.text = name;
        tabButton.style.fontSize = 14;
        tabButton.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        tabButton.style.backgroundColor = StyleKeyword.None;
        tabButton.style.borderBottomWidth = 2;
        tabButton.style.borderBottomColor = new StyleColor(Color.clear);
        tabButton.style.paddingLeft = 14;
        tabButton.style.paddingRight = 14;
        tabButton.style.paddingTop = 8;
        tabButton.style.paddingBottom = 8;
        tabButton.style.marginRight = 2;
        tabButton.style.borderTopLeftRadius = 0;
        tabButton.style.borderTopRightRadius = 0;
        tabButton.style.borderBottomLeftRadius = 0;
        tabButton.style.borderBottomRightRadius = 0;
        _tabBar?.Add(tabButton);

        _tabs.Add(new TabDefinition
        {
            Name = name,
            TabButton = tabButton,
            BuildContent = buildContent
        });
    }

    public static void SwitchToTabByName(string name)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].Name == name)
            {
                SwitchToTab(i);
                return;
            }
        }
    }

    private static void SwitchToTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _activeTabIndex = index;

        // Update tab button styling
        for (int i = 0; i < _tabs.Count; i++)
        {
            var btn = _tabs[i].TabButton;
            bool active = i == index;
            btn.style.color = active
                ? new StyleColor(Color.white)
                : new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            btn.style.borderBottomColor = active
                ? new StyleColor(new Color(0.4f, 0.7f, 1f))
                : new StyleColor(Color.clear);
        }

        // Rebuild content
        _contentArea.Clear();
        _tabs[index].BuildContent(_contentArea);
    }

    /// <summary>
    /// Refresh the current tab content, preserving scroll position.
    /// </summary>
    public static void RefreshCurrentTab()
    {
        if (!_isVisible || _activeTabIndex < 0) return;

        // Save scroll position
        float scrollY = 0;
        var scrollView = _contentArea?.Q<ScrollView>();
        if (scrollView != null)
            scrollY = scrollView.scrollOffset.y;

        SwitchToTab(_activeTabIndex);

        // Restore scroll position after rebuild
        var newScrollView = _contentArea?.Q<ScrollView>();
        if (newScrollView != null)
        {
            float savedY = scrollY;
            newScrollView.schedule.Execute(() =>
            {
                newScrollView.scrollOffset = new Vector2(0, savedY);
            }).ExecuteLater(1);
        }
    }

    /// <summary>
    /// Lightweight refresh: only rebuild expanded stats panels in the Players tab.
    /// Does nothing if the Players tab isn't currently visible.
    /// </summary>
    public static void RefreshPlayerStats()
    {
        if (!_isVisible || _contentArea == null) return;
        PlayersTab.RefreshExpandedStats(_contentArea);
    }

    public static void Destroy()
    {
        if (_overlay != null)
        {
            _overlay.RemoveFromHierarchy();
            _overlay = null;
        }
        _panel = null;
        _tabBar = null;
        _contentArea = null;
        _tabs.Clear();
        _activeTabIndex = -1;
        _isSetup = false;
        _isVisible = false;
    }
}
