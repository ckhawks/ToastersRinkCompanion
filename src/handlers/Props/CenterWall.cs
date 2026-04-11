using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class CenterWall
{
    private static readonly PrefabSpawner _spawner = new(
        "assetbundles/centerwall",
        "assets/toaster's rink/centerwall.prefab",
        new PrefabSpawner.SpawnPoint(Vector3.zero, new Vector3(-90, 0, 0)));

    public static GameObject SpawnedObject =>
        _spawner.SpawnedObjects.Count > 0 ? _spawner.SpawnedObjects[0] : null;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>("centerwall",
            (_, p) => _spawner.SetEnabled(p.enabled));
    }
}
