using UnityEngine;

namespace ToastersRinkCompanion;

// Thin wrapper over Puck's built-in toast system (UIManager.ToastManager). The native
// toasts are game-styled and live on the persistent UI document, so they survive a
// connection change (useful during a server migration). Note: the game uppercases toast
// content internally.
public static class Toast
{
    public static void Show(string message, float hideDelaySeconds = 4f, string name = "tr-toast")
    {
        if (Application.isBatchMode) return;

        var uiManager = MonoBehaviourSingleton<UIManager>.Instance;
        if (uiManager == null || uiManager.ToastManager == null)
        {
            Plugin.LogError("Cannot show toast, UIManager/ToastManager is null.");
            return;
        }

        uiManager.ToastManager.ShowToast(name, message, hideDelaySeconds);
    }
}
