using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Balls
{
    public static bool currentBallsEnabled = false;

    // Store original mesh and material for restoration
    // private static Dictionary<Puck, (Mesh mesh, Material material)> originalMeshData =
    //     new Dictionary<Puck, (Mesh, Material)>();

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
        // MeshRenderer[] meshRenderers = colliderObject.GetComponents<MeshRenderer>();
        // Plugin.Log($"Found {meshRenderers.Length} mesh renderers");
        // for (int i = 0; i < meshRenderers.Length; i++)
        // {
        //     MeshRenderer meshRenderer = meshRenderers[i];
        //     Plugin.Log($"puck had {meshRenderer.name} {meshRenderer.material.name} {meshRenderer.material.shader.name} {i}");
        // }
        // if (puckMeshRenderer == null)
        // {
        //     meshRenderer = colliderObject.GetComponentInChildren<MeshRenderer>();
        // }
        
        if (puckMeshRenderer != null)
        {
            // Store original material if we haven't already
            // if (!originalMeshData.ContainsKey(puck))
            // {
            //     MeshFilter mf = puckMeshRenderer.GetComponent<MeshFilter>();
            //     if (mf == null) mf = puckMeshRenderer.GetComponentInChildren<MeshFilter>();
            //     Mesh originalMesh = mf != null ? mf.mesh : null;
            //     originalMeshData[puck] = (originalMesh, puckMeshRenderer.sharedMaterial);
            // }
        
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
        
            // Calculate proper scale - much smaller than before
            Bounds originalBounds = puckMeshRenderer.bounds;
            // float scaleFactor = PuckScale.currentPuckScale;
            // Use a more reasonable scale calculation
            float baseSize = originalBounds.extents.magnitude * 0.8f; // Half the original size
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
        if (puck == null 
            // || !originalMeshData.ContainsKey(puck)
            )
            return;

        MeshRenderer puckMeshRenderer =
            puck.gameObject.transform.Find("puck").Find("Puck").GetComponent<MeshRenderer>();
        puckMeshRenderer.enabled = true;
        
        GameObject colliderObject = puck.gameObject;
        //
        // // Re-enable the original mesh renderer
        // MeshRenderer meshRenderer = colliderObject.GetComponent<MeshRenderer>();
        // if (meshRenderer == null)
        // {
        //     meshRenderer = colliderObject.GetComponentInChildren<MeshRenderer>();
        // }
        //
        // if (meshRenderer != null)
        // {
        //     // meshRenderer.enabled = true;
        //     
        //
        //     // Restore original scale
        //     colliderObject.transform.localScale = Vector3.one;
        // }

        // Remove the sphere visual (child GameObject created from primitive)
        foreach (Transform child in colliderObject.transform)
        {
            if (child.gameObject.name == "sphere")
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        // originalMeshData.Remove(puck);
        Plugin.Log("Restored puck from ball visuals.");
    }

    // this was basically doing the same patch as below, just firing twice which was causing problems
    // [HarmonyPatch(typeof(PuckManager), nameof(PuckManager.AddPuck))]
    // public static class BallsAddPuckPatch
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(PuckManager __instance, Puck puck)
    //     {
    //         if (!MessagingHandler.connectedToToastersRink || !currentBallsEnabled) return;
    //         ApplyBallVisuals(puck);
    //     }
    // }

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

[Serializable]
public class BallsPayload
{
    public bool enabled;

    public BallsPayload(bool e)
    {
        this.enabled = e;
    }
}
