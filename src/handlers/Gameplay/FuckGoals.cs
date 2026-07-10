using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class FuckGoals
{
    private static GameObject goalFrameBluePrefab;
    private static GameObject goalFrameRedPrefab;
    private static AssetBundle _loadedAssetBundle;

    // Track spawned frames and disabled renderers so we can clean them up
    private static readonly Dictionary<Goal, List<GameObject>> SpawnedFrames = new();
    private static readonly Dictionary<Goal, List<MeshRenderer>> DisabledRenderers = new();

    /// <summary>True when goals have non-default position or scale (set by goalpositions handler).</summary>
    public static bool GoalsAreNonDefault { get; private set; }

    private static void LoadPrefabRed()
    {
        if (goalFrameRedPrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/goalframes");
        goalFrameRedPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/collectibles/goalframered.prefab");
    }
    
    private static void LoadPrefabBlue()
    {
        if (goalFrameBluePrefab != null) return; // Don't reload it
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/goalframes");
        goalFrameBluePrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/collectibles/goalframeblue.prefab");
    }

    private static void ApplyCustomFrame(Goal goal)
    {
        bool isRed = goal.gameObject.name.Contains("Red");

        // Load prefabs if not already loaded
        LoadPrefabRed();
        LoadPrefabBlue();

        // Clean up any previously spawned frames for this goal
        CleanupCustomFrame(goal);

        // Disable the goal's currently-enabled mesh renderers and track them. B1149's goal
        // remodel nested the visual frame mesh below the goal root (it used to sit on direct
        // children), so search the whole hierarchy instead of just immediate children. The net
        // is a SkinnedMeshRenderer, so it is not matched here and stays visible as before.
        List<MeshRenderer> disabledRenderers = new();
        foreach (MeshRenderer renderer in goal.gameObject.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer != null && renderer.enabled)  // Only disable if currently enabled
            {
                renderer.enabled = false;
                disabledRenderers.Add(renderer);
            }
        }
        DisabledRenderers[goal] = disabledRenderers;

        // Instantiate custom frame
        GameObject newFrame = Object.Instantiate(isRed ? goalFrameRedPrefab : goalFrameBluePrefab, goal.transform);

        // Fix shaders
        MeshRenderer[] renderers = newFrame.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            }
        }

        // Configure frame transform. The Y is swapped (red 0 / blue 180) versus pre-B1149 to
        // cancel the goal roots' 180deg orientation flip from the B1149 goal remodel.
        newFrame.transform.localPosition = Vector3.zero;
        newFrame.transform.localRotation = Quaternion.Euler(-90f, isRed ? 0 : 180, 0);
        newFrame.transform.localScale = new Vector3(92, 100, 92);

        // Track the spawned frame
        if (!SpawnedFrames.ContainsKey(goal))
        {
            SpawnedFrames[goal] = new List<GameObject>();
        }
        SpawnedFrames[goal].Add(newFrame);

        Plugin.Log($"Applied custom frame to {goal.gameObject.name}");
    }

    private static void CleanupCustomFrame(Goal goal)
    {
        // Destroy spawned frames
        if (SpawnedFrames.TryGetValue(goal, out var frames))
        {
            foreach (GameObject frame in frames)
            {
                if (frame != null)
                {
                    Object.Destroy(frame);
                }
            }
            SpawnedFrames.Remove(goal);
        }

        // Re-enable original renderers
        if (DisabledRenderers.TryGetValue(goal, out var renderers))
        {
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            DisabledRenderers.Remove(goal);
        }

        Plugin.Log($"Cleaned up custom frame for {goal.gameObject.name}");
    }

    public static void CleanupAllCustomFrames()
    {
        GoalsAreNonDefault = false;
        Goal[] goals = Object.FindObjectsByType<Goal>(FindObjectsSortMode.None);
        foreach (Goal goal in goals)
        {
            CleanupCustomFrame(goal);
        }
    }

    public static void Initialize()
    {
        // Register handler for goal position updates from the server
        JsonMessageRouter.RegisterHandler("goalpositions", HandleGoalPositionUpdate);
    }

    private static void HandleGoalPositionUpdate(ulong senderClientId, string payloadJson)
    {
        try
        {
            GoalPositionPayload payload = Newtonsoft.Json.JsonConvert.DeserializeObject<GoalPositionPayload>(payloadJson);

            // Convert payload once before the loop
            Vector3 redPos = new Vector3(payload.redPosition.x, payload.redPosition.y, payload.redPosition.z);
            Vector3 redScl = new Vector3(payload.redScale.x, payload.redScale.y, payload.redScale.z);
            Quaternion redRot = new Quaternion(payload.redRotation.x, payload.redRotation.y, payload.redRotation.z, payload.redRotation.w);

            Vector3 bluePos = new Vector3(payload.bluePosition.x, payload.bluePosition.y, payload.bluePosition.z);
            Vector3 blueScl = new Vector3(payload.blueScale.x, payload.blueScale.y, payload.blueScale.z);
            Quaternion blueRot = new Quaternion(payload.blueRotation.x, payload.blueRotation.y, payload.blueRotation.z, payload.blueRotation.w);

            GoalsAreNonDefault = payload.modified;

            Plugin.Log($"Goal positions updated. NonDefault={GoalsAreNonDefault} redPos={redPos} bluePos={bluePos} redScl={redScl} blueScl={blueScl}");

            Goal[] goals = Object.FindObjectsByType<Goal>(FindObjectsSortMode.None);

            foreach (Goal goal in goals)
            {
                bool isRed = goal.gameObject.name.Contains("Red");

                // Apply position, rotation, and scale
                if (isRed)
                {
                    goal.transform.position = redPos;
                    goal.transform.rotation = redRot;
                    goal.transform.localScale = redScl;
                }
                else
                {
                    goal.transform.position = bluePos;
                    goal.transform.rotation = blueRot;
                    goal.transform.localScale = blueScl;
                }

                if (payload.modified)
                {
                    // Apply custom frame for non-default positions
                    ApplyCustomFrame(goal);
                }
                else
                {
                    // Clean up custom frame if reverting to default
                    CleanupCustomFrame(goal);
                }

                Plugin.Log($"Updated {goal.gameObject.name} to position {goal.transform.position}, rotation {goal.transform.rotation.eulerAngles}, scale {goal.transform.localScale}");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.LogError($"Error handling goal position update: {ex.Message}");
        }
    }
}

[System.Serializable]
public class GoalPositionPayload
{
    public bool modified;
    public Vec3 redPosition;
    public QuatData redRotation;
    public Vec3 redScale;
    public Vec3 bluePosition;
    public QuatData blueRotation;
    public Vec3 blueScale;
}

[System.Serializable]
public struct QuatData
{
    public float x, y, z, w;
}