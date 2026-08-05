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

        MeshRenderer puckMeshRenderer = PuckVisuals.FindPuckMeshRenderer(puck);

        if (puckMeshRenderer != null)
        {
            // Hide the original mesh
            puckMeshRenderer.enabled = false;

            // Get the original material to keep the texture
            Material originalMaterial = puckMeshRenderer.sharedMaterial;

            // Create a simple sphere visual without colliders
            GameObject sphereVisual = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereVisual.name = PuckVisuals.BallVisualName;

            // CreatePrimitive ships a SphereCollider; strip it so the cosmetic
            // sphere doesn't add a second collider to the live puck's physics.
            foreach (Collider col in sphereVisual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.Destroy(col);
            }

            // Parent it to the puck and centre it on the body mesh
            sphereVisual.transform.SetParent(colliderObject.transform, false);
            sphereVisual.transform.localPosition = PuckVisuals.GetBodyCenterInRootSpace(puck, puckMeshRenderer);

            // Apply the original material to the sphere
            Renderer sphereRenderer = sphereVisual.GetComponent<Renderer>();
            if (sphereRenderer && originalMaterial)
            {
                sphereRenderer.material = originalMaterial;
            }

            // Size from the body mesh in root space, so this is independent of
            // puckScale — the sphere inherits the root's localScale as a child.
            Vector3 bodySize = PuckVisuals.GetBodySizeInRootSpace(puck, puckMeshRenderer);
            float diameter = (bodySize.magnitude * 0.5f) * 0.8f * 2f;
            sphereVisual.transform.localScale = new Vector3(diameter, diameter, diameter);

            Plugin.Log($"Applied ball visuals to puck (mesh '{puckMeshRenderer.gameObject.name}', diameter {diameter}).");
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

        // Remove the sphere visual (child GameObject created from primitive)
        foreach (Transform child in colliderObject.transform)
        {
            if (child.gameObject.name == PuckVisuals.BallVisualName)
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
