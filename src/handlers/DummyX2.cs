using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public class DummyX2
{
    public static bool enabled = false;

    private static List<GameObject> spawnedDummies = new List<GameObject>();
    private static GameObject dummyX2Prefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateDummyX2ToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (dummyX2Prefab == null) LoadPrefab();

        ClearDummyX2();

        if (payload.enabled)
        {
            GameObject dummyx21 = Object.Instantiate(dummyX2Prefab);
            MeshRenderer[] renderers1 = dummyx21.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            dummyx21.transform.position = new Vector3(0, 0, 0);
            dummyx21.transform.rotation = Quaternion.Euler(0, 0, 0);
            spawnedDummies.Add(dummyx21);
            
            GameObject dummyx22 = Object.Instantiate(dummyX2Prefab);
            MeshRenderer[] renderers2 = dummyx22.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers2)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            dummyx22.transform.position = new Vector3(0, 0, 0);
            dummyx22.transform.rotation = Quaternion.Euler(0, 180, 0);
            spawnedDummies.Add(dummyx22);
        }
    }
    
    public static void ClearDummyX2()
    {
        foreach (GameObject ramp in spawnedDummies)
        {
            UnityEngine.Object.Destroy(ramp);
        }
        spawnedDummies.Clear();
    }
    
    private static void LoadPrefab()
    {
        if (dummyX2Prefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/dummiesx2");
        dummyX2Prefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/goaliedummyx2.prefab");
    }
}