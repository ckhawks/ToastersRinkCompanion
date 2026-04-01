using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Servers tab showing Toaster's Rink servers from the PuckStats API.
/// Pings each server via TCP connect to measure latency.
/// Sorts by players first, then ping. EIS servers shown in a separate section.
/// </summary>
public static class ServersTab
{
    private static readonly HttpClient _http = new();
    private const string ServersApiUrl = "https://puckstats.io/api/servers";

    private static List<ServerInfo> _cachedServers;
    private static Dictionary<string, int> _pingResults = new();
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

        // Header row
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 12;
        content.Add(headerRow);

        var header = new Label("Servers");
        header.style.fontSize = 18;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color = UIHelpers.TextPrimary;
        headerRow.Add(header);

        var refreshBtn = new Button(() => FetchServers());
        refreshBtn.text = "Refresh";
        refreshBtn.style.fontSize = 12;
        refreshBtn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        refreshBtn.style.color = UIHelpers.TextPrimary;
        refreshBtn.style.paddingLeft = 10;
        refreshBtn.style.paddingRight = 10;
        refreshBtn.style.paddingTop = 4;
        refreshBtn.style.paddingBottom = 4;
        refreshBtn.style.borderTopLeftRadius = 0;
        refreshBtn.style.borderTopRightRadius = 0;
        refreshBtn.style.borderBottomLeftRadius = 0;
        refreshBtn.style.borderBottomRightRadius = 0;
        headerRow.Add(refreshBtn);

        _listContainer = new VisualElement();
        content.Add(_listContainer);

