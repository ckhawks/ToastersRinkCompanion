using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Jail
{
    private static Dictionary<int, GameObject> spawnedJails = new Dictionary<int, GameObject>();
    public static IReadOnlyDictionary<int, GameObject> SpawnedJails => spawnedJails;
    private static GameObject jailPrefab;
    private static AssetBundle _loadedAssetBundle;

    public static void SpawnJail(JailSpawnPayload payload)
    {
        if (jailPrefab == null) LoadPrefab();

        // Remove existing jail with same ID if any
        DespawnJail(payload.id);

        GameObject jail = Object.Instantiate(jailPrefab);

        // Fix shaders for URP
        MeshRenderer[] renderers = jail.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            }
        }

        jail.transform.position = new Vector3(payload.x, payload.y, payload.z);
        spawnedJails[payload.id] = jail;
    }

    public static void DespawnJail(int jailId)
    {
        if (spawnedJails.TryGetValue(jailId, out GameObject jail))
        {
            Object.Destroy(jail);
            spawnedJails.Remove(jailId);
        }
    }

    public static void ClearAllJails()
    {
        foreach (var jail in spawnedJails.Values)
        {
            Object.Destroy(jail);
        }
        spawnedJails.Clear();
    }

    private static void LoadPrefab()
    {
        if (jailPrefab != null) return;
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/jail");
        jailPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/jail.prefab");
    }

    public class JailSpawnPayload
    {
        public int id;
        public float x;
        public float y;
        public float z;
    }

    public class JailDespawnPayload
    {
        public int id;
    }
}
