using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Animates Toaster's Rink server rows in the game's native server browser.
/// Randomly picks orange or city-color bg, corner cuts, shimmer, pulsing stars.
/// </summary>
public static class ServerNameAnimation
{
    private const string ToasterTag = "<color=orange>Toaster's Rink</color>";
    private const long IntervalMs = 50; // ~20fps

    private static readonly Dictionary<VisualElement, AnimState> _animatedRows = new();

    private static readonly Regex CityColorRegex = new(@"<color=#([0-9A-Fa-f]{6})>", RegexOptions.Compiled);

    private static readonly Color OrangeBg = new(1.0f, 0.55f, 0.0f);
    private static readonly Color OrangeBgHover = new(1.0f, 0.65f, 0.15f);
    private static readonly Color FallbackCityColor = new(0.38f, 0.38f, 0.66f);
    private static readonly Color StarBright = new(1.0f, 1.0f, 1.0f);
    private static readonly Color StarDim = new(0.5f, 0.5f, 0.5f);
    private static readonly Color CornerCutColor = new(0.196f, 0.196f, 0.196f); // #323232

    private const string Star = "\u2726"; // ✦

    private static readonly System.Random _rng = new();

    private class AnimState
    {
        public string InvertedText;
        public Label NameLabel;
        public Label PlayersLabel;
        public Label PingLabel;
        public VisualElement ShimmerOverlay;
        public VisualElement CornerCutTR;
        public VisualElement CornerCutBL;
        public Color BgColor;
        public Color BgColorHover;
        public bool IsHovered;
    }

