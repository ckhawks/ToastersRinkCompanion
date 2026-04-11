using System;
using System.Collections.Generic;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Client-side store for the "three stars of the match" result sent by the server.
/// Updated when a <c>match_stars</c> envelope arrives from the server. Several UI
/// layers (chat prefix, scoreboard badge, end-of-match panel, in-world fresnel glow)
/// subscribe to <see cref="OnStarsChanged"/> and query the helper methods.
///
/// The store represents the LAST COMPLETED match's stars, which persist into the
/// following match until the next GameOver broadcast replaces them.
/// </summary>
public static class MatchStarsStore
{
    [Serializable]
    public class StarEntry
    {
        public string steamId;
        public int starRank;        // 1 = first star, 2 = second, 3 = third
        public double points;
        public string username;
        public int number;
        public string teamColor;    // "Red" / "Blue" / "None"
        public bool isGoalie;
        public string statLine;

        public int goals;
        public int assists;
        public int shots;
        public int saves;
        public int shotsFaced;
        public int passes;
        public int blocks;
        public int tacklesGiven;
        public int takeaways;
        public int turnovers;
        public int plusMinus;
    }

    [Serializable]
    public class CategoryLeader
    {
        public string category;
        public string steamId;
        public string username;
        public int number;
        public string teamColor;
        public int value;
    }

    [Serializable]
    public class MatchStarsPayload
    {
        public StarEntry[] stars;
        public CategoryLeader[] leaders;
        public int matchId;
        public int blueScore;
        public int redScore;
        public string winningTeam;
        public long finishedAtUnix;
        public bool isLateJoinSync;
    }

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------

    private static MatchStarsPayload _payload;
    private static readonly Dictionary<string, StarEntry> _bySteamId = new();

    /// <summary>
    /// Raised whenever a match_stars envelope is applied (new stars OR updated matchId).
    /// Subscribers should rebuild whatever display they own.
    /// </summary>
    public static event Action OnStarsChanged;

    /// <summary>
    /// Raised specifically when the matchId transitions from 0 to a real id
    /// (the asynchronous fill-in from the websocket upload confirmation).
    /// The end-of-match panel uses this to refresh its PuckStats.io button in place.
    /// </summary>
    public static event Action<int> OnMatchIdResolved;

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    public static MatchStarsPayload Current => _payload;

    public static bool HasStars => _payload != null && _payload.stars != null && _payload.stars.Length > 0;

    public static int MatchId => _payload?.matchId ?? 0;

    public static int BlueScore => _payload?.blueScore ?? 0;
    public static int RedScore => _payload?.redScore ?? 0;
    public static string WinningTeam => _payload?.winningTeam ?? "None";
    public static CategoryLeader[] Leaders => _payload?.leaders ?? Array.Empty<CategoryLeader>();

    /// <summary>
    /// True if the most recent Apply() call was a late-join sync sent to this client
    /// after the match already ended. UI layers that only want to auto-open at the
    /// actual GameOver moment should check this flag.
    /// </summary>
    public static bool LastApplyWasLateJoinSync { get; private set; }

    /// <summary>
    /// Star rank (1/2/3) for the given steamId, or 0 if not a star.
    /// </summary>
    public static int RankBySteamId(string steamId)
    {
        if (string.IsNullOrEmpty(steamId)) return 0;
        return _bySteamId.TryGetValue(steamId, out var entry) ? entry.starRank : 0;
    }

    /// <summary>
    /// Full star entry for the given steamId, or null.
    /// </summary>
    public static StarEntry EntryBySteamId(string steamId)
    {
        if (string.IsNullOrEmpty(steamId)) return null;
        return _bySteamId.TryGetValue(steamId, out var entry) ? entry : null;
    }

    /// <summary>
    /// Returns the three star entries ordered 1 → 2 → 3, with null gaps for missing ranks.
    /// </summary>
    public static StarEntry[] GetOrderedStars()
    {
        var result = new StarEntry[3];
        if (_payload?.stars == null) return result;

        foreach (var s in _payload.stars)
        {
            if (s == null) continue;
            if (s.starRank >= 1 && s.starRank <= 3)
                result[s.starRank - 1] = s;
        }
        return result;
    }

    // ---------------------------------------------------------------
    // Mutation (called from MessagingHandler match_stars handler)
    // ---------------------------------------------------------------

    /// <summary>
    /// Apply a fresh payload from the server. Triggers <see cref="OnStarsChanged"/>,
    /// and additionally <see cref="OnMatchIdResolved"/> if the matchId flipped from 0
    /// (or a different value) to a real one.
    /// </summary>
    public static void Apply(MatchStarsPayload payload)
    {
        if (payload == null) return;

        int previousMatchId = _payload?.matchId ?? 0;

        _payload = payload;
        LastApplyWasLateJoinSync = payload.isLateJoinSync;
        _bySteamId.Clear();
        if (payload.stars != null)
        {
            foreach (var entry in payload.stars)
            {
                if (entry?.steamId != null)
                    _bySteamId[entry.steamId] = entry;
            }
        }

        OnStarsChanged?.Invoke();

        if (payload.matchId > 0 && payload.matchId != previousMatchId)
            OnMatchIdResolved?.Invoke(payload.matchId);
    }

    public static void Clear()
    {
        _payload = null;
        _bySteamId.Clear();
        OnStarsChanged?.Invoke();
    }
}
