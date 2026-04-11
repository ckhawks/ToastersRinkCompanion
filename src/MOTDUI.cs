using ToastersRinkCompanion.modifiers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion;

public static class MOTDUI
{
    private static VisualElement _overlay;
    private static bool _isVisible;

    public static bool IsVisible => _isVisible;

    public static void Show()
    {
        if (Application.isBatchMode) return;

        var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
        if (root == null)
        {
            Plugin.LogError("MOTDUI: RootVisualElement is null");
            return;
        }

        // Remove old overlay if it exists
        _overlay?.RemoveFromHierarchy();

        Build();
        root.Add(_overlay);

        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        _isVisible = true;
        Plugin.Log("MOTD shown.");
    }

    public static void Hide()
    {
        if (_overlay == null) return;

        _overlay.RemoveFromHierarchy();
        _overlay = null;

        // Team selection screen will be behind us - leave cursor visible
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        _isVisible = false;
    }

    private static void Build()
    {
        // Full-screen overlay
        _overlay = new VisualElement();
        _overlay.name = "MOTDOverlay";
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0;
        _overlay.style.top = 0;
        _overlay.style.right = 0;
        _overlay.style.bottom = 0;
        _overlay.style.alignItems = Align.Center;
        _overlay.style.justifyContent = Justify.Center;
        _overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));

        // Panel
        var panel = new VisualElement();
        panel.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
        panel.style.maxWidth = 700;
        panel.style.height = new StyleLength(new Length(75, LengthUnit.Percent));
        panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 0.97f));
        panel.style.borderTopLeftRadius = 8;
        panel.style.borderTopRightRadius = 8;
        panel.style.borderBottomLeftRadius = 8;
        panel.style.borderBottomRightRadius = 8;
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.overflow = Overflow.Hidden;
        _overlay.Add(panel);

        // Header
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.paddingLeft = 20;
        header.style.paddingRight = 16;
        header.style.paddingTop = 14;
        header.style.paddingBottom = 14;
        header.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
        panel.Add(header);

        var title = new Label("Welcome to Toaster's Rink!");
        title.style.fontSize = 22;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = Color.white;
        header.Add(title);

        // Scrollable content
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        panel.Add(scrollView);

        var content = scrollView.contentContainer;
        content.style.paddingLeft = 24;
        content.style.paddingRight = 28;
        content.style.paddingTop = 16;
        content.style.paddingBottom = 20;

        // --- About Section ---
        BuildSectionHeader(content, "About", UIHelpers.AccentBlue);
        BuildParagraph(content,
            $"{H("Toaster's Rink")} is a Puck community focused on {H("expanding the standard Puck experience")} " +
            $"in dynamic, sometimes silly ways. Whether you're here to {H("compete")}, " +
            $"{H("train")}, or just {H("vibe")} \u2014 you're welcome here.");
        BuildParagraph(content,
            $"Powered by a {H("custom mod suite")} and the {H("PuckStats backend")}, " +
            "we're always adding new ways to play Puck together.");

        // Get panel keybind display name
        string panelKeyDisplay = modifiers.SettingsTab.GetKeyDisplayName(
            Plugin.modSettings?.panelKeybind ?? "<keyboard>/f3");

        // --- This Server Section ---
        string flavor = MessagingHandler.serverFlavor;
        bool compTweaks = MessagingHandler.serverCompTweaksEnabled;
        if (!string.IsNullOrEmpty(flavor))
        {
            BuildSectionHeader(content, "This Server", new Color(0.9f, 0.75f, 0.3f));

            string flavorDesc = flavor.ToLower() switch
            {
                "chaos" => $"This is a {H("Chaos")} server \u2014 {H("all modifiers")} are available and games are often modified. Expect {H("portals")}, {H("big pucks")}, {H("speed bumps")}, and general silliness.",
                "standard" => $"This is a {H("Standard")} server \u2014 a more regular Puck experience. {H("Some modifiers")} are available, but gameplay stays {H("closer to vanilla")}.",
                "training" => $"This is a {H("Training")} server \u2014 warmup-only, designed for practice. Use the {H("training tools")} (passer, cones, goalie trainer, dummies) to work on your game.",
                _ => $"This server is running the {H($"\"{flavor}\"")} flavor."
            };
            BuildParagraph(content, flavorDesc);

            if (compTweaks)
            {
                BuildParagraph(content,
                    $"This server has {H("Comp Tweaks")} enabled, which adjusts {H("gameplay physics")} " +
                    "like turning radius, puck size, speeds, and recovery times.");
            }

            BuildParagraph(content,
                $"Toaster's Rink servers come in different flavors: {H("Chaos")} for full modifiers, " +
                $"{H("Standard")} for a more vanilla experience, and {H("Training")} for warmup and practice.");
        }

        // --- Features Section ---
        BuildSectionHeader(content, "Features", new Color(0.3f, 0.8f, 0.4f));

        BuildFeature(content, "Game Modifiers",
            $"{H("Vote on modifiers")} that change how the game plays \u2014 big puck, portals, speed bumps, and more. Press {H(panelKeyDisplay)} to open the panel.");
        BuildFeature(content, "Collectibles & Cases",
            $"{H("Earn currency")}, {H("open cases")}, and collect items with different rarities and traits. Build your collection and show it off.");
        BuildFeature(content, "Training Tools",
            $"Spawn pucks, set up cones, use the {H("passer")} and {H("goalie trainer")}. Practice your skills without needing a private server.");
        BuildFeature(content, "Community Events",
            $"{H("Rock boss fights")}, {H("daily memes")}, and community-driven features that keep things fresh.");

        // --- Rules Section ---
        BuildSectionHeader(content, "Rules", new Color(0.9f, 0.4f, 0.4f));
        BuildRule(content, "1", "Respect",
            $"Treat all players with {H("courtesy")}. {H("Harassment")}, personal attacks, and discriminatory language are {H("strictly prohibited")}.");
        BuildRule(content, "2", "Sportsmanship",
            $"Win or lose, maintain {H("good sportsmanship")}. No excessive trash talk, {H("hitting goalies in crease")}, or disrupting others' activities.");
        BuildRule(content, "3", "No Spamming",
            $"Don't {H("spam chat")} with repetitive messages, {H("mic-spam")}, or advertise without admin permission.");
        BuildRule(content, "4", "No Cheating / Exploiting",
            $"{H("Cheating")}, hacking, or exploiting glitches is {H("forbidden")}. Client mods are allowed if they don't negatively impact others. {H("Report exploits")} \u2014 don't abuse them.");
        BuildRule(content, "5", "Reporting & Appeals",
            $"Report rule-breakers or appeal bans via the {H("TR Modmail")} user in the {H("Toaster's Rink Discord")} server.");

        BuildSmallNote(content, "These are abbreviated \u2014 see the full rules at puckstats.io/rules");

        var rulesLinkButton = new Button(() => Application.OpenURL("https://puckstats.io/rules"));
        rulesLinkButton.text = "View Full Rules";
        rulesLinkButton.style.fontSize = 12;
        rulesLinkButton.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        rulesLinkButton.style.color = new StyleColor(new Color(0.9f, 0.4f, 0.4f));
        rulesLinkButton.style.paddingLeft = 12;
        rulesLinkButton.style.paddingRight = 12;
        rulesLinkButton.style.paddingTop = 6;
        rulesLinkButton.style.paddingBottom = 6;
        rulesLinkButton.style.marginTop = 4;
        rulesLinkButton.style.marginBottom = 4;
        rulesLinkButton.style.alignSelf = Align.FlexStart;
        rulesLinkButton.style.borderTopLeftRadius = 0;
        rulesLinkButton.style.borderTopRightRadius = 0;
        rulesLinkButton.style.borderBottomLeftRadius = 0;
        rulesLinkButton.style.borderBottomRightRadius = 0;
        UIHelpers.SetBorder(rulesLinkButton, 1, new Color(0.9f, 0.4f, 0.4f, 0.3f));
        rulesLinkButton.RegisterCallback<MouseEnterEvent>(evt =>
        {
            rulesLinkButton.style.backgroundColor = new StyleColor(new Color(0.9f, 0.4f, 0.4f));
            rulesLinkButton.style.color = new StyleColor(UIHelpers.BgDark);
        });
        rulesLinkButton.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            rulesLinkButton.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
            rulesLinkButton.style.color = new StyleColor(new Color(0.9f, 0.4f, 0.4f));
        });
        content.Add(rulesLinkButton);

        // --- Footer / Dismiss ---
        var footer = new VisualElement();
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.alignItems = Align.Center;
        footer.style.paddingLeft = 20;
        footer.style.paddingRight = 20;
        footer.style.paddingTop = 12;
        footer.style.paddingBottom = 12;
        footer.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
        panel.Add(footer);

        var footerNote = new Label("You can view this again from the Home tab in the panel.");
        footerNote.style.fontSize = 11;
        footerNote.style.color = new StyleColor(UIHelpers.TextMuted);
        footer.Add(footerNote);

        var gotItButton = new Button(() => Hide());
        gotItButton.text = "Got it!";
        gotItButton.style.fontSize = 15;
        gotItButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        gotItButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        gotItButton.style.backgroundColor = new StyleColor(UIHelpers.AccentBlue);
        gotItButton.style.color = new StyleColor(Color.white);
        gotItButton.style.paddingLeft = 28;
        gotItButton.style.paddingRight = 28;
        gotItButton.style.paddingTop = 8;
        gotItButton.style.paddingBottom = 8;
        gotItButton.style.marginTop = 0;
        gotItButton.style.marginBottom = 0;
        gotItButton.style.borderTopLeftRadius = 4;
        gotItButton.style.borderTopRightRadius = 4;
        gotItButton.style.borderBottomLeftRadius = 4;
        gotItButton.style.borderBottomRightRadius = 4;

        gotItButton.RegisterCallback<MouseEnterEvent>(evt =>
        {
            gotItButton.style.backgroundColor = new StyleColor(new Color(0.5f, 0.8f, 1f));
        });
        gotItButton.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            gotItButton.style.backgroundColor = new StyleColor(UIHelpers.AccentBlue);
        });
        footer.Add(gotItButton);
    }

    // --- Builder Helpers ---

    /// <summary>Wraps text in a highlight color tag (white) for use in muted-base-color labels.</summary>
    private static string H(string text) => $"<color=#FFFFFF>{text}</color>";

    private static void BuildSectionHeader(VisualElement parent, string text, Color color)
    {
        var label = new Label(text);
        label.style.fontSize = 17;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = new StyleColor(color);
        label.style.marginTop = 16;
        label.style.marginBottom = 8;
        parent.Add(label);
    }

    private static void BuildParagraph(VisualElement parent, string text)
    {
        var label = new Label(text);
        label.enableRichText = true;
        label.style.fontSize = 14;
        label.style.color = new StyleColor(UIHelpers.TextSecondary);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginBottom = 8;
        parent.Add(label);
    }

    private static void BuildRule(VisualElement parent, string number, string title, string description)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.FlexStart;
        row.style.marginBottom = 8;
        row.style.paddingLeft = 4;
        parent.Add(row);

        var numLabel = new Label(number + ".");
        numLabel.style.fontSize = 14;
        numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        numLabel.style.color = new StyleColor(new Color(0.9f, 0.4f, 0.4f));
        numLabel.style.minWidth = 22;
        row.Add(numLabel);

        var textContainer = new VisualElement();
        textContainer.style.flexShrink = 1;
        row.Add(textContainer);

        var titleLabel = new Label(title);
        titleLabel.style.fontSize = 14;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.color = new StyleColor(UIHelpers.TextPrimary);
        textContainer.Add(titleLabel);

        var descLabel = new Label(description);
        descLabel.enableRichText = true;
        descLabel.style.fontSize = 13;
        descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.marginTop = 2;
        textContainer.Add(descLabel);
    }

    private static void BuildSmallNote(VisualElement parent, string text)
    {
        var label = new Label(text);
        label.style.fontSize = 11;
        label.style.color = new StyleColor(UIHelpers.TextMuted);
        label.style.unityFontStyleAndWeight = FontStyle.Italic;
        label.style.marginTop = 4;
        label.style.marginBottom = 4;
        parent.Add(label);
    }

    private static void BuildFeature(VisualElement parent, string title, string description)
    {
        var container = new VisualElement();
        container.style.marginBottom = 10;
        container.style.paddingLeft = 12;
        container.style.borderLeftWidth = 3;
        container.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.8f, 0.4f));
        parent.Add(container);

        var titleLabel = new Label(title);
        titleLabel.style.fontSize = 14;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.color = new StyleColor(UIHelpers.TextPrimary);
        titleLabel.style.marginBottom = 2;
        container.Add(titleLabel);

        var descLabel = new Label(description);
        descLabel.enableRichText = true;
        descLabel.style.fontSize = 13;
        descLabel.style.color = new StyleColor(UIHelpers.TextSecondary);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        container.Add(descLabel);
    }
}
