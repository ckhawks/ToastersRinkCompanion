using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Cones
{
    private static List<GameObject> spawnedCones = new List<GameObject>();
    private static GameObject conePrefab; // cone prefab
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateConesToPayload(MessagingHandler.ConesPayload conesPayload)
    {
        if (conePrefab == null) LoadPrefab();

        ClearCones();
        
        foreach (MessagingHandler.ConeLocation coneLocation in conesPayload.coneLocations)
        {
            MessagingHandler.Vec3 conePosition = coneLocation.position;
            Vector3 conePositionReal  = new Vector3(conePosition.x, conePosition.y, conePosition.z);
            GameObject cone = Object.Instantiate(conePrefab);
            foreach(Material mat in cone.transform.Find("Cone").GetComponent<MeshRenderer>().sharedMaterials)
            {
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            cone.transform.position = conePositionReal;
            cone.transform.localScale = new Vector3(1, 1, 1);
            spawnedCones.Add(cone);
        }
    }

    public static void ClearCones()
    {
        foreach (GameObject cone in spawnedCones)
        {
            Object.Destroy(cone);
        }
        spawnedCones.Clear();
    }

    private static void LoadPrefab()
    {
        if (conePrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/cone");
        conePrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/cone.prefab");
    }
}