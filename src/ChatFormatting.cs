using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;

namespace ToastersRinkCompanion;

public static class ChatFormatting
{
    private static readonly HashSet<string> _donorSteamIds = new HashSet<string>();
    private static readonly Dictionary<string, TeamEntry[]> _teamRosters = new Dictionary<string, TeamEntry[]>();

    public static bool IsDonor(string steamId) => _donorSteamIds.Contains(steamId);

    public static TeamEntry[] GetPlayerTeams(string steamId)
    {
        return _teamRosters.TryGetValue(steamId, out var teams) ? teams : null;
    }

    private static readonly string DONOR_PREFIX = "<size=16><b><color=#487fe6>DONOR</color></b></size> ";

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterHandler("chat_metadata", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                if (string.IsNullOrEmpty(payloadJson)) return;

                var payload = JsonConvert.DeserializeObject<ChatMetadataPayload>(payloadJson);
                if (payload == null) return;

                _donorSteamIds.Clear();
                if (payload.donors != null)
                {
                    foreach (string steamId in payload.donors)
                    {
                        _donorSteamIds.Add(steamId);
                    }
                }

                _teamRosters.Clear();
                if (payload.teamRosters != null)
                {
                    foreach (var entry in payload.teamRosters)
                    {
                        _teamRosters[entry.steamId] = entry.teams;
                    }
                }

                Plugin.Log($"ChatFormatting: Updated metadata ({_donorSteamIds.Count} donors, {_teamRosters.Count} rosters)");
            }
            catch (Exception e)
            {
                Plugin.LogError($"ChatFormatting: Failed to parse chat_metadata: {e.Message}");
            }
        });

        Plugin.Log("ChatFormatting handlers registered");
    }

    public static void Clear()
    {
        _donorSteamIds.Clear();
        _teamRosters.Clear();
    }

    public static string FormatTeamSuffix(string steamId)
    {
        if (!_teamRosters.TryGetValue(steamId, out TeamEntry[] teams) || teams.Length == 0)
            return "";

        var parts = new List<string>();
        foreach (var team in teams)
        {
            parts.Add($"<size=16><b><color=#{team.hexColor}>{team.acronym}</color></b></size>");
        }
        return " " + string.Join(" ", parts);
    }

    // Patch GetChatMessagePrefix to inject donor prefix and team suffix
    [HarmonyPatch(typeof(UIChat), "GetChatMessagePrefix")]
    public class GetChatMessagePrefixPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ChatMessage chatMessage, ref string __result)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            if (chatMessage.IsSystem) return;
            if (chatMessage.SteamID == null) return;

            string steamId = chatMessage.SteamID.Value.ToString();

            // Build the modified prefix
            string teamChatPrefix = chatMessage.IsTeamChat ? "[TEAM] " : "";
            string donorPrefix = _donorSteamIds.Contains(steamId) ? DONOR_PREFIX : "";
            string teamColoredName = StringUtils.WrapInTeamColor(
                chatMessage.Username.ToString(), chatMessage.Team.Value);
            string teamSuffix = FormatTeamSuffix(steamId);

            __result = teamChatPrefix + donorPrefix + teamColoredName + teamSuffix + ": ";
        }
    }

    [Serializable]
    public class ChatMetadataPayload
    {
        public string[] donors;
        public PlayerTeamRosterEntry[] teamRosters;
    }

    [Serializable]
    public class PlayerTeamRosterEntry
    {
        public string steamId;
        public TeamEntry[] teams;
    }

    [Serializable]
    public class TeamEntry
    {
        public string acronym;
        public string hexColor;
    }
}
