using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public class Tarps
{
    public static bool enabled = false;

    private static List<GameObject> spawnedTarps = new List<GameObject>();
    private static GameObject tarpPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateTarpsToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (tarpPrefab == null) LoadPrefab();

        ClearTarps();

        if (payload.enabled)
        {
            GameObject tarp1 = Object.Instantiate(tarpPrefab);
            MeshRenderer[] renderers1 = tarp1.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            tarp1.transform.position = new Vector3(0, 0, 0);
            tarp1.transform.rotation = Quaternion.Euler(-90, 0, 0);
            spawnedTarps.Add(tarp1);
            
            GameObject tarp2 = Object.Instantiate(tarpPrefab);
            MeshRenderer[] renderers2 = tarp2.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers2)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            tarp2.transform.position = new Vector3(0, 0, 0);
            tarp2.transform.rotation = Quaternion.Euler(-90, 180, 0);
            spawnedTarps.Add(tarp2);
        }
    }
    
    public static void ClearTarps()
    {
        foreach (GameObject ramp in spawnedTarps)
        {
            UnityEngine.Object.Destroy(ramp);
        }
        spawnedTarps.Clear();
    }
    
    private static void LoadPrefab()
    {
        if (tarpPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/goaltargettarp");
        tarpPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/goaltargettarp.prefab");
    }
}