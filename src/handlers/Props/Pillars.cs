using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Pillars
{
    private static readonly PrefabSpawner _spawner = new(
        "assetbundles/pillars",
        "assets/toaster's rink/pillars.prefab",
        PrefabSpawner.SpawnPoint.AtOrigin);

    public static IReadOnlyList<GameObject> SpawnedObjects => _spawner.SpawnedObjects;

    public static GameObject SpawnedObject =>
        _spawner.SpawnedObjects.Count > 0 ? _spawner.SpawnedObjects[0] : null;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>("pillars",
            (_, p) => _spawner.SetEnabled(p.enabled));
    }
}
