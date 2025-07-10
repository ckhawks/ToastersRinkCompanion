using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ToastersRinkCompanion.collectibles;

public static class CollectiblePrefabs
{
    public static AssetBundle _loadedAssetBundle;
    
    // Prefabs
    public static GameObject billboardTextPrefab;
    public static GameObject casePrefab;
    public static GameObject particlesPrefab;
    public static GameObject holographicPrefab;
    // public static Material holographicMaterial;
    public static GameObject glisteningPrefab;
    // public static Material glisteningMaterial;
    public static GameObject flamingPrefab;
    public static GameObject smokingPrefab;
    
    private static Dictionary<string, GameObject> objectPrefabs = new Dictionary<string, GameObject>();
    private static Dictionary<string, Texture2D> patternTextures = new Dictionary<string, Texture2D>();
    private static Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();

    public static void Setup()
    {
        LoadCollectibleAssetBundle();
        InitializeTextPrefab();
        LoadCasePrefab();
        LoadCollectiblesParticlesPrefab();
        LoadParticlesPrefabs();
    }

    private static void LoadParticlesPrefabs()
    {
        holographicPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle,
            $"assets/collectibles/traitholographicrenderer.prefab");
        if (holographicPrefab == null)
        {
            Debug.LogError($"Failed to load holographicPrefab's prefab!");
        }
        glisteningPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle,
            $"assets/collectibles/traitsparklingparticles.prefab");
        if (glisteningPrefab == null)
        {
            Debug.LogError($"Failed to load glisteningPrefab's prefab!");
        }
        // glisteningMaterial = PrefabHelper.LoadMaterial(_loadedAssetBundle,
        //     $"assets/collectibles/traitsparklingmaterial.mat");
        // if (glisteningMaterial == null)
        // {
        //     Debug.LogError($"Failed to load glisteningMaterial's prefab!");
        // }
        flamingPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle,
            $"assets/collectibles/traitburningparticles.prefab");
        if (flamingPrefab == null)
        {
            Debug.LogError($"Failed to load flamingPrefab's prefab!");
        }
        smokingPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle,
            $"assets/collectibles/traitsmokingparticles.prefab");
        if (smokingPrefab == null)
        {
            Debug.LogError($"Failed to load smokingPrefab's prefab!");
        }
    }
    
    public static void InitializeTextPrefab()
    {
        if (billboardTextPrefab != null) return;
        if (_loadedAssetBundle == null) LoadCollectibleAssetBundle();
        billboardTextPrefab =
            PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/collectibles/collectiblenamecanvas.prefab");
        if (billboardTextPrefab == null)
        {
            Debug.LogError("Failed to load collectiblenamecanvas.prefab! Text will not be displayed.");
        }
        else
        {
            // The prefab should have ONE TMP_Text child configured.
            TMP_Text[] components = billboardTextPrefab.GetComponentsInChildren<TMP_Text>();
            if (components.Length != 1)
            {
                Debug.LogWarning(
                    $"Expected collectiblenamecanvas.prefab to have exactly one TMP_Text component, but found {components.Length}. Ensure correct prefab setup for text cloning.");
            }
        }
    }
    
    public static void LoadCasePrefab()
    {
        if (casePrefab != null) return;
        if (_loadedAssetBundle == null) LoadCollectibleAssetBundle();
        casePrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/collectibles/case.prefab");
        if (casePrefab == null)
        {
            Debug.LogError("Failed to load toaster.fbx prefab!");
        }
    }

    public static void LoadCollectiblesParticlesPrefab()
    {
        if (particlesPrefab != null) return;
        if (_loadedAssetBundle == null) LoadCollectibleAssetBundle();
        particlesPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/collectibles/collectibleparticles.prefab");
        if (casePrefab == null)
        {
            Debug.LogError("Failed to load toaster.fbx prefab!");
        }
    }

    private static void LoadCollectibleAssetBundle()
    {
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/collectibles");
    }

    public static GameObject LoadPrefab(string collectibleType)
    {
        if (objectPrefabs.TryGetValue(collectibleType, out GameObject prefab))
        {
            return prefab;
        }
        else
        {
            LoadCollectibleAssetBundle();
            GameObject prefab2 = PrefabHelper.LoadPrefab(_loadedAssetBundle,
                $"assets/collectibles/models/{CollectiblesConstants.ITEM_PATHS[collectibleType]}");
            if (prefab2 == null)
            {
                Debug.LogError($"Failed to load {collectibleType}'s prefab!");
            }

            objectPrefabs[collectibleType] = prefab2;
            return prefab2;
        }
    }

    public static Texture2D LoadTexture2D(string textureName)
    {
        if (patternTextures.TryGetValue(textureName, out Texture2D texture))
        {
            return texture;
        }
        else
        {
            LoadCollectibleAssetBundle();
            Texture2D texture2 = PrefabHelper.LoadTexture2D(_loadedAssetBundle,
                $"assets/collectibles/skintextures/{CollectiblesConstants.PATTERN_PATHS[textureName]}");
            if (texture2 == null)
            {
                Debug.LogError($"Failed to load {textureName}'s prefab!");
            }

            patternTextures[textureName] = texture2;
            return texture2;
        }
    }
    
    public static AudioClip LoadAudioClip(string audioFileName)
    {
        if (audioClips.TryGetValue(audioFileName, out AudioClip audioClip))
        {
            return audioClip;
        }
        else
        {
            LoadCollectibleAssetBundle();
            AudioClip audioClip2 = PrefabHelper.LoadAudioClip(_loadedAssetBundle,
                $"assets/collectibles/{audioFileName}");
            if (audioClip2 == null)
            {
                Debug.LogError($"Failed to load {audioFileName}'s prefab!");
            }

            audioClips.Add(audioFileName, audioClip2);
            return audioClip2;
        }
    }

}