using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToastersRinkCompanion;

public static class PrefabHelper
{
    public static AssetBundle LoadAssetBundle(string assetBundlePath)
    {
        try
        {
            // You'll need to figure out the actual path to your asset bundle.
            // It could be alongside your DLL, or in a specific mod data folder.
            string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                assetBundlePath);

            if (!File.Exists(fullPath))
            {
                Plugin.LogError($"[MeshReplacer] AssetBundle not found at: {fullPath}");
                return null;
            }

            AssetBundle loadedAssetBundle = AssetBundle.LoadFromFile(fullPath);
            if (loadedAssetBundle == null)
            {
                Plugin.LogError("[MeshReplacer] Failed to load AssetBundle.");
            }
            else
            {
                Plugin.Log("[MeshReplacer] AssetBundle loaded successfully.");
            }

            return loadedAssetBundle;
        }
        catch (System.Exception ex)
        {
            Plugin.LogError($"[MeshReplacer] Error loading AssetBundle: {ex.Message}");
            return null;
        }
    }
    
    public static GameObject LoadPrefab(AssetBundle assetBundle, string assetPath)
    {
        try
        {
            // string assetPath = "assets/teleporter.prefab";
            GameObject loadedObject = assetBundle.LoadAsset<GameObject>(assetPath);
            if (loadedObject == null)
            {
                Plugin.LogError($"[MeshReplacer] Custom mesh '{assetPath}' not found in AssetBundle.");
                return null;
            }

            return loadedObject;
        }

        catch (Exception ex)
        {
            Plugin.LogError($"[MeshReplacer] Error loading AssetBundle: {ex.Message}");
            return null;
        }
    }
    
    public static Texture2D LoadTexture2D(AssetBundle assetBundle, string assetPath)
    {
        try
        {
            // string assetPath = "assets/teleporter.prefab";
            Texture2D loadedObject = assetBundle.LoadAsset<Texture2D>(assetPath);
            if (loadedObject == null)
            {
                Plugin.LogError($"[MeshReplacer] Custom Texture2D '{assetPath}' not found in AssetBundle.");
                return null;
            }

            return loadedObject;
        }

        catch (Exception ex)
        {
            Plugin.LogError($"[MeshReplacer] Error loading AssetBundle: {ex.Message}");
            return null;
        }
    }
}