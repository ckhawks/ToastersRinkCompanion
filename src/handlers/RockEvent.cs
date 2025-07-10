using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ToastersRinkCompanion.handlers;

public static class RockEvent
{
    // configuration
    private static float rockRiseSpeedMultiplier = 0.6f;
    
    // state
    private static AssetBundle _loadedAssetBundle;
    private static List<GameObject> rockPrefabs = new List<GameObject>();
    private static GameObject spawnedRock;
    private static List<Texture2D> rockTextures = new List<Texture2D>();
    private static GameObject hitSparksParticlePrefab;
    private static AudioClip rockSpawnAudioClip;
    private static AudioClip rockHitAudioClip;
    private static AudioClip rockDeathAudioClip;
    private static List<Material> copiedMaterials = new List<Material>();
    
    public static void SpawnRockForPayload(RockEventPayload payload)
    {
        LoadPrefabs();
        spawnedRock = Object.Instantiate(rockPrefabs[payload.RockShape - 1], payload.Position, Quaternion.Euler(payload.Rotation));
        MeshRenderer[] renderers = spawnedRock.GetComponentsInChildren<MeshRenderer>();
        Texture2D texture = rockTextures[payload.RockType - 1];
        copiedMaterials = new List<Material>(); // To store materials for later cleanup
        
        foreach (MeshRenderer renderer in renderers)
        {
            Material[] currentMaterials = renderer.materials;
            foreach (var mat in currentMaterials)
            {
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");

                if (texture != null)
                {
                    mat.SetTexture("_BaseMap", texture);
                    mat.SetColor("_BaseColor", Color.white);
                }
                copiedMaterials.Add(mat);
            }

            renderer.materials = currentMaterials;
        }
        
        if (!payload.AlreadyBegun)
        {
            AudioSource audioSourceBase = spawnedRock.AddComponent<AudioSource>();
            audioSourceBase.clip = rockSpawnAudioClip;
            audioSourceBase.loop = false;
            audioSourceBase.playOnAwake = false;
            audioSourceBase.reverbZoneMix = 0.1f;
            audioSourceBase.maxDistance = 20f;
            audioSourceBase.volume = 1f;
            audioSourceBase.spatialBlend = 0.8f;
            audioSourceBase.Play();
        }
        
        RockEventUI.ShowOrUpdateUI(payload.RockMaxHealth, payload.RockCurrentHealth, payload.AlreadyBegun);
        RockEventUI.SetBossName(payload.RockName);
    }
    
