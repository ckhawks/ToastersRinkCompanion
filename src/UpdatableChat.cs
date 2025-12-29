using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion;

public static class UpdatableChat
{
    private const string THINKING_PLACEHOLDER = "{{THINKING}}";
    private const float ANIMATION_INTERVAL = 0.4f;

    // Map message IDs to their Label elements
    private static readonly Dictionary<string, Label> _messageLabels = new Dictionary<string, Label>();

    // Track messages that are animating: id -> (templateText, lastUpdateTime, dotState)
    private static readonly Dictionary<string, ThinkingState> _thinkingMessages = new Dictionary<string, ThinkingState>();

    private class ThinkingState
    {
        public string TemplateText;
        public float LastUpdateTime;
        public int DotState; // 0-3 for "", ".", "..", "..."
    }

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterHandler("updatable_chat_create", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                if (string.IsNullOrEmpty(payloadJson))
                {
                    Plugin.LogError("updatable_chat_create: Payload JSON is null or empty");
                    return;
                }

                var payload = JsonConvert.DeserializeObject<UpdatableChatCreatePayload>(payloadJson);
                if (payload == null)
                {
                    Plugin.LogError("updatable_chat_create: Failed to deserialize payload");
                    return;
                }

                CreateUpdatableMessage(payload.id, payload.text);
            }
            catch (Exception e)
            {
                Plugin.LogError($"updatable_chat_create: Failed to parse payload: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("updatable_chat_update", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                if (string.IsNullOrEmpty(payloadJson))
                {
                    Plugin.LogError("updatable_chat_update: Payload JSON is null or empty");
                    return;
                }

                var payload = JsonConvert.DeserializeObject<UpdatableChatUpdatePayload>(payloadJson);
                if (payload == null)
                {
                    Plugin.LogError("updatable_chat_update: Failed to deserialize payload");
                    return;
                }

                UpdateMessage(payload.id, payload.text);
            }
            catch (Exception e)
            {
                Plugin.LogError($"updatable_chat_update: Failed to parse payload: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("updatable_chat_remove", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                if (string.IsNullOrEmpty(payloadJson))
                {
                    Plugin.LogError("updatable_chat_remove: Payload JSON is null or empty");
                    return;
                }

                var payload = JsonConvert.DeserializeObject<UpdatableChatRemovePayload>(payloadJson);
                if (payload == null)
                {
                    Plugin.LogError("updatable_chat_remove: Failed to deserialize payload");
                    return;
                }

                RemoveMessage(payload.id);
            }
            catch (Exception e)
            {
                Plugin.LogError($"updatable_chat_remove: Failed to parse payload: {e}");
            }
        });

