using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Donors tab showing supporters with avatars, fetched from PuckStats API.
/// </summary>
public static class DonorsTab
{
    private static readonly HttpClient _http = new();
    private const string DonorsApiUrl = "https://puckstats.io/api/donors";
    private const string DonateUrl = "https://ko-fi.com/stellaric";

    private static List<DonorInfo> _cachedDonors;
    private static readonly Dictionary<string, Texture2D> _avatarCache = new();
    private static bool _fetching;
    private static VisualElement _listContainer;

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

        // Header
        var header = new Label("Supporters");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = UIHelpers.TextPrimary;
        header.style.marginBottom = 4;
        content.Add(header);

        var desc = new Label(
            "These are the people who help keep Toaster's Rink alive. " +
            "Their contributions go directly toward server hosting, development, " +
            "and making the experience better for everyone. Thank you!");
        desc.style.fontSize = 13;
        desc.style.color = new StyleColor(UIHelpers.TextSecondary);
        desc.style.whiteSpace = WhiteSpace.Normal;
        desc.style.marginBottom = 12;
        content.Add(desc);

        // Donate button
        var donateBtn = new Button(() => Application.OpenURL(DonateUrl));
        donateBtn.text = "Support on Ko-fi";
        donateBtn.style.fontSize = 14;
        donateBtn.style.backgroundColor = new StyleColor(new Color(1f, 0.35f, 0.45f));
        donateBtn.style.color = Color.white;
        donateBtn.style.paddingLeft = 16;
        donateBtn.style.paddingRight = 16;
        donateBtn.style.paddingTop = 8;
        donateBtn.style.paddingBottom = 8;
        donateBtn.style.marginBottom = 16;
        donateBtn.style.borderTopLeftRadius = 0;
        donateBtn.style.borderTopRightRadius = 0;
        donateBtn.style.borderBottomLeftRadius = 0;
        donateBtn.style.borderBottomRightRadius = 0;
        donateBtn.style.alignSelf = Align.FlexStart;
        content.Add(donateBtn);

        _listContainer = new VisualElement();
        content.Add(_listContainer);

        if (_cachedDonors != null)
            RenderDonors();
        else
            FetchDonors();
    }

    private static async void FetchDonors()
    {
        if (_fetching) return;
        _fetching = true;

        if (_listContainer != null)
        {
            _listContainer.Clear();
            var loading = new Label("Loading...");
            loading.style.color = new StyleColor(UIHelpers.TextMuted);
            loading.style.fontSize = 14;
            _listContainer.Add(loading);
        }

        try
        {
            string json = await _http.GetStringAsync(DonorsApiUrl);
            var response = JsonConvert.DeserializeObject<DonorsResponse>(json);
            _cachedDonors = response?.donors ?? new List<DonorInfo>();
            RenderDonors();
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to fetch donors: {e.Message}");
            if (_listContainer != null)
            {
                _listContainer.Clear();
                var error = new Label("Failed to load donors.");
                error.style.color = new StyleColor(UIHelpers.ErrorRed);
                error.style.fontSize = 14;
                _listContainer.Add(error);
            }
        }
        finally
        {
            _fetching = false;
        }
    }

    private static void RenderDonors()
    {
        if (_listContainer == null || _cachedDonors == null) return;
        _listContainer.Clear();

        // Randomize order so no one is always first
        var shuffled = new List<DonorInfo>(_cachedDonors);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        foreach (var donor in shuffled)
            BuildDonorCard(_listContainer, donor);
    }

    private static void BuildDonorCard(VisualElement parent, DonorInfo donor)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.Center;
        card.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        card.style.paddingLeft = 10;
        card.style.paddingRight = 12;
        card.style.paddingTop = 5;
        card.style.paddingBottom = 5;
        card.style.marginBottom = 2;
        card.style.borderTopLeftRadius = 4;
        card.style.borderTopRightRadius = 4;
        card.style.borderBottomLeftRadius = 4;
        card.style.borderBottomRightRadius = 4;
        parent.Add(card);

        // Avatar
        var avatarEl = new VisualElement();
        avatarEl.style.width = 28;
        avatarEl.style.height = 28;
        avatarEl.style.marginRight = 8;
        avatarEl.style.borderTopLeftRadius = 14;
        avatarEl.style.borderTopRightRadius = 14;
        avatarEl.style.borderBottomLeftRadius = 14;
        avatarEl.style.borderBottomRightRadius = 14;
        avatarEl.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        card.Add(avatarEl);

        // Load avatar async
        if (!string.IsNullOrEmpty(donor.avatarMedium))
            LoadAvatar(donor.steam_id, donor.avatarMedium, avatarEl);

        // Name
        var nameLabel = new Label(donor.username ?? "Unknown");
        nameLabel.style.fontSize = 14;
        nameLabel.style.color = new StyleColor(new Color(0.28f, 0.50f, 0.90f));
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        card.Add(nameLabel);

        // Click to open Steam profile
        card.RegisterCallback<ClickEvent>(evt =>
        {
            if (!string.IsNullOrEmpty(donor.steam_id))
                Application.OpenURL($"https://steamcommunity.com/profiles/{donor.steam_id}");
        });
        card.RegisterCallback<MouseEnterEvent>(evt =>
            card.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f)));
        card.RegisterCallback<MouseLeaveEvent>(evt =>
            card.style.backgroundColor = new StyleColor(UIHelpers.BgRow));
    }

    private static async void LoadAvatar(string key, string url, VisualElement target)
    {
        if (_avatarCache.TryGetValue(key, out var cached))
        {
            if (cached != null)
                target.style.backgroundImage = new StyleBackground(cached);
            return;
        }

        try
        {
            byte[] data = await _http.GetByteArrayAsync(url);
            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(data))
            {
                _avatarCache[key] = tex;
                target.style.backgroundImage = new StyleBackground(tex);
            }
        }
        catch
        {
            _avatarCache[key] = null;
        }
    }

    // ─── API models ──────────────────────────────────────────────────

    [Serializable]
    private class DonorsResponse
    {
        public List<DonorInfo> donors;
    }

    [Serializable]
    private class DonorInfo
    {
        public string steam_id;
        public string username;
        public string avatarIcon;
        public string avatarMedium;
        public string avatarFull;
    }
}