    private static void LoadPrefabs()
    {
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/rocks");
        if (hitSparksParticlePrefab == null)
            hitSparksParticlePrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/miningsparksfx.prefab");
        if (rockSpawnAudioClip == null)
            rockSpawnAudioClip = PrefabHelper.LoadAudioClip(_loadedAssetBundle, "assets/toaster's rink/ore_rock.mp3");
        if (rockHitAudioClip == null)
            rockHitAudioClip = PrefabHelper.LoadAudioClip(_loadedAssetBundle, "assets/toaster's rink/metal_pipe_1.wav");
        if (rockDeathAudioClip == null)
            rockDeathAudioClip = PrefabHelper.LoadAudioClip(_loadedAssetBundle, "assets/toaster's rink/rockshatter.wav");
        
        if (rockPrefabs.Count >= 3) return; // Don't reload it
        rockPrefabs.Add(PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/rock1.prefab"));
        rockPrefabs.Add(PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/rock2.prefab"));
        rockPrefabs.Add(PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/rock3.prefab"));

        if (rockTextures.Count >= 3) return;
        rockTextures.Add(PrefabHelper.LoadTexture2D(_loadedAssetBundle, "assets/toaster's rink/ore_iron.png"));
        rockTextures.Add(PrefabHelper.LoadTexture2D(_loadedAssetBundle, "assets/toaster's rink/ore_gold.png"));
        rockTextures.Add(PrefabHelper.LoadTexture2D(_loadedAssetBundle, "assets/toaster's rink/ore_crystal.png"));
    }

    

    private static void RockEventTick()
    {
        if (spawnedRock == null) return; // only work when rock exists
        
        // Move rock up if not up
        if (spawnedRock.transform.position.y < 0)
        {
            spawnedRock.transform.position = new Vector3(spawnedRock.transform.position.x, spawnedRock.transform.position.y + (Time.deltaTime * rockRiseSpeedMultiplier), spawnedRock.transform.position.z);
        }
    }

    public static void PlayRockHitFromPayload(RockHitPayload payload)
    {
        if (spawnedRock == null) return;
        Player localPlayer = PlayerManager.Instance.GetLocalPlayer();
        if (localPlayer == null) return;
        
        // spawn hit sound
        GameObject hitNow = new GameObject();
        hitNow.transform.position = payload.Position;
        hitNow.transform.parent = spawnedRock.transform;
        AudioSource audioSourceBase = hitNow.AddComponent<AudioSource>();
        audioSourceBase.clip = rockHitAudioClip;
        audioSourceBase.loop = false;
        audioSourceBase.playOnAwake = false;
        audioSourceBase.reverbZoneMix = 0.1f;
        audioSourceBase.maxDistance = 20f;
        audioSourceBase.volume = payload.HitterClientId == localPlayer.OwnerClientId ? 1f : 0.5f;
        audioSourceBase.spatialBlend = 0.8f;
        audioSourceBase.pitch = Random.Range(0.9f, 1.1f);
        audioSourceBase.Play();
        Object.Destroy(hitNow, audioSourceBase.clip.length);
        
        // spawn particles
        GameObject sparksGo = Object.Instantiate(hitSparksParticlePrefab, payload.Position, Quaternion.LookRotation(payload.NormalDirection * -1));
        sparksGo.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        ParticleSystem ps = sparksGo.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogWarning("ParticleSystem component not found on the instantiated prefab.");
            Object.Destroy(sparksGo, 5f); // Destroy after a fallback time
            return;
        }
        
        var mainModule = ps.main;
        var collisionModule = ps.collision;
        collisionModule.enabled = false;
        ps.Play();
        Object.Destroy(sparksGo, mainModule.duration + mainModule.startLifetime.constantMax + 0.1f);
        
        RockEventUI.ShowOrUpdateUI(payload.RockMaxHealth, payload.RockCurrentHealth, false);
    }

    [HarmonyPatch(typeof(LevelManagerController), "Event_OnGamePhaseChanged")]
    public static class LevelManagerControllerEventOnGamePhaseChanged
    {
        [HarmonyPostfix]
        public static void Postfix(LevelManagerController __instance, Dictionary<string, object> message)
        {
            GamePhase newGamePhase = (GamePhase) message["newGamePhase"];
            GamePhase oldGamePhase = (GamePhase) message["oldGamePhase"];
            if (oldGamePhase == GamePhase.Warmup && newGamePhase != GamePhase.Warmup)
            {
                DestroyRock(false);
            }
        }
    }

    private static void DestroyRock(bool playSound = false)
    {
        if (spawnedRock != null)
        {
            // spawn destroy sound effect
            if (playSound)
            {
                GameObject hitNow = new GameObject();
                hitNow.transform.position = spawnedRock.transform.position;
                AudioSource audioSourceBase = hitNow.AddComponent<AudioSource>();
                audioSourceBase.clip = rockDeathAudioClip;
                audioSourceBase.loop = false;
                audioSourceBase.playOnAwake = false;
                audioSourceBase.reverbZoneMix = 0.1f;
                audioSourceBase.maxDistance = 20f;
                audioSourceBase.volume = 1f; // TODO make this quieter if is not self
                audioSourceBase.spatialBlend = 0.8f;
                audioSourceBase.pitch = Random.Range(0.9f, 1.1f);
                audioSourceBase.Play();
                GameObject.Destroy(hitNow, audioSourceBase.clip.length);
            }
            
            // destroy rock
            Object.Destroy(spawnedRock);
            
            foreach (Material mat in copiedMaterials)
            {
                if (mat != null) Object.Destroy(mat);
            }

            copiedMaterials.Clear();
        }
        if (playSound)
            RockEventUI.ShowOrUpdateUI(100, 0, false); // update the progress bar
        RockEventUI.Hide();
    }
    
    public static void DespawnRockFromPayload(RockEventEndedPayload payload)
    {
        DestroyRock(true);
    }
 
    [HarmonyPatch(typeof(SynchronizedObjectManager), "Update")]
    public class SynchronizedObjectManagerUpdate
    {
        [HarmonyPostfix]
        public static void Postfix(SynchronizedObjectManager __instance)
        {
            RockEventTick();
        }
    }
    
}

[Serializable]
public class RockEventPayload
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public string RockName { get; set; }
    public bool AlreadyBegun { get; set; } = false;
    public int RockMaxHealth { get; set; } = 20;
    public int RockCurrentHealth { get; set; } = 20;
    public int RockShape { get; set; }
    public int RockType { get; set; }

    public RockEventPayload(Vector3 p, Vector3 r, string n, bool a, int m, int c, int s, int t)
    {
        Position = p;
        Rotation = r;
        RockName = n;
        AlreadyBegun = a;
        RockMaxHealth = m;
        RockCurrentHealth = c;
        RockShape = s;
        RockType = t;
    }
}

[Serializable]
public class RockHitPayload
{
    public Vector3 Position { get; set; }
    public Vector3 NormalDirection { get; set; }
    public ulong HitterClientId { get; set; }
    public int RockMaxHealth { get; set; }
    public int RockCurrentHealth { get; set; }

    public RockHitPayload(Vector3 p, Vector3 n, ulong id, int m, int c)
    {
        Position = p;
        NormalDirection = n;
        HitterClientId = id;
        RockMaxHealth = m;
        RockCurrentHealth = c;
    }
}

[Serializable]
public class RockEventEndedPayload
{
    private bool ended = true;
}