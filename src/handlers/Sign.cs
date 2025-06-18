using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Sign
{
    private static GameObject spawnedSign;
    private static GameObject signPrefab;// cone prefab
    private static AssetBundle _loadedAssetBundle;
    private static string assetBundlePath = "assetbundles/sign"; // Adjust this
    
    public static void SpawnSign()
    {
        if (signPrefab == null) LoadSignPrefab();
        
        spawnedSign = Object.Instantiate(signPrefab);
        spawnedSign.transform.position = new Vector3(0, 9.2f, 0);
        spawnedSign.transform.localScale = new Vector3(60, 60, 60);
    }

    public static void DestroySign()
    {
        Object.Destroy(spawnedSign);
    }
    
    public static void LoadSignPrefab()
    {
        if (signPrefab != null) return;
        
        // TODO break out loading the asset bundle into generic
        // TODO break out loading type thingy from path in bundle into generic
        if (_loadedAssetBundle != null)
        {
            Debug.LogWarning("[MeshReplacer] AssetBundle already loaded.");
            return;
        }

        try
        {
            // You'll need to figure out the actual path to your asset bundle.
            // It could be alongside your DLL, or in a specific mod data folder.
            string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), assetBundlePath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MeshReplacer] AssetBundle not found at: {fullPath}");
                return;
            }

            _loadedAssetBundle = AssetBundle.LoadFromFile(fullPath);
            if (_loadedAssetBundle == null)
            {
                Debug.LogError("[MeshReplacer] Failed to load AssetBundle.");
            }
            else
            {
                Debug.Log("[MeshReplacer] AssetBundle loaded successfully.");
            }

            string assetPath = "assets/sign.prefab";
            GameObject customColliderMesh = _loadedAssetBundle.LoadAsset<GameObject>(assetPath);
            if (customColliderMesh == null)
            {
                Debug.LogError($"[MeshReplacer] Custom mesh '{assetPath}' not found in AssetBundle.");
                return;
            }

            signPrefab = customColliderMesh;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MeshReplacer] Error loading AssetBundle: {ex.Message}");
        }
    }
}