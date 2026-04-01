using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Fetches and caches EIS team data (logos + names) from the PuckStats API.
/// </summary>
public static class EISTeamData
{
    private const string StandingsUrl = "https://puckstats.io/api/eis/standings";
    private const string ResourceBaseUrl = "https://puckstats.io/api/resource/";

    private static readonly HttpClient _http = new();
    private static Dictionary<string, EISTeam> _teamsByAcronym = new();
    private static Dictionary<string, Texture2D> _logoCache = new();
    private static bool _fetched;
    private static bool _fetching;

    public class EISTeam
    {
        public string name;
        public string acronym;
        public string hexColor;
        public string logoUrl; // relative path
    }

    public static bool HasData => _fetched;

    public static EISTeam GetTeamByAcronym(string acronym)
    {
        return _teamsByAcronym.TryGetValue(acronym, out var team) ? team : null;
    }

    public static async void EnsureFetched()
    {
        if (_fetched || _fetching) return;
        _fetching = true;

        try
        {
            string json = await _http.GetStringAsync(StandingsUrl);
            var response = JsonConvert.DeserializeObject<StandingsResponse>(json);
            if (response?.standings == null) return;

            _teamsByAcronym.Clear();
            foreach (var team in response.standings)
            {
                _teamsByAcronym[team.acronym] = new EISTeam
                {
                    name = team.name,
                    acronym = team.acronym,
                    hexColor = team.hex_color,
                    logoUrl = team.logo_url
                };
            }
            _fetched = true;
            Plugin.Log($"EISTeamData: Fetched {_teamsByAcronym.Count} teams");
        }
        catch (Exception e)
        {
            Plugin.LogError($"EISTeamData: Failed to fetch standings: {e.Message}");
        }
        finally
        {
            _fetching = false;
        }
    }

    private static readonly Dictionary<string, List<Action<Texture2D>>> _pendingCallbacks = new();

    /// <summary>
    /// Gets the logo texture asynchronously. Calls the callback immediately if cached,
    /// or when the download completes. Callback receives null if logo unavailable.
    /// </summary>
    public static void GetLogoAsync(string acronym, Action<Texture2D> callback)
    {
        // Already cached
        if (_logoCache.TryGetValue(acronym, out var tex))
        {
            callback(tex);
            return;
        }

        var team = GetTeamByAcronym(acronym);
        if (team == null || string.IsNullOrEmpty(team.logoUrl))
        {
            callback(null);
            return;
        }

        // Already downloading - add to pending callbacks
        if (_pendingCallbacks.ContainsKey(acronym))
        {
            _pendingCallbacks[acronym].Add(callback);
            return;
        }

        // Start download
        _pendingCallbacks[acronym] = new List<Action<Texture2D>> { callback };
        DownloadLogo(acronym, team.logoUrl);
    }

    private static async void DownloadLogo(string acronym, string relativePath)
    {
        Texture2D result = null;
        try
        {
            string url = ResourceBaseUrl + relativePath;
            byte[] data = await _http.GetByteArrayAsync(url);

            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(data))
                result = tex;
        }
        catch (Exception e)
        {
            Plugin.LogError($"EISTeamData: Failed to download logo for {acronym}: {e.Message}");
        }

        _logoCache[acronym] = result;

        // Fire all pending callbacks
        if (_pendingCallbacks.TryGetValue(acronym, out var callbacks))
        {
            foreach (var cb in callbacks)
            {
                try { cb(result); } catch { /* UI element may be gone */ }
            }
            _pendingCallbacks.Remove(acronym);
        }
    }

    [Serializable]
    private class StandingsResponse
    {
        public List<StandingsEntry> standings;
    }

    [Serializable]
    private class StandingsEntry
    {
        public string name;
        public string acronym;
        public string hex_color;
        public string logo_url;
    }
}
