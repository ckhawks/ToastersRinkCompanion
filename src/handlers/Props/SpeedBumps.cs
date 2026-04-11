using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class SpeedBumps
{
    private static readonly PrefabSpawner _spawner = new(
        "assetbundles/speedbump",
        "assets/toaster's rink/speedbump.prefab",
        new PrefabSpawner.SpawnPoint(Vector3.zero, new Vector3(-90, 0, 0)),
        new PrefabSpawner.SpawnPoint(Vector3.zero, new Vector3(-90, 180, 0)));

    public static IReadOnlyList<GameObject> SpawnedObjects => _spawner.SpawnedObjects;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>("speedbumps",
            (_, p) => _spawner.SetEnabled(p.enabled));
    }
}
