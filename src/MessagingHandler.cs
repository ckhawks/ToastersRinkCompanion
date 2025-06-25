// RpcTest2.cs

using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;
using ToastersRinkCompanion.handlers;
using Unity.Netcode;
using UnityEngine;

namespace ToastersRinkCompanion;

public static class MessagingHandler
{
    private static bool _handlersRegistered = false;
    public static bool connectedToToastersRink = false;
    
    // Only run for clients
    public static void Setup()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Plugin.LogError("NetworkManager is null during setup");
            return;
        }

        // Handle initial connection
        nm.OnClientConnectedCallback += OnClientConnected;
        
        // Handle disconnection (cleanup)
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        
        // If already connected, set up immediately
        if (nm.IsConnectedClient || nm.IsServer)
        {
            SetupHandlers();
        }
    }
    
    private static void OnClientConnected(ulong clientId)
    {
        // on both client & server builds this fires when the client connects to a server
        Plugin.Log($"Client {clientId} connected, setting up handlers...");
        SetupHandlers();
    }

    private static void OnClientDisconnected(ulong clientId)
    {
        Plugin.Log($"Client {clientId} disconnected");
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Local client disconnected, reset state
            _handlersRegistered = false;
            connectedToToastersRink = false;
            PuckScale.currentPuckScale = 1;
            Cones.ClearCones();
            Portals.ClearPortals();
            Ramps.ClearRamps();
            Sign.DestroySign();
            Plugin.Log("Local client disconnected, handlers will be re-registered on reconnect");
        }
    }

    private static void SetupHandlers()
    {
        if (_handlersRegistered) return;
        
        try
        {
            JsonMessageRouter.ForceReinitialize(); // Force re-init to handle reconnections
            
            JsonMessageRouter.RegisterHandler("greetings", (sender, payloadJson) =>
            {
                // Plugin.Log($"Handling `greetings` message from sender {sender}");
                // Plugin.Log($"Raw payload JSON: '{payloadJson}'");
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    var greetingsPayload = JsonUtility.FromJson<GreetingsPayload>(payloadJson);
                    
                    if (greetingsPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize GreetingsPayload - result is null");
                        return;
                    }

                    if (greetingsPayload?.toastersRinkSuiteVersion != null)
                    {
                        connectedToToastersRink = true;
                        UIChat.Instance.AddChatMessage($"<size=14><i>Toaster's Rink Companion for {Plugin.TRS_VERSION} connected.</i> {(greetingsPayload?.toastersRinkSuiteVersion == Plugin.TRS_VERSION ? "" : $" <br><color=red>Companion is out of date (server on {greetingsPayload?.toastersRinkSuiteVersion})! Type <b>/outdated</b> for info.</color>")}</size>");
                        Plugin.Log($"Received `Greetings` message from Toaster's Rink {greetingsPayload?.toastersRinkSuiteVersion}, we're connected!");
                        Sign.SpawnSign();
                    }

                    return;
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse greetings payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("Chat", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                // Plugin.Log($"Handling `Chat` message from sender {sender}");
                // Plugin.Log($"Raw payload JSON: '{payloadJson}'");
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    var chatPayload = JsonUtility.FromJson<ChatPayload>(payloadJson);
                    
                    if (chatPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize ChatPayload - result is null");
                        return;
                    }
                    
                    Plugin.Log($"[{(NetworkManager.Singleton.IsServer ? "SVR" : "CLT")}] " +
                               $"Got chat from {sender}: {chatPayload.text}");
                    UIChat.Instance.AddChatMessage($"[{(NetworkManager.Singleton.IsServer ? "SVR" : "CLT")}] " +
                                                   $"Got chat from {sender}: {chatPayload.text}");
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse Chat payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("openLink", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                // Plugin.Log($"Handling `cones` message from sender {sender}");
                // Plugin.Log($"Raw payload JSON: '{payloadJson}'");
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var openLinkPayload = JsonConvert.DeserializeObject<OpenLinkInBrowserPayload>(payloadJson);
                    
                    if (openLinkPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize openLinkPayload - result is null");
                        return;
                    }
                    
                    Application.OpenURL(openLinkPayload.link);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse openLink payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("cones", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                // Plugin.Log($"Handling `cones` message from sender {sender}");
                // Plugin.Log($"Raw payload JSON: '{payloadJson}'");
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var conesPayload = JsonConvert.DeserializeObject<ConesPayload>(payloadJson);
                    
                    if (conesPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize ConesPayload - result is null");
                        return;
                    }
                    
                    Cones.UpdateConesToPayload(conesPayload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse cones payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("ramps", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var rampsPayload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (rampsPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize RampsPayload - result is null");
                        return;
                    }
                    
                    Ramps.UpdateRampsToPayload(rampsPayload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse ramps payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("pillars", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }
                    
                    var payload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (payload == null)
                    {
                        Plugin.LogError("Failed to deserialize pillars Payload - result is null");
                        return;
                    }
                    
                    Pillars.UpdatePillarsToPayload(payload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse pillars payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("bigwalls", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }
                    
                    var payload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (payload == null)
                    {
                        Plugin.LogError("Failed to deserialize bigwalls Payload - result is null");
                        return;
                    }
                    
                    BigWalls.UpdateWallsToPayload(payload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse pillars payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("centerwall", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }
                    
                    var payload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (payload == null)
                    {
                        Plugin.LogError("Failed to deserialize centerwall Payload - result is null");
                        return;
                    }
                    
                    CenterWall.UpdateWallToPayload(payload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse pillars payload: {e}");
                }
            });
            
            // Reuse the same payload type because the messageType determines
            JsonMessageRouter.RegisterHandler("goalramps", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var rampsPayload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (rampsPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize RampsPayload - result is null");
                        return;
                    }
                    
                    GoalRamps.UpdateRampsToPayload(rampsPayload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse goalramps payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("tarps", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var tarpsPayload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (tarpsPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize EnabledPayload tarps - result is null");
                        return;
                    }
                    
                    Tarps.UpdateTarpsToPayload(tarpsPayload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse tarps payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("dummyx2", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var dummyX2Payload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (dummyX2Payload == null)
                    {
                        Plugin.LogError("Failed to deserialize EnabledPayload dummyx2 - result is null");
                        return;
                    }
                    
                    DummyX2.UpdateDummyX2ToPayload(dummyX2Payload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse tarps payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("speedbumps", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var speedbumpsPayload = JsonConvert.DeserializeObject<EnabledPayload>(payloadJson);
                    
                    if (speedbumpsPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize SpeedBumpsPayload - result is null");
                        return;
                    }
                    
                    SpeedBumps.UpdateSpeedBumpsToPayload(speedbumpsPayload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse speedbumps payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("puckscale", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;

                // Plugin.Log($"Handling `puckscale` message from sender {sender}");
                // Plugin.Log($"Raw payload JSON: '{payloadJson}'");
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var puckScalePayload = JsonConvert.DeserializeObject<PuckScalePayload>(payloadJson);
                    
                    if (puckScalePayload == null)
                    {
                        Plugin.LogError("Failed to deserialize puckScalePayload - result is null");
                        return;
                    }
                    
                    PuckScale.UpdatePuckScaleToPayload(puckScalePayload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse puckscale payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("portals", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;

                // Plugin.Log($"Handling `portals` message from sender {sender}");
                // Plugin.Log($"Raw payload JSON: '{payloadJson}'");
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // var conesPayload = JsonUtility.FromJson<ConesPayload>(payloadJson);
                    var portalsPayload = JsonConvert.DeserializeObject<PortalsPayload>(payloadJson);
                    
                    if (portalsPayload == null)
                    {
                        Plugin.LogError("Failed to deserialize portalsPayload - result is null");
                        return;
                    }
                    
                    Portals.UpdatePortalsToPayload(portalsPayload);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse portals payload: {e}");
                }
            });
            
            JsonMessageRouter.RegisterHandler("ImageNotice", (sender, payloadJson) =>
            {
                if (!connectedToToastersRink) return;

                // Plugin.Log($"Handling `ImageNotice` message from sender {sender}");
                // Plugin.Log($"Raw payload JSON: '{payloadJson}'"); // Debug log
                
                try
                {
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        Plugin.LogError("Payload JSON is null or empty");
                        return;
                    }

                    // payloadJson is the raw JSON payload like {"from":123,"text":"hello!"}
                    var imageNoticePayload = JsonUtility.FromJson<ImageNoticePayload>(payloadJson);
                    
                    if (imageNoticePayload == null)
                    {
                        Plugin.LogError("Failed to deserialize ImageNoticePayload - result is null");
                        return;
                    }
                    
                    Plugin.Log($"[{(NetworkManager.Singleton.IsServer ? "SVR" : "CLT")}] " +
                               $"Got imageNotice from {sender}: {imageNoticePayload.imageUrl} {imageNoticePayload.note}");
                    // UIChat.Instance.AddChatMessage(
                    //     $"[{(NetworkManager.Singleton.IsServer ? "SVR" : "CLT")}] " +
                    //     $"Got imageNotice from {sender}: {imageNoticePayload.imageUrl} {imageNoticePayload.note}");
                    UIPopup.Show(imageNoticePayload.from, imageNoticePayload.imageUrl, imageNoticePayload.note);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"Failed to parse ImageNotice payload: {e}");
                }
            });
            
            _handlersRegistered = true;
            Plugin.Log($"Setup is complete - handlers registered.");
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to setup handlers: {e}");
        }
    }
    
    [Serializable]
    public class GreetingsPayload
    {
        public string toastersRinkSuiteVersion;
    }
    
    [Serializable]
    public class ChatPayload
    {
        public ulong from;
        public string text;
    }

    public class ImageNoticePayload
    {
        public ulong from;
        public string imageUrl;
        public string note;
    }

    [Serializable]
    public class PortalsPayload
    {
        public bool enabled;
    }
    
    [Serializable]
    public class EnabledPayload
    {
        public bool enabled;
    }
    
    [Serializable]
    public class OpenLinkInBrowserPayload
    {
        public string link;

        public OpenLinkInBrowserPayload(string link2)
        {
            this.link = link2;
        }
    }
    
    [Serializable]
    public class ConesPayload
    {
        public ConeLocation[] coneLocations;
    }

    [Serializable]
    public class ConeLocation
    {
        public Vec3 position;
        // public Vector3 scale;
        // public Vector3 rotation;

        public ConeLocation(Vector3 v)
        {
            position = new Vec3 { x = v.x, y = v.y, z = v.z };
        }
    }

    [Serializable]
    public struct Vec3 { public float x, y, z; }
}