using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Balls
{
    public static bool currentBallsEnabled = false;

    [Serializable]
    public class BallsPayload
    {
        public bool enabled;

        public BallsPayload(bool e)
        {
            this.enabled = e;
        }
    }

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<BallsPayload>("balls",
            (_, p) => UpdateBallsToPayload(p));
    }

    public static void UpdateBallsToPayload(BallsPayload payload)
    {
        currentBallsEnabled = payload.enabled;
        List<Puck> pucks = PuckManager.Instance.GetPucks();

        foreach (Puck puck in pucks)
        {
            if (payload.enabled)
            {
                ApplyBallVisuals(puck);
            }
            else
            {
                RestorePuckVisuals(puck);
            }
        }
    }

    private static void ApplyBallVisuals(Puck puck)
    {
        if (puck == null) return;

        GameObject colliderObject = puck.gameObject;

        // Get the mesh renderer from main object or children
        MeshRenderer puckMeshRenderer =
            puck.gameObject.transform.Find("puck").Find("Puck").GetComponent<MeshRenderer>();
        Plugin.Log($"puckMeshRenderer {puckMeshRenderer.name}");
        Plugin.Log($"puckMeshRenderer GO name {puckMeshRenderer.gameObject.name}");
        Plugin.Log($"puckMeshRenderer GO parent name {puckMeshRenderer.transform.parent.gameObject.name}");

        if (puckMeshRenderer != null)
        {
            // Hide the original mesh
            puckMeshRenderer.enabled = false;
        
            // Get the original material to keep the texture
            Material originalMaterial = puckMeshRenderer.sharedMaterial;
            Plugin.Log($"originalMaterial: {originalMaterial}");
            Plugin.Log($"originalMaterial shader: {originalMaterial.shader.name}");
            
        
            // Create a simple sphere visual without colliders
            GameObject sphereVisual = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereVisual.name = "sphere";
        
            // Remove ALL colliders from the sphere visual to eliminate shadow
            // foreach (Collider col in sphereVisual.GetComponentsInChildren<Collider>())
            // {
            //     UnityEngine.Object.Destroy(col);
            // }
        
            // Parent it to the puck and position it correctly
            sphereVisual.transform.SetParent(colliderObject.transform, false);
            sphereVisual.transform.localPosition = Vector3.zero;
        
            // Apply the original material to the sphere
            Renderer sphereRenderer = sphereVisual.GetComponent<Renderer>();
            if (sphereRenderer && originalMaterial)
            {
                sphereRenderer.material = originalMaterial;
            }
        
            // Use local bounds so sizing is independent of puckScale.
            // The sphere is a child of the puck, so it inherits transform.localScale automatically.
            MeshFilter mf = puckMeshRenderer.GetComponent<MeshFilter>();
            Bounds localBounds = mf != null ? mf.sharedMesh.bounds : puckMeshRenderer.localBounds;
            float baseSize = localBounds.extents.magnitude * 0.8f;
            float diameter = baseSize * 2f;
            sphereVisual.transform.localScale = new Vector3(diameter, diameter, diameter);
        
            Plugin.Log($"Created ball visual with scale {diameter} and material {(originalMaterial != null ? originalMaterial.name : "null")}");
        
            Plugin.Log("Applied ball visuals to puck.");
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

        // Remove the sphere visual (child GameObject created from primitive)
        foreach (Transform child in colliderObject.transform)
        {
            if (child.gameObject.name == "sphere")
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        Plugin.Log("Restored puck from ball visuals.");
    }

    [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
    public static class BallsNetworkPostSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Puck __instance)
        {
            if (!MessagingHandler.connectedToToastersRink || !currentBallsEnabled) return;
            ApplyBallVisuals(__instance);
        }
    }
}
