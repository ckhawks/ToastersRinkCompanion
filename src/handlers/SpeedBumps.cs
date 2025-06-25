using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class SpeedBumps
{
    private static List<GameObject> spawnedSpeedBumps = new List<GameObject>();
    private static GameObject speedBumpPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateSpeedBumpsToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (speedBumpPrefab == null) LoadPrefab();

        ClearRamps();

        if (payload.enabled)
        {
            GameObject speedbump1 = Object.Instantiate(speedBumpPrefab);
            MeshRenderer[] renderers1 = speedbump1.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            speedbump1.transform.position = new Vector3(0, 0, 0);
            speedbump1.transform.rotation = Quaternion.Euler(-90, 0, 0);
            spawnedSpeedBumps.Add(speedbump1);
            
            GameObject speedbump2 = Object.Instantiate(speedBumpPrefab);
            MeshRenderer[] renderers2 = speedbump2.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers2)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            speedbump2.transform.position = new Vector3(0, 0, 0);
            speedbump2.transform.rotation = Quaternion.Euler(-90, 180, 0);
            spawnedSpeedBumps.Add(speedbump2);
        }
    }
    
    public static void ClearRamps()
    {
        foreach (GameObject ramp in spawnedSpeedBumps)
        {
            Object.Destroy(ramp);
        }
        spawnedSpeedBumps.Clear();
    }
    
    private static void LoadPrefab()
    {
        if (speedBumpPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/speedbump");
        speedBumpPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/speedbump.prefab");
    }
}