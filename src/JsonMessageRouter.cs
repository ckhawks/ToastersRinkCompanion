// JsonMessageRouter.cs

using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ToastersRinkCompanion;

public static class JsonMessageRouter
{
  // maps your “type” → raw‐JSON‐payload handler
  static readonly Dictionary<string, Action<ulong, string>> _handlers
    = new Dictionary<string, Action<ulong, string>>();

  static bool _initialized = false;

  /// <summary>
    /// Call once on startup (both client & server).
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Plugin.LogError($"Can't initialize, NetworkManager is null");
            return;
        }

        var cmm = nm.CustomMessagingManager;
        if (cmm == null)
        {
            Plugin.LogError($"Can't initialize, CustomMessagingManager is null");
            return;
        }

        // Clean up any existing handlers first
        try
        {
            cmm.UnregisterNamedMessageHandler("tr-jsonMessage");
        }
        catch
        {
            // Ignore if handler wasn't registered
        }

        cmm.RegisterNamedMessageHandler(
            "tr-jsonMessage",
            (senderClientId, reader) =>
            {
                try
                {
                    // Read the length prefix first
                    reader.ReadValueSafe(out ushort length);
                    
                    // Read bytes one by one (safe approach)
                    var bytes = new byte[length];
                    for (int i = 0; i < length; i++)
                    {
                        reader.ReadValueSafe(out bytes[i]);
                    }
                    
                    // Convert to string
                    string envelopeJson = Encoding.UTF8.GetString(bytes);
                    Plugin.Log($"Received envelope JSON: '{envelopeJson}'");

                    // Parse the envelope manually
                    string messageType = ExtractMessageType(envelopeJson);
                    string payloadJson = ExtractPayloadJson(envelopeJson);

                    Plugin.Log($"Extracted type: '{messageType}', payload: '{payloadJson}'");

                    // invoke your handler if registered
                    if (_handlers.TryGetValue(messageType, out var cb))
                    {
                        try 
                        { 
                            cb(senderClientId, payloadJson); 
                        }
                        catch (Exception e)
                        {
                            Plugin.LogError($"[{messageType}] handler threw: {e}");
                        }
                    }
                    else
                    {
                        Plugin.LogError($"No JSON‐handler for type '{messageType}'");
                    }
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to process jsonMessage: {e}");
                }
            });

        Plugin.Log("JsonMessageRouter initialized successfully");
    }
  
    /// <summary>
    /// Force re-initialization (useful after reconnection)
    /// </summary>
    public static void ForceReinitialize()
    {
        Plugin.Log("Force reinitializing JsonMessageRouter...");
        _initialized = false;
        Initialize();
    }

    private static string ExtractMessageType(string envelopeJson)
    {
        try
        {
            int typeStart = envelopeJson.IndexOf("\"type\":\"") + 8;
            int typeEnd = envelopeJson.IndexOf("\"", typeStart);
            return envelopeJson.Substring(typeStart, typeEnd - typeStart);
        }
        catch
        {
            return "";
        }
    }

    private static string ExtractPayloadJson(string envelopeJson)
    {
        try
        {
            int payloadStart = envelopeJson.IndexOf("\"payload\":") + 10;
            int braceCount = 0;
            int start = -1;
            
            for (int i = payloadStart; i < envelopeJson.Length; i++)
            {
                if (envelopeJson[i] == '{')
                {
                    if (start == -1) start = i;
                    braceCount++;
                }
                else if (envelopeJson[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0 && start != -1)
                    {
                        return envelopeJson.Substring(start, i - start + 1);
                    }
                }
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Register a handler for a given messageType. The handler
    /// receives (senderClientId, rawPayloadJson).
    /// </summary>
    public static void RegisterHandler(string messageType, Action<ulong, string> handler)
    {
        Plugin.Log($"Registering handler: {messageType}");
        Initialize();
        _handlers[messageType] = handler;
        Plugin.Log($"Registered handler: {messageType} !");
    }

    /// <summary>
    /// Send an object (will be wrapped in {"type":..., "payload":...})
    /// to a specific client or to the server (use ServerClientId).
    /// </summary>
    public static void SendMessage(string messageType, ulong recipientClientId, object payload)
    {
        Initialize();

        // Serialize the payload directly to JSON first
        // TODO make this match how the server is serializing shit
        string payloadJson = JsonUtility.ToJson(payload);
        
        // Create envelope manually to avoid Unity JsonUtility issues with object types
        string envelopeJson = $"{{\"type\":\"{messageType}\",\"payload\":{payloadJson}}}";
        
        Plugin.Log($"Sending envelope: '{envelopeJson}'"); // Debug log
        
        byte[] bytes = Encoding.UTF8.GetBytes(envelopeJson);

        // Use a more generous buffer size to avoid overflow
        int bufferSize = sizeof(ushort) + bytes.Length + 64; // Extra padding
        using var writer = new FastBufferWriter(bufferSize, Allocator.Temp);

        // 1) write the ushort length
        writer.WriteValueSafe((ushort)bytes.Length);
        // 2) write bytes one by one (safe approach)
        for (int i = 0; i < bytes.Length; i++)
        {
            writer.WriteValueSafe(bytes[i]);
        }

        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage("tr-jsonMessage", recipientClientId, writer);
    }

  [Serializable]
  struct Envelope
  {
    public string type;
    public object payload;
  }
}