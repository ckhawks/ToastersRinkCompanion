using System.Collections;
using UnityEngine;

namespace ToastersRinkCompanion;

// Handles a server-pushed "migrate_server" command: the server we're on is being
// drained (e.g. for a restart), so connect to the destination server. Puck's
// ConnectionManager.Client_StartClient handles the disconnect-from-current +
// reconnect-to-target handoff natively (it queues a pendingConnection, disconnects,
// and Event_OnClientStopped auto-connects to the pending target).
public static class ServerMigration
{
    public static void Handle(MessagingHandler.MigrateServerPayload payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.ip) || payload.port <= 0)
        {
            Plugin.LogError("Received invalid migrate_server payload; ignoring.");
            return;
        }

        string dest = string.IsNullOrEmpty(payload.serverName) ? "another server" : payload.serverName;
        Toast.Show($"This server is restarting — moving you to {dest}...", 6f);
        Plugin.Log($"Migrating to {dest} ({payload.ip}:{payload.port})");

        ushort port = (ushort)payload.port;

        // Give the toast a moment to render before we tear the connection down. Fall back
        // to an instant connect if we can't get a coroutine host.
        var host = MonoBehaviourSingleton<UIManager>.Instance?.GameState;
        if (host != null)
            host.StartCoroutine(MigrateAfterDelay(payload.ip, port));
        else
            Connect(payload.ip, port);
    }

    private static IEnumerator MigrateAfterDelay(string ip, ushort port)
    {
        yield return new WaitForSeconds(1.25f);
        Connect(ip, port);
    }

    private static void Connect(string ip, ushort port)
    {
        var connectionManager = MonoBehaviourSingleton<ConnectionManager>.Instance;
        if (connectionManager == null)
        {
            Plugin.LogError("ConnectionManager unavailable; cannot migrate.");
            return;
        }
        connectionManager.Client_StartClient(ip, port, null);
    }
}
