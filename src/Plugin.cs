// Plugin.cs

using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ToastersRinkCompanion;

public class Plugin : IPuckMod
{
    public static string MOD_NAME = "ToastersRinkCompanion";
    public static string MOD_VERSION = "1.5.1";
    // public static string TRS_VERSION = "Build 1054";
    public static string MOD_GUID = "pw.stellaric.toaster.rinkcompanion";

    static readonly Harmony harmony = new Harmony(MOD_GUID);
    
    public static InputAction spawnPuckAction;
    public static ModSettings modSettings;

    public bool OnEnable()
    {
        Plugin.Log($"Enabling...");
        try
        {
            if (IsDedicatedServer())
            {
                Plugin.Log("Environment: dedicated server.");
                Plugin.Log($"This is only meant to be used on clients!");
            }
            else
            {
                Plugin.Log("Environment: client.");
                Plugin.Log("Patching methods...");
                harmony.PatchAll();
                Plugin.Log($"All patched! Patched methods:");
                LogAllPatchedMethods();
                MessagingHandler.Setup();
                modSettings = ModSettings.Load();
                modSettings.Save(); // So that it writes any missing config values immediately
                spawnPuckAction = new InputAction(binding: modSettings.spawnPuckKeybind);
                spawnPuckAction.Enable();
                Plugin.Log($"Fully setup!");
            }
            
            Plugin.Log($"Enabled!");
            return true;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to Enable: {e.Message}!");
            return false;
        }
    }

    public bool OnDisable()
    {
        try
        {
            Plugin.Log($"Disabling...");
            harmony.UnpatchSelf();
            Plugin.Log($"Disabled! Goodbye!");
            return true;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Failed to disable: {e.Message}!");
            return false;
        }
    }

    public static bool IsDedicatedServer()
    {
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
    }

    public static void LogAllPatchedMethods()
    {
        var allPatchedMethods = harmony.GetPatchedMethods();
        var pluginId  = harmony.Id;

        var mine = allPatchedMethods
            .Select(m => new { method = m, info = Harmony.GetPatchInfo(m) })
            .Where(x =>
                // could be prefix, postfix, transpiler or finalizer
                x.info.Prefixes.  Any(p => p.owner == pluginId) ||
                x.info.Postfixes. Any(p => p.owner == pluginId) ||
                x.info.Transpilers.Any(p => p.owner == pluginId) ||
                x.info.Finalizers.Any(p => p.owner == pluginId)
            )
            .Select(x => x.method);

        foreach (var m in mine)
            Plugin.Log($" - {m.DeclaringType.FullName}.{m.Name}");
    }
    
    public static void Log(string message)
    {
        Debug.Log($"[{MOD_NAME}] {message}");
    }

    public static void LogError(string message)
    {
        Debug.LogError($"[{MOD_NAME}] {message}");
    }
    
    public static void LogWarning(string message)
    {
        Debug.LogWarning($"[{MOD_NAME}] {message}");
    }
}