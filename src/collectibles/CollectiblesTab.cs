using System;
using System.Collections.Generic;
using System.Linq;
using ToastersRinkCompanion.modifiers;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.collectibles;

public static class CollectiblesTab
{
    private static int _activeSubTab = 0;
    private static readonly string[] SubTabNames = { "Info", "Shop", "Inventory", "Trade", "History" };
    private static float _lastCaseOpenTime;
    private static string _inventorySort = "value";

    public static void BuildContent(VisualElement parent)
    {
        // Sub-tab bar
        var subTabBar = new VisualElement();
        subTabBar.style.flexDirection = FlexDirection.Row;
        subTabBar.style.paddingLeft = 12;
        subTabBar.style.paddingTop = 4;
        subTabBar.style.paddingBottom = 0;
        subTabBar.style.backgroundColor = new StyleColor(UIHelpers.BgDark);
        parent.Add(subTabBar);

        for (int i = 0; i < SubTabNames.Length; i++)
        {
            int idx = i;
            var btn = new Button(() =>
            {
                _activeSubTab = idx;
                CollectiblesStore.ClearStatus();
                // Always refetch transactions when switching to History tab
                if (idx == 4)
                    CollectiblesStore.IsTransactionsLoaded = false;
                ModifierPanelUI.RefreshCurrentTab();
            });
            btn.text = SubTabNames[i];
            bool active = i == _activeSubTab;
            btn.style.color = active ? UIHelpers.TextPrimary : UIHelpers.TextMuted;
            btn.style.backgroundColor = StyleKeyword.None;
            btn.style.fontSize = 13;
            btn.style.paddingTop = 6;
            btn.style.paddingBottom = 6;
            btn.style.paddingLeft = 12;
            btn.style.paddingRight = 12;
            btn.style.marginRight = 2;
            btn.style.borderTopWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderBottomWidth = 2;
            btn.style.borderBottomColor = active ? UIHelpers.AccentBlue : Color.clear;
            subTabBar.Add(btn);
        }

        // Balance label on far right
        var balanceSpacer = new VisualElement();
        balanceSpacer.style.flexGrow = 1;
        subTabBar.Add(balanceSpacer);

        if (!CollectiblesStore.IsBalanceLoaded)
            CollectiblesMessaging.RequestBalance();

        var balanceLabel = new Label(CollectiblesStore.IsBalanceLoaded
            ? $"T${CollectiblesStore.Balance:n0}"
            : "T$...");
        balanceLabel.style.fontSize = 13;
        balanceLabel.style.color = new Color(1f, 0.64f, 0.08f);
        balanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        balanceLabel.style.paddingRight = 12;
        balanceLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        subTabBar.Add(balanceLabel);

        // Status message
        if (!string.IsNullOrEmpty(CollectiblesStore.StatusMessage))
        {
            var statusBar = new VisualElement();
            statusBar.style.paddingLeft = 12;
            statusBar.style.paddingRight = 12;
            statusBar.style.paddingTop = 6;
            statusBar.style.paddingBottom = 6;
            statusBar.style.backgroundColor = new StyleColor(
                CollectiblesStore.StatusMessageType == "error"
                    ? new Color(0.9f, 0.1f, 0.1f, 0.15f)
                    : new Color(0.3f, 0.8f, 0.4f, 0.15f));

            var statusLabel = new Label(CollectiblesStore.StatusMessage);
            statusLabel.style.fontSize = 13;
            statusLabel.style.color = CollectiblesStore.StatusMessageType == "error"
                ? UIHelpers.ErrorRed
                : UIHelpers.ActiveGreen;
            statusBar.Add(statusLabel);
            parent.Add(statusBar);
        }

        // Content area
        var content = new ScrollView(ScrollViewMode.Vertical);
        content.style.flexGrow = 1;
        content.style.paddingLeft = 12;
        content.style.paddingRight = 12;
        content.style.paddingTop = 12;
        content.style.paddingBottom = 8;
        parent.Add(content);

        switch (_activeSubTab)
        {
            case 0: BuildInfoContent(content); break;
            case 1: BuildShopContent(content); break;
            case 2: BuildInventoryContent(content); break;
            case 3: BuildTradeContent(content); break;
            case 4: BuildHistoryContent(content); break;
        }
    }

