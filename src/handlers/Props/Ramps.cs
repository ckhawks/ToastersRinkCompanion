using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Ramps
{
    private static readonly PrefabSpawner _spawner = new(
        "assetbundles/ramp",
        "assets/toaster's rink/ramp.prefab",
        new PrefabSpawner.SpawnPoint(new Vector3(-16, 0, 0), new Vector3(-90, 90, 0)),
        new PrefabSpawner.SpawnPoint(new Vector3(16, 0, 0), new Vector3(-90, 90, 0)));

    public static IReadOnlyList<GameObject> SpawnedObjects => _spawner.SpawnedObjects;

    public static void ClearRamps() => _spawner.Clear();

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>("ramps",
            (_, p) => _spawner.SetEnabled(p.enabled));
    }
}
