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
        public int asSingleGoalieSeconds;
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

    [Serializable]
    public class PlayerStatsDeltaEntry
    {
        public string steamId;
        public int? goals;
        public int? assists;
        public int? touches;
        public int? shots;
        public int? saves;
        public int? possessions;
        public int? possessionSeconds;
        public int? onIceSeconds;
        public int? onBlueSeconds;
        public int? onRedSeconds;
        public int? asGoalieSeconds;
        public int? asSingleGoalieSeconds;
        public int? jumps;
        public int? airborneSeconds;
        public int? slides;
        public int? slidingSeconds;
        public float? totalDistanceTravelled;
        public float? averageSpeed;
        public float? totalRevolutions;
        public int? juggles;
        public int? tacklesGiven;
        public int? tacklesReceived;
        public int? passes;
        public int? passesReceived;
        public int? blocks;
        public int? shotsFaced;
        public int? savesByStick;
        public int? savesByBody;
        public int? savesHomePlate;
        public int? takeaways;
        public int? turnovers;
        public int? faceoffWins;
        public int? faceoffTotal;
        public int? plusMinus;
        public int? ownGoals;
    }

    [Serializable]
    public class PlayerStatsDeltaPayload
    {
        public PlayerStatsDeltaEntry[] players;
    }

    private static readonly Dictionary<string, PlayerStatsEntry> _playerStats = new();

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<PlayerStatsPayload>("player_stats",
            (_, payload) =>
            {
                Update(payload);
                ModifierPanelUI.RefreshPlayerStats();
                ToastersRinkCompanion.handlers.ScoreboardStats.RefreshAllPlayers();
            });

        JsonMessageRouter.RegisterTypedHandler<PlayerStatsDeltaPayload>("player_stats_delta",
            (_, payload) =>
            {
                ApplyDelta(payload);
                ModifierPanelUI.RefreshPlayerStats();
                ToastersRinkCompanion.handlers.ScoreboardStats.RefreshAllPlayers();
            });
    }

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

    public static void ApplyDelta(PlayerStatsDeltaPayload delta)
    {
        if (delta?.players == null) return;
        foreach (var d in delta.players)
        {
            if (d?.steamId == null) continue;

            if (!_playerStats.TryGetValue(d.steamId, out var entry))
            {
                entry = new PlayerStatsEntry { steamId = d.steamId };
                _playerStats[d.steamId] = entry;
            }

            if (d.goals.HasValue)                  entry.goals = d.goals.Value;
            if (d.assists.HasValue)                entry.assists = d.assists.Value;
            if (d.touches.HasValue)                entry.touches = d.touches.Value;
            if (d.shots.HasValue)                  entry.shots = d.shots.Value;
            if (d.saves.HasValue)                  entry.saves = d.saves.Value;
            if (d.possessions.HasValue)            entry.possessions = d.possessions.Value;
            if (d.possessionSeconds.HasValue)      entry.possessionSeconds = d.possessionSeconds.Value;
            if (d.onIceSeconds.HasValue)           entry.onIceSeconds = d.onIceSeconds.Value;
            if (d.onBlueSeconds.HasValue)          entry.onBlueSeconds = d.onBlueSeconds.Value;
            if (d.onRedSeconds.HasValue)           entry.onRedSeconds = d.onRedSeconds.Value;
            if (d.asGoalieSeconds.HasValue)        entry.asGoalieSeconds = d.asGoalieSeconds.Value;
            if (d.asSingleGoalieSeconds.HasValue)  entry.asSingleGoalieSeconds = d.asSingleGoalieSeconds.Value;
            if (d.jumps.HasValue)                  entry.jumps = d.jumps.Value;
            if (d.airborneSeconds.HasValue)        entry.airborneSeconds = d.airborneSeconds.Value;
            if (d.slides.HasValue)                 entry.slides = d.slides.Value;
            if (d.slidingSeconds.HasValue)         entry.slidingSeconds = d.slidingSeconds.Value;
            if (d.totalDistanceTravelled.HasValue) entry.totalDistanceTravelled = d.totalDistanceTravelled.Value;
            if (d.averageSpeed.HasValue)           entry.averageSpeed = d.averageSpeed.Value;
            if (d.totalRevolutions.HasValue)       entry.totalRevolutions = d.totalRevolutions.Value;
            if (d.juggles.HasValue)                entry.juggles = d.juggles.Value;
            if (d.tacklesGiven.HasValue)           entry.tacklesGiven = d.tacklesGiven.Value;
            if (d.tacklesReceived.HasValue)        entry.tacklesReceived = d.tacklesReceived.Value;
            if (d.passes.HasValue)                 entry.passes = d.passes.Value;
            if (d.passesReceived.HasValue)         entry.passesReceived = d.passesReceived.Value;
            if (d.blocks.HasValue)                 entry.blocks = d.blocks.Value;
            if (d.shotsFaced.HasValue)             entry.shotsFaced = d.shotsFaced.Value;
            if (d.savesByStick.HasValue)           entry.savesByStick = d.savesByStick.Value;
            if (d.savesByBody.HasValue)            entry.savesByBody = d.savesByBody.Value;
            if (d.savesHomePlate.HasValue)         entry.savesHomePlate = d.savesHomePlate.Value;
            if (d.takeaways.HasValue)              entry.takeaways = d.takeaways.Value;
            if (d.turnovers.HasValue)              entry.turnovers = d.turnovers.Value;
            if (d.faceoffWins.HasValue)            entry.faceoffWins = d.faceoffWins.Value;
            if (d.faceoffTotal.HasValue)           entry.faceoffTotal = d.faceoffTotal.Value;
            if (d.plusMinus.HasValue)              entry.plusMinus = d.plusMinus.Value;
            if (d.ownGoals.HasValue)              entry.ownGoals = d.ownGoals.Value;
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
