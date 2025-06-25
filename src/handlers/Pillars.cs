using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Pillars
{
    private static GameObject spawnedPillar;
    private static GameObject pillarPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdatePillarsToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (pillarPrefab == null) LoadPrefab();

        if (payload.enabled)
        {
            spawnedPillar = Object.Instantiate(pillarPrefab);
            MeshRenderer[] renderers1 = spawnedPillar.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            spawnedPillar.transform.position = new Vector3(0, 0, 0);
            spawnedPillar.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else
        {
            ClearPillars();
        }
    }

    private static void ClearPillars()
    {
        Object.Destroy(spawnedPillar);
    }
    
    private static void LoadPrefab()
    {
        if (pillarPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/pillars");
        pillarPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/pillars.prefab");
    }
}