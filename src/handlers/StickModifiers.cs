using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Client-side patches for stick modifiers (Free Blade, High Sticking).
/// Removes client-side clamping so the expanded server limits actually work.
/// Toggled via modifier state sync from the server.
/// </summary>
public static class StickModifiers
{
    public static bool freeBladeEnabled = false;
    public static bool highStickingEnabled = false;

    private static readonly AccessTools.FieldRef<PlayerInput, int> minBladeRef =
        AccessTools.FieldRefAccess<PlayerInput, int>("minimumBladeAngle");
    private static readonly AccessTools.FieldRef<PlayerInput, int> maxBladeRef =
        AccessTools.FieldRefAccess<PlayerInput, int>("maximumBladeAngle");

    private static readonly AccessTools.FieldRef<PlayerInput, Vector2> minStickAngleRef =
        AccessTools.FieldRefAccess<PlayerInput, Vector2>("minimumStickRaycastOriginAngle");
    private static readonly AccessTools.FieldRef<PlayerInput, Vector2> maxStickAngleRef =
        AccessTools.FieldRefAccess<PlayerInput, Vector2>("maximumStickRaycastOriginAngle");

    public static void SetFreeBlade(bool enabled)
    {
        freeBladeEnabled = enabled;
        ApplyToLocalPlayer();
    }

    public static void SetHighSticking(bool enabled)
    {
        highStickingEnabled = enabled;
        ApplyToLocalPlayer();
    }

    private static void ApplyToLocalPlayer()
    {
        // Apply to ALL players' PlayerInputs (not just local) because
        // Server_BladeAngleInputRpc runs on each remote player's PlayerInput
        // and clamps with their local limits.
        foreach (var player in PlayerManager.Instance.GetPlayers(true))
        {
            if (player?.PlayerInput != null)
                ApplyToPlayerInput(player.PlayerInput);
        }
    }

    public static void ApplyToPlayerInput(PlayerInput input)
    {
        if (input == null) return;

        // Free Blade
        minBladeRef(input) = freeBladeEnabled ? -127 : -4;
        maxBladeRef(input) = freeBladeEnabled ? 127 : 4;

        // High Sticking
        Vector2 min = minStickAngleRef(input);
        Vector2 max = maxStickAngleRef(input);
        min.x = highStickingEnabled ? -80f : -25f;
        max.x = highStickingEnabled ? 80f : 80f;
        minStickAngleRef(input) = min;
        maxStickAngleRef(input) = max;
    }

    /// <summary>
    /// Apply on character spawn so it works after respawns.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Server_SpawnCharacter))]
    public static class ApplyOnSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            if (__instance.PlayerInput != null)
                ApplyToPlayerInput(__instance.PlayerInput);
        }
    }

    /// <summary>
    /// Override the server blade angle RPC to skip clamping when Free Blade is active.
    /// Without this, remote players' blade angles get clamped to [-4,4] on the receiving client.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.Server_BladeAngleInputRpc))]
    public static class SkipBladeAngleClampPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerInput __instance, sbyte value)
        {
            if (!freeBladeEnabled) return;
            // The original method clamped to [min, max]. Override with the raw value.
            __instance.BladeAngleInput.ServerValue = value;
        }
    }

    private static readonly AccessTools.FieldRef<PlayerInput, float> bladeAngleBufferRef =
        AccessTools.FieldRefAccess<PlayerInput, float>("bladeAngleBuffer");

    /// <summary>
    /// Replace blade angle Up handler to wrap instead of clamp when Free Blade is active.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInput), "OnBladeAngleUpActionPerformed")]
    public static class WrapBladeAngleUpPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerInput __instance, UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!freeBladeEnabled) return true; // run original

            if (GlobalStateManager.UIState.IsMouseRequired) return false;
            if (!__instance.Player.Stick) return false;

            float buffer = bladeAngleBufferRef(__instance);
            buffer += context.ReadValue<float>();
            // Wrap around instead of clamp
            if (buffer > 127f) buffer -= 255f;
            if (buffer < -128f) buffer += 255f;
            bladeAngleBufferRef(__instance) = buffer;
            __instance.BladeAngleInput.ClientValue = (sbyte)buffer;
            return false; // skip original
        }
    }

    [HarmonyPatch(typeof(PlayerInput), "OnBladeAngleDownActionPerformed")]
    public static class WrapBladeAngleDownPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerInput __instance, UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!freeBladeEnabled) return true;

            if (GlobalStateManager.UIState.IsMouseRequired) return false;
            if (!__instance.Player.Stick) return false;

            float buffer = bladeAngleBufferRef(__instance);
            buffer -= context.ReadValue<float>();
            if (buffer > 127f) buffer -= 255f;
            if (buffer < -128f) buffer += 255f;
            bladeAngleBufferRef(__instance) = buffer;
            __instance.BladeAngleInput.ClientValue = (sbyte)buffer;
            return false;
        }
    }
}
