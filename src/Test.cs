using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ToastersRinkCompanion;

public static class Test
{
    // private static List<GameObject> goals = new List<GameObject>();
    //
    // [HarmonyPatch(typeof(UIChat), nameof(UIChat.Client_SendClientChatMessage))]
    // public static class UIChatClientSendClientChatMessage
    // {
    //     [HarmonyPrefix]
    //     public static bool Prefix(UIChat __instance, string message, bool useTeamChat)
    //     {
    //         if (message.Equals("/test"))
    //         {
    //             DoStuff();
    //             return false;
    //         }
    //
    //         return true;
    //     }
    // }

    // public static void DoStuff()
    // {
    //     Plugin.Log($"LoadLevel1Scene Postfix");
    //     // Find all GameObjects in the scene
    //     Object[] allObjects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject));
    //
    //     // Iterate through all objects
    //     foreach (var obj in allObjects)
    //     {
    //         // Try to cast the object to a GameObject
    //         GameObject gameObject = obj as GameObject;
    //         if (gameObject == null || gameObject.transform == null)
    //         {
    //             continue;
    //         }
    //
    //         // If it's a root object, write its hierarchy
    //         if (gameObject.transform.parent == null)
    //         {
    //             WriteGameObjectHierarchyToFile(gameObject);
    //         }
    //     }
    //
    //     Plugin.Log($"changing scale of {goals.Count} goals");
    //     foreach (GameObject goal in goals)
    //     {
    //         goal.transform.localScale = new Vector3(3, 3, 3);
    //     }
    //     Plugin.Log($"done");
    // }
    //
    // private static void WriteGameObjectHierarchyToFile(GameObject obj)
    // {
    //     if (obj == null || obj.transform == null)
    //     {
    //         return;
    //     }
    //
    //     if (obj.isStatic)
    //     {
    //         obj.isStatic = false;
    //         Plugin.Log($"Made {obj.name} not static anymore");
    //     }
    //
    //     if (obj.name.Equals("Goal Blue") || obj.name.Equals("Goal Red"))
    //     {
    //         goals.Add(obj);
    //     }
    //
    //         
    //     // Recursively write all children
    //     foreach (var childObj in obj.transform)
    //     {
    //         if (childObj is Transform childTransform && childTransform.gameObject != null)
    //         {
    //             WriteGameObjectHierarchyToFile(childTransform.gameObject);
    //         }
    //     }
    // }
}