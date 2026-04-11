// JsonMessageRouter.cs

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Netcode;

namespace ToastersRinkCompanion;

public static class JsonMessageRouter
{
    // maps your "type" → raw-JSON-payload handler
    private static readonly Dictionary<string, Action<ulong, string>> _handlers = new();

    private static bool _initialized;

    private static bool VerboseLogging =>
        Plugin.modSettings != null && Plugin.modSettings.verboseMessageLogging;

    /// <summary>
    /// Call once on startup (both client &amp; server). Subsequent calls are no-ops
    /// unless <see cref="ForceReinitialize"/> is used.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Plugin.LogError("Can't initialize, NetworkManager is null");
            return;
        }

        var cmm = nm.CustomMessagingManager;
        if (cmm == null)
        {
            Plugin.LogError("Can't initialize, CustomMessagingManager is null");
            return;
        }

        // Clean up any existing handler first (harmless if none was registered).
        try { cmm.UnregisterNamedMessageHandler("tr-jsonMessage"); } catch { }

        cmm.RegisterNamedMessageHandler("tr-jsonMessage", HandleIncoming);

        Plugin.Log("JsonMessageRouter initialized successfully");
    }

    private static void HandleIncoming(ulong senderClientId, FastBufferReader reader)
    {
        try
        {
            reader.ReadValueSafe(out ushort length);

            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
                reader.ReadValueSafe(out bytes[i]);

            string envelopeJson = Encoding.UTF8.GetString(bytes);

            if (!TryParseEnvelope(envelopeJson, out var messageType, out var payloadJson))
            {
                Plugin.LogError($"Malformed envelope JSON: '{envelopeJson}'");
                return;
            }

            if (VerboseLogging)
                Plugin.Log($"Received envelope type='{messageType}' payload='{payloadJson}'");

            if (_handlers.TryGetValue(messageType, out var cb))
            {
                try { cb(senderClientId, payloadJson); }
                catch (Exception e) { Plugin.LogError($"[{messageType}] handler threw: {e}"); }
            }
            else
            {
                Plugin.LogError($"No JSON-handler for type '{messageType}'");
                Plugin.AddLocalChatMessage(
                    "<size=14><color=red>There was no handler for a message received from the server; " +
                    "you might be on an outdated version of Toaster's Rink Companion! Type <b>/outdated</b> for help.</color></size>");
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to process jsonMessage: {e}");
        }
    }

    /// <summary>
    /// Force re-initialization (useful after reconnection).
    /// </summary>
    public static void ForceReinitialize()
    {
        if (VerboseLogging) Plugin.Log("Force reinitializing JsonMessageRouter...");
        _initialized = false;
        Initialize();
    }

    /// <summary>
    /// Parse the envelope JSON into its <c>type</c> and <c>payload</c> components.
    /// Uses a real JSON parser so whitespace, escaped quotes, and non-object
    /// payloads are handled correctly.
    /// </summary>
    private static bool TryParseEnvelope(string envelopeJson, out string messageType, out string payloadJson)
    {
        messageType = "";
        payloadJson = "";
        if (string.IsNullOrEmpty(envelopeJson)) return false;

        try
        {
            var envelope = JObject.Parse(envelopeJson);
            messageType = (string)envelope["type"] ?? "";
            var payloadToken = envelope["payload"];
            payloadJson = payloadToken != null ? payloadToken.ToString(Formatting.None) : "";
            return !string.IsNullOrEmpty(messageType);
        }
        catch (Exception e)
        {
            Plugin.LogError($"Envelope parse failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Register a handler for a given messageType. The handler receives
    /// (senderClientId, rawPayloadJson). Prefer <see cref="RegisterTypedHandler{T}"/>
    /// for the common case where you want a deserialized payload.
    /// </summary>
    public static void RegisterHandler(string messageType, Action<ulong, string> handler)
    {
        Initialize();
        _handlers[messageType] = handler;
        if (VerboseLogging) Plugin.Log($"Registered handler: {messageType}");
    }

    /// <summary>
    /// Register a handler that receives an already-deserialized payload of type
    /// <typeparamref name="T"/>. Takes care of empty-payload rejection, null
    /// deserialization results, and exception catching so call sites can stay
    /// focused on business logic.
    ///
    /// When <paramref name="requireConnected"/> is true (the default), the
    /// handler is skipped unless the client has received the greetings message
    /// from Toaster's Rink.
    /// </summary>
    public static void RegisterTypedHandler<T>(
        string messageType,
        Action<ulong, T> handler,
        bool requireConnected = true) where T : class
    {
        RegisterHandler(messageType, (sender, payloadJson) =>
        {
            if (requireConnected && !MessagingHandler.connectedToToastersRink) return;

            if (string.IsNullOrEmpty(payloadJson))
            {
                Plugin.LogError($"[{messageType}] payload JSON is null or empty");
                return;
            }

            T payload;
            try
            {
                payload = JsonConvert.DeserializeObject<T>(payloadJson);
            }
            catch (Exception e)
            {
                Plugin.LogError($"[{messageType}] failed to deserialize {typeof(T).Name}: {e}");
                return;
            }

            if (payload == null)
            {
                Plugin.LogError($"[{messageType}] deserialized {typeof(T).Name} was null");
                return;
            }

            try { handler(sender, payload); }
            catch (Exception e) { Plugin.LogError($"[{messageType}] handler threw: {e}"); }
        });
    }

    /// <summary>
    /// Send an object (will be wrapped in {"type":..., "payload":...})
    /// to a specific client or to the server (use ServerClientId).
    /// </summary>
    public static void SendMessage(string messageType, ulong recipientClientId, object payload)
    {
        Initialize();

        // Serialize with Newtonsoft to properly handle Dictionary and complex types
        string payloadJson = JsonConvert.SerializeObject(payload);
        string envelopeJson = $"{{\"type\":\"{messageType}\",\"payload\":{payloadJson}}}";

        if (VerboseLogging) Plugin.Log($"Sending envelope: '{envelopeJson}'");

        byte[] bytes = Encoding.UTF8.GetBytes(envelopeJson);

        // Use a more generous buffer size to avoid overflow
        int bufferSize = sizeof(ushort) + bytes.Length + 64; // Extra padding
        using var writer = new FastBufferWriter(bufferSize, Allocator.Temp);

        writer.WriteValueSafe((ushort)bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
            writer.WriteValueSafe(bytes[i]);

        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage("tr-jsonMessage", recipientClientId, writer);
    }
}
