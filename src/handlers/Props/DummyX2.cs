using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class DummyX2
{
    private static readonly PrefabSpawner _spawner = new(
        "assetbundles/dummiesx2",
        "assets/toaster's rink/goaliedummyx2.prefab",
        new PrefabSpawner.SpawnPoint(Vector3.zero, Vector3.zero),
        new PrefabSpawner.SpawnPoint(Vector3.zero, new Vector3(0, 180, 0)));

    public static IReadOnlyList<GameObject> SpawnedObjects => _spawner.SpawnedObjects;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>("dummyx2",
            (_, p) => _spawner.SetEnabled(p.enabled));
    }
}
