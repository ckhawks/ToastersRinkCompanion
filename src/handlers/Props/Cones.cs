using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToastersRinkCompanion.handlers;

public static class Cones
{
    [Serializable]
    public class ConesPayload
    {
        public ConeLocation[] coneLocations;
    }

    [Serializable]
    public class ConeLocation
    {
        public Vec3 position;

        public ConeLocation() { }

        public ConeLocation(Vector3 v)
        {
            position = new Vec3(v);
        }
    }

    private static readonly List<GameObject> _spawnedCones = new();
    public static IReadOnlyList<GameObject> SpawnedObjects => _spawnedCones;

    private static GameObject _conePrefab;
    private static AssetBundle _loadedAssetBundle;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<ConesPayload>("cones",
            (_, payload) => UpdateConesToPayload(payload));
    }

    public static void UpdateConesToPayload(ConesPayload conesPayload)
    {
        if (_conePrefab == null) LoadPrefab();
        if (_conePrefab == null) return;

        ClearCones();

        foreach (var coneLocation in conesPayload.coneLocations)
        {
            var cone = Object.Instantiate(_conePrefab);
            var coneMesh = cone.transform.Find("Cone");
            if (coneMesh != null)
            {
                var renderer = coneMesh.GetComponent<MeshRenderer>();
                if (renderer != null)
                    foreach (var mat in renderer.sharedMaterials)
                        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            cone.transform.position = coneLocation.position.ToVector3();
            cone.transform.localScale = Vector3.one;
            _spawnedCones.Add(cone);
        }
    }

    public static void ClearCones()
    {
        foreach (var cone in _spawnedCones)
            if (cone != null) Object.Destroy(cone);
        _spawnedCones.Clear();
    }

    private static void LoadPrefab()
    {
        if (_conePrefab != null) return;
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/cone");
        if (_loadedAssetBundle == null) return;
        _conePrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/toaster's rink/cone.prefab");
    }
}
