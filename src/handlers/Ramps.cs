using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Ramps
{
    private static List<GameObject> spawnedRamps = new List<GameObject>();
    private static GameObject rampPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateRampsToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (rampPrefab == null) LoadPrefab();

        ClearRamps();

        if (payload.enabled)
        {
            GameObject ramp1 = Object.Instantiate(rampPrefab);
            MeshRenderer[] renderers1 = ramp1.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            ramp1.transform.position = new Vector3(-16, 0, 0);
            ramp1.transform.rotation = Quaternion.Euler(-90, 90, 0);
            spawnedRamps.Add(ramp1);
            
            GameObject ramp2 = Object.Instantiate(rampPrefab);
            MeshRenderer[] renderers2 = ramp2.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers2)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            ramp2.transform.position = new Vector3(16, 0, 0);
            ramp2.transform.rotation = Quaternion.Euler(-90, 90, 0);
            spawnedRamps.Add(ramp2);
        }
    }
    
    public static void ClearRamps()
    {
        foreach (GameObject ramp in spawnedRamps)
        {
            Object.Destroy(ramp);
        }
        spawnedRamps.Clear();
    }
    
    private static void LoadPrefab()
    {
        if (rampPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/ramp");
        rampPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/ramp.prefab");
    }
}