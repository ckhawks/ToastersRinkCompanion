using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ToastersRinkCompanion.collectibles;

public static class CollectiblesStore
{
    // Balance
    public static double Balance { get; private set; }
    public static bool IsBalanceLoaded { get; private set; }

    // Shop
    public static ShopItem[] ShopItems { get; private set; } = Array.Empty<ShopItem>();
    public static ShopSlotUpgradeInfo SlotUpgrade { get; private set; }
    public static bool IsShopLoaded { get; private set; }

    // Cases
    public static PlayerCaseEntry[] Cases { get; private set; } = Array.Empty<PlayerCaseEntry>();
    public static bool IsCasesLoaded { get; private set; }

    // Inventory
    public static CollectibleItem[] InventoryItems { get; private set; } = Array.Empty<CollectibleItem>();
    public static int TotalSlots { get; private set; }
    public static int UsedSlots { get; private set; }
    public static bool IsInventoryLoaded { get; private set; }

    // Transactions
    public static TransactionEntry[] Transactions { get; private set; } = Array.Empty<TransactionEntry>();
    public static bool IsTransactionsLoaded { get; set; }

    // Status messages (for showing purchase/sell results)
    public static string StatusMessage { get; set; }
    public static string StatusMessageType { get; set; } // "success", "error"

    public static void UpdateBalance(double balance)
    {
        Balance = balance;
        IsBalanceLoaded = true;
    }

    public static void UpdateShop(ShopItem[] items, ShopSlotUpgradeInfo upgrade, double balance)
    {
        ShopItems = items ?? Array.Empty<ShopItem>();
        SlotUpgrade = upgrade;
        Balance = balance;
        IsBalanceLoaded = true;
        IsShopLoaded = true;
    }

    public static void UpdateCases(PlayerCaseEntry[] cases)
    {
        Cases = cases ?? Array.Empty<PlayerCaseEntry>();
        IsCasesLoaded = true;
    }

    public static void UpdateInventory(CollectibleItem[] items, int totalSlots, int usedSlots)
    {
        InventoryItems = items ?? Array.Empty<CollectibleItem>();
        TotalSlots = totalSlots;
        UsedSlots = usedSlots;
        IsInventoryLoaded = true;
    }

    public static void UpdateTransactions(TransactionEntry[] transactions)
    {
        Transactions = transactions ?? Array.Empty<TransactionEntry>();
        IsTransactionsLoaded = true;
    }

    public static void UpdateItemProtection(string serial, bool isProtected)
    {
        if (InventoryItems == null) return;
        foreach (var item in InventoryItems)
        {
            if (item.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase))
            {
                item.Protected = isProtected;
                break;
            }
        }
    }

    public static void RemoveItem(string serial)
    {
        if (InventoryItems == null) return;
        var list = new List<CollectibleItem>(InventoryItems);
        list.RemoveAll(i => i.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase));
        InventoryItems = list.ToArray();
        UsedSlots = InventoryItems.Length;
    }

    public static void SetStatus(string message, string type)
    {
        StatusMessage = message;
        StatusMessageType = type;
    }

    public static void ClearStatus()
    {
        StatusMessage = null;
        StatusMessageType = null;
    }

    public static void InvalidateAll()
    {
        IsBalanceLoaded = false;
        IsShopLoaded = false;
        IsCasesLoaded = false;
        IsInventoryLoaded = false;
        IsTransactionsLoaded = false;
    }

    public static void Clear()
    {
        Balance = 0;
        ShopItems = Array.Empty<ShopItem>();
        SlotUpgrade = null;
        Cases = Array.Empty<PlayerCaseEntry>();
        InventoryItems = Array.Empty<CollectibleItem>();
        TotalSlots = 0;
        UsedSlots = 0;
        IsBalanceLoaded = false;
        IsShopLoaded = false;
        IsCasesLoaded = false;
        IsInventoryLoaded = false;
        IsTransactionsLoaded = false;
        Transactions = Array.Empty<TransactionEntry>();
        StatusMessage = null;
        StatusMessageType = null;
    }
}

[Serializable]
public class ShopItem
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("shorthand")] public string Shorthand { get; set; }
    [JsonProperty("item_type")] public string ItemType { get; set; }
    [JsonProperty("price")] public object Price { get; set; } // Can be number or "N/A"
    [JsonProperty("details")] public ShopItemDetails Details { get; set; }

    public double GetPrice()
    {
        if (Price is double d) return d;
        if (Price is long l) return l;
        if (double.TryParse(Price?.ToString(), out var parsed)) return parsed;
        return -1;
    }
}

[Serializable]
public class ShopItemDetails
{
    [JsonProperty("seriesId")] public int? SeriesId { get; set; }
    [JsonProperty("seriesName")] public string SeriesName { get; set; }
    [JsonProperty("currentTotalSlots")] public int? CurrentTotalSlots { get; set; }
    [JsonProperty("nextUpgradePrice")] public double? NextUpgradePrice { get; set; }
    [JsonProperty("nextSlotsAdded")] public int? NextSlotsAdded { get; set; }
    [JsonProperty("newTotalSlots")] public int? NewTotalSlots { get; set; }
}

[Serializable]
public class ShopSlotUpgradeInfo
{
    [JsonProperty("currentTotalSlots")] public int CurrentTotalSlots { get; set; }
    [JsonProperty("nextUpgradePrice")] public double NextUpgradePrice { get; set; }
    [JsonProperty("nextSlotsAdded")] public int NextSlotsAdded { get; set; }
}

[Serializable]
public class PlayerCaseEntry
{
    [JsonProperty("seriesId")] public int SeriesId { get; set; }
    [JsonProperty("quantity")] public int Quantity { get; set; }
    [JsonProperty("seriesName")] public string SeriesName { get; set; }
    [JsonProperty("shorthand")] public string Shorthand { get; set; }
}

[Serializable]
public class TransactionEntry
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("amount")] public long Amount { get; set; }
    [JsonProperty("transaction_type")] public string TransactionType { get; set; }
    [JsonProperty("description")] public string Description { get; set; }
    [JsonProperty("created_at")] public string CreatedAt { get; set; }
}
