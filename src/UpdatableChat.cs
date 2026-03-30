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
        var uiChat = MonoBehaviourSingleton<UIManager>.Instance.Chat;
        if (uiChat == null)
        {
            Plugin.LogError("UpdatableChat: MonoBehaviourSingleton<UIManager>.Instance.Chat is null");
            return;
        }

        // Check if this message has the thinking placeholder
        bool isThinking = text.Contains(THINKING_PLACEHOLDER);
        string displayText = isThinking ? text.Replace(THINKING_PLACEHOLDER, "<color=orange><i>Thinking</i></color>") : text;

        // Add the message
        uiChat.AddChatMessage(new ChatMessage { Content = displayText, IsSystem = true }, Units.Metric, false);

        // Play notification sound
        MonoBehaviourSingleton<UIManager>.Instance.PlayNotificationSound();

        // Grab the last added UIChatMessage's label via the uiChatMessages list
        var uiChatMessages = AccessTools.Field(typeof(UIChat), "uiChatMessages")?.GetValue(uiChat) as System.Collections.IList;
        if (uiChatMessages != null && uiChatMessages.Count > 0)
        {
            var lastMessage = uiChatMessages[uiChatMessages.Count - 1];
            var labelField = AccessTools.Field(lastMessage.GetType(), "label");
            var label = labelField?.GetValue(lastMessage) as Label;
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
            else
            {
                Plugin.LogError("UpdatableChat: Could not access label from UIChatMessage");
            }
        }
        else
        {
            Plugin.LogError("UpdatableChat: Could not access uiChatMessages list");
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
        var uiChat = MonoBehaviourSingleton<UIManager>.Instance.Chat;
        if (uiChat == null || label == null) return;

        // Find the UIChatMessage that owns this label
        var uiChatMessages = AccessTools.Field(typeof(UIChat), "uiChatMessages")?.GetValue(uiChat) as System.Collections.IList;
        if (uiChatMessages == null) return;

        object targetMessage = null;
        var labelField = typeof(UIChatMessage).GetField("label",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        foreach (var msg in uiChatMessages)
        {
            var msgLabel = labelField?.GetValue(msg) as Label;
            if (msgLabel == label)
            {
                targetMessage = msg;
                break;
            }
        }

        if (targetMessage == null) return;

        // Update the ChatMessage timestamp so the expiry timer resets
        var chatMsgField = AccessTools.Field(typeof(UIChatMessage), "ChatMessage");
        if (chatMsgField != null)
        {
            var chatMsg = (ChatMessage)chatMsgField.GetValue(targetMessage);
            chatMsg.Timestamp = Utils.GetTimestamp();
            chatMsgField.SetValue(targetMessage, chatMsg);
        }

        // Call Focus() to make it visible again (removes "blurred" class)
        var focusMethod = AccessTools.Method(typeof(UIChatMessage), "Focus");
        focusMethod?.Invoke(targetMessage, null);

        // Restart the expiry tween with the new timestamp
        var startExpiryMethod = AccessTools.Method(typeof(UIChatMessage), "StartExpiryTween");
        startExpiryMethod?.Invoke(targetMessage, null);
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

    // Patch UIManager.Update to drive our animations (UIChat no longer has Update in b312)
    [HarmonyPatch(typeof(UIManager), "Update")]
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
