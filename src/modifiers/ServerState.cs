using System;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Client-side cache of server state (training tools, team names, etc.)
/// </summary>
public static class ServerState
{
    private static bool _passerEnabled;
    private static bool _conesEnabled;
    private static bool _redGoalieEnabled;
    private static bool _blueGoalieEnabled;
    private static bool _redDummyEnabled;
    private static bool _blueDummyEnabled;
    private static int _puckOnStringPlayerCount;
    private static bool _autocleanEnabled;
    private static string _blueTeamName = "Team Blue";
    private static string _redTeamName = "Team Red";
    private static bool _isWarmup;

    public static bool PasserEnabled => _passerEnabled;
    public static bool ConesEnabled => _conesEnabled;
    public static bool RedGoalieEnabled => _redGoalieEnabled;
    public static bool BlueGoalieEnabled => _blueGoalieEnabled;
    public static bool RedDummyEnabled => _redDummyEnabled;
    public static bool BlueDummyEnabled => _blueDummyEnabled;
    public static int PuckOnStringPlayerCount => _puckOnStringPlayerCount;
    public static bool AutocleanEnabled => _autocleanEnabled;
    public static string BlueTeamName => _blueTeamName;
    public static string RedTeamName => _redTeamName;
    public static bool IsWarmup => _isWarmup;

    /// <summary>
    /// Updates cached state from payload. Returns true if any value changed.
    /// </summary>
    public static bool Update(ServerStatePayload payload)
    {
        bool changed = false;
        changed |= Set(ref _passerEnabled, payload.passerEnabled);
        changed |= Set(ref _conesEnabled, payload.conesEnabled);
        changed |= Set(ref _redGoalieEnabled, payload.redGoalieEnabled);
        changed |= Set(ref _blueGoalieEnabled, payload.blueGoalieEnabled);
        changed |= Set(ref _redDummyEnabled, payload.redDummyEnabled);
        changed |= Set(ref _blueDummyEnabled, payload.blueDummyEnabled);
        changed |= Set(ref _puckOnStringPlayerCount, payload.puckOnStringPlayerCount);
        changed |= Set(ref _autocleanEnabled, payload.autocleanEnabled);
        changed |= Set(ref _blueTeamName, payload.blueTeamName ?? "Team Blue");
        changed |= Set(ref _redTeamName, payload.redTeamName ?? "Team Red");
        changed |= Set(ref _isWarmup, payload.isWarmup);
        return changed;
    }

    private static bool Set<T>(ref T field, T value)
    {
        if (Equals(field, value)) return false;
        field = value;
        return true;
    }

    public static void Clear()
    {
        _passerEnabled = false;
        _conesEnabled = false;
        _redGoalieEnabled = false;
        _blueGoalieEnabled = false;
        _redDummyEnabled = false;
        _blueDummyEnabled = false;
        _puckOnStringPlayerCount = 0;
        _autocleanEnabled = false;
        _blueTeamName = "Team Blue";
        _redTeamName = "Team Red";
        _isWarmup = false;
    }

    [Serializable]
    public class ServerStatePayload
    {
        public bool passerEnabled;
        public bool conesEnabled;
        public bool redGoalieEnabled;
        public bool blueGoalieEnabled;
        public bool redDummyEnabled;
        public bool blueDummyEnabled;
        public int puckOnStringPlayerCount;
        public bool autocleanEnabled;
        public string blueTeamName;
        public string redTeamName;
        public bool isWarmup;
    }
}
