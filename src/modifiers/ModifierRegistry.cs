using System.Collections.Generic;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Client-side cache of modifier data received from the server.
/// </summary>
public static class ModifierRegistry
{
    public static bool IsAdmin { get; set; }
    public static string ServerFlavor { get; set; } = "";

    // Full modifier list from server (keyed by modifier key)
    public static Dictionary<string, ModifierRegistryEntry> Modifiers { get; set; } = new();

    // Categories from server (ordered)
    public static CategoryEntry[] Categories { get; set; } = System.Array.Empty<CategoryEntry>();

    // Currently active modifiers
    public static List<ActiveModifierEntry> ActiveModifiers { get; set; } = new();

    // Current vote state
    public static VoteState CurrentVote { get; set; }

    public static void SetRegistry(ModifierRegistryPayload payload)
    {
        IsAdmin = payload.isAdmin;
        ServerFlavor = payload.flavor;
        Categories = payload.categories ?? System.Array.Empty<CategoryEntry>();
        Modifiers.Clear();
        foreach (var mod in payload.modifiers)
        {
            Modifiers[mod.key] = mod;
        }
    }

    public static void SetState(ModifierStatePayload payload)
    {
        ActiveModifiers.Clear();
        if (payload.activeModifiers != null)
        {
            ActiveModifiers.AddRange(payload.activeModifiers);
        }
    }

    public static void Clear()
    {
        IsAdmin = false;
        ServerFlavor = "";
        Modifiers.Clear();
        ActiveModifiers.Clear();
        CurrentVote = null;
    }
}

public class VoteState
{
    public string ModifierKey;
    public string ModifierName;
    public string Description;
    public string InitiatorName;
    public Dictionary<string, string> Parameters;
    public int YesCount;
    public int NoCount;
    public int RequiredVotes;
    public int TotalPlayers;
    public float InitialSoftSeconds;
    public float SoftSecondsRemaining;
    public float HardSecondsRemaining;
    public bool IsDisabling;
    public string Result; // null while active, set on vote_ended
}
