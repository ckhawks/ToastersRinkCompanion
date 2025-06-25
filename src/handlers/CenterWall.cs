using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class CenterWall
{
    private static GameObject spawnedCenterWall;
    private static GameObject centerWallPrefab;
    private static AssetBundle _loadedAssetBundle;
    
    public static void UpdateWallToPayload(MessagingHandler.EnabledPayload payload)
    {
        if (centerWallPrefab == null) LoadPrefab();

        if (payload.enabled)
        {
            spawnedCenterWall = Object.Instantiate(centerWallPrefab);
            MeshRenderer[] renderers1 = spawnedCenterWall.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
            spawnedCenterWall.transform.position = new Vector3(0, 0, 0);
            spawnedCenterWall.transform.rotation = Quaternion.Euler(-90, 0, 0);
        }
        else
        {
            ClearWall();
        }
    }

    private static void ClearWall()
    {
        Object.Destroy(spawnedCenterWall);
    }
    
    private static void LoadPrefab()
    {
        if (centerWallPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/centerwall");
        centerWallPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/centerwall.prefab");
    }
}