    [HarmonyPatch(typeof(UIServerBrowser), "StyleServer")]
    public static class StyleServerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIServerBrowser __instance, EndPoint endPoint)
        {
            var map = AccessTools.Field(typeof(UIServerBrowser), "endPointVisualElementMap")
                ?.GetValue(__instance) as Dictionary<EndPoint, VisualElement>;
            if (map == null || !map.TryGetValue(endPoint, out var container))
                return;

            var serverEl = container.Q("Server");
            if (serverEl == null) return;

            var nameLabel = serverEl.Q<Label>("NameLabel");
            if (nameLabel == null) return;

            string rawText = nameLabel.text ?? "";

            if (!rawText.Contains(ToasterTag))
            {
                StopAnimation(serverEl);
                return;
            }

            StartAnimation(serverEl, nameLabel, rawText);
        }
    }

    private static Color ParseCityColor(string serverName)
    {
        var matches = CityColorRegex.Matches(serverName);
        foreach (Match match in matches)
        {
            string hex = match.Groups[1].Value;
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color parsed))
                return parsed;
        }
        return FallbackCityColor;
    }

    private static Color BrightenColor(Color c, float amount)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return Color.HSVToRGB(h, s * 0.85f, Mathf.Min(v + amount, 1f));
    }

    private static void StartAnimation(VisualElement serverEl, Label nameLabel, string originalText)
    {
        if (_animatedRows.ContainsKey(serverEl)) return;

        var playersLabel = serverEl.Q<Label>("PlayersLabel");
        var pingLabel = serverEl.Q<Label>("PingLabel");

        // Randomly pick orange or city color
        Color cityColor = ParseCityColor(originalText);
        bool useOrange = _rng.Next(2) == 0;
        Color bgColor = useOrange ? OrangeBg : cityColor;
        Color bgColorHover = useOrange ? OrangeBgHover : BrightenColor(cityColor, 0.15f);

        // ── Row styling ──────────────────────────────────────────────
        serverEl.style.backgroundColor = new StyleColor(bgColor);
        serverEl.style.overflow = Overflow.Hidden;
        serverEl.style.borderTopLeftRadius = 0;
        serverEl.style.borderTopRightRadius = 0;
        serverEl.style.borderBottomLeftRadius = 0;
        serverEl.style.borderBottomRightRadius = 0;

        // ── Text colors ──────────────────────────────────────────────
        nameLabel.enableRichText = true;
        string invertedText;
        if (useOrange)
        {
            invertedText = originalText.Replace(
                "<color=orange>Toaster's Rink</color>",
                "<color=#1A1A1A>Toaster's Rink</color>");
        }
        else
        {
            invertedText = originalText.Replace(
                "<color=orange>Toaster's Rink</color>",
                "<color=#FFFFFF>Toaster's Rink</color>");
            invertedText = CityColorRegex.Replace(invertedText, "<color=#FFFFFF>");
        }

        if (playersLabel != null)
            playersLabel.style.color = new StyleColor(
                useOrange ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.95f, 0.95f, 0.95f));
        if (pingLabel != null)
            pingLabel.style.color = new StyleColor(
                useOrange ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.95f, 0.95f, 0.95f));

        // ── Corner cuts (18px - 50% bigger) ──────────────────────────
        var cornerTR = new VisualElement();
        cornerTR.name = "ToasterCornerTR";
        cornerTR.pickingMode = PickingMode.Ignore;
        cornerTR.style.position = Position.Absolute;
        cornerTR.style.top = -9;
        cornerTR.style.right = -9;
        cornerTR.style.width = 18;
        cornerTR.style.height = 18;
        cornerTR.style.backgroundColor = new StyleColor(CornerCutColor);
        cornerTR.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(45f)));
        serverEl.Add(cornerTR);

        var cornerBL = new VisualElement();
        cornerBL.name = "ToasterCornerBL";
        cornerBL.pickingMode = PickingMode.Ignore;
        cornerBL.style.position = Position.Absolute;
        cornerBL.style.bottom = -9;
        cornerBL.style.left = -9;
        cornerBL.style.width = 18;
        cornerBL.style.height = 18;
        cornerBL.style.backgroundColor = new StyleColor(CornerCutColor);
        cornerBL.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(45f)));
        serverEl.Add(cornerBL);

        // ── Diagonal shimmer ─────────────────────────────────────────
        var shimmer = new VisualElement();
        shimmer.name = "ToasterShimmer";
        shimmer.pickingMode = PickingMode.Ignore;
        shimmer.style.position = Position.Absolute;
        shimmer.style.top = new StyleLength(new Length(-50, LengthUnit.Percent));
        shimmer.style.bottom = new StyleLength(new Length(-50, LengthUnit.Percent));
        shimmer.style.width = new StyleLength(new Length(8, LengthUnit.Percent));
        shimmer.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0f));
        shimmer.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(20f)));
        serverEl.Add(shimmer);

        var state = new AnimState
        {
            InvertedText = invertedText,
            NameLabel = nameLabel,
            PlayersLabel = playersLabel,
            PingLabel = pingLabel,
            ShimmerOverlay = shimmer,
            CornerCutTR = cornerTR,
            CornerCutBL = cornerBL,
            BgColor = bgColor,
            BgColorHover = bgColorHover,
            IsHovered = false
        };
        _animatedRows[serverEl] = state;

        serverEl.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
        serverEl.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

        serverEl.schedule
            .Execute(() => UpdateAnimation(serverEl))
            .Every(IntervalMs);

        UpdateAnimation(serverEl);
    }

    private static void OnMouseEnter(MouseEnterEvent evt)
    {
        if (evt.currentTarget is VisualElement el && _animatedRows.TryGetValue(el, out var state))
            state.IsHovered = true;
    }

    private static void OnMouseLeave(MouseLeaveEvent evt)
    {
        if (evt.currentTarget is VisualElement el && _animatedRows.TryGetValue(el, out var state))
            state.IsHovered = false;
    }

    private static void StopAnimation(VisualElement serverEl)
    {
        if (!_animatedRows.TryGetValue(serverEl, out var state)) return;
        state.ShimmerOverlay?.RemoveFromHierarchy();
        state.CornerCutTR?.RemoveFromHierarchy();
        state.CornerCutBL?.RemoveFromHierarchy();
        serverEl.UnregisterCallback<MouseEnterEvent>(OnMouseEnter);
        serverEl.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
        _animatedRows.Remove(serverEl);
    }

    private static void UpdateAnimation(VisualElement serverEl)
    {
        if (!_animatedRows.TryGetValue(serverEl, out var state) || serverEl.panel == null)
        {
            _animatedRows.Remove(serverEl);
            return;
        }

        float time = Time.time;

        // ── Hover ────────────────────────────────────────────────────
        serverEl.style.backgroundColor = new StyleColor(
            state.IsHovered ? state.BgColorHover : state.BgColor);

        // ── Diagonal shimmer sweep ───────────────────────────────────
        float sweepDuration = 1.2f;
        float cycleDuration = 3.5f;
        float cycleT = time % cycleDuration;
        float sweepT = Mathf.Clamp01(cycleT / sweepDuration);
        float eased = sweepT * sweepT * (3f - 2f * sweepT);
        float leftPercent = Mathf.Lerp(-15f, 105f, eased);
        state.ShimmerOverlay.style.left = new StyleLength(new Length(leftPercent, LengthUnit.Percent));
        float shimmerAlpha = 1f - Mathf.Abs(eased - 0.5f) * 2f;
        shimmerAlpha = Mathf.Pow(Mathf.Clamp01(shimmerAlpha), 0.5f) * 0.25f;
        state.ShimmerOverlay.style.backgroundColor = new StyleColor(
            new Color(1f, 1f, 1f, shimmerAlpha));

        // ── Build name label text ────────────────────────────────────
        var sb = new StringBuilder(state.InvertedText.Length + 120);

        // Left star: pulsing ✦
        float starPulseL = Mathf.Sin(time * 3f) * 0.5f + 0.5f;
        Color starColorL = Color.Lerp(StarDim, StarBright, starPulseL);
        string starHexL = ColorUtility.ToHtmlStringRGB(starColorL);
        sb.Append("<color=#").Append(starHexL).Append('>').Append(Star).Append("</color>");
        sb.Append("  ");

        sb.Append(state.InvertedText);

        // Right star: pulsing ✦ (offset phase)
        float starPulseR = Mathf.Sin(time * 3f + 1.5f) * 0.5f + 0.5f;
        Color starColorR = Color.Lerp(StarDim, StarBright, starPulseR);
        string starHexR = ColorUtility.ToHtmlStringRGB(starColorR);
        sb.Append("  <color=#").Append(starHexR).Append('>').Append(Star).Append("</color>");

        state.NameLabel.text = sb.ToString();
    }

    [HarmonyPatch(typeof(UIServerBrowser), "RemoveAllServers")]
    public static class RemoveAllServersPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            _animatedRows.Clear();
        }
    }
}
