using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class BigWalls
{
    private static List<GameObject> spawnedBigWalls = new List<GameObject>();
    private static GameObject bigwallPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateWallsToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (bigwallPrefab == null) LoadPrefab();

        ClearWalls();

        if (payload.enabled)
        {
            GameObject bigwall1 = Object.Instantiate(bigwallPrefab);
            MeshRenderer[] renderers1 = bigwall1.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            bigwall1.transform.position = new Vector3(0, 0, 0);
            bigwall1.transform.rotation = Quaternion.Euler(0, 0, 0);
            spawnedBigWalls.Add(bigwall1);
            
            GameObject bigwall2 = Object.Instantiate(bigwallPrefab);
            MeshRenderer[] renderers2 = bigwall2.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers2)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            bigwall2.transform.position = new Vector3(0, 0, 0);
            bigwall2.transform.rotation = Quaternion.Euler(0, 180, 0);
            spawnedBigWalls.Add(bigwall2);
        }
    }
    
    public static void ClearWalls()
    {
        foreach (GameObject bigwall in spawnedBigWalls)
        {
            Object.Destroy(bigwall);
        }
        spawnedBigWalls.Clear();
    }
    
    private static void LoadPrefab()
    {
        if (bigwallPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/bigwall");
        bigwallPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/bigwall.prefab");
    }
}