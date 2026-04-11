using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class Portals
{
    private static readonly PrefabSpawner _spawner = new(
        "assetbundles/teleporter",
        "assets/toaster's rink/teleporter.prefab",
        new PrefabSpawner.SpawnPoint(new Vector3(19.2f, 0, -20), new Vector3(0, -90, 0)),
        new PrefabSpawner.SpawnPoint(new Vector3(-19.2f, 0, 20), new Vector3(0, 90, 0)));

    public static IReadOnlyList<GameObject> SpawnedObjects => _spawner.SpawnedObjects;

    public static void ClearPortals() => _spawner.Clear();

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>("portals",
            (_, p) => _spawner.SetEnabled(p.enabled));
    }
}
