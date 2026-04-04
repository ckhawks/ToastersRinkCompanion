# Task: Press-to-Set Keybinding System

## Goal

Replace the current F-key-only dropdown keybind selectors with a "press any key to bind" system, similar to how vanilla game settings controls work. The user clicks a keybind button, it enters a listening state ("Press a key..."), and the next key pressed becomes the new binding.

## Current System

### How keybinds are stored (`src/ModSettings.cs`)

```csharp
public string spawnPuckKeybind { get; set; } = "<keyboard>/g";     // InputSystem path
public string voteYesKeybind { get; set; } = "f1";                 // Simple key name
public string voteNoKeybind { get; set; } = "f2";
public string panelKeybind { get; set; } = "f3";
```

Two different formats:
- `spawnPuckKeybind` uses Unity InputSystem binding path syntax (`<keyboard>/g`)
- The other three use simple lowercase key names (`f1`, `f2`, `f3`)

Settings are persisted to JSON via `System.Text.Json` in `config/ToastersRinkCompanion.json`.

### How keybinds are checked (`src/handlers/SpawnPuckKeybind.cs`)

The `SpawnPuckKeybind.PlayerInputUpdatePatch` Harmony postfix runs every frame:

- **Spawn puck**: Uses `Plugin.spawnPuckAction` (an `InputAction` created from the binding path). Checked via `spawnPuckAction.WasPressedThisFrame()`.
- **Vote yes/no, panel toggle**: Uses a manual `IsKeyPressed(string keyName)` method with a switch statement that only handles F1-F12:

```csharp
private static bool IsKeyPressed(string keyName)
{
    if (Keyboard.current == null || string.IsNullOrEmpty(keyName)) return false;
    return keyName.ToLower() switch
    {
        "f1" => Keyboard.current.f1Key.wasPressedThisFrame,
        "f2" => Keyboard.current.f2Key.wasPressedThisFrame,
        // ... only F1-F12
        _ => false,
    };
}
```

### How the settings UI works (`src/modifiers/SettingsTab.cs`)

- `BuildKeybindRow()` creates a `PopupField<string>` dropdown limited to F1-F12.
- `BuildTextSettingRow()` is used for `spawnPuckKeybind` - a free-text `TextField` where users type InputSystem paths manually.

## Implementation Plan

### 1. Unify keybind format

Standardize all keybinds to use Unity's `InputAction` binding path format (`<keyboard>/g`, `<keyboard>/f1`, `<keyboard>/leftShift`, etc.). This is already used for `spawnPuckKeybind` and supports every key.

**Migration**: When loading settings, convert old-style values:
- `"f1"` -> `"<keyboard>/f1"`
- `"f2"` -> `"<keyboard>/f2"`
- etc.

Or keep the simple format and convert in `IsKeyPressed`. Either way works, but InputAction paths are more future-proof.

### 2. Replace `IsKeyPressed` with `InputAction` (`SpawnPuckKeybind.cs`)

Instead of a switch statement, create `InputAction` instances for each keybind (like `spawnPuckAction`):

```csharp
// In Plugin.cs or SpawnPuckKeybind.cs
public static InputAction voteYesAction;
public static InputAction voteNoAction;
public static InputAction panelAction;
```

Initialize them from settings, check with `.WasPressedThisFrame()`. When a keybind changes, dispose the old action and create a new one (same pattern as `spawnPuckKeybind` in `SettingsTab.cs` lines 50-54).

### 3. Build "press to bind" UI widget (`SettingsTab.cs`)

Replace `BuildKeybindRow`'s `PopupField` dropdown and `BuildTextSettingRow`'s `TextField` with a clickable button:

**Normal state**: Button shows current key name (e.g. "G", "F1", "Left Shift")
**Listening state**: Button text changes to "Press a key...", background highlights

**Implementation approach**:

