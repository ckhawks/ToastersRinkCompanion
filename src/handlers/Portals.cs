using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Portals
{
    private static List<GameObject> spawnedPortals = new List<GameObject>();
    private static GameObject portalPrefab;// cone prefab
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdatePortalsToPayload(MessagingHandler.PortalsPayload payload)
    {
        if (portalPrefab == null) LoadPrefab();

        ClearPortals();

        if (payload.enabled)
        {
            GameObject portal1 = Object.Instantiate(portalPrefab);
            MeshRenderer[] renderers1 = portal1.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            portal1.transform.position = new Vector3(19.2f, 0, -20);
            portal1.transform.rotation = Quaternion.Euler(0, -90, 0);
            spawnedPortals.Add(portal1);
            
            GameObject portal2 = Object.Instantiate(portalPrefab);
            MeshRenderer[] renderers2 = portal2.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers2)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            
            // 0, 0, 0 -> 0, 0, -3
            portal2.transform.position = new Vector3(-19.2f, 0, 20);
            portal2.transform.rotation = Quaternion.Euler(0, 90, 0);
            spawnedPortals.Add(portal2);
        }
    }
    
    public static void ClearPortals()
    {
        foreach (GameObject portal in spawnedPortals)
        {
            Object.Destroy(portal);
        }
        spawnedPortals.Clear();
    }
    
    private static void LoadPrefab()
    {
        if (portalPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/teleporter");
        portalPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/teleporter.prefab");
    }
}