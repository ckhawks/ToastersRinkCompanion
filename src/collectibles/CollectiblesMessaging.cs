using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ToastersRinkCompanion.modifiers;

namespace ToastersRinkCompanion.collectibles;

public static class CollectiblesMessaging
{
    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterHandler("collectibles_balance", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<BalancePayload>(payloadJson);
                if (payload == null) return;
                CollectiblesStore.UpdateBalance(payload.balance);
                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_balance handler: {e}"); }
        });

        JsonMessageRouter.RegisterHandler("collectibles_shop", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<ShopPayload>(payloadJson);
                if (payload == null) return;

                ShopItem[] items = Array.Empty<ShopItem>();
                if (!string.IsNullOrEmpty(payload.shopItems))
                    items = JsonConvert.DeserializeObject<ShopItem[]>(payload.shopItems);

                ShopSlotUpgradeInfo upgradeInfo = null;
                if (!string.IsNullOrEmpty(payload.slotUpgradeInfo))
                    upgradeInfo = JsonConvert.DeserializeObject<ShopSlotUpgradeInfo>(payload.slotUpgradeInfo);

                CollectiblesStore.UpdateShop(items, upgradeInfo, (double)payload.balance);
                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_shop handler: {e}"); }
        });

        JsonMessageRouter.RegisterHandler("collectibles_purchase_result", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<PurchaseResultPayload>(payloadJson);
                if (payload == null) return;

                if (payload.status == "success")
                {
                    try
                    {
                        var response = JObject.Parse(payload.response);
                        double balance = response["balance"]?.Value<double>() ?? CollectiblesStore.Balance;
                        string item = response["item"]?.Value<string>() ?? "item";
                        CollectiblesStore.UpdateBalance(balance);
                        CollectiblesStore.SetStatus($"Purchased {item}!", "success");
                    }
                    catch { CollectiblesStore.SetStatus("Purchase successful!", "success"); }

                    CollectiblesStore.InvalidateAll();
                }
                else
                {
                    try
                    {
                        var response = JObject.Parse(payload.response);
                        string message = response["message"]?.Value<string>() ?? "Purchase failed.";
                        CollectiblesStore.SetStatus(message, "error");
                    }
                    catch { CollectiblesStore.SetStatus("Purchase failed.", "error"); }
                }

                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_purchase_result handler: {e}"); }
        });

        JsonMessageRouter.RegisterHandler("collectibles_cases", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<CasesPayload>(payloadJson);
                if (payload == null) return;
                CollectiblesStore.UpdateCases(payload.cases);
                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_cases handler: {e}"); }
        });

        JsonMessageRouter.RegisterHandler("collectibles_case_open_result", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<CaseOpenResultPayload>(payloadJson);
                if (payload == null) return;

                if (payload.status == "success")
                {
                    CollectiblesStore.SetStatus(payload.message, "success");
                    // Invalidate inventory and cases since they changed
                    CollectiblesStore.InvalidateAll();
                }
                else
                {
                    CollectiblesStore.SetStatus(payload.message, "error");
                }

                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_case_open_result handler: {e}"); }
        });

        JsonMessageRouter.RegisterHandler("collectibles_inventory", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<InventoryPayload>(payloadJson);
                if (payload == null) return;

                CollectibleItem[] items = Array.Empty<CollectibleItem>();
                if (!string.IsNullOrEmpty(payload.items))
                    items = JsonConvert.DeserializeObject<CollectibleItem[]>(payload.items);

                CollectiblesStore.UpdateInventory(items, payload.totalSlots, payload.usedSlots);
                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_inventory handler: {e}"); }
        });

        JsonMessageRouter.RegisterHandler("collectibles_sell_result", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<SellResultPayload>(payloadJson);
                if (payload == null) return;

                if (payload.status == "success")
                {
                    try
                    {
                        var response = JObject.Parse(payload.response);
                        double amount = response["amount"]?.Value<double>() ?? 0;
                        double balance = response["balance"]?.Value<double>() ?? CollectiblesStore.Balance;
                        CollectiblesStore.UpdateBalance(balance);
                        CollectiblesStore.RemoveItem(payload.serial);
                        CollectiblesStore.SetStatus($"Sold for T${amount:n0}! Balance: T${balance:n0}", "success");
                    }
                    catch { CollectiblesStore.SetStatus("Item sold!", "success"); }
                }
                else
                {
                    try
                    {
                        var response = JObject.Parse(payload.response);
                        string message = response["message"]?.Value<string>() ?? "Sell failed.";
                        CollectiblesStore.SetStatus(message, "error");
                    }
                    catch { CollectiblesStore.SetStatus("Sell failed.", "error"); }
                }

                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_sell_result handler: {e}"); }
        });

        JsonMessageRouter.RegisterHandler("collectibles_protect_result", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<ProtectResultPayload>(payloadJson);
                if (payload == null) return;

                if (payload.status == "success")
                {
                    CollectiblesStore.UpdateItemProtection(payload.serial, payload.isProtected);
                    CollectiblesStore.SetStatus(
                        payload.isProtected ? "Item protected." : "Item unprotected.",
                        "success");
                }
                else
                {
                    CollectiblesStore.SetStatus(payload.message ?? "Failed to toggle protection.", "error");
                }

                ModifierPanelUI.RefreshCurrentTab();
            }
            catch (Exception e) { Plugin.LogError($"collectibles_protect_result handler: {e}"); }
        });
    }

    // --- Send methods ---

    public static void RequestBalance()
        => JsonMessageRouter.SendMessage("collectibles_balance_request", 0, new { });

    public static void RequestShop()
        => JsonMessageRouter.SendMessage("collectibles_shop_request", 0, new { });

    public static void PurchaseItem(string shorthand, int quantity)
        => JsonMessageRouter.SendMessage("collectibles_shop_purchase", 0, new { shorthand, quantity });

    public static void RequestCases()
        => JsonMessageRouter.SendMessage("collectibles_cases_request", 0, new { });

    public static void OpenCase(string seriesShorthand)
        => JsonMessageRouter.SendMessage("collectibles_case_open", 0, new { seriesShorthand });

    public static void RequestInventory()
        => JsonMessageRouter.SendMessage("collectibles_inventory_request", 0, new { });

    public static void SellItem(string serial)
        => JsonMessageRouter.SendMessage("collectibles_sell", 0, new { serial });

    public static void ToggleProtection(string serial)
        => JsonMessageRouter.SendMessage("collectibles_protect_toggle", 0, new { serial });

    // --- Payload classes ---

    [Serializable]
    private class BalancePayload
    {
        public double balance;
    }

    [Serializable]
    private class ShopPayload
    {
        public decimal balance;
        public string shopItems; // JSON string of ShopItem[]
        public string slotUpgradeInfo; // JSON string of ShopSlotUpgradeInfo
    }

    [Serializable]
    private class PurchaseResultPayload
    {
        public string status;
        public string response; // JSON string of full response
    }

    [Serializable]
    private class CasesPayload
    {
        public PlayerCaseEntry[] cases;
    }

    [Serializable]
    private class CaseOpenResultPayload
    {
        public string status;
        public string message;
    }

    [Serializable]
    private class InventoryPayload
    {
        public string items; // JSON string of CollectibleItem[]
        public int totalSlots;
        public int usedSlots;
    }

    [Serializable]
    private class SellResultPayload
    {
        public string status;
        public string serial;
        public string response; // JSON string of full response
    }

    [Serializable]
    private class ProtectResultPayload
    {
        public string status;
        public string serial;
        public bool isProtected;
        public string message;
    }
}
