using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Sign
{
    private static GameObject spawnedSign;
    private static GameObject signPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void SpawnSign()
    {
        if (signPrefab == null) LoadPrefab();
        
        spawnedSign = Object.Instantiate(signPrefab);
        foreach(Material mat in spawnedSign.GetComponent<MeshRenderer>().sharedMaterials)
        {
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        spawnedSign.transform.position = new Vector3(0, 9.2f, 0);
        spawnedSign.transform.localScale = new Vector3(60, 60, 60);
    }

    public static void DestroySign()
    {
        Object.Destroy(spawnedSign);
    }
    
    private static void LoadPrefab()
    {
        if (signPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/sign");
        signPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/sign.prefab");
    }
}