        Plugin.Log("UpdatableChat handlers registered");
    }

    private static void CreateUpdatableMessage(string id, string text)
    {
        var uiChat = UIChat.Instance;
        if (uiChat == null)
        {
            Plugin.LogError("UpdatableChat: UIChat.Instance is null");
            return;
        }

        // Get the chatScrollView via reflection
        var chatScrollView = AccessTools.Field(typeof(UIChat), "chatScrollView").GetValue(uiChat) as ScrollView;
        if (chatScrollView == null)
        {
            Plugin.LogError("UpdatableChat: Could not access chatScrollView");
            return;
        }

        // Check if this message has the thinking placeholder
        bool isThinking = text.Contains(THINKING_PLACEHOLDER);
        string displayText = isThinking ? text.Replace(THINKING_PLACEHOLDER, "<color=orange><i>Thinking</i></color>") : text;

        // Add the message
        uiChat.AddChatMessage(displayText);

        // Grab the last added label
        int childCount = chatScrollView.childCount;
        if (childCount > 0)
        {
            var label = chatScrollView[childCount - 1] as Label;
            if (label != null)
            {
                _messageLabels[id] = label;
                Plugin.Log($"UpdatableChat: Created message with id '{id}'");

                // Start animation if this is a thinking message
                if (isThinking)
                {
                    _thinkingMessages[id] = new ThinkingState
                    {
                        TemplateText = text,
                        LastUpdateTime = Time.time,
                        DotState = 0
                    };
                    Plugin.Log($"UpdatableChat: Started thinking animation for id '{id}'");
                }
            }
        }
    }

    private static void UpdateMessage(string id, string text)
    {
        // Stop any thinking animation for this message
        if (_thinkingMessages.ContainsKey(id))
        {
            _thinkingMessages.Remove(id);
            Plugin.Log($"UpdatableChat: Stopped thinking animation for id '{id}'");
        }

        if (!_messageLabels.TryGetValue(id, out var label))
        {
            Plugin.Log($"UpdatableChat: No message found with id '{id}' (may have been removed from history)");
            return;
        }

        // Check if label is still valid and in the visual tree
        if (label == null || label.panel == null)
        {
            Plugin.Log($"UpdatableChat: Label for id '{id}' is no longer valid, removing from tracking");
            _messageLabels.Remove(id);
            return;
        }

        // Check if the new text also has thinking placeholder (unlikely but handle it)
        if (text.Contains(THINKING_PLACEHOLDER))
        {
            _thinkingMessages[id] = new ThinkingState
            {
                TemplateText = text,
                LastUpdateTime = Time.time,
                DotState = 0
            };
            text = text.Replace(THINKING_PLACEHOLDER, "<color=orange><i>Thinking</i></color>");
        }

        label.text = text;

        // Fade the chat back in so the update is visible
        ShowSingleChatMessage(label);
    }

    private static void ShowSingleChatMessage(Label label)
    {
        var uiChat = UIChat.Instance;
        if (uiChat == null || label == null) return;

        // Get the chatMessages dictionary via reflection
        var chatMessagesField = AccessTools.Field(typeof(UIChat), "chatMessages");
        if (chatMessagesField == null) return;

        var chatMessages = chatMessagesField.GetValue(uiChat) as System.Collections.IDictionary;
        if (chatMessages == null) return;

        // Find the ChatMessage by matching its MessageLabel field
        // (The dictionary keys are orphaned TemplateContainers, not in the visual tree)
        object chatMessage = null;
        foreach (var value in chatMessages.Values)
        {
            var messageLabelField = AccessTools.Field(value.GetType(), "MessageLabel");
            if (messageLabelField != null)
            {
                var messageLabel = messageLabelField.GetValue(value) as Label;
                if (messageLabel == label)
                {
                    chatMessage = value;
                    break;
                }
            }
        }

        if (chatMessage == null) return;

        var chatMessageType = chatMessage.GetType();

        // Reset CreateTime so RemainingFadeTime resets to 15 seconds
        var createTimeField = AccessTools.Field(chatMessageType, "CreateTime");
        createTimeField?.SetValue(chatMessage, Time.time);

        // Set IsVisible to false so Show() actually runs (it returns early if already visible)
        var isVisibleField = AccessTools.Field(chatMessageType, "IsVisible");
        isVisibleField?.SetValue(chatMessage, false);

        // Call Show(0f, true) to make it visible and auto-hide after fade time
        var showMethod = AccessTools.Method(chatMessageType, "Show");
        showMethod?.Invoke(chatMessage, new object[] { 0f, true });
    }

    private static void RemoveMessage(string id)
    {
        // Stop any thinking animation
        if (_thinkingMessages.ContainsKey(id))
        {
            _thinkingMessages.Remove(id);
        }

        if (!_messageLabels.TryGetValue(id, out var label))
        {
            Plugin.Log($"UpdatableChat: No message found with id '{id}' to remove");
            return;
        }

        // Check if label is still valid
        if (label != null && label.panel != null)
        {
            // Hide the label
            label.style.display = DisplayStyle.None;
            Plugin.Log($"UpdatableChat: Removed/hid message with id '{id}'");
        }

        _messageLabels.Remove(id);
    }

    // Called from UIChat.Update patch to animate thinking messages
    public static void Update()
    {
        if (_thinkingMessages.Count == 0) return;

        float currentTime = Time.time;
        var toRemove = new List<string>();

        foreach (var kvp in _thinkingMessages)
        {
            string id = kvp.Key;
            var state = kvp.Value;

            // Check if enough time has passed for next animation frame
            if (currentTime - state.LastUpdateTime < ANIMATION_INTERVAL) continue;

            // Check if label still exists
            if (!_messageLabels.TryGetValue(id, out var label) || label == null || label.panel == null)
            {
                toRemove.Add(id);
                continue;
            }

            // Advance dot state
            state.DotState = (state.DotState + 1) % 4;
            state.LastUpdateTime = currentTime;

            // Generate dots string
            string dots = state.DotState switch
            {
                0 => "",
                1 => ".",
                2 => "..",
                3 => "...",
                _ => ""
            };

            // Update the label with animated text
            string animatedText = state.TemplateText.Replace(THINKING_PLACEHOLDER, $"<color=orange><i>Thinking{dots}</i></color>");
            label.text = animatedText;
        }

        // Clean up invalid entries
        foreach (var id in toRemove)
        {
            _thinkingMessages.Remove(id);
            _messageLabels.Remove(id);
        }
    }

    // Call this on disconnect to clean up
    public static void Clear()
    {
        _messageLabels.Clear();
        _thinkingMessages.Clear();
        Plugin.Log("UpdatableChat: Cleared all tracked messages");
    }

    // Patch UIChat.Update to drive our animations
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
    public class UpdatableChatCreatePayload
    {
        public string id;
        public string text;
    }

    [Serializable]
    public class UpdatableChatUpdatePayload
    {
        public string id;
        public string text;
    }

    [Serializable]
    public class UpdatableChatRemovePayload
    {
        public string id;
    }
}
