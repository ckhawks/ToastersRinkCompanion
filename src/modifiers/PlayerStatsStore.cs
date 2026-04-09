using System;
using System.Collections.Generic;

namespace ToastersRinkCompanion.modifiers;

public static class PlayerStatsStore
{
    [Serializable]
    public class PlayerStatsEntry
    {
        public string steamId;
        public int goals;
        public int assists;
        public int touches;
        public int shots;
        public int saves;
        public int possessions;
        public int possessionSeconds;
        public int onIceSeconds;
        public int onBlueSeconds;
        public int onRedSeconds;
        public int asGoalieSeconds;
        public int jumps;
        public int airborneSeconds;
        public int slides;
        public int slidingSeconds;
        public float totalDistanceTravelled;
        public float averageSpeed;
        public float totalRevolutions;
        public int juggles;
        // Advanced tracking stats
        public int tacklesGiven;
        public int tacklesReceived;
        public int passes;
        public int passesReceived;
        public int blocks;
        public int shotsFaced;
        public int savesByStick;
        public int savesByBody;
        public int savesHomePlate;
        public int takeaways;
        public int turnovers;
        public int faceoffWins;
        public int faceoffTotal;
        public int plusMinus;
        public int ownGoals;
    }

    [Serializable]
    public class PlayerStatsPayload
    {
        public PlayerStatsEntry[] players;
    }

    private static readonly Dictionary<string, PlayerStatsEntry> _playerStats = new();

    public static void Update(PlayerStatsPayload payload)
    {
        _playerStats.Clear();
        if (payload?.players == null) return;
        foreach (var entry in payload.players)
        {
            if (entry?.steamId != null)
                _playerStats[entry.steamId] = entry;
        }
    }

    public static PlayerStatsEntry GetStats(string steamId)
    {
        return _playerStats.TryGetValue(steamId, out var entry) ? entry : null;
    }

    public static void Clear()
    {
        _playerStats.Clear();
    }
}
