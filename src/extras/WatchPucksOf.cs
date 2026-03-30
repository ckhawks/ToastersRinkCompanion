using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.extras;

public static class WatchPucksOf
{
    public static Player target = null;

    static readonly FieldInfo _minimumLookAngleField = typeof(PlayerInput)
        .GetField("minimumLookAngle", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    
    static readonly FieldInfo _maximumLookAngleField = typeof(PlayerInput)
        .GetField("maximumLookAngle", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    
    static readonly FieldInfo _initialLookAngleField = typeof(PlayerInput)
        .GetField("initialLookAngle", 
            BindingFlags.Instance | BindingFlags.NonPublic);
    
    [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.UpdateLookAngle))]
    public static class PlayerInputUpdateLookAnglePatch
    {
        
        // Most of the code is referenced from the base game's PlayerInput.UpdateLookAngle
        [HarmonyPrefix]
        public static bool Prefix(PlayerInput __instance, float deltaTime)
        {
            if (target == null) return true;
            
            Vector2 minimumLookAngle = (Vector2) _minimumLookAngleField.GetValue(__instance);
            Vector2 maximumLookAngle = (Vector2) _maximumLookAngleField.GetValue(__instance);
            Vector3 initialLookAngle = (Vector3) _initialLookAngleField.GetValue(__instance);
            
            // If they're pressing track input (left click), and not pressing right click
            if (__instance.TrackInput.ClientValue && !__instance.LookInput.ClientValue)
            {
                Puck puck = MonoBehaviourSingleton<PuckManager>.Instance.GetPlayerPuck(target.OwnerClientId); // the secret sauce
                if (!puck)
                {
                    puck = MonoBehaviourSingleton<PuckManager>.Instance.GetPuck(false);
                }
                PlayerCamera playerCamera = __instance.Player.PlayerCamera;
                PlayerBody playerBody = __instance.Player.PlayerBody;
                if (puck && playerCamera && playerBody)
                {
                    Quaternion quaternion = Quaternion.LookRotation(puck.transform.position - playerCamera.transform.position);
                    Vector3 vector = Utils.WrapEulerAngles((Quaternion.Inverse(playerBody.transform.rotation) * quaternion).eulerAngles);
                    vector = Utils.Vector2Clamp(vector, minimumLookAngle, maximumLookAngle);
                    __instance.LookAngleInput.ClientValue = Vector3.LerpUnclamped(__instance.LookAngleInput.ClientValue, vector, deltaTime * 10f);
                }

                return false;
            }
            
            // if right click
            if (__instance.LookInput.ClientValue)
            {
                Vector2 vector2 = InputManager.StickAction.ReadValue<Vector2>();
                Vector2 vector3 = new Vector2(-vector2.y * (SettingsManager.LookSensitivity / 2f), vector2.x * (SettingsManager.LookSensitivity / 2f));
                __instance.LookAngleInput.ClientValue = Utils.Vector2Clamp(__instance.LookAngleInput.ClientValue + vector3, minimumLookAngle, maximumLookAngle);
                return false;
            }
            // if not left click
            if (!__instance.TrackInput.ClientValue)
            {
                __instance.LookAngleInput.ClientValue = Vector3.Lerp(__instance.LookAngleInput.ClientValue, initialLookAngle, deltaTime * 10f);
            }

            return false;

            return true;
        }
    }
}

// public void UpdateLookAngle(float deltaTime)
// {
//     
//     // if left click and no right click
//     if (this.TrackInput.ClientValue && !this.LookInput.ClientValue)
//     {
//         Puck puck = MonoBehaviourSingleton<PuckManager>.Instance.GetPlayerPuck(base.OwnerClientId);
//         if (!puck)
//         {
//             puck = MonoBehaviourSingleton<PuckManager>.Instance.GetPuck(false);
//         }
//         PlayerCamera playerCamera = this.Player.PlayerCamera;
//         PlayerBody playerBody = this.Player.PlayerBody;
//         if (puck && playerCamera && playerBody)
//         {
//             Quaternion quaternion = Quaternion.LookRotation(puck.transform.position - playerCamera.transform.position);
//             Vector3 vector = Utils.WrapEulerAngles((Quaternion.Inverse(playerBody.transform.rotation) * quaternion).eulerAngles);
//             vector = Utils.Vector2Clamp(vector, this.minimumLookAngle, this.maximumLookAngle);
//             this.LookAngleInput.ClientValue = Vector3.LerpUnclamped(this.LookAngleInput.ClientValue, vector, deltaTime * 10f);
//         }
//     }
//     // if right click
//     if (this.LookInput.ClientValue)
//     {
//         Vector2 vector2 = InputManager.StickAction.ReadValue<Vector2>();
//         Vector2 vector3 = new Vector2(-vector2.y * (SettingsManager.LookSensitivity / 2f), vector2.x * (SettingsManager.LookSensitivity / 2f));
//         this.LookAngleInput.ClientValue = Utils.Vector2Clamp(this.LookAngleInput.ClientValue + vector3, this.minimumLookAngle, this.maximumLookAngle);
//         return;
//     }
//     // if not left click
//     if (!this.TrackInput.ClientValue)
//     {
//         this.LookAngleInput.ClientValue = Vector3.Lerp(this.LookAngleInput.ClientValue, this.initialLookAngle, deltaTime * 10f);
//     }
// }