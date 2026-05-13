using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine.UIElements;

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

    // Colored star prefix for the 3 stars of the last match (gold/silver/bronze).
    // starRank: 1 = first star, 2 = second, 3 = third. Returns "" for anything else.
    public static string GetStarPrefix(int starRank)
    {
        return starRank switch
        {
            1 => "<size=16><b><color=#FFD700>★</color></b></size> ",
            2 => "<size=16><b><color=#EDEDED>★</color></b></size> ",
            3 => "<size=16><b><color=#CD7F32>★</color></b></size> ",
            _ => ""
        };
    }

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

    // Insert a star Label (using a font that has the ★ glyph) as a sibling of the
    // message's text Label when the sender has a match-star rank. The B323 default
    // chat font lacks the Misc Symbols block so ★ won't render inline via rich text.
    [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
    public class AddChatMessagePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIChat __instance, ChatMessage chatMessage)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            if (chatMessage.IsSystem || chatMessage.SteamID == null) return;

            string steamId = chatMessage.SteamID.Value.ToString();
            int starRank = ToastersRinkCompanion.modifiers.MatchStarsStore.RankBySteamId(steamId);
            if (starRank < 1 || starRank > 3) return;

            var uiChatMessages = AccessTools.Field(typeof(UIChat), "uiChatMessages")?.GetValue(__instance) as System.Collections.IList;
            if (uiChatMessages == null || uiChatMessages.Count == 0) return;

            var lastMessage = uiChatMessages[uiChatMessages.Count - 1];
            var textLabel = AccessTools.Field(lastMessage.GetType(), "label")?.GetValue(lastMessage) as Label;
            if (textLabel == null) return;

            var parent = textLabel.parent;
            if (parent == null) return;

            UnityEngine.Color32 color = starRank switch
            {
                1 => new UnityEngine.Color32(0xFF, 0xD7, 0x00, 0xFF),
                2 => new UnityEngine.Color32(0xED, 0xED, 0xED, 0xFF),
                _ => new UnityEngine.Color32(0xCD, 0x7F, 0x32, 0xFF),
            };

            var starLabel = new Label("★");
            GlyphFont.ApplyStarFont(starLabel);
            starLabel.style.color = new StyleColor(color);
            starLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            starLabel.style.fontSize = 16;
            starLabel.style.marginLeft = 4;
            starLabel.style.marginRight = 0;
            starLabel.style.paddingLeft = 0;
            starLabel.style.paddingRight = 0;
            starLabel.style.paddingTop = 0;
            starLabel.style.paddingBottom = 0;
            starLabel.style.flexShrink = 0;
            starLabel.style.translate = new StyleTranslate(new Translate(0, -4));

            parent.style.flexDirection = FlexDirection.Row;
            parent.style.alignItems = Align.Center;

            int idx = parent.IndexOf(textLabel);
            parent.Insert(idx < 0 ? 0 : idx, starLabel);

            // Mirror the text label's resolved opacity onto the star so the fade
            // (driven by the "blurred" class on textLabel) carries to the star.
            starLabel.schedule.Execute(() =>
            {
                if (textLabel.panel == null || starLabel.panel == null) return;
                starLabel.style.opacity = textLabel.resolvedStyle.opacity;
            }).Every(33);
        }
    }

    // Patch GetChatMessagePrefix to inject donor prefix and team suffix
    [HarmonyPatch(typeof(UIChat), "GetChatMessagePrefix")]
    public class GetChatMessagePrefixPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ChatMessage chatMessage, ref string __result)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            if (chatMessage.IsSystem || chatMessage.SteamID == null) return;

            string steamId = chatMessage.SteamID.Value.ToString();

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
