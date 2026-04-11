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

        // Get the mesh renderer from main object or children
        MeshRenderer puckMeshRenderer =
            puck.gameObject.transform.Find("puck").Find("Puck").GetComponent<MeshRenderer>();

        if (puckMeshRenderer != null)
        {
            // Hide the original mesh
            puckMeshRenderer.enabled = false;

            // Get the original material to keep the texture
            Material originalMaterial = puckMeshRenderer.sharedMaterial;

            // Create a cube visual
            GameObject cubeVisual = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeVisual.name = "cube";

            // Parent it to the puck and position it correctly
            cubeVisual.transform.SetParent(colliderObject.transform, false);
            cubeVisual.transform.localPosition = Vector3.zero;

            // Apply the original material to the cube
            Renderer cubeRenderer = cubeVisual.GetComponent<Renderer>();
            if (cubeRenderer && originalMaterial)
            {
                cubeRenderer.material = originalMaterial;
            }

            // Use local bounds so sizing is independent of puckScale.
            // The cube is a child of the puck, so it inherits transform.localScale automatically.
            MeshFilter mf = puckMeshRenderer.GetComponent<MeshFilter>();
            Bounds localBounds = mf != null ? mf.sharedMesh.bounds : puckMeshRenderer.localBounds;
            Vector3 size = localBounds.size * 1.3f;
            float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            cubeVisual.transform.localScale = new Vector3(maxDim, maxDim, maxDim);

            Plugin.Log($"Created cube visual with scale {maxDim} and material {(originalMaterial != null ? originalMaterial.name : "null")}");
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

        MeshRenderer puckMeshRenderer =
            puck.gameObject.transform.Find("puck").Find("Puck").GetComponent<MeshRenderer>();
        puckMeshRenderer.enabled = true;

        GameObject colliderObject = puck.gameObject;

        // Remove the cube visual
        foreach (Transform child in colliderObject.transform)
        {
            if (child.gameObject.name == "cube")
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
