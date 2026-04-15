// MessagingHandler.cs
//
// Owns the client's connection lifecycle to a Toaster's Rink server and the
// handful of core envelopes (greetings, showVersion, Chat debug, openLink) that
// don't have an obvious feature owner. Everything else is delegated to feature
// modules that self-register via their own RegisterHandlers() static method
// from SetupHandlers() below.

using System;
using HarmonyLib;
using ToastersRinkCompanion.collectibles;
using ToastersRinkCompanion.handlers;
using Unity.Netcode;
using UnityEngine;

namespace ToastersRinkCompanion;

public static class MessagingHandler
{
    private static bool _handlersRegistered = false;

    public static bool connectedToToastersRink = false;
    public static string serverVersion = "";
    public static string serverFlavor = "";
    public static bool serverCompTweaksEnabled = false;

    // Only run for clients
    public static void Setup()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Plugin.LogError("NetworkManager is null during setup");
            return;
        }

        // Handle initial connection and disconnect cleanup.
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;

        // If already connected, set up immediately
        if (nm.IsConnectedClient || nm.IsServer)
            SetupHandlers();
    }

    private static void OnClientConnected(ulong clientId)
    {
        Plugin.Log($"Client {clientId} connected, setting up handlers...");
        SetupHandlers();
    }

    private static void OnClientDisconnected(ulong clientId)
    {
        Plugin.Log($"Client {clientId} disconnected");
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        // Local client disconnected, reset all feature state.
        _handlersRegistered = false;
        connectedToToastersRink = false;
        serverFlavor = "";
        serverCompTweaksEnabled = false;

        PuckScale.currentPuckScale = 1;
        Balls.currentBallsEnabled = false;
        Cubes.currentCubesEnabled = false;
        Cones.ClearCones();
        Portals.ClearPortals();
        Ramps.ClearRamps();
        Jail.ClearAllJails();
        Sign.DestroySign();
        MemeDisplay.Cleanup();
        TeamLogoDisplay.Cleanup();
        UpdatableChat.Clear();
        ChatFormatting.Clear();
        JuggleRallyTimer.Clear();
        FuckGoals.CleanupAllCustomFrames();
        RockEventUI.Hide();
        MinimapObjects.Clear();
        ScoreboardStats.ResetHeaders();
        MatchEndPanel.Hide();
        StarPlayerGlow.Cleanup();

        ToastersRinkCompanion.modifiers.ModifierRegistry.Clear();
        ToastersRinkCompanion.modifiers.ServerState.Clear();
        ToastersRinkCompanion.modifiers.ActiveModifiersHUD.Clear();
        ToastersRinkCompanion.modifiers.VotePopupUI.Hide();
        ToastersRinkCompanion.modifiers.ModifierPanelUI.Hide();
        ToastersRinkCompanion.modifiers.PlayerModStore.Clear();
        ToastersRinkCompanion.modifiers.PlayerStatsStore.Clear();
        ToastersRinkCompanion.modifiers.MatchStarsStore.Clear();
        ToastersRinkCompanion.modifiers.FeedbackTab.Clear();

        MOTDUI.Hide();
        CollectiblesStore.Clear();

        Plugin.Log("Local client disconnected, handlers will be re-registered on reconnect");
    }

    private static void SetupHandlers()
    {
        if (_handlersRegistered) return;

        try
        {
            JsonMessageRouter.ForceReinitialize(); // handle reconnections

            // ----- Modules that own their own registration -----
            // Chat / display
            UpdatableChat.RegisterHandlers();
            JuggleRallyTimer.RegisterHandlers();
            ChatFormatting.RegisterHandlers();
            MemeDisplay.RegisterHandlers();
            TeamLogoDisplay.RegisterHandlers();
            UIPopup.RegisterHandlers();

            // Modifier system
            ToastersRinkCompanion.modifiers.ModifierMessaging.RegisterHandlers();
            ToastersRinkCompanion.modifiers.PlayerModStore.RegisterHandlers();
            ToastersRinkCompanion.modifiers.PlayerStatsStore.RegisterHandlers();
            ToastersRinkCompanion.modifiers.MatchStarsStore.RegisterHandlers();
            ToastersRinkCompanion.modifiers.FeedbackTab.RegisterHandlers();

            // Collectibles
            CollectiblesMessaging.RegisterHandlers();
            Opening.RegisterHandlers();
            CollectibleRenderer.RegisterHandlers();

            // Match flow / visuals
            MatchEndPanel.RegisterEvents();
            StarPlayerGlow.RegisterEvents();
            SuppressCameraOverlay.RegisterHandlers();
            RockEvent.RegisterHandlers();

            // Prop toggles
            Cones.RegisterHandlers();
            Ramps.RegisterHandlers();
            Pillars.RegisterHandlers();
            BigWalls.RegisterHandlers();
            CenterWall.RegisterHandlers();
            GoalRamps.RegisterHandlers();
            Tarps.RegisterHandlers();
            DummyX2.RegisterHandlers();
            SpeedBumps.RegisterHandlers();
            PuckScale.RegisterHandlers();
            Balls.RegisterHandlers();
            Cubes.RegisterHandlers();
            Portals.RegisterHandlers();
            Jail.RegisterHandlers();

            // Goal position syncing (keybind/Harmony setup)
            FuckGoals.Initialize();

            // ----- Core envelopes that don't have a natural module home -----
            RegisterCoreHandlers();

            _handlersRegistered = true;
            Plugin.Log("Setup is complete - handlers registered.");
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to setup handlers: {e}");
        }
    }

    private static void RegisterCoreHandlers()
    {
        // `greetings` — initial handshake from the server. Without
        // requireConnected, because it's literally what flips us to connected.
        JsonMessageRouter.RegisterTypedHandler<GreetingsPayload>(
            "greetings",
            (_, greetingsPayload) =>
            {
                if (greetingsPayload.companionTargetVersion == null) return;

                connectedToToastersRink = true;
                AIGoalieFilter.RemoveExistingAIGoalies();
                serverVersion = greetingsPayload.toastersRinkSuiteVersion ?? "";
                serverFlavor = greetingsPayload.serverFlavor ?? "";
                serverCompTweaksEnabled = greetingsPayload.compTweaksEnabled;

                string outdatedNote = greetingsPayload.companionTargetVersion == Plugin.MOD_VERSION
                    ? ""
                    : $" <br><color=red>Companion is out of date (server expecting {greetingsPayload.companionTargetVersion}, client on {Plugin.MOD_VERSION})! Type <b>/outdated</b> for info.</color>";
                Plugin.AddLocalChatMessage(
                    $"<size=14><i>Toaster's Rink Companion version {Plugin.MOD_VERSION} connected.</i>{outdatedNote}</size>");

                Plugin.Log($"Received `Greetings` message from Toaster's Rink {greetingsPayload.companionTargetVersion}, we're connected!");
                Sign.SpawnSign();
                CollectiblePrefabs.Setup();
                MOTDUI.Show();

                // Tell the server we have the companion installed.
                JsonMessageRouter.SendMessage("companion_hello", 0, new { version = Plugin.MOD_VERSION });
            },
            requireConnected: false);

        // `showVersion` — server-pinged version line printed to local chat.
        JsonMessageRouter.RegisterTypedHandler<EnabledPayload>(
            "showVersion",
            (_, _) => Plugin.AddLocalChatMessage(
                $"Toaster's Rink Companion {Plugin.MOD_VERSION} connected."));

        // `Chat` — debug passthrough that logs any chat relayed by the server.
        JsonMessageRouter.RegisterTypedHandler<ChatPayload>(
            "Chat",
            (sender, chatPayload) =>
            {
                string prefix = NetworkManager.Singleton.IsServer ? "SVR" : "CLT";
                Plugin.Log($"[{prefix}] Got chat from {sender}: {chatPayload.text}");
                Plugin.AddLocalChatMessage($"[{prefix}] Got chat from {sender}: {chatPayload.text}");
            });

        // `openLink` — open a URL in the player's browser.
        JsonMessageRouter.RegisterTypedHandler<OpenLinkInBrowserPayload>(
            "openLink",
            (_, payload) => Application.OpenURL(payload.link));
    }

    // ---------------------------------------------------------------
    // Core envelope DTOs
    // ---------------------------------------------------------------

    [Serializable]
    public class GreetingsPayload
    {
        public string companionTargetVersion;
        public string toastersRinkSuiteVersion;
        public string serverFlavor;
        public bool compTweaksEnabled;
    }

    [Serializable]
    public class ChatPayload
    {
        public ulong from;
        public string text;
    }

    [Serializable]
    public class OpenLinkInBrowserPayload
    {
        public string link;

        public OpenLinkInBrowserPayload(string linkValue)
        {
            this.link = linkValue;
        }
    }
}
