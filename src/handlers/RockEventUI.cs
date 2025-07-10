using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

public static class RockEventUI
{
    private static VisualElement _bossBarUIContainer; // Container for Flexbox
    private static VisualElement _bossBarUI;
    private static Label _bossTitleLabel;
    private static ProgressBar _bossProgressBar;
    private static VisualElement _progressBarProgressElement; // We'll get this via reflection
    private static bool isSetup = false;
    
    // For fade animation
    private static Coroutine _fadeCoroutine;
    
    // For progress bar animation
    private static Coroutine _bossProgressBarAnimationCoroutine;
    
    static readonly FieldInfo _uiHudField = typeof(UIHUDController)
        .GetField("uiHud",
            BindingFlags.Instance | BindingFlags.NonPublic);
    
    static readonly FieldInfo _uiHudContainerField = typeof(UIHUD)
        .GetField("container",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static FieldInfo _progressBarProgressField;
    
    private static void Setup(UIHUD uiHud)
    {
        VisualElement uiHudContainer = (VisualElement) _uiHudContainerField.GetValue(uiHud);
            

        CreateRockEventBossBar(uiHud, uiHudContainer);
        // Try to get the internal progress element using reflection
        // This is done after the ProgressBar has been added to the hierarchy,
        // as its internal elements might be created then.
        
        // LogVisualElementHierarchy(_bossProgressBar, "ProgressBar Hierarchy:");
        
        // If still not found, iterate through children
        _progressBarProgressElement = _bossProgressBar.Q<VisualElement>(className: "unity-progress-bar__progress");

        if (_progressBarProgressElement != null)
        {
            Plugin.Log("ProgressBar: Successfully found internal fill element via descendant query using class 'unity-progress-bar__progress'.");
            ApplyProgressBarCustomStyles();
        }
        else
        {
            Plugin.LogError(
                "ProgressBar: FAILED to find internal fill element with class 'unity-progress-bar__progress'."
            );
            // Always log the full hierarchy if it fails for further manual inspection.
            LogVisualElementHierarchy(_bossProgressBar, "FULL ProgressBar Hierarchy (DEBUG):");
        }


        EventManager em = EventManager.Instance;
        em.AddEventListener(
            "Event_OnClientDisconnected",
            new Action<Dictionary<string, object>>(
                (evt) =>
                {
                    // Stop any ongoing fade animation and immediately hide
                    if (_fadeCoroutine != null)
                    {
                        // Assuming Plugin.Instance is a MonoBehaviour
                        if (UIChat.Instance != null && UIChat.Instance is MonoBehaviour monoBehaviourInstance)
                        {
                            monoBehaviourInstance.StopCoroutine(_fadeCoroutine);
                        }
                        _fadeCoroutine = null;
                    }
                    if (_bossProgressBarAnimationCoroutine != null)
                    {
                        if (UIChat.Instance != null && UIChat.Instance is MonoBehaviour monoBehaviourInstance)
                        {
                            monoBehaviourInstance.StopCoroutine(_bossProgressBarAnimationCoroutine);
                        }
                        _bossProgressBarAnimationCoroutine = null;
                    }

                    _bossBarUIContainer.style.display = DisplayStyle.None;
                    _bossBarUIContainer.style.opacity = 0;
                }
            )
        );

        isSetup = true;
    }
    
    // Helper to log the hierarchy of a VisualElement (for debugging)
    private static void LogVisualElementHierarchy(VisualElement element, string prefix, int depth = 0)
    {
        string indent = new string(' ', depth * 2);
        // Corrected line: Use string.Join to convert ClassList (IEnumerable<string>) to a single string
        Plugin.Log($"{indent}{prefix} Name: {element.name}, Type: {element.GetType().Name}, ClassList: [{string.Join(", ", element.GetClasses().ToArray())}]");

        foreach (var child in element.Children())
        {
            LogVisualElementHierarchy(child, "Child:", depth + 1);
        }
    }
    
    // New method to apply styles to the internal progress element
    private static void ApplyProgressBarCustomStyles()
    {
        if (_progressBarProgressElement != null)
        {
            // Set the fill color of the progress bar
            // _progressBarProgressElement.style.backgroundColor = new StyleColor(new Color(79f / 255f, 0, 4f / 255f));
            // _progressBarProgressElement.style.color 
            _progressBarProgressElement.style.backgroundColor = new StyleColor(new Color(255f / 255f, 0f, 0f / 255f));

            // Apply rounded corners to the filled part
            _progressBarProgressElement.style.borderTopLeftRadius = 4;
            _progressBarProgressElement.style.borderTopRightRadius = 4;
            _progressBarProgressElement.style.borderBottomLeftRadius = 4;
            _progressBarProgressElement.style.borderBottomRightRadius = 4;

            // Ensure the progress element is visible if it was hidden by default style
            _progressBarProgressElement.style.display = DisplayStyle.Flex;
        }
    }
    
    // [HarmonyPatch(typeof(UIHUDController), "Event_OnPlayerBodySpawned")]
    // public static class UiHudControllerEventOnPlayerBodySpawnedPatch
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(UIHUDController __instance, Dictionary<string, object> message)
    //     {
    //         if (isSetup) return;
    //
    //         Setup(__instance);
    //         isSetup = true;
    //     }
    // }

    private static void CreateRockEventBossBar(UIHUD hud, VisualElement rootVisualElement)
    {
        if (rootVisualElement == null)
        {
            Plugin.LogError("Root VisualElement not found!");
            return;
        }
        
        VisualElement root = rootVisualElement.parent;
        // Create a CONTAINER VisualElement for Flexbox layout
        _bossBarUIContainer = new VisualElement();
        _bossBarUIContainer.name = "RockEventBossBarUIContainer";

        // Flexbox styles for the CONTAINER
        root.Add(_bossBarUIContainer);
        _bossBarUIContainer.style.position = Position.Absolute;
        _bossBarUIContainer.style.display = DisplayStyle.None; // Start hidden
        _bossBarUIContainer.style.bottom = 100; // Anchor to the bottom
        _bossBarUIContainer.style.left = 0;
        _bossBarUIContainer.style.right = 0;
        _bossBarUIContainer.style.opacity = 0; // Start with 0 opacity for fading
        
        _bossBarUI = new VisualElement();
        _bossBarUI.style.display = DisplayStyle.Flex;
        _bossBarUI.style.position = Position.Relative;
        _bossBarUI.style.flexDirection = FlexDirection.Column;
        _bossBarUI.style.alignItems = Align.Center;
        _bossBarUIContainer.Add(_bossBarUI);
        
        _bossTitleLabel = new Label("Boss Name Label"); // Initial text
        _bossTitleLabel.text = "Boss Name Here";
        _bossTitleLabel.style.fontSize = 16;
        _bossTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _bossTitleLabel.style.marginBottom = 4;
        _bossTitleLabel.name = "Boss Name Label"; // Useful for styling in USS
        _bossTitleLabel.style.color = Color.red; 
        _bossBarUI.Add(_bossTitleLabel);
        
        _bossProgressBar = new ProgressBar();
        _bossProgressBar.style.width = 400;
        _bossProgressBar.style.height = 14;
        _bossProgressBar.style.color = Color.red; // TODO this did not change the progress bar's color
        _bossProgressBar.style.backgroundColor = new StyleColor(new Color(79f / 255f, 0, 4f / 255f));
        _bossProgressBar.lowValue = 0f;
        _bossProgressBar.highValue = 100f;
        _bossProgressBar.value = 60f;
        _bossProgressBar.style.borderBottomColor = new StyleColor(new Color(0, 0, 0, 0.5f));
        _bossProgressBar.style.borderTopColor = new StyleColor(new Color(0, 0, 0, 0.5f));
        _bossProgressBar.style.borderLeftColor = new StyleColor(new Color(0, 0, 0, 0.5f));
        _bossProgressBar.style.borderRightColor = new StyleColor(new Color(0, 0, 0, 0.5f));
        _bossProgressBar.style.borderBottomWidth = 2;
        _bossProgressBar.style.borderTopWidth = 2;
        _bossProgressBar.style.borderLeftWidth = 2;
        _bossProgressBar.style.borderRightWidth = 2;
        _bossProgressBar.style.borderTopLeftRadius = 6;
        _bossProgressBar.style.borderTopRightRadius = 6;
        _bossProgressBar.style.borderBottomLeftRadius = 6;
        _bossProgressBar.style.borderBottomRightRadius = 6;
        // TODO the progress bar itself does not have the rounded corners, just the border
        // TODO is it possible to have changes to the progress bar value be animated visually between the old value and new value?
        _bossBarUI.Add(_bossProgressBar);
    }
    
    // Helper to start a fade coroutine. Assumes you have a MonoBehaviour to run this from.
    private static void StartFadeCoroutine(
        float startOpacity,
        float endOpacity,
        float delay = 0f,
        float duration = 1f
    )
    {
        if (UIChat.Instance == null || !(UIChat.Instance is MonoBehaviour monoBehaviour))
        {
            Plugin.LogError("Cannot start fade coroutine: No MonoBehaviour instance found via UIChat.Instance.");
            return;
        }

        // Stop any existing fade coroutine
        if (_fadeCoroutine != null)
        {
            monoBehaviour.StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        // Start new coroutine
        _fadeCoroutine = monoBehaviour.StartCoroutine(
            FadeAnimation(startOpacity, endOpacity, delay, duration)
        );
    }

    private static IEnumerator FadeAnimation(
        float startOpacity,
        float endOpacity,
        float delay,
        float duration
    )
    {
        yield return new WaitForSeconds(delay); // Wait for the delay

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float currentOpacity = Mathf.Lerp(startOpacity, endOpacity, progress);
            _bossBarUIContainer.style.opacity = currentOpacity;
            yield return null;
        }
        _bossBarUIContainer.style.opacity = endOpacity;

        // If fading out completely, hide the display style after animation
        if (Mathf.Approximately(endOpacity, 0f))
        {
            _bossBarUIContainer.style.display = DisplayStyle.None;
        }
        _fadeCoroutine = null; // Mark coroutine as finished
    }

    // Coroutine for animating the progress bar value
    private static IEnumerator AnimateProgressBar(
        float startValue,
        float endValue,
        float duration = 0.1f // Duration for health bar animation
    )
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            _bossProgressBar.value = Mathf.Lerp(startValue, endValue, progress);
            ApplyProgressBarCustomStyles();
            yield return null;
        }
        _bossProgressBar.value = endValue; // Ensure it ends at the exact target value
        ApplyProgressBarCustomStyles(); // Re-apply styles one last time after animation finishes
        _bossProgressBarAnimationCoroutine = null; // Mark coroutine as finished
    }

    // now argument is used for player leaving
    public static void Hide(bool now = false)
    {
        if (!isSetup) return;
        if (_bossBarUIContainer == null) return;
        
        if (_bossBarUIContainer.style.display == DisplayStyle.None &&
            _bossBarUIContainer.style.opacity == 0) return; // Already hidden

        // Stop any ongoing fade-in or update animation before fading out
        if (UIChat.Instance != null && UIChat.Instance is MonoBehaviour monoBehaviourInstance)
        {
            if (_fadeCoroutine != null)
            {
                monoBehaviourInstance.StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
            if (_bossProgressBarAnimationCoroutine != null)
            {
                // monoBehaviourInstance.StopCoroutine(_bossProgressBarAnimationCoroutine); // TODO added this to make it so we get the last tick of damage on bar
                _bossProgressBarAnimationCoroutine = null;
            }
        }
        
        // Start fade out: 2 seconds duration
        StartFadeCoroutine(_bossBarUIContainer.style.opacity.value, 0f, now ? 0f : 1f, now ? 0f : 1f);
    }

    public static void ShowOrUpdateUI(int bossMaxHealth, int bossHealth, bool displayNow)
    {
        if (!isSetup) Setup(UIManager.Instance.Hud);
        
        // If currently hidden, initiate fade-in
        if (_bossBarUIContainer.style.display == DisplayStyle.None ||
            _bossBarUIContainer.style.opacity.value < 1f)
        {
            _bossBarUIContainer.style.display = DisplayStyle.Flex;
            // Start fade-in: 4-second delay, then 2-second fade
            StartFadeCoroutine(_bossBarUIContainer.style.opacity.value, 1f, displayNow ? 0f : 5f, 2f);
        }

        // Animate progress bar if value changes
        if (!Mathf.Approximately(_bossProgressBar.highValue, bossMaxHealth))
        {
            _bossProgressBar.highValue = bossMaxHealth;
        }

        // Animate the progress bar value change
        if (!Mathf.Approximately(_bossProgressBar.value, bossHealth))
        {
            if (UIChat.Instance != null && UIChat.Instance is MonoBehaviour monoBehaviour)
            {
                // Stop any previous progress bar animation to start a new one
                if (_bossProgressBarAnimationCoroutine != null)
                {
                    monoBehaviour.StopCoroutine(_bossProgressBarAnimationCoroutine);
                }
                _bossProgressBarAnimationCoroutine = monoBehaviour.StartCoroutine(
                    AnimateProgressBar(_bossProgressBar.value, bossHealth)
                );
            }
        }
    }

    public static void SetBossName(string bossName)
    {
        if (_bossTitleLabel == null)
        {
            Plugin.LogError($"_bossTitleLabel is null");
            return;
        }
        _bossTitleLabel.text = bossName;
    }
}