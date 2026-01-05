using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class PuckScale
{
    public static float currentPuckScale = 1f;
    
    public static void UpdatePuckScaleToPayload(PuckScalePayload payload)
    {
        currentPuckScale = payload.puckscale;
        List<Puck> pucks = PuckManager.Instance.GetPucks();
        foreach (Puck puck in pucks)
        {
            puck.gameObject.transform.localScale = new Vector3(payload.puckscale, payload.puckscale, payload.puckscale);
            MeshRenderer puckMeshRenderer = puck.GetComponent<MeshRenderer>();
            if (puckMeshRenderer != null && !Balls.currentBallsEnabled)
            {
                puckMeshRenderer.transform.localScale = new Vector3(payload.puckscale, payload.puckscale, payload.puckscale);;
            }
        }
    }
    
    [HarmonyPatch(typeof(PuckManager), nameof(PuckManager.AddPuck))]
    public static class PuckManagerAddPuckPatch
    {
        [HarmonyPostfix]
        public static void Prefix(PuckManager __instance, Puck puck)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            
            puck.gameObject.transform.localScale = new Vector3(currentPuckScale, currentPuckScale, currentPuckScale);
            MeshRenderer puckMeshRenderer = puck.GetComponent<MeshRenderer>();
            if (puckMeshRenderer != null && !Balls.currentBallsEnabled)
            {
                puckMeshRenderer.transform.localScale = new Vector3(currentPuckScale, currentPuckScale, currentPuckScale);
            }
        }
    }

    // Added this in addition to above to try to handle cases where it wasn't changing the scale for certain people
    [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
    public static class PuckOnNetworkPostSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Puck __instance)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            
            __instance.gameObject.transform.localScale = new Vector3(currentPuckScale, currentPuckScale, currentPuckScale);
            MeshRenderer puckMeshRenderer = __instance.GetComponent<MeshRenderer>();
            if (puckMeshRenderer != null && !Balls.currentBallsEnabled)
            {
                puckMeshRenderer.transform.localScale = new Vector3(currentPuckScale, currentPuckScale, currentPuckScale);
            }
        }
    }
}

[Serializable]
public class PuckScalePayload
{
    public float puckscale;

    public PuckScalePayload(float ps)
    {
        this.puckscale = ps;
    }
}