```csharp
private static Button _listeningButton = null;  // Track which button is listening
private static System.Action<string> _listeningCallback = null;

private static void BuildKeybindRow(VisualElement parent, string label, string currentValue,
    System.Action<string> onChanged)
{
    // ... row setup same as before ...
    
    var bindButton = new Button();
    bindButton.text = GetKeyDisplayName(currentValue);
    // ... style it ...
    
    bindButton.RegisterCallback<ClickEvent>(evt => {
        // Enter listening mode
        _listeningButton = bindButton;
        _listeningCallback = onChanged;
        bindButton.text = "Press a key...";
        bindButton.style.backgroundColor = /* highlight color */;
    });
    
    row.Add(bindButton);
}
```

**Capturing the key press**: Register a `KeyDownEvent` callback on the panel root or use `Keyboard.current.anyKey` polling:

Option A (UIElements event - preferred):
```csharp
// In ModifierPanelUI or SettingsTab setup:
_overlay.RegisterCallback<KeyDownEvent>(evt => {
    if (_listeningButton == null) return;
    
    var keyCode = evt.keyCode;
    string bindingPath = KeyCodeToInputPath(keyCode); // e.g. KeyCode.G -> "<keyboard>/g"
    
    _listeningCallback?.Invoke(bindingPath);
    _listeningButton.text = GetKeyDisplayName(bindingPath);
    _listeningButton.style.backgroundColor = /* normal color */;
    _listeningButton = null;
    _listeningCallback = null;
    
    evt.StopPropagation(); // Don't let the key do anything else
});
```

Option B (InputSystem polling in Update):
```csharp
// In SpawnPuckKeybind's Postfix, when listening mode is active:
if (Keyboard.current.anyKey.wasPressedThisFrame)
{
    foreach (var key in Keyboard.current.allKeys)
    {
        if (key.wasPressedThisFrame)
        {
            string path = key.path; // e.g. "/Keyboard/g"
            // Convert to binding format and apply
            break;
        }
    }
}
```

**Escape to cancel**: If ESC is pressed during listening, cancel without changing the bind.

### 4. Key display name mapping

Create a helper to convert InputSystem paths to readable names:
- `<keyboard>/g` -> "G"
- `<keyboard>/f1` -> "F1"  
- `<keyboard>/leftShift` -> "Left Shift"
- `<keyboard>/numpad0` -> "Numpad 0"

Unity's `InputControlPath.ToHumanReadableString()` might help, or build a simple mapping.

### 5. Conflict detection (optional but nice)

When a key is pressed:
- Check if it's already bound to another action
- If so, show a warning: "Already bound to [Vote Yes]. Override?" or swap the binds

## Key Files to Modify

- **`src/modifiers/SettingsTab.cs`** - Main UI changes. Replace dropdown/text field with press-to-bind button widget.
- **`src/handlers/SpawnPuckKeybind.cs`** - Replace `IsKeyPressed` switch with `InputAction`-based checking (or expand the switch to handle all keys).
- **`src/ModSettings.cs`** - Optionally migrate keybind format. Add migration logic in `Load()`.
- **`src/Plugin.cs`** - If creating additional `InputAction` fields for vote/panel keybinds.

## Key Files to Study

- **`src/modifiers/SettingsTab.cs`** - Current keybind UI (lines 131-162 for dropdown, 164-193 for text field)
- **`src/handlers/SpawnPuckKeybind.cs`** - Current key checking logic (lines 13-31 for IsKeyPressed, 39-111 for the Harmony patch)
- **`src/modifiers/ModifierPanelUI.cs`** - Panel overlay where you'd register the KeyDown listener
- **`src/Plugin.cs`** - Where `spawnPuckAction` is created (search for `spawnPuckAction`)

## Notes

- The panel already suppresses game input when open (`SetGameInputSuppressed`), so capturing keys in the settings tab shouldn't conflict with gameplay.
- Be careful with ESC - it's used to close the panel. When in listening mode, ESC should cancel the listen, not close the panel.
- Mouse buttons could also be supported in the future but keyboard-only is fine for now.
- The `spawnPuckKeybind` already uses `InputAction` and supports all keys - the goal is to bring the other three keybinds to the same level and make ALL of them configurable via the press-to-set UI.
