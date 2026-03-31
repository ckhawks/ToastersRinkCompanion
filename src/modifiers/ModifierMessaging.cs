using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ToastersRinkCompanion.modifiers;

/// <summary>
/// Registers message handlers for modifier-related messages from the server.
/// </summary>
public static class ModifierMessaging
{
    public static void RegisterHandlers()
    {
        // No connectedToToastersRink gate — these only come from our server
        // and may arrive before the greetings handler sets the flag
        JsonMessageRouter.RegisterHandler("modifier_registry", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<ModifierRegistryPayload>(payloadJson);
                if (payload == null) return;

                ModifierRegistry.SetRegistry(payload);
                Plugin.Log($"Received modifier_registry: {payload.modifiers.Length} modifiers, admin={payload.isAdmin}");
            }
            catch (Exception e)
            {
                Plugin.LogError($"Failed to parse modifier_registry: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("modifier_state", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<ModifierStatePayload>(payloadJson);
                if (payload == null) return;

                ModifierRegistry.SetState(payload);
                ActiveModifiersHUD.Refresh();
                ModifierPanelUI.RefreshCurrentTab();
                Plugin.Log($"Received modifier_state: {payload.activeModifiers?.Length ?? 0} active");
            }
            catch (Exception e)
            {
                Plugin.LogError($"Failed to parse modifier_state: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("vote_started", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            try
            {
                var payload = JsonConvert.DeserializeObject<VoteStartedPayload>(payloadJson);
                if (payload == null) return;

                ModifierRegistry.CurrentVote = new VoteState
                {
                    ModifierKey = payload.modifierKey,
                    ModifierName = payload.modifierName,
                    Description = payload.description,
                    InitiatorName = payload.initiatorName,
                    Parameters = payload.parameters ?? new Dictionary<string, string>(),
                    YesCount = payload.yesCount,
                    NoCount = payload.noCount,
                    RequiredVotes = payload.requiredVotes,
                    TotalPlayers = payload.totalPlayers,
                    InitialSoftSeconds = (float)payload.softTimeoutSeconds,
                    SoftSecondsRemaining = (float)payload.softTimeoutSeconds,
                    HardSecondsRemaining = (float)payload.hardTimeoutSeconds,
                    IsDisabling = payload.isDisabling
                };

                VotePopupUI.Show();
                ModifierPanelUI.RefreshCurrentTab();
                Plugin.Log($"Vote started: {payload.modifierName}");
            }
            catch (Exception e)
            {
                Plugin.LogError($"Failed to parse vote_started: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("vote_update", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            try
            {
                var payload = JsonConvert.DeserializeObject<VoteUpdatePayload>(payloadJson);
                if (payload == null || ModifierRegistry.CurrentVote == null) return;

                ModifierRegistry.CurrentVote.YesCount = payload.yesCount;
                ModifierRegistry.CurrentVote.NoCount = payload.noCount;
                ModifierRegistry.CurrentVote.SoftSecondsRemaining = (float)payload.softSecondsRemaining;
                ModifierRegistry.CurrentVote.HardSecondsRemaining = (float)payload.hardSecondsRemaining;

                VotePopupUI.UpdateDisplay();
            }
            catch (Exception e)
            {
                Plugin.LogError($"Failed to parse vote_update: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("vote_ended", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            try
            {
                var payload = JsonConvert.DeserializeObject<VoteEndedPayload>(payloadJson);
                if (payload == null) return;

                if (ModifierRegistry.CurrentVote != null)
                {
                    ModifierRegistry.CurrentVote.YesCount = payload.yesCount;
                    ModifierRegistry.CurrentVote.NoCount = payload.noCount;
                    ModifierRegistry.CurrentVote.Result = payload.result;
                }

                VotePopupUI.ShowResult(payload.result);
                ModifierPanelUI.RefreshCurrentTab();
                Plugin.Log($"Vote ended: {payload.modifierName} -> {payload.result}");
            }
            catch (Exception e)
            {
                Plugin.LogError($"Failed to parse vote_ended: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("server_state", (sender, payloadJson) =>
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<ServerState.ServerStatePayload>(payloadJson);
                if (payload == null) return;

                ServerState.Update(payload);
                ModifierPanelUI.RefreshCurrentTab();
                Plugin.Log($"Received server_state");
            }
            catch (Exception e)
            {
                Plugin.LogError($"Failed to parse server_state: {e}");
            }
        });
    }

    /// <summary>
    /// Send a vote request to the server (start vote or force).
    /// </summary>
    public static void SendVoteRequest(string modifierKey, Dictionary<string, string> parameters, bool isForce)
    {
        var payload = new ModifierVoteRequestPayload
        {
            modifierKey = modifierKey,
            parameters = parameters ?? new Dictionary<string, string>(),
            isForce = isForce
        };
        // Send to server (clientId 0 = server)
        JsonMessageRouter.SendMessage("modifier_vote_request", 0, payload);
    }

    /// <summary>
    /// Cast a vote on the current active vote.
    /// </summary>
    public static void SendCastVote(bool voteYes)
    {
        var payload = new ModifierCastVotePayload { voteYes = voteYes };
        JsonMessageRouter.SendMessage("modifier_cast_vote", 0, payload);
    }
}