    // ==================== INFO ====================

    private static void BuildInfoContent(VisualElement parent)
    {
        AddSectionHeader(parent, "Collectibles");

        AddParagraph(parent, "This server features a collectibles system. Collectibles are items you can obtain by opening cases. They don't serve any purpose besides showing them off, and they cannot be exchanged or purchased with real money.");

        AddSpacer(parent, 8);
        AddParagraph(parent, "Use the tabs above to browse the shop, open cases, and manage your inventory. You can also show off items in-game during warmup.");

        AddSpacer(parent, 16);
        AddSectionHeader(parent, "Rarity Tiers");

        var rarities = new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic" };
        foreach (var rarity in rarities)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            var swatch = new VisualElement();
            swatch.style.width = 12;
            swatch.style.height = 12;
            swatch.style.marginRight = 8;
            swatch.style.borderTopLeftRadius = 2;
            swatch.style.borderTopRightRadius = 2;
            swatch.style.borderBottomLeftRadius = 2;
            swatch.style.borderBottomRightRadius = 2;
            swatch.style.backgroundColor = new StyleColor(CollectiblesConstants.GetColor(rarity));
            row.Add(swatch);

            var label = new Label(rarity);
            label.style.fontSize = 13;
            label.style.color = new StyleColor(CollectiblesConstants.GetColor(rarity));
            row.Add(label);

            parent.Add(row);
        }

    }

    // ==================== SHOP ====================

    private static void BuildShopContent(VisualElement parent)
    {
        if (!CollectiblesStore.IsShopLoaded)
        {
            CollectiblesMessaging.RequestShop();
            AddMutedLabel(parent, "Loading shop...");
            return;
        }

        AddSectionHeader(parent, "Shop");

        if (CollectiblesStore.ShopItems.Length == 0)
        {
            AddMutedLabel(parent, "No items available.");
            return;
        }

        foreach (var item in CollectiblesStore.ShopItems)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.marginBottom = 4;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            // Left: item info
            var infoCol = new VisualElement();

            var nameLabel = new Label(item.Name);
            nameLabel.style.fontSize = 14;
            nameLabel.style.color = UIHelpers.TextPrimary;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            infoCol.Add(nameLabel);

            if (item.ItemType == "collectibles_slots_upgrade" && item.Details?.CurrentTotalSlots != null)
            {
                var detailLabel = new Label($"Current: {item.Details.CurrentTotalSlots} slots, +{item.Details.NextSlotsAdded} next");
                detailLabel.style.fontSize = 12;
                detailLabel.style.color = UIHelpers.TextMuted;
                infoCol.Add(detailLabel);
            }

            row.Add(infoCol);

            // Right: price + buy button
            var rightCol = new VisualElement();
            rightCol.style.flexDirection = FlexDirection.Row;
            rightCol.style.alignItems = Align.Center;

            double price = item.GetPrice();
            var priceLabel = new Label(price >= 0 ? $"T${price:n0}" : "N/A");
            priceLabel.style.fontSize = 14;
            priceLabel.style.color = price >= 0 ? new Color(1f, 0.64f, 0.08f) : UIHelpers.TextMuted;
            priceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            priceLabel.style.marginRight = 10;
            rightCol.Add(priceLabel);

            if (price >= 0)
            {
                string shorthand = item.Shorthand;
                string itemName = item.Name;
                bool isCase = item.ItemType == "case";

                if (isCase)
                {
                    // Quantity selector for cases
                    var qtyField = new IntegerField();
                    qtyField.value = 1;
                    qtyField.style.width = 40;
                    qtyField.style.marginRight = 4;
                    qtyField.style.fontSize = 13;
                    var qtyInput = qtyField.Q(className: "unity-base-text-field__input");
                    if (qtyInput != null)
                    {
                        qtyInput.style.backgroundColor = new StyleColor(UIHelpers.BgDark);
                        qtyInput.style.color = UIHelpers.TextPrimary;
                        UIHelpers.SetBorder(qtyInput, 1, UIHelpers.BorderGray);
                    }
                    rightCol.Add(qtyField);

                    double itemPrice = price;
                    var buyBtn = MakeButton("Buy", UIHelpers.ActiveGreen, () =>
                    {
                        int qty = Math.Max(1, qtyField.value);
                        ConfirmationDialog.Show(
                            "Confirm Purchase",
                            $"Buy {qty}x {itemName} for T${itemPrice * qty:n0}?",
                            "Buy",
                            UIHelpers.ActiveGreen,
                            () => CollectiblesMessaging.PurchaseItem(shorthand, qty));
                    });
                    rightCol.Add(buyBtn);
                }
                else
                {
                    var buyBtn = MakeButton("Buy", UIHelpers.ActiveGreen, () =>
                    {
                        ConfirmationDialog.Show(
                            "Confirm Purchase",
                            $"Buy {itemName} for T${price:n0}?",
                            "Buy",
                            UIHelpers.ActiveGreen,
                            () => CollectiblesMessaging.PurchaseItem(shorthand, 1));
                    });
                    rightCol.Add(buyBtn);
                }
            }

            row.Add(rightCol);
            parent.Add(row);
        }
    }

    // ==================== INVENTORY ====================

    private static void BuildInventoryContent(VisualElement parent)
    {
        // Request both cases and inventory if not loaded
        bool needsLoad = false;
        if (!CollectiblesStore.IsCasesLoaded)
        {
            CollectiblesMessaging.RequestCases();
            needsLoad = true;
        }
        if (!CollectiblesStore.IsInventoryLoaded)
        {
            CollectiblesMessaging.RequestInventory();
            needsLoad = true;
        }
        if (!CollectiblesStore.IsBalanceLoaded)
        {
            CollectiblesMessaging.RequestBalance();
        }
        if (needsLoad)
        {
            AddMutedLabel(parent, "Loading inventory...");
            return;
        }

        // --- Cases Section ---
        if (CollectiblesStore.Cases.Length > 0)
        {
            AddSectionHeader(parent, "Cases");

            bool isWarmup = modifiers.ServerState.IsWarmup;
            float cooldownRemaining = _lastCaseOpenTime > 0 ? 8.5f - (Time.time - _lastCaseOpenTime) : 0;
            bool onCooldown = cooldownRemaining > 0;

            if (!isWarmup)
            {
                var warningLabel = new Label("Case opening is only available during warmup.");
                warningLabel.style.fontSize = 12;
                warningLabel.style.color = UIHelpers.TextMuted;
                warningLabel.style.marginBottom = 6;
                warningLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                parent.Add(warningLabel);
            }

            foreach (var caseEntry in CollectiblesStore.Cases)
            {
                var caseRow = new VisualElement();
                caseRow.style.flexDirection = FlexDirection.Row;
                caseRow.style.alignItems = Align.Center;
                caseRow.style.justifyContent = Justify.SpaceBetween;
                caseRow.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
                caseRow.style.paddingTop = 6;
                caseRow.style.paddingBottom = 6;
                caseRow.style.paddingLeft = 10;
                caseRow.style.paddingRight = 10;
                caseRow.style.marginBottom = 3;
                caseRow.style.borderTopLeftRadius = 4;
                caseRow.style.borderTopRightRadius = 4;
                caseRow.style.borderBottomLeftRadius = 4;
                caseRow.style.borderBottomRightRadius = 4;

                var caseInfo = new VisualElement();
                caseInfo.style.flexDirection = FlexDirection.Row;
                caseInfo.style.alignItems = Align.Center;

                var caseName = new Label($"{caseEntry.SeriesName} Case");
                caseName.style.fontSize = 13;
                caseName.style.color = UIHelpers.TextPrimary;
                caseName.style.unityFontStyleAndWeight = FontStyle.Bold;
                caseName.style.marginRight = 8;
                caseInfo.Add(caseName);

                var caseCount = new Label($"x{caseEntry.Quantity}");
                caseCount.style.fontSize = 12;
                caseCount.style.color = UIHelpers.TextMuted;
                caseInfo.Add(caseCount);

                caseRow.Add(caseInfo);

                bool canOpen = isWarmup && !onCooldown && caseEntry.Quantity > 0;
                string shorthand = caseEntry.Shorthand;

                var openBtn = MakeSmallButton(
                    onCooldown ? $"Wait ({cooldownRemaining:F0}s)" : "Open",
                    canOpen ? UIHelpers.AccentBlue : UIHelpers.TextMuted,
                    canOpen ? () =>
                    {
                        _lastCaseOpenTime = Time.time;
                        CollectiblesMessaging.OpenCase(shorthand);
                        ModifierPanelUI.Hide();
                    } : (Action)null,
                    !canOpen);
                caseRow.Add(openBtn);

                parent.Add(caseRow);
            }

            AddSpacer(parent, 10);
        }

        // --- Items Section ---
        // Header row with title, storage count, and sort
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 8;

        var titleLabel = new Label("Items");
        titleLabel.style.fontSize = 16;
        titleLabel.style.color = UIHelpers.TextPrimary;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerRow.Add(titleLabel);

        var rightHeader = new VisualElement();
        rightHeader.style.flexDirection = FlexDirection.Row;
        rightHeader.style.alignItems = Align.Center;

        var storageLabel = new Label($"{CollectiblesStore.UsedSlots}/{CollectiblesStore.TotalSlots} slots");
        storageLabel.style.fontSize = 13;
        storageLabel.style.color = UIHelpers.TextMuted;
        storageLabel.style.marginRight = 8;
        rightHeader.Add(storageLabel);

        // Sort dropdown
        var sortOptions = new List<string> { "Rarity", "Value", "Name" };
        var sortDropdown = new PopupField<string>(sortOptions, sortOptions.IndexOf(
            _inventorySort == "value" ? "Value" : _inventorySort == "name" ? "Name" : "Rarity"));
        sortDropdown.RegisterValueChangedCallback(evt =>
        {
            _inventorySort = evt.newValue.ToLower();
            ModifierPanelUI.RefreshCurrentTab();
        });
        UIHelpers.StyleDropdown(sortDropdown, 80, 120);
        rightHeader.Add(sortDropdown);

        headerRow.Add(rightHeader);
        parent.Add(headerRow);

        if (CollectiblesStore.InventoryItems.Length == 0)
        {
            AddMutedLabel(parent, "Your inventory is empty. Open some cases!");
            return;
        }

        // Sort items
        var items = SortItems(CollectiblesStore.InventoryItems, _inventorySort);

        foreach (var item in items)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.marginBottom = 3;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            // Left side: serial + name + traits
            var infoCol = new VisualElement();
            infoCol.style.flexShrink = 1;

            // Name row with serial
            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            var serialLabel = new Label(item.Serial.ToUpper());
            serialLabel.style.fontSize = 11;
            serialLabel.style.color = UIHelpers.TextMuted;
            serialLabel.style.marginRight = 8;
            nameRow.Add(serialLabel);

            var nameLabel = new Label(item.FullName);
            nameLabel.style.fontSize = 13;
            nameLabel.style.color = new StyleColor(CollectiblesConstants.GetColor(item.RarityName));
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameRow.Add(nameLabel);

            // "NEW" badge for recently unboxed items (last 5 minutes)
            if (DateTime.TryParse(item.CreatedAt, out var createdAt) &&
                (DateTime.UtcNow - createdAt).TotalMinutes < 5)
            {
                var newBadge = new Label("NEW");
                newBadge.style.fontSize = 9;
                newBadge.style.color = UIHelpers.ActiveGreen;
                newBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                newBadge.style.marginLeft = 6;
                newBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
                nameRow.Add(newBadge);
            }

            infoCol.Add(nameRow);

            row.Add(infoCol);

            // Right side: value + protect + sell
            var actionsCol = new VisualElement();
            actionsCol.style.flexDirection = FlexDirection.Row;
            actionsCol.style.alignItems = Align.Center;
            actionsCol.style.flexShrink = 0;

            var valueLabel = new Label($"T${item.Value}");
            valueLabel.style.fontSize = 13;
            valueLabel.style.color = new Color(1f, 0.64f, 0.08f);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.marginRight = 8;
            actionsCol.Add(valueLabel);

            // Protect toggle
            string serial = item.Serial;
            bool isProtected = item.Protected;
            var protectBtn = MakeSmallButton(isProtected ? "Unlock" : "Lock",
                isProtected ? new Color(0.9f, 0.6f, 0.1f) : UIHelpers.TextMuted,
                () => CollectiblesMessaging.ToggleProtection(serial));
            protectBtn.style.marginRight = 4;
            actionsCol.Add(protectBtn);

            // Sell button
            if (isProtected)
            {
                var sellBtn = MakeSmallButton("Sell", UIHelpers.TextMuted, null, true);
                sellBtn.tooltip = "Unprotect this item first";
                actionsCol.Add(sellBtn);
            }
            else
            {
                string itemName = item.FullName;
                int itemValue = item.Value;
                var sellBtn = MakeSmallButton("Sell", UIHelpers.ErrorRed, () =>
                {
                    ConfirmationDialog.Show(
                        "Confirm Sale",
                        $"Sell {itemName} (#{serial.ToUpper()}) for T${itemValue}?",
                        "Sell",
                        UIHelpers.ErrorRed,
                        () => CollectiblesMessaging.SellItem(serial));
                });
                actionsCol.Add(sellBtn);
            }

            // Show button
            var showBtn = MakeSmallButton("Show", UIHelpers.BgButton, () =>
            {
                NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage($"/show {serial}", false, false);
                ModifierPanelUI.Hide();
            });
            actionsCol.Add(showBtn);

            row.Add(actionsCol);
            parent.Add(row);
        }
    }

    // ==================== TRADE ====================

    private static void BuildTradeContent(VisualElement parent)
    {
        AddSectionHeader(parent, "Trading");

        AddParagraph(parent, "Item trading is currently available on the PuckStats website. You can create, view, and accept trade offers there.");

        AddSpacer(parent, 12);

        var linkBtn = MakeButton("Open PuckStats Trades", UIHelpers.AccentBlue, () =>
        {
            Application.OpenURL("https://puckstats.io/trades");
        });
        linkBtn.style.alignSelf = Align.FlexStart;
        parent.Add(linkBtn);
    }

    // ==================== HISTORY ====================

    private static void BuildHistoryContent(VisualElement parent)
    {
        if (!CollectiblesStore.IsTransactionsLoaded)
        {
            CollectiblesMessaging.RequestTransactions();
            AddMutedLabel(parent, "Loading transactions...");
            return;
        }

        AddSectionHeader(parent, "Recent Transactions");

        if (CollectiblesStore.Transactions.Length == 0)
        {
            AddMutedLabel(parent, "No transactions found.");
            return;
        }

        foreach (var tx in CollectiblesStore.Transactions)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.backgroundColor = new StyleColor(UIHelpers.BgRow);
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.marginBottom = 3;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            // Left side: description + timestamp
            var infoCol = new VisualElement();
            infoCol.style.flexShrink = 1;

            var descLabel = new Label(tx.Description ?? "Transaction");
            descLabel.style.fontSize = 12;
            descLabel.style.color = UIHelpers.TextPrimary;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            infoCol.Add(descLabel);

            // Timestamp + type row
            var metaRow = new VisualElement();
            metaRow.style.flexDirection = FlexDirection.Row;
            metaRow.style.alignItems = Align.Center;
            metaRow.style.marginTop = 2;

            string typeLabel = tx.TransactionType switch
            {
                "award" => "Award",
                "purchase" => "Purchase",
                "sale" => "Sale",
                "trade" => "Trade",
                _ => tx.TransactionType
            };

            var typeBadge = new Label(typeLabel);
            typeBadge.style.fontSize = 10;
            typeBadge.style.color = tx.TransactionType switch
            {
                "award" => new StyleColor(UIHelpers.ActiveGreen),
                "purchase" => new StyleColor(UIHelpers.AccentBlue),
                "sale" => new StyleColor(new Color(0.9f, 0.6f, 0.1f)),
                "trade" => new StyleColor(new Color(0.7f, 0.5f, 0.9f)),
                _ => UIHelpers.TextMuted
            };
            typeBadge.style.marginRight = 8;
            metaRow.Add(typeBadge);

            if (DateTime.TryParse(tx.CreatedAt, out var createdAt))
            {
                var timeLabel = new Label(FormatTimeAgo(createdAt));
                timeLabel.style.fontSize = 10;
                timeLabel.style.color = UIHelpers.TextMuted;
                metaRow.Add(timeLabel);
            }

            infoCol.Add(metaRow);
            row.Add(infoCol);

            // Right side: amount
            bool isPositive = tx.Amount >= 0;
            var amountLabel = new Label($"{(isPositive ? "+" : "")}T${tx.Amount:n0}");
            amountLabel.style.fontSize = 14;
            amountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            amountLabel.style.color = isPositive
                ? new StyleColor(UIHelpers.ActiveGreen)
                : new StyleColor(UIHelpers.ErrorRed);
            amountLabel.style.flexShrink = 0;
            amountLabel.style.marginLeft = 10;
            row.Add(amountLabel);

            parent.Add(row);
        }
    }

    private static string FormatTimeAgo(DateTime utcTime)
    {
        var span = DateTime.UtcNow - utcTime;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return utcTime.ToLocalTime().ToString("MMM d");
    }

    // ==================== HELPERS ====================

    private static readonly Dictionary<string, int> RarityOrder = new()
    {
        { "Common", 0 }, { "Uncommon", 1 }, { "Rare", 2 },
        { "Epic", 3 }, { "Legendary", 4 }, { "Mythic", 5 }
    };

    private static CollectibleItem[] SortItems(CollectibleItem[] items, string sortBy)
    {
        return sortBy switch
        {
            "value" => items.OrderByDescending(i => i.Value).ToArray(),
            "name" => items.OrderBy(i => i.FullName).ToArray(),
            _ => items.OrderByDescending(i => RarityOrder.GetValueOrDefault(i.RarityName, 0))
                      .ThenByDescending(i => i.Value).ToArray(),
        };
    }

    private static void AddSectionHeader(VisualElement parent, string text)
    {
        var label = new Label(text);
        label.style.fontSize = 16;
        label.style.color = UIHelpers.TextPrimary;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 8;
        parent.Add(label);
    }

    private static void AddParagraph(VisualElement parent, string text)
    {
        var label = new Label(text);
        label.style.fontSize = 13;
        label.style.color = UIHelpers.TextSecondary;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginBottom = 4;
        parent.Add(label);
    }

    private static void AddMutedLabel(VisualElement parent, string text)
    {
        var label = new Label(text);
        label.style.fontSize = 13;
        label.style.color = UIHelpers.TextMuted;
        label.style.unityFontStyleAndWeight = FontStyle.Italic;
        parent.Add(label);
    }

    private static void AddSpacer(VisualElement parent, int height)
    {
        var spacer = new VisualElement();
        spacer.style.height = height;
        parent.Add(spacer);
    }

    private static Button MakeButton(string text, Color color, Action onClick, bool disabled = false)
    {
        var btn = new Button(disabled ? null : onClick);
        btn.text = text;
        btn.style.fontSize = 14;
        btn.style.paddingTop = 4;
        btn.style.paddingBottom = 4;
        btn.style.paddingLeft = 12;
        btn.style.paddingRight = 12;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;

        if (disabled)
        {
            btn.style.backgroundColor = new StyleColor(UIHelpers.BgButtonDisabled);
            btn.style.color = UIHelpers.TextMuted;
        }
        else
        {
            btn.style.backgroundColor = new StyleColor(color);
            btn.style.color = UIHelpers.TextPrimary;
        }

        return btn;
    }

    private static Button MakeSmallButton(string text, Color color, Action onClick, bool disabled = false)
    {
        var btn = new Button(disabled ? null : onClick);
        btn.text = text;
        btn.style.fontSize = 11;
        btn.style.paddingTop = 2;
        btn.style.paddingBottom = 2;
        btn.style.paddingLeft = 6;
        btn.style.paddingRight = 6;
        btn.style.marginLeft = 3;
        btn.style.borderTopLeftRadius = 0;
        btn.style.borderTopRightRadius = 0;
        btn.style.borderBottomLeftRadius = 0;
        btn.style.borderBottomRightRadius = 0;

        if (disabled)
        {
            btn.style.backgroundColor = new StyleColor(UIHelpers.BgButtonDisabled);
            btn.style.color = UIHelpers.TextMuted;
        }
        else
        {
            btn.style.backgroundColor = new StyleColor(UIHelpers.BgButton);
            btn.style.color = UIHelpers.TextPrimary;
        }

        return btn;
    }
}
