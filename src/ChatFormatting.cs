using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion;

public static class ChatFormatting
{
    // Role definitions keyed by slug, and per-player role assignments keyed by steamId.
    // Both are rebuilt on every chat_metadata broadcast.
    private static readonly Dictionary<string, RoleDefinition> _roleDefinitions = new Dictionary<string, RoleDefinition>();
    private static readonly Dictionary<string, List<PlayerRoleAssignment>> _playerRoleSlugs = new Dictionary<string, List<PlayerRoleAssignment>>();
    private static readonly Dictionary<string, TeamEntry[]> _teamRosters = new Dictionary<string, TeamEntry[]>();

    // Whitelisted rich-text tags the sanitizer allows through verbatim.
    private static readonly HashSet<string> _allowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "b", "i", "color", "size", "noparse"
    };

    private static readonly Regex _tagRegex = new Regex(@"<\s*(/?)\s*([a-zA-Z]+)[^>]*?>", RegexOptions.Compiled);

    /// <summary>
    /// Returns the player's active (non-expired) role definitions, ordered by chat priority (highest first).
    /// Hidden assignments are already filtered out server-side. Expiry is checked at call time so an
    /// expired badge disappears immediately without waiting for a rebroadcast.
    /// </summary>
    public static List<RoleDefinition> GetChatTags(string steamId)
    {
        var result = new List<RoleDefinition>();
        if (!_playerRoleSlugs.TryGetValue(steamId, out var assignments) || assignments == null)
            return result;

        DateTime now = DateTime.UtcNow;
        foreach (var assignment in assignments)
        {
            if (assignment == null || string.IsNullOrEmpty(assignment.slug)) continue;
            if (assignment.expiresAt.HasValue && assignment.expiresAt.Value <= now) continue;
            if (!_roleDefinitions.TryGetValue(assignment.slug, out var def) || def == null) continue;
            result.Add(def);
        }

        // "member" is the base community tier — only show it when it's the player's ONLY tag.
        // If they also hold donor/staff/etc., the higher tag stands in for it (so donor + member
        // never render together).
        if (result.Count > 1)
            result.RemoveAll(def => string.Equals(def.slug, "member", StringComparison.OrdinalIgnoreCase));

        result.Sort((a, b) => b.chatPriority.CompareTo(a.chatPriority));
        return result;
    }

    public static bool HasRole(string steamId, string slug)
    {
        return GetChatTags(steamId).Any(def => string.Equals(def.slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    // Backwards-compat shim: a player is a "donor" if they hold an active 'donor' role.
    public static bool IsDonor(string steamId) => HasRole(steamId, "donor");

    public static TeamEntry[] GetPlayerTeams(string steamId)
    {
        return _teamRosters.TryGetValue(steamId, out var teams) ? teams : null;
    }

    /// <summary>
    /// Last line of defense against a bad chat_markup row. Strips any tag not in the whitelist
    /// (b, i, color, size, noparse) and, if the remaining tags are not balanced, falls back to the
    /// role's plain-text slug so an unclosed &lt;color&gt; can't bleed into the rest of the chat line.
    /// </summary>
    public static string SanitizeMarkup(string chatMarkup, string fallbackLabel)
    {
        string safeFallback = System.Security.SecurityElement.Escape(fallbackLabel ?? "") ?? (fallbackLabel ?? "");
        if (string.IsNullOrEmpty(chatMarkup))
            return safeFallback;

        // Strip tags whose name is not whitelisted; keep whitelisted tags verbatim.
        string stripped = _tagRegex.Replace(chatMarkup, m =>
            _allowedTags.Contains(m.Groups[2].Value) ? m.Value : "");

        // Verify the surviving tags are balanced (properly nested).
        var stack = new Stack<string>();
        foreach (Match m in _tagRegex.Matches(stripped))
        {
            bool isClosing = m.Groups[1].Value == "/";
            string name = m.Groups[2].Value.ToLowerInvariant();
            if (!_allowedTags.Contains(name)) continue; // defensive; already stripped

            if (isClosing)
            {
                if (stack.Count == 0 || stack.Pop() != name)
                    return safeFallback;
            }
            else
            {
                stack.Push(name);
            }
        }

        if (stack.Count != 0)
            return safeFallback;

        return stripped;
    }

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

                _roleDefinitions.Clear();
                if (payload.roleDefinitions != null)
                {
                    foreach (var def in payload.roleDefinitions)
                    {
                        if (def == null || string.IsNullOrEmpty(def.slug)) continue;
                        _roleDefinitions[def.slug] = def;
                    }
                }

                _playerRoleSlugs.Clear();
                if (payload.playerRoles != null)
                {
                    foreach (var entry in payload.playerRoles)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.steamId)) continue;
                        _playerRoleSlugs[entry.steamId] = entry.assignments != null
                            ? new List<PlayerRoleAssignment>(entry.assignments)
                            : new List<PlayerRoleAssignment>();
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

                Plugin.Log($"ChatFormatting: Updated metadata ({_roleDefinitions.Count} role defs, {_playerRoleSlugs.Count} players with roles, {_teamRosters.Count} rosters)");
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
        _roleDefinitions.Clear();
        _playerRoleSlugs.Clear();
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

    // Patch GetChatMessagePrefix to inject dynamic role prefixes and team suffix
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

            // Build the role prefix from the player's active tags (priority-ordered), each role's
            // chat markup rendered verbatim through the sanitizer + a trailing space.
            var rolePrefix = new StringBuilder();
            foreach (var role in GetChatTags(steamId))
            {
                rolePrefix.Append(SanitizeMarkup(role.chatMarkup, role.slug));
                rolePrefix.Append(' ');
            }

            string teamColoredName = StringUtils.WrapInTeamColor(
                chatMessage.Username.ToString(), chatMessage.Team.Value);
            string teamSuffix = FormatTeamSuffix(steamId);

            __result = teamChatPrefix + rolePrefix + teamColoredName + teamSuffix + ": ";
        }
    }

    [Serializable]
    public class ChatMetadataPayload
    {
        public string[] donors;                        // compat — no longer used for rendering
        public PlayerRoleEntry[] playerRoles;
        public RoleDefinition[] roleDefinitions;
        public PlayerTeamRosterEntry[] teamRosters;
    }

    [Serializable]
    public class PlayerRoleEntry
    {
        public string steamId;
        public PlayerRoleAssignment[] assignments;
    }

    [Serializable]
    public class PlayerRoleAssignment
    {
        public string slug;
        public DateTime? expiresAt;   // null = permanent; filtered at render time
    }

    [Serializable]
    public class RoleDefinition
    {
        public string slug;
        public string chatMarkup;     // full rich-text prefix, rendered verbatim (through sanitizer)
        public int chatPriority;      // higher = shown first
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
