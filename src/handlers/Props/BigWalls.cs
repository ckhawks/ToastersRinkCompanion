using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class BigWalls
{
    private static readonly PrefabSpawner _spawner = new(
        "assetbundles/bigwall",
        "assets/toaster's rink/bigwall.prefab",
        new PrefabSpawner.SpawnPoint(Vector3.zero, Vector3.zero),
        new PrefabSpawner.SpawnPoint(Vector3.zero, new Vector3(0, 180, 0)));

    public static IReadOnlyList<GameObject> SpawnedObjects => _spawner.SpawnedObjects;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>("bigwalls",
            (_, p) => _spawner.SetEnabled(p.enabled));
    }
}
