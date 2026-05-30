using System;
using UnityEngine;

namespace ToastersRinkCompanion.collectibles;

/// <summary>
/// Centralized case-opening: a single shared cooldown plus warmup/ownership
/// checks, and the "open my previous case" hotkey logic. Both the Collectibles
/// tab Open buttons and the open-case keybind funnel through here so they share
/// one cooldown instead of tracking it independently.
/// </summary>
public static class CaseOpening
{
    /// <summary>Server-side case-open cooldown (seconds). Mirrors the TRS limit.</summary>
    public const float CooldownSeconds = 8.5f;

    private static float _lastOpenTime;

    public static float CooldownRemaining
        => _lastOpenTime > 0 ? CooldownSeconds - (Time.time - _lastOpenTime) : 0f;

    public static bool OnCooldown => CooldownRemaining > 0f;

    /// <summary>Series shorthand of the most recently opened case (persisted in settings).</summary>
    public static string LastOpenedShorthand => Plugin.modSettings?.lastOpenedCaseShorthand;

    /// <summary>
    /// Attempt to open a case by series shorthand. Performs client-side warmup,
    /// cooldown and (when the case list is loaded) ownership checks. On success,
    /// starts the cooldown, remembers the series as the "previous" case, and sends
    /// the open request to the server. Returns false with a reason in <paramref name="error"/>.
    /// </summary>
    public static bool TryOpen(string shorthand, out string error)
    {
        error = null;

        if (string.IsNullOrEmpty(shorthand))
        {
            error = "No case selected.";
            return false;
        }
        if (!modifiers.ServerState.IsWarmup)
        {
            error = "Cases can only be opened during warmup.";
            return false;
        }
        if (OnCooldown)
        {
            error = $"Case cooldown: {CooldownRemaining:F0}s remaining.";
            return false;
        }

        // If we know the player's cases, make sure they actually own this one.
        // When the list isn't loaded yet (hotkey before opening the panel), let
        // the server be the authority and validate on its end.
        if (CollectiblesStore.IsCasesLoaded)
        {
            var entry = FindCase(shorthand);
            if (entry == null || entry.Quantity <= 0)
            {
                error = "You don't have any of that case.";
                return false;
            }
        }

        _lastOpenTime = Time.time;
        if (Plugin.modSettings != null)
        {
            Plugin.modSettings.lastOpenedCaseShorthand = shorthand;
            Plugin.modSettings.Save();
        }
        CollectiblesMessaging.OpenCase(shorthand);
        return true;
    }

    /// <summary>
    /// Open the player's previously opened case (falling back to the first case
    /// they own). Surfaces the outcome as a local chat message, since this is
    /// normally triggered by a hotkey with the panel closed.
    /// </summary>
    public static void OpenPreviousFromHotkey()
    {
        // We need the case list to resolve names and to fall back to a first
        // owned case. Request it if we don't have it yet.
        if (!CollectiblesStore.IsCasesLoaded)
        {
            CollectiblesMessaging.RequestCases();

            // No remembered series and no list — ask the user to retry once it loads.
            if (string.IsNullOrEmpty(LastOpenedShorthand))
            {
                Plugin.AddLocalChatMessage("<color=#ffcc00>Loading your cases — press again in a moment.</color>");
                return;
            }
        }

        string shorthand = ResolveTarget();
        if (string.IsNullOrEmpty(shorthand))
        {
            Plugin.AddLocalChatMessage("<color=#ff6666>You have no cases to open.</color>");
            return;
        }

        if (TryOpen(shorthand, out string error))
        {
            string name = FindCase(shorthand)?.SeriesName;
            string label = string.IsNullOrEmpty(name) ? "case" : $"{name} Case";
            Plugin.AddLocalChatMessage($"<color=#66ccff>Opening {label}...</color>");
        }
        else
        {
            Plugin.AddLocalChatMessage($"<color=#ff6666>{error}</color>");
        }
    }

    /// <summary>The remembered series if still owned, otherwise the first owned case.</summary>
    private static string ResolveTarget()
    {
        string remembered = LastOpenedShorthand;
        if (!string.IsNullOrEmpty(remembered))
        {
            if (!CollectiblesStore.IsCasesLoaded) return remembered; // server will validate
            var entry = FindCase(remembered);
            if (entry != null && entry.Quantity > 0) return remembered;
        }

        // Fall back to the first case the player owns.
        foreach (var c in CollectiblesStore.Cases)
            if (c.Quantity > 0) return c.Shorthand;

        return null;
    }

    private static PlayerCaseEntry FindCase(string shorthand)
    {
        foreach (var c in CollectiblesStore.Cases)
            if (string.Equals(c.Shorthand, shorthand, StringComparison.OrdinalIgnoreCase))
                return c;
        return null;
    }
}
