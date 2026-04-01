using System;
using System.Collections.Generic;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Client-side store for player mod data received from the server.
/// </summary>
public static class PlayerModStore
{
    public class PlayerModEntry
    {
        public string steamId;
        public string[] modNames;
        public int localModCount;
    }

    [Serializable]
    public class PlayerModsPayload
    {
        public PlayerModEntry[] players;
    }

    // steamId -> mod entry
    private static readonly Dictionary<string, PlayerModEntry> _playerMods = new();

    public static void Update(PlayerModsPayload payload)
    {
        _playerMods.Clear();
        if (payload?.players == null) return;
        foreach (var entry in payload.players)
        {
            if (entry?.steamId != null)
                _playerMods[entry.steamId] = entry;
        }
    }

    public static PlayerModEntry GetMods(string steamId)
    {
        return _playerMods.TryGetValue(steamId, out var entry) ? entry : null;
    }

    public static void Clear()
    {
        _playerMods.Clear();
    }
}
