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

        // Handle initial connection and disconnect cleanup. Unsubscribe first so a
        // repeated Setup() (e.g. plugin re-enable) doesn't stack duplicate handlers.
        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;

        // If already connected, set up immediately
        if (nm.IsConnectedClient || nm.IsServer)
            SetupHandlers();
    }

    // Unsubscribe the NetworkManager lifecycle callbacks. Called on plugin disable.
    public static void Teardown()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        _handlersRegistered = false;
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
            PuckBlockOutline.RegisterHandlers();
            PuckBlockInput.RegisterHandlers();
            PuckBlockIndicator.RegisterHandlers();

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

    /// <summary>
    /// Runs one piece of post-greeting decoration, logging and swallowing anything it
    /// throws. Missing asset bundles are the common case — a hand-placed install with the
    /// DLL but no assetbundles folder — and that should cost the player the sign or the
    /// meme board, not the whole handshake.
    /// </summary>
    private static void TrySetup(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Plugin.LogError($"greeting setup step '{what}' failed (continuing): {e.Message}");
        }
    }

    private static bool _bundleWarningShown;

    /// <summary>
    /// Tells the player once when Companion's asset bundles are absent from disk.
    ///
    /// Without this the failure is silent from in-game: the sign, meme board,
    /// collectibles and props all quietly do nothing, which reads as the server being
    /// broken rather than the install being incomplete. Shown locally rather than
    /// reported to the server, because it is entirely a client-side install problem and
    /// the fix is on this machine.
    /// </summary>
    private static void WarnIfAssetBundlesMissing()
    {
        if (_bundleWarningShown || !PrefabHelper.AnyBundleMissing) return;
        _bundleWarningShown = true;

        string where = PrefabHelper.ExpectedBundleDirectory ?? "the assetbundles folder next to the .dll";
        Plugin.LogError($"asset bundles missing — expected them in {where}");
        Plugin.AddLocalChatMessage(
            "<size=14><color=orange><b>Companion is missing its asset bundles.</b></color> " +
            "The sign, meme board, collectibles and map props will not appear. " +
            $"Copy the <b>assetbundles</b> folder to <i>{where}</i> and restart Puck.</size>");
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

                // Answer the handshake FIRST, before any of the cosmetic setup below.
                // This used to be the last statement in the handler, which meant a missing
                // asset bundle took the handshake down with it: Sign.SpawnSign() on an
                // install with no assetbundles folder throws "The Object you want to
                // instantiate is null", the handler aborts, and the server never learns the
                // client has Companion at all — so it reports Companion as not detected on a
                // machine where it is plainly installed and running. Whether the sign mesh
                // loaded has nothing to do with whether we are here to answer.
                JsonMessageRouter.SendMessage("companion_hello", 0, new { version = Plugin.MOD_VERSION });

                // Each of these is independent decoration. One failing must not skip the
                // others, and none of them may take down the handler.
                TrySetup("Sign.SpawnSign", Sign.SpawnSign);
                TrySetup("CollectiblePrefabs.Setup", CollectiblePrefabs.Setup);
                TrySetup("MOTDUI.Show", MOTDUI.Show);

                WarnIfAssetBundlesMissing();
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

        // `migrate_server` — server is draining; connect to the destination immediately.
        JsonMessageRouter.RegisterTypedHandler<MigrateServerPayload>(
            "migrate_server",
            (_, payload) => ServerMigration.Handle(payload));
    }

    // ---------------------------------------------------------------
    // Core envelope DTOs
    // ---------------------------------------------------------------

    [Serializable]
    public class MigrateServerPayload
    {
        public string ip;
        public int port;
        public string serverName;
    }

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
