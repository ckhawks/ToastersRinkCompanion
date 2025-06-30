using System;
using Newtonsoft.Json;
using UnityEngine;

namespace ToastersRinkCompanion.collectibles;

public static class Collectible
{
    public static string FormatItemWithRarityColor(CollectibleItem item)
    {
        string color;
        switch (item.RarityName)
        {
            case "Mythic":
                color = "#eb333d";
                break;
            case "Legendary":
                color = "#ffa315";
                break;
            case "Epic":
                color = "#7b08f9";
                break;
            case "Rare":
                color = "#0197db";
                break;
            case "Uncommon":
                color = "#5ac252";
                break;
            case "Common":
            default:
                color = "#efefef";
                break;
        }
        
        return $"<b><color={color}>{item.FullName}</color></b>";
    }

    public static bool HasTrait(CollectibleItem item, string traitName)
    {
        foreach (CollectibleTrait trait in item.Traits)
        {
            if (trait.Name == traitName)
            {
                return true;
            }
        }

        return false;
    }
}

[Serializable]
public class OpenCasePayload
{
    public CollectibleItem CollectibleItem { get; set; }
    public Vector3 Position { get; set; }
    
    public Quaternion Rotation { get; set; }

    public OpenCasePayload(Vector3 p, Quaternion r, CollectibleItem ci)
    {
        CollectibleItem = ci;
        Position = p;
        Rotation = r;
    }
}

[Serializable]
public class ItemShowPayload
{
    public CollectibleItem CollectibleItem { get; set; }
    public Vector3 Position { get; set; }
    
    // We don't need rotation here because the item (text display) always faces the local player

    public ItemShowPayload(Vector3 p, CollectibleItem ci)
    {
        CollectibleItem = ci;
        Position = p;
    }
}

[Serializable]
public class CollectibleItem // Maps to CollectibleDisplayData from socket
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("fullName")]
    public string FullName { get; set; }
        
    [JsonProperty("serial")]
    public string Serial { get; set; }
    
    [JsonProperty("itemName")]
    public string ItemName { get; set; }
    
    [JsonProperty("seriesName")]
    public string Series { get; set; }
    
    [JsonProperty("scaleFactor")]
    public float ScaleFactor { get; set; }
    
    [JsonProperty("sizingName")]
    public string SizingName { get; set; }
    
    [JsonProperty("patternName")]
    public string PatternName { get; set; }
    
    [JsonProperty("rarityName")]
    public string RarityName { get; set; }
    
    // Ignoring rarityDisplayColor because we will handle based on rarity name
    
    [JsonProperty("traits")]
    public CollectibleTrait[] Traits { get; set; }
    
    [JsonProperty("value")]
    public int Value { get; set; }
    
    [JsonProperty("unboxedBySteamId")]
    public string UnboxedBySteamId { get; set; }
    
    [JsonProperty("currentOwnerSteamId")]
    public string CurrentOwnerSteamId { get; set; }

    [JsonProperty("created_at")]
    public string CreatedAt { get; set; }
}

[Serializable]
public class CollectibleTrait
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}