        if (_cachedServers != null)
            RenderServers();
        else
            FetchServers();
    }

    private static async void FetchServers()
    {
        if (_fetching) return;
        _fetching = true;

        if (_listContainer != null)
        {
            _listContainer.Clear();
            var loading = new Label("Loading servers...");
            loading.style.color = new StyleColor(UIHelpers.TextMuted);
            loading.style.fontSize = 14;
            _listContainer.Add(loading);
        }

        try
        {
            string json = await _http.GetStringAsync(ServersApiUrl);
            var response = JsonConvert.DeserializeObject<ServersResponse>(json);
            _cachedServers = response?.data?.servers?
                .Where(s => s.status == "online")
                .ToList() ?? new List<ServerInfo>();

            // Render immediately with no pings, then ping in background
            RenderServers();
            await PingAllServers();
            RenderServers();
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to fetch servers: {e.Message}");
            if (_listContainer != null)
            {
                _listContainer.Clear();
                var error = new Label("Failed to load servers.");
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

    // ─── Ping ────────────────────────────────────────────────────────

    private static async Task PingAllServers()
    {
        if (_cachedServers == null) return;

        // Ping unique IPs (not per-port — same IP = same latency)
        var uniqueIPs = _cachedServers
            .Select(s => s.ip)
            .Where(ip => !string.IsNullOrEmpty(ip))
            .Distinct()
            .ToList();

        var tasks = uniqueIPs.Select(async ip =>
        {
            int ping = await MeasurePing(ip);
            return (ip, ping);
        });

        var results = await Task.WhenAll(tasks);
        foreach (var (ip, ping) in results)
            _pingResults[ip] = ping;
    }

    /// <summary>
    /// Measures ping via TCP connect to the game's preview port.
    /// TODO: Once servers upgrade to b312+, use the game's TCPClient with
    /// the preview protocol (send {"type":0}, measure round-trip to {"type":1} response)
    /// for accurate ping like UIServerBrowser.PingServer does.
    /// For now, falls back to ICMP-style ping via System.Net.NetworkInformation.
    /// </summary>
    private static async Task<int> MeasurePing(string ip)
    {
        try
        {
            return await Task.Run(() =>
            {
                var pinger = new System.Net.NetworkInformation.Ping();
                var reply = pinger.Send(ip, 3000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    return (int)reply.RoundtripTime;
                return -1;
            });
        }
        catch
        {
            return -1;
        }
    }

    private static int GetPing(ServerInfo server)
    {
        return _pingResults.TryGetValue(server.ip, out int ping) ? ping : -1;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    private static void RenderServers()
    {
        if (_listContainer == null || _cachedServers == null) return;
        _listContainer.Clear();

        if (_cachedServers.Count == 0)
        {
            var empty = new Label("No servers online.");
            empty.style.color = new StyleColor(UIHelpers.TextMuted);
            empty.style.fontSize = 14;
            _listContainer.Add(empty);
            return;
        }

        // Split TR vs EIS
        var trServers = _cachedServers.Where(s => !IsEIS(s)).ToList();
        var eisServers = _cachedServers.Where(s => IsEIS(s)).ToList();

        // Sort: servers with players first, then by ping (ascending), then by name
        var sorted = SortServers(trServers);

        // Render TR servers
        foreach (var server in sorted)
            BuildServerRow(server);

        // EIS section at bottom
        if (eisServers.Count > 0)
        {
            BuildSectionHeader("EIS Servers", new Color(0.2f, 0.7f, 0.8f));
            foreach (var server in SortServers(eisServers))
                BuildServerRow(server);
        }
    }

    private static bool IsEIS(ServerInfo server)
    {
        return server.server_id != null && server.server_id.StartsWith("EIS", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ServerInfo> SortServers(List<ServerInfo> servers)
    {
        return servers
            .OrderByDescending(s => s.scoreboard?.numPlayers ?? 0)
            .ThenBy(s =>
            {
                int p = GetPing(s);
                return p < 0 ? int.MaxValue : p;
            })
            .ThenBy(s => s.server_name_shorthand)
            .ToList();
    }

    private static void BuildSectionHeader(string title, Color color)
    {
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        sep.style.marginTop = 14;
        sep.style.marginBottom = 6;
        _listContainer.Add(sep);

        var label = new Label(title);
        label.style.fontSize = 14;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = new StyleColor(color);
        label.style.marginBottom = 6;
        _listContainer.Add(label);
    }

    private static void BuildServerRow(ServerInfo server)
    {
        int players = server.scoreboard?.numPlayers ?? 0;
        int maxPlayers = server.scoreboard?.maxPlayers ?? 0;
        bool hasPlayers = players > 0;
        int ping = GetPing(server);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 10;
        row.style.paddingRight = 6;
        row.style.paddingTop = 5;
        row.style.paddingBottom = 5;
        row.style.marginBottom = 2;
        row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
        row.style.borderTopLeftRadius = 4;
        row.style.borderTopRightRadius = 4;
        row.style.borderBottomLeftRadius = 4;
        row.style.borderBottomRightRadius = 4;
        _listContainer.Add(row);

        // Player count dot
        var dot = new VisualElement();
        dot.style.width = 8;
        dot.style.height = 8;
        dot.style.borderTopLeftRadius = 4;
        dot.style.borderTopRightRadius = 4;
        dot.style.borderBottomLeftRadius = 4;
        dot.style.borderBottomRightRadius = 4;
        dot.style.backgroundColor = hasPlayers
            ? new StyleColor(UIHelpers.ActiveGreen)
            : new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dot.style.marginRight = 8;
        row.Add(dot);

        // Info column
        var infoCol = new VisualElement();
        infoCol.style.flexGrow = 1;
        row.Add(infoCol);

        // Name row
        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        infoCol.Add(nameRow);

        var nameLabel = new Label(server.server_name_shorthand ?? server.server_name);
        nameLabel.style.fontSize = 13;
        nameLabel.style.color = hasPlayers ? UIHelpers.TextPrimary : new StyleColor(UIHelpers.TextSecondary);
        nameLabel.style.unityFontStyleAndWeight = hasPlayers ? FontStyle.Bold : FontStyle.Normal;
        nameRow.Add(nameLabel);

        // Flavor tag
        if (!string.IsNullOrEmpty(server.flavor))
        {
            Color flavorColor = server.flavor switch
            {
                "chaos" => new Color(1f, 0.6f, 0.1f),
                "training" => new Color(0.3f, 0.8f, 0.4f),
                "standard" => new Color(0.3f, 0.5f, 1f),
                _ => UIHelpers.TextMuted
            };
            var flavorLabel = new Label(server.flavor);
            flavorLabel.style.fontSize = 10;
            flavorLabel.style.color = new StyleColor(flavorColor);
            flavorLabel.style.marginLeft = 8;
            nameRow.Add(flavorLabel);
        }

        // Location
        var locLabel = new Label(server.location ?? "");
        locLabel.style.fontSize = 11;
        locLabel.style.color = new StyleColor(UIHelpers.TextMuted);
        infoCol.Add(locLabel);

        // Game info when players present
        if (hasPlayers && server.scoreboard != null)
        {
            string phase = server.scoreboard.gamePhase ?? "";
            string score = $"{server.scoreboard.blueScore}-{server.scoreboard.redScore}";
            var gameLabel = new Label(phase == "Warmup" ? "Warmup" : $"{phase} {score}");
            gameLabel.style.fontSize = 11;
            gameLabel.style.color = new StyleColor(UIHelpers.AccentBlue);
            infoCol.Add(gameLabel);
        }

        // Ping
        var pingLabel = new Label(ping >= 0 ? $"{ping}ms" : "...");
        pingLabel.style.fontSize = 12;
        pingLabel.style.minWidth = 45;
        pingLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        pingLabel.style.marginRight = 8;
        if (ping < 0)
            pingLabel.style.color = new StyleColor(UIHelpers.TextMuted);
        else if (ping <= 50)
            pingLabel.style.color = new StyleColor(UIHelpers.ActiveGreen);
        else if (ping <= 100)
            pingLabel.style.color = new StyleColor(new Color(0.9f, 0.8f, 0.2f));
        else
            pingLabel.style.color = new StyleColor(new Color(0.9f, 0.4f, 0.1f));
        row.Add(pingLabel);

        // Player count
        var playerLabel = new Label($"{players}/{maxPlayers}");
        playerLabel.style.fontSize = 13;
        playerLabel.style.color = hasPlayers ? UIHelpers.TextPrimary : new StyleColor(UIHelpers.TextSecondary);
        playerLabel.style.minWidth = 40;
        playerLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        playerLabel.style.marginRight = 8;
        row.Add(playerLabel);

        // Join button
        var joinBtn = new Button(() =>
        {
            EventManager.TriggerEvent("Event_OnMainMenuClickJoinServer",
                new Dictionary<string, object>
                {
                    { "ipAddress", server.ip },
                    { "port", (ushort)server.port },
                    { "password", "" }
                });
            ModifierPanelUI.Hide();
        });
        joinBtn.text = "Join";
        joinBtn.style.fontSize = 12;
        joinBtn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
        joinBtn.style.color = UIHelpers.TextPrimary;
        joinBtn.style.paddingLeft = 10;
        joinBtn.style.paddingRight = 10;
        joinBtn.style.paddingTop = 3;
        joinBtn.style.paddingBottom = 3;
        joinBtn.style.borderTopLeftRadius = 0;
        joinBtn.style.borderTopRightRadius = 0;
        joinBtn.style.borderBottomLeftRadius = 0;
        joinBtn.style.borderBottomRightRadius = 0;
        row.Add(joinBtn);
    }

    // ─── API response models ─────────────────────────────────────────

    [Serializable]
    private class ServersResponse
    {
        public ServersData data;
    }

    [Serializable]
    private class ServersData
    {
        public List<ServerInfo> servers;
    }

    [Serializable]
    private class ServerInfo
    {
        public string server_id;
        public string server_name;
        public string server_name_shorthand;
        public string location;
        public string ip;
        public int port;
        public string region;
        public string flavor;
        public string status;
        public bool comptweaks;
        public ScoreboardInfo scoreboard;
    }

    [Serializable]
    private class ScoreboardInfo
    {
        public int redScore;
        public int blueScore;
        public string redTeamName;
        public string blueTeamName;
        public string gamePhase;
        public int periodTime;
        public int period;
        public int numPlayers;
        public int maxPlayers;
        public List<object> players;
        public List<object> gameModifiers;
    }
}
