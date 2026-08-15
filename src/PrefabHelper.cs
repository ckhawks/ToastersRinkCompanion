using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToastersRinkCompanion;

public static class PrefabHelper
{
    /// <summary>
    /// True once any asset bundle file turned out not to exist on disk. That is an
    /// install problem specifically — the DLL was placed by hand without the
    /// assetbundles folder beside it — and is worth telling the player about, because
    /// every bundle-backed feature silently does nothing and the game looks broken
    /// rather than misinstalled.
    ///
    /// Deliberately separate from a bundle that exists but fails to load: that is a
    /// different fault (usually a second copy of Companion already holding the file
    /// open) with different advice.
    /// </summary>
    public static bool AnyBundleMissing { get; private set; }

    /// <summary>Directory the first missing bundle was expected in, for the message.</summary>
    public static string ExpectedBundleDirectory { get; private set; }

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
                AnyBundleMissing = true;
                ExpectedBundleDirectory ??= Path.GetDirectoryName(fullPath);
                return null;
            }

            AssetBundle loadedAssetBundle = AssetBundle.LoadFromFile(fullPath);
            if (loadedAssetBundle == null)
            {
                Plugin.LogError("[MeshReplacer] Failed to load AssetBundle.");
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
    
    public static AudioClip LoadAudioClip(AssetBundle assetBundle, string assetPath)
    {
        try
        {
            AudioClip loadedObject = assetBundle.LoadAsset<AudioClip>(assetPath);
            if (loadedObject == null)
            {
                Plugin.LogError($"[MeshReplacer] Custom AudioClip '{assetPath}' not found in AssetBundle.");
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
    
    public static Material LoadMaterial(AssetBundle assetBundle, string assetPath)
    {
        try
        {
            Material loadedObject = assetBundle.LoadAsset<Material>(assetPath);
            if (loadedObject == null)
            {
                Plugin.LogError($"[MeshReplacer] Custom Material '{assetPath}' not found in AssetBundle.");
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