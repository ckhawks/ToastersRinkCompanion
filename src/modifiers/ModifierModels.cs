using System;
using System.Collections.Generic;

namespace ToastersRinkCompanion.modifiers;

// --- Registry (received on connect) ---

[Serializable]
public class ModifierRegistryPayload
{
    public string flavor;
    public bool isAdmin;
    public ModifierRegistryEntry[] modifiers;
    public CategoryEntry[] categories;
}

[Serializable]
public class CategoryEntry
{
    public string key;
    public string label;
    public string color; // hex color
}

[Serializable]
public class ModifierRegistryEntry
{
    public string key;
    public string name;
    public string description;
    public string type;      // "Toggle", "SetValue", "FunctionCall"
    public string category;  // "Gameplay", "Admin", "Gamemode", "Silly"
    public string usage;
    public bool availableOnFlavor = true;
    public ArgSchemaEntry[] argSchemas;
}

[Serializable]
public class ArgSchemaEntry
{
    public string name;
    public string controlType; // "None", "Slider", "Dropdown", "PlayerPicker", "IntField", "TeamPicker"
    public string[] allowedValues;
    public float minValue;
    public float maxValue;
    public float defaultValue;
    public string[] keywords;
    public bool isRequired;
}

// --- State (received on connect + broadcast on change) ---

[Serializable]
public class ModifierStatePayload
{
    public ActiveModifierEntry[] activeModifiers;
}

[Serializable]
public class ActiveModifierEntry
{
    public string key;
    public string name;
    public string category;
    public Dictionary<string, string> parameters;
}

// --- Vote lifecycle ---

[Serializable]
public class VoteStartedPayload
{
    public string modifierKey;
    public string modifierName;
    public string description;
    public string initiatorName;
    public Dictionary<string, string> parameters;
    public int yesCount;
    public int noCount;
    public int requiredVotes;
    public int totalPlayers;
    public double softTimeoutSeconds;
    public double hardTimeoutSeconds;
    public bool isDisabling;
}

[Serializable]
public class VoteUpdatePayload
{
    public int yesCount;
    public int noCount;
    public double softSecondsRemaining;
    public double hardSecondsRemaining;
}

[Serializable]
public class VoteEndedPayload
{
    public string modifierKey;
    public string modifierName;
    public string result; // "passed", "failed", "timed_out", "overridden"
    public int yesCount;
    public int noCount;
}

// --- Client -> Server requests ---

[Serializable]
public class ModifierVoteRequestPayload
{
    public string modifierKey;
    public Dictionary<string, string> parameters;
    public bool isForce;
}

[Serializable]
public class ModifierCastVotePayload
{
    public bool voteYes;
}
