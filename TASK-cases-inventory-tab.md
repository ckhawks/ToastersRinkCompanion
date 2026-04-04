# Task: Cases / Inventory / Balance UI Tab

## Goal

Move the cases, inventory, balance, and shop interactions out of chat and into a new UI tab in the F3 panel (`ModifierPanelUI`). Currently these interactions are all chat-command based (e.g. `/balance`, `/open`, `/buy`, `/inv`, `/sell`) and the responses come back as chat messages. We want a proper UI for browsing and interacting with these systems.

## Context

### How the current system works (server-side, in `../ToastersRinkSuite`)

The server-side plugin (`ToastersRinkSuite`) handles economy/collectible commands via chat commands that send WebSocket requests to a PuckStats backend and return results as chat messages:

- **Balance** (`EconomyCommands.BalanceCommand`): Sends `balance_view_request` via WebSocket, response comes back with `balance` (double).
- **Shop** (`EconomyCommands.BuyCommand`): `/buy` with no args sends `shop_view_request`, gets back shop items list with prices. `/buy <item> [qty]` sends `shop_purchase_request`.
- **Case Opening** (`CollectibleCommands.OpenCaseCommand`): `/open` with no args sends `case_inventory_request` to show available cases. `/open <series>` sends a case open request. Results trigger `case_open` message to the companion.
- **Inventory**: Likely a similar request/response pattern (look for `inventory_request` or similar).
- **Sell** (`EconomyHelper.HandleCollectibleSellResponse`): Sells by serial number, returns amount + new balance.

All responses are currently rendered as formatted chat messages (HTML-ish markup with `<size>`, `<color>`, `<b>` tags).

### How to send data to the companion client

The server communicates with the companion via `JsonMessageRouter` - the server sends named JSON messages that the client registers handlers for. See `MessagingHandler.cs` for 40+ examples of this pattern:

```csharp
// Server-side (ToastersRinkSuite) sends:
MessagingHandler.SendToClient(clientId, "message_type", jsonPayload);

// Client-side (ToastersRinkCompanion) receives:
JsonMessageRouter.RegisterHandler("message_type", (sender, payloadJson) => {
    var payload = JsonConvert.DeserializeObject<PayloadType>(payloadJson);
    // Update UI state
});
```

**The approach**: Instead of having the server format chat messages, create new message types (or modify existing response handlers) that send structured data to the companion. The companion stores this data and renders it in the new tab.

### Client-side architecture

- **Tab system**: `ModifierPanelUI.cs` has `RegisterTab(name, buildContentCallback)`. See existing tabs like `PlayersTab.cs`, `ModifierListTab.cs` for patterns.
- **UI framework**: Unity UIElements (not IMGUI). All UI is built programmatically in C#.
- **Styling**: Use `UIHelpers.cs` for shared colors (`BgRow`, `TextPrimary`, `AccentBlue`, etc.) and helpers (`SetBorder`, `StyleDropdown`).
- **State pattern**: See `ModifierRegistry.cs` or `PlayerStatsStore.cs` for how data is stored statically and the UI rebuilds from it.

## Implementation Plan

### 1. Define payload models (client-side)

Create payload classes for the data the tab needs. At minimum:
- `BalancePayload` - `{ balance: number }`
- `ShopPayload` - `{ balance: number, items: ShopItem[] }` where `ShopItem` has name, shorthand, price, type, details
- `CaseInventoryPayload` - `{ cases: CaseInfo[] }` with series name, count, shorthand
- `InventoryPayload` - `{ items: CollectibleItem[] }` (reuse existing `CollectibleItem` model from `collectibles/Collectible.cs`)
- Case open results already work via `case_open` handler

### 2. Create a data store (client-side)

Similar to `PlayerStatsStore.cs` - a static class that holds the latest economy/inventory state and gets updated by message handlers. Something like `EconomyStore.cs`:
- `Balance` (decimal)
- `ShopItems` (list)
- `CaseInventory` (list)  
- `Inventory` (list of owned collectibles)
- `Update()` methods called from message handlers

### 3. Register new message handlers (client-side)

In `MessagingHandler.cs` (or a new `EconomyMessaging.cs` similar to `ModifierMessaging.cs`), register handlers for:
- `balance_data` / `shop_data` / `case_inventory_data` / `inventory_data`
- On receive: deserialize, update `EconomyStore`, refresh UI if tab is visible

### 4. Build the UI tab (client-side)

New file `src/modifiers/EconomyTab.cs` (or `CollectiblesTab.cs`). Register it in `ModifierPanelUI.Setup()`:
```csharp
RegisterTab("Economy", EconomyTab.BuildContent);
```

Sub-sections within the tab (could use sub-tabs or accordion sections):
- **Balance** - Show current balance prominently
- **Shop** - Grid/list of buyable items with Buy buttons
- **Cases** - Show owned cases with Open buttons, trigger case opening
- **Inventory** - Scrollable list of owned collectibles with rarity colors, traits, value, sell button

For actions (buy, open, sell), send chat commands via:
```csharp
NetworkBehaviourSingleton<ChatManager>.Instance.Client_SendChatMessage("/buy intro 1", false, false);
```
This piggybacks on existing server command handling. Alternatively, if you want to bypass chat, you'll need to add new WebSocket message types on the server side too.

### 5. Server-side changes (in `../ToastersRinkSuite`)

Modify the response handlers to send companion messages instead of (or in addition to) chat messages. For example, in `EconomyHelper.HandleBalanceViewResponse`, after getting the balance, also send a companion message:

```csharp
// In addition to or instead of chat message:
MessagingHandler.SendToClient(player.OwnerClientId, "economy_balance", 
    JsonSerializer.Serialize(new { balance = balance }));
```

Same pattern for shop view, case inventory, etc.

### 6. Request data on tab open

When the user opens the Economy tab, request fresh data. Either:
- Send chat commands silently (`/balance`, `/inv`) to trigger server responses
- Or add a new companion message type for requesting data refresh

## Key Files to Study

### Client (ToastersRinkCompanion)
- `src/modifiers/ModifierPanelUI.cs` - Tab registration (line 210-218)
- `src/modifiers/PlayersTab.cs` - Good example of a data-driven tab
- `src/modifiers/ModifierListTab.cs` - Good example of a searchable list tab  
- `src/modifiers/UIHelpers.cs` - Shared styling
- `src/modifiers/PlayerStatsStore.cs` - Data store pattern
- `src/modifiers/ModifierMessaging.cs` - Message handler registration pattern
- `src/MessagingHandler.cs` - Central message handler setup
- `src/collectibles/Collectible.cs` - Existing collectible item model
- `src/collectibles/CollectiblesConstants.cs` - Rarity colors

### Server (../ToastersRinkSuite)
- `src/commands/EconomyCommands.cs` - Balance, buy, shop commands
- `src/commands/CollectibleCommands.cs` - Case opening, inventory commands
- `src/collectibles/EconomyHelper.cs` - Response handlers (balance, shop, sell)
- `src/collectibles/CollectiblesHelper.cs` - Collectible formatting utilities
- `src/companion/MessagingHandler.cs` - How server sends messages to companion

## Notes

- The simplest initial approach: use `Client_SendChatMessage` to trigger existing commands, and create new companion message types for the responses. This minimizes server changes.
- Collectible items have traits (Holographic, Glistening, Flaming, etc.) and rarities (Common through Mythic) - the UI should reflect these with appropriate colors from `CollectiblesConstants.cs`.
- The case opening animation (`Opening.cs`) already works great - keep using it. The tab just needs to provide a button to trigger it.
- Consider requesting data when the tab becomes visible (not on connect), to avoid stale data.
