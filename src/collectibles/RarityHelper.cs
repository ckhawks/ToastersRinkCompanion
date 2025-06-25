using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.collectibles;

public class RarityHelper
{
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Mythic = 5,
        Artifact = 6 // Even higher, for truly unique items
    }

    // A dictionary to easily look up the color by rarity enum
    public static readonly Dictionary<Rarity, Color> RarityColors = new Dictionary<Rarity, Color>
    {
        { Rarity.Common,    new Color(0.7f, 0.7f, 0.7f, 1.0f) }, // Light Gray
        { Rarity.Uncommon,  new Color(0.0f, 0.8f, 0.2f, 1.0f) }, // Green
        { Rarity.Rare,      new Color(0.0f, 0.4f, 1.0f, 1.0f) }, // Blue
        { Rarity.Epic,      new Color(0.7f, 0.1f, 0.9f, 1.0f) }, // Purple
        { Rarity.Legendary, new Color(1.0f, 0.6f, 0.0f, 1.0f) }, // Orange/Gold
        { Rarity.Mythic,    new Color(1.0f, 0.0f, 0.0f, 1.0f) }, // Red (can be vibrant or dark depending on preference)
        { Rarity.Artifact,  new Color(1.0f, 1.0f, 0.0f, 1.0f) }  // Yellow/Gold (for ultimate items)
    };

    // Helper method to get the color for a given rarity
    public static Color GetColor(Rarity rarity)
    {
        if (RarityColors.TryGetValue(rarity, out Color color))
        {
            return color;
        }
        // Fallback for unexpected rarities
        return Color.white; 
    }
}