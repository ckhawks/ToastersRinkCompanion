// PrefabSpawner.cs

using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Shared implementation for the many "load a prefab from an asset bundle, spawn
/// one or more copies at fixed transforms, force URP shaders on every mesh, and
/// despawn on disable" prop handlers (Pillars, BigWalls, CenterWall, Tarps,
/// SpeedBumps, DummyX2, Ramps, GoalRamps, Portals, ...).
///
/// Feature classes own a single static instance of this type and just forward
/// the server's enable/disable messages to <see cref="SetEnabled"/>.
/// </summary>
public sealed class PrefabSpawner
{
    public readonly struct SpawnPoint
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public SpawnPoint(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public SpawnPoint(Vector3 position, Vector3 eulerDegrees)
            : this(position, Quaternion.Euler(eulerDegrees)) { }

        public static SpawnPoint AtOrigin =>
            new SpawnPoint(Vector3.zero, Quaternion.identity);
    }

    private readonly string _bundlePath;
    private readonly string _assetPath;
    private readonly SpawnPoint[] _spawnPoints;

    private AssetBundle _loadedBundle;
    private GameObject _prefab;
    private readonly List<GameObject> _spawned = new();

    public PrefabSpawner(string bundlePath, string assetPath, params SpawnPoint[] spawnPoints)
    {
        _bundlePath = bundlePath;
        _assetPath = assetPath;
        _spawnPoints = spawnPoints.Length == 0
            ? new[] { SpawnPoint.AtOrigin }
            : spawnPoints;
    }

    public IReadOnlyList<GameObject> SpawnedObjects => _spawned;

    /// <summary>
    /// Enable or disable the prop set. Idempotent: always clears existing
    /// instances first, so receiving the same "enabled=true" twice does not
    /// double-spawn.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        Clear();
        if (!enabled) return;

        if (_prefab == null) LoadPrefab();
        if (_prefab == null) return; // load failed; already logged

        foreach (var sp in _spawnPoints)
        {
            var go = Object.Instantiate(_prefab);
            ApplyUrpShader(go);
            go.transform.position = sp.Position;
            go.transform.rotation = sp.Rotation;
            _spawned.Add(go);
        }
    }

    public void Clear()
    {
        foreach (var go in _spawned)
            if (go != null) Object.Destroy(go);
        _spawned.Clear();
    }

    private void LoadPrefab()
    {
        if (_prefab != null) return;
        if (_loadedBundle == null) _loadedBundle = PrefabHelper.LoadAssetBundle(_bundlePath);
        if (_loadedBundle == null) return;
        _prefab = PrefabHelper.LoadPrefab(_loadedBundle, _assetPath);
    }

    /// <summary>
    /// Every prop prefab in this project ships with Standard shaders and has to
    /// be re-pointed at URP/Lit at runtime. Centralizing that here means the
    /// dozen prop handlers don't each copy-paste the loop.
    /// </summary>
    public static void ApplyUrpShader(GameObject go)
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;
        foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
        foreach (var mat in renderer.sharedMaterials)
            if (mat != null)
                mat.shader = urpLit;
    }
}
