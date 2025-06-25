using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public class GoalRamps
{
    private static List<GameObject> spawnedGoalRamps = new List<GameObject>();
    private static GameObject goalRampPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateRampsToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (goalRampPrefab == null) LoadPrefab();

        ClearRamps();

        if (payload.enabled)
        {
            GameObject ramp1 = Object.Instantiate(goalRampPrefab);
            MeshRenderer[] renderers1 = ramp1.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            ramp1.transform.position = new Vector3(0, 0, 0);
            ramp1.transform.rotation = Quaternion.Euler(0, 0, 0);
            spawnedGoalRamps.Add(ramp1);
            
            GameObject ramp2 = Object.Instantiate(goalRampPrefab);
            MeshRenderer[] renderers2 = ramp2.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers2)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            ramp2.transform.position = new Vector3(0, 0, 0);
            ramp2.transform.rotation = Quaternion.Euler(0, 180, 0);
            spawnedGoalRamps.Add(ramp2);
        }
    }
    
    public static void ClearRamps()
    {
        foreach (GameObject ramp in spawnedGoalRamps)
        {
            Object.Destroy(ramp);
        }
        spawnedGoalRamps.Clear();
    }
    
    private static void LoadPrefab()
    {
        if (goalRampPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/goalramp");
        goalRampPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/goalramp.prefab");
    }
}