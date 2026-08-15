// Plugin.cs

using System;
using System.Linq;
using HarmonyLib;
using ToastersRinkCompanion.handlers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ToastersRinkCompanion;

public class Plugin : IPuckPlugin
{
    public static string MOD_NAME = "ToastersRinkCompanion";
    // 2.0.4: built against the B1235 assemblies. The version is the compatibility
    // contract with Suite, not a feature marker — B1235 moved the object-synchronization
    // and replay surfaces enough that a client built against one game build should not be
    // treated as interchangeable with one built against the other, even though this
    // release carries no source changes of its own.
    public static string MOD_VERSION = "2.0.4";
    public static string MOD_GUID = "pw.stellaric.toaster.rinkcompanion";

    static readonly Harmony harmony = new Harmony(MOD_GUID);
    
    public static InputAction spawnPuckAction;
    public static InputAction voteYesAction;
    public static InputAction voteNoAction;
    public static InputAction panelAction;
    public static InputAction drillSaveAction;
    public static InputAction drillLoadAction;
    public static InputAction openCaseAction;
    public static InputAction puckBlockAction;
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
                WarnIfLoadedTwice();
                Plugin.Log("Patching methods...");
                int patchedCount = 0;
                int failedCount = 0;
                foreach (var type in typeof(Plugin).Assembly.GetTypes())
                {
                    if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0 ||
                        type.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                            .Any(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0))
                    {
                        try
                        {
                            harmony.PatchAll(type);
                            patchedCount++;
                        }
                        catch (Exception e)
                        {
                            failedCount++;
                            LogError($"Failed to patch {type.FullName}: {e.Message}");
                        }
                    }
                }
                Plugin.Log($"Patching complete: {patchedCount} succeeded, {failedCount} failed.");
                AIGoalieFilter.Setup();
                AIGoalieFilter.TryPatchTRLHeadColors(harmony);
                Plugin.Log($"Patched methods:");
                LogAllPatchedMethods();
                MessagingHandler.Setup();

                // Preload collectibles asset bundle + case/particle/text prefabs at boot so
                // the first case-open broadcast doesn't pay ~4.7 MB of synchronous asset loads
                // on the main thread mid-game. Per-item prefabs (LoadPrefab(itemName)) still
                // load lazily since the full item list isn't known here.
                try { ToastersRinkCompanion.collectibles.CollectiblePrefabs.Setup(); }
                catch (Exception e) { Plugin.LogError($"CollectiblePrefabs.Setup failed: {e.Message}"); }

                modSettings = ModSettings.Load();
                modSettings.Save(); // So that it writes any missing config values immediately

                RecreateAction(ref spawnPuckAction, modSettings.spawnPuckKeybind);
                RecreateAction(ref voteYesAction,   modSettings.voteYesKeybind);
                RecreateAction(ref voteNoAction,    modSettings.voteNoKeybind);
                RecreateAction(ref panelAction,     modSettings.panelKeybind);
                RecreateAction(ref drillSaveAction, modSettings.drillSaveKeybind);
                RecreateAction(ref drillLoadAction, modSettings.drillLoadKeybind);
                RecreateAction(ref openCaseAction,  modSettings.openCaseKeybind);
                RecreateAction(ref puckBlockAction, modSettings.puckBlockKeybind);

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

            // Tear down the messaging lifecycle and input so nothing dangles or
            // double-registers if the plugin is re-enabled in the same session.
            try { MessagingHandler.Teardown(); } catch (Exception e) { LogError($"MessagingHandler teardown failed: {e.Message}"); }
            try { JsonMessageRouter.Shutdown(); } catch (Exception e) { LogError($"JsonMessageRouter shutdown failed: {e.Message}"); }

            DisposeAction(ref spawnPuckAction);
            DisposeAction(ref voteYesAction);
            DisposeAction(ref voteNoAction);
            DisposeAction(ref panelAction);
            DisposeAction(ref drillSaveAction);
            DisposeAction(ref drillLoadAction);
            DisposeAction(ref openCaseAction);
            DisposeAction(ref puckBlockAction);

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
    
    /// <summary>
    /// Detects a second copy of Companion loaded into the same process — the usual cause
    /// being a Workshop subscription and a hand-placed copy in Plugins/ at the same time,
    /// especially when a server lists Companion with isClientRequired and force-loads the
    /// Workshop copy on top of a sideloaded one.
    ///
    /// This has to be found here rather than server-side. Netcode's
    /// CustomMessagingManager.RegisterNamedMessageHandler assigns into a dictionary, so the
    /// second copy's handler silently overwrites the first and only one copy ever answers
    /// the server's greeting — the server sees exactly one companion_hello and cannot tell.
    /// Harmony patches do stack though, so both copies patch PlayerInput.Update and every
    /// keybind fires twice: one F3 press opens the panel and closes it again in the same
    /// frame, which reads as "the menu doesn't work".
    /// </summary>
    private static void WarnIfLoadedTwice()
    {
        try
        {
            string self = typeof(Plugin).Assembly.GetName().Name;
            int copies = 0;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == self) copies++;
            }
            if (copies <= 1) return;

            LogError($"Companion is loaded {copies} times in this process. Keybinds and patches " +
                     "will fire once per copy, so the F3 menu will open and immediately close. " +
                     "Keep either the Steam Workshop subscription or a copy in Puck/Plugins, not both.");
        }
        catch (Exception e)
        {
            // Never let a diagnostic stop the mod from loading.
            LogError($"duplicate-load check failed: {e.Message}");
        }
    }

    public static void RecreateAction(ref InputAction action, string binding)
    {
        action?.Disable();
        action = new InputAction(binding: binding);
        action.Enable();
    }

    public static void DisposeAction(ref InputAction action)
    {
        action?.Disable();
        action?.Dispose();
        action = null;
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

    public static void AddLocalChatMessage(string content)
    {
        NetworkBehaviourSingleton<ChatManager>.Instance.AddChatMessage(new ChatMessage
        {
            Content = content,
            IsSystem = true,
            Timestamp = Utils.GetTimestamp()
        });
    }
}