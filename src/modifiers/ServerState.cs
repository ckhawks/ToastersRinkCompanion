using System;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Client-side cache of server state (training tools, team names, etc.)
/// </summary>
public static class ServerState
{
    public static bool PasserEnabled { get; set; }
    public static bool ConesEnabled { get; set; }
    public static bool RedGoalieEnabled { get; set; }
    public static bool BlueGoalieEnabled { get; set; }
    public static bool RedDummyEnabled { get; set; }
    public static bool BlueDummyEnabled { get; set; }
    public static int PuckOnStringPlayerCount { get; set; }
    public static bool AutocleanEnabled { get; set; }
    public static string BlueTeamName { get; set; } = "Team Blue";
    public static string RedTeamName { get; set; } = "Team Red";

    public static void Update(ServerStatePayload payload)
    {
        PasserEnabled = payload.passerEnabled;
        ConesEnabled = payload.conesEnabled;
        RedGoalieEnabled = payload.redGoalieEnabled;
        BlueGoalieEnabled = payload.blueGoalieEnabled;
        RedDummyEnabled = payload.redDummyEnabled;
        BlueDummyEnabled = payload.blueDummyEnabled;
        PuckOnStringPlayerCount = payload.puckOnStringPlayerCount;
        AutocleanEnabled = payload.autocleanEnabled;
        BlueTeamName = payload.blueTeamName ?? "Team Blue";
        RedTeamName = payload.redTeamName ?? "Team Red";
    }

    public static void Clear()
    {
        PasserEnabled = false;
        ConesEnabled = false;
        RedGoalieEnabled = false;
        BlueGoalieEnabled = false;
        RedDummyEnabled = false;
        BlueDummyEnabled = false;
        PuckOnStringPlayerCount = 0;
        AutocleanEnabled = false;
        BlueTeamName = "Team Blue";
        RedTeamName = "Team Red";
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
    }
}
