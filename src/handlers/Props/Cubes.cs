using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Cubes
{
    public static bool currentCubesEnabled = false;

    [Serializable]
    public class CubesPayload
    {
        public bool enabled;

        public CubesPayload(bool e)
        {
            this.enabled = e;
        }
    }

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<CubesPayload>("cubes",
            (_, p) => UpdateCubesToPayload(p));
    }

    public static void UpdateCubesToPayload(CubesPayload payload)
    {
        currentCubesEnabled = payload.enabled;
        List<Puck> pucks = PuckManager.Instance.GetPucks();

        foreach (Puck puck in pucks)
        {
            if (payload.enabled)
            {
                ApplyCubeVisuals(puck);
            }
            else
            {
                RestorePuckVisuals(puck);
            }
        }
    }

    private static void ApplyCubeVisuals(Puck puck)
    {
        if (puck == null) return;

        GameObject colliderObject = puck.gameObject;

        MeshRenderer puckMeshRenderer = PuckVisuals.FindPuckMeshRenderer(puck);

        if (puckMeshRenderer != null)
        {
            // Hide the original mesh
            puckMeshRenderer.enabled = false;

            // Get the original material to keep the texture
            Material originalMaterial = puckMeshRenderer.sharedMaterial;

            // Create a cube visual
            GameObject cubeVisual = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeVisual.name = PuckVisuals.CubeVisualName;

            // CreatePrimitive ships a BoxCollider; strip it so the cosmetic cube
            // doesn't add a second collider to the live puck's physics.
            foreach (Collider col in cubeVisual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.Destroy(col);
            }

            // Parent it to the puck and centre it on the body mesh
            cubeVisual.transform.SetParent(colliderObject.transform, false);
            cubeVisual.transform.localPosition = PuckVisuals.GetBodyCenterInRootSpace(puck, puckMeshRenderer);

            // Apply the original material to the cube
            Renderer cubeRenderer = cubeVisual.GetComponent<Renderer>();
            if (cubeRenderer && originalMaterial)
            {
                cubeRenderer.material = originalMaterial;
            }

            // Size from the body mesh in root space, so this is independent of
            // puckScale — the cube inherits the root's localScale as a child.
            Vector3 size = PuckVisuals.GetBodySizeInRootSpace(puck, puckMeshRenderer) * 1.3f;
            float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            cubeVisual.transform.localScale = new Vector3(maxDim, maxDim, maxDim);

            Plugin.Log($"Applied cube visuals to puck (mesh '{puckMeshRenderer.gameObject.name}', scale {maxDim}).");
        }
        else
        {
            Plugin.LogError("Could not find MeshRenderer on puck");
        }
    }

    private static void RestorePuckVisuals(Puck puck)
    {
        if (puck == null)
            return;

        MeshRenderer puckMeshRenderer = PuckVisuals.FindPuckMeshRenderer(puck);
        if (puckMeshRenderer != null)
            puckMeshRenderer.enabled = true;

        GameObject colliderObject = puck.gameObject;

        // Remove the cube visual
        foreach (Transform child in colliderObject.transform)
        {
            if (child.gameObject.name == PuckVisuals.CubeVisualName)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        Plugin.Log("Restored puck from cube visuals.");
    }

    [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
    public static class CubesNetworkPostSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Puck __instance)
        {
            if (!MessagingHandler.connectedToToastersRink || !currentCubesEnabled) return;
            ApplyCubeVisuals(__instance);
        }
    }
}
