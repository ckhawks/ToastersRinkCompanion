using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Client-side patch for the LookUpTurn modifier.
/// Widens the torso/head backward bend clamp in PlayerMesh.LookAt()
/// so players visually bend way back when looking up.
/// Vanilla clamp is -11.25° — we push it to -60° for maximum comedy.
/// </summary>
public static class LookUpTurnVisuals
{
    // Vanilla: -11.25°, ours: -60° (exaggerated backward bend)
    public static float bendBackMin = -60f;

    private static readonly AccessTools.FieldRef<PlayerMesh, Transform> torsoBoneRef =
        AccessTools.FieldRefAccess<PlayerMesh, Transform>("torsoBone");

    private static readonly AccessTools.FieldRef<PlayerMesh, Transform> headBoneRef =
        AccessTools.FieldRefAccess<PlayerMesh, Transform>("headBone");

    private static readonly AccessTools.FieldRef<PlayerMesh, float> lookAtSpeedRef =
        AccessTools.FieldRefAccess<PlayerMesh, float>("lookAtSpeed");

    /// <summary>
    /// Replaces PlayerMesh.LookAt when enabled, using a wider backward bend clamp.
    /// When disabled, the original method runs unchanged.
    /// </summary>
    [HarmonyPatch(typeof(PlayerMesh), nameof(PlayerMesh.LookAt))]
    public static class LookAtBendPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerMesh __instance, Vector3 targetPosition, float deltaTime,
            bool rotateTorso, bool rotateHead)
        {
            if (!MessagingHandler.connectedToToastersRink) return true; // only on TR servers

            Transform torsoBone = torsoBoneRef(__instance);
            Transform headBone = headBoneRef(__instance);
            float lookAtSpeed = lookAtSpeedRef(__instance);

            Quaternion quaternion;
            if (rotateTorso && rotateHead)
            {
                quaternion = Utils.GetLocalLookRotation(torsoBone, targetPosition);
                quaternion = Quaternion.Slerp(Quaternion.identity, quaternion, 0.5f);
            }
            else if (rotateTorso)
            {
                quaternion = Utils.GetLocalLookRotation(torsoBone, targetPosition);
            }
            else if (rotateHead)
            {
                quaternion = Utils.GetLocalLookRotation(headBone, targetPosition);
            }
            else
            {
                return false;
            }

            Vector3 euler = Utils.WrapEulerAngles(quaternion.eulerAngles);
            // Vanilla clamp: X [-11.25, 45], Y [-45, 45], Z [0, 0]
            // Our clamp: widen X min to bendBackMin (-60°)
            euler = Utils.Vector3Clamp(euler, new Vector3(bendBackMin, -45f, 0f), new Vector3(45f, 45f, 0f));

            if (rotateTorso)
            {
                torsoBone.localRotation = Quaternion.Lerp(torsoBone.localRotation, Quaternion.Euler(euler),
                    lookAtSpeed * deltaTime);
            }

            if (rotateHead)
            {
                headBone.localRotation = Quaternion.Lerp(headBone.localRotation, Quaternion.Euler(euler),
                    lookAtSpeed * deltaTime);
            }

            return false; // skip original
        }
    }
}
