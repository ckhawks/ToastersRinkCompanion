using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

public static class JuggleRallyTimer
{
    // Map message IDs to their timer state
    private static readonly Dictionary<string, RallyTimerState> _activeTimers = new Dictionary<string, RallyTimerState>();

    private class RallyTimerState
    {
        public Label Label;
        public float StartTime;
        public float ElapsedAtStart;
        public string MessageTemplate;
    }

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterHandler("rally_started", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                if (string.IsNullOrEmpty(payloadJson))
                {
                    Plugin.LogError("rally_started: Payload JSON is null or empty");
                    return;
                }

                var payload = JsonConvert.DeserializeObject<RallyStartedPayload>(payloadJson);
                if (payload == null)
                {
                    Plugin.LogError("rally_started: Failed to deserialize payload");
                    return;
                }

                StartRallyTimer(payload.id, payload.messageText, payload.elapsedTime);
            }
            catch (Exception e)
            {
                Plugin.LogError($"rally_started: Failed to parse payload: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("updatable_chat_remove", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                if (string.IsNullOrEmpty(payloadJson))
                {
                    return;
                }

                var payload = JsonConvert.DeserializeObject<UpdatableChatRemovePayload>(payloadJson);
                if (payload == null)
                {
                    return;
                }

                // Check if this is a rally timer we're tracking
                if (_activeTimers.ContainsKey(payload.id))
                {
                    var state = _activeTimers[payload.id];
                    if (state.Label != null)
                    {
                        state.Label.style.display = DisplayStyle.None;
                        Plugin.Log($"JuggleRallyTimer: Hid timer with id '{payload.id}'");
                    }
                    _activeTimers.Remove(payload.id);
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"JuggleRallyTimer updatable_chat_remove: Failed to parse payload: {e}");
            }
        });

        Plugin.Log("JuggleRallyTimer handlers registered");
    }

    private static void StartRallyTimer(string id, string messageTemplate, float elapsedTime)
    {
        var uiChat = UIChat.Instance;
        if (uiChat == null)
        {
            Plugin.LogError("JuggleRallyTimer: UIChat.Instance is null");
            return;
        }

        // Get the chatScrollView via reflection
        var chatScrollView = AccessTools.Field(typeof(UIChat), "chatScrollView").GetValue(uiChat) as ScrollView;
        if (chatScrollView == null)
        {
            Plugin.LogError("JuggleRallyTimer: Could not access chatScrollView");
            return;
        }

        // Format the initial timer display
        string timerText = FormatTime(elapsedTime);
        string message = messageTemplate.Replace("{{TIMER}}", timerText);

        // Add the message
        uiChat.AddChatMessage(message);

        // Grab the last added label
        int childCount = chatScrollView.childCount;
        if (childCount > 0)
        {
            var label = chatScrollView[childCount - 1] as Label;
            if (label != null)
            {
                _activeTimers[id] = new RallyTimerState
                {
                    Label = label,
                    StartTime = Time.time,
                    ElapsedAtStart = elapsedTime,
                    MessageTemplate = messageTemplate
                };
                Plugin.Log($"JuggleRallyTimer: Started timer with id '{id}', initial elapsed: {elapsedTime:F3}s");
            }
        }
    }

    private static string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        int milliseconds = (int)((seconds % 1f) * 1000f);

        if (minutes > 0)
        {
            return $"{minutes}:{secs:D2}.{milliseconds:D3}";
        }
        else
        {
            return $"{secs}.{milliseconds:D3}";
        }
    }

    // Called from UIChat.Update patch to update active timers
    public static void Update()
    {
        if (_activeTimers.Count == 0) return;

        float currentTime = Time.time;
        var toRemove = new List<string>();

        foreach (var kvp in _activeTimers)
        {
            string id = kvp.Key;
            var state = kvp.Value;

            // Check if label still exists
            if (state.Label == null || state.Label.panel == null)
            {
                toRemove.Add(id);
                continue;
            }

            // Calculate elapsed time since rally started on server
            float clientElapsedTime = state.ElapsedAtStart + (currentTime - state.StartTime);
            string timerText = FormatTime(clientElapsedTime);
            string message = state.MessageTemplate.Replace("{{TIMER}}", timerText);

            state.Label.text = message;
        }

        // Clean up invalid entries
        foreach (var id in toRemove)
        {
            _activeTimers.Remove(id);
            Plugin.Log($"JuggleRallyTimer: Removed timer with id '{id}'");
        }
    }

    // Call this when a rally timer message is removed to stop tracking it
    public static void RemoveTimer(string id)
    {
        if (_activeTimers.Remove(id))
        {
            Plugin.Log($"JuggleRallyTimer: Removed timer with id '{id}'");
        }
    }

    // Call this on disconnect to clean up
    public static void Clear()
    {
        _activeTimers.Clear();
        Plugin.Log("JuggleRallyTimer: Cleared all active timers");
    }

    // Patch UIChat.Update to drive our timer updates
    [HarmonyPatch(typeof(UIChat), "Update")]
    public class UIChatUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Update();
        }
    }

    [Serializable]
    public class RallyStartedPayload
    {
        public string id;
        public string messageText;
        public float elapsedTime;
    }

    [Serializable]
    public class UpdatableChatRemovePayload
    {
        public string id;
    }
}
