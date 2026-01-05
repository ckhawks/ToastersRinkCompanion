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

    // public static void FuckGoalsNow()
    // {
    //     UIChat.Instance.AddChatMessage($"fucking goals!");
    //
    //     Goal[] goals = UnityEngine.Object.FindObjectsByType<Goal>(FindObjectsSortMode.None);
    //
    //     Plugin.Log($"Found {goals.Length} goal classes.");
    //
    //     foreach (Goal goal in goals)
    //     {
    //         Vector3 newPos = Vector3.zero;
    //         if (goal.gameObject.name.Contains("Red"))
    //         {
    //             newPos.z = 5;
    //         }
    //         else
    //         {
    //             newPos.z = -5;
    //         }
    //
    //         goal.gameObject.transform.position = newPos;
    //         
    //         UIChat.Instance.AddChatMessage($"Moved {goal.gameObject.name} to {goal.gameObject.transform.position}");
    //
    //         // TODO the colliders/triggers aren't moving to the new position
    //         // TODO need to recreate all of the MeshRenderers in the new position and store them
    //         foreach (Transform childTransform in goal.gameObject.transform)
    //         {
    //             GameObject go = childTransform.gameObject;
    //             Plugin.Log($"moving {go.gameObject.name}");
    //             go.transform.position = newPos;
    //             go.isStatic = false;
    //             MeshRenderer renderer = go.GetComponent<MeshRenderer>();
    //             if (renderer != null)
    //             {
    //                 renderer.enabled = false;
    //                 renderer.enabled = true;
    //             }
    //         }
    //     }
    // }
    
    // TODO we need to be able to re-enable the original goal renderer things when we turn off fuck goals
    private static readonly Dictionary<Goal, List<GameObject>> Clones =
        new();

    public static void FuckGoalsNow()
    {
        LoadPrefabRed();
        LoadPrefabBlue();
        
        UIChat.Instance.AddChatMessage("fucking goals!");

        Goal[] goals =
            Object.FindObjectsByType<Goal>(FindObjectsSortMode.None);

        Plugin.Log($"Found {goals.Length} goal classes.");

        foreach (Goal goal in goals)
        {
            bool isRed = goal.gameObject.name.Contains("Red");

            Vector3 newPos = goal.transform.position;
            newPos.z = isRed ? -5f : 5f; // TODO these positions will be told to the client by the server
            
            // Move root (client-side visual only)
            goal.transform.position = newPos;
            goal.transform.rotation = Quaternion.Euler(0, isRed ? 180 : 0, 0);
            Plugin.Log($"parent goal scale {goal.gameObject.transform.localScale}");

            // Rebuild static visuals
            // RecreateMeshRenderers(goal);
            
            foreach (Transform childTransform in goal.gameObject.transform)
            {
                GameObject go = childTransform.gameObject;
                Plugin.Log($"moving {go.gameObject.name} {childTransform.transform.localScale}");
                // go.transform.position = newPos;
                // go.isStatic = false;
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                    // renderer.enabled = true;
                }
            }
            
            GameObject newFrame = Object.Instantiate(isRed ? goalFrameRedPrefab : goalFrameBluePrefab, goal.transform);
            MeshRenderer[] renderers1 = newFrame.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers1)
            {
                foreach(Material mat in renderer.sharedMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }
        
            newFrame.transform.localPosition = Vector3.zero;
            newFrame.transform.localRotation = Quaternion.Euler(-90f, isRed ? 180 : 0, 0); // flip the goal around if it is red. also they are imported from unity at x: -90 rotation, so we need to keep that
            // Plugin.Log($"original frame scale {newFrame.transform.localScale}");
            // Vector3 s = newFrame.transform.localScale; // prefabs import with a scale of 100,100,100
            // newFrame.transform.localScale = new Vector3(s.x * 0.90f, s.z * 0.80f, s.y * 0.90f); // -> 90, 90, 80
            // Plugin.Log($"new frame scale {newFrame.transform.localScale}");
            // // newFrame.transform.localScale = new Vector3(100, 100, 100);
            newFrame.transform.localScale = new Vector3(92, 100, 92); // These values make the goal the correct size
            // X axis - is the left/right of the rink
            // Y axis - forward/back of the rink
            // Z axis - up/down
            // THESE AXIS'S ARE NOT HOW YOU WOULD EXPECT BECAUSE THE GOAL FRAME IS ROTATED
        }
    }

    private static void DumpRendererState(MeshRenderer r)
    {
        Plugin.Log(
            $"[{r.name}] " +
            $"matCount={r.sharedMaterials.Length} " +
            $"lightmapIndex={r.lightmapIndex} " +
            $"lightmapScaleOffset={r.lightmapScaleOffset} " +
            $"renderingLayerMask={r.renderingLayerMask} " +
            $"static={r.gameObject.isStatic}"
        );
    }
    
    private static void RecreateMeshRenderers(Goal goal)
    {
        

        // Prevent duplicate cloning if called multiple times
        if (Clones.ContainsKey(goal))
            return;

        List<GameObject> created = new();

        foreach (MeshRenderer oldRenderer in goal
                     .GetComponentsInChildren<MeshRenderer>(true))
        {
            // Skip SkinnedMeshRenderer (the net)
            if (oldRenderer is SkinnedMeshRenderer)
                continue;

            if (!oldRenderer.enabled) continue;

            MeshFilter oldFilter =
                oldRenderer.GetComponent<MeshFilter>();

            if (oldFilter == null || oldFilter.sharedMesh == null)
                continue;

            DumpRendererState(oldRenderer);
            
            GameObject oldGO = oldRenderer.gameObject;

            GameObject clone = new GameObject(
                oldGO.name + "_ClientClone"
            );

            // Parent to the same parent as original
            clone.transform.SetParent(
                oldGO.transform.parent,
                false
            );

            // Preserve local transform
            clone.transform.localPosition =
                oldGO.transform.localPosition;
            clone.transform.localRotation =
                oldGO.transform.localRotation;
            clone.transform.localScale =
                oldGO.transform.localScale;

            // Mesh
            MeshFilter newFilter =
                clone.AddComponent<MeshFilter>();
            newFilter.mesh = oldFilter.mesh;

            // Renderer
            MeshRenderer newRenderer =
                clone.AddComponent<MeshRenderer>();
            newRenderer.material =
                oldRenderer.material;
            Plugin.Log($"{oldGO.name}'shaders: {oldRenderer.material.shader.name}");
            Plugin.Log($"old {oldRenderer.material.color}");
            newRenderer.material.shader = oldRenderer.material.shader;
            newRenderer.material.color = oldRenderer.material.color;

            newRenderer.sharedMaterials =
                oldRenderer.sharedMaterials;

            // ---- CRITICAL STATIC DATA ----
            newRenderer.lightmapIndex =
                oldRenderer.lightmapIndex;
            newRenderer.lightmapScaleOffset =
                oldRenderer.lightmapScaleOffset;

            newRenderer.renderingLayerMask =
                oldRenderer.renderingLayerMask;

            newRenderer.shadowCastingMode =
                oldRenderer.shadowCastingMode;
            newRenderer.receiveShadows =
                oldRenderer.receiveShadows;
            newRenderer.lightProbeUsage =
                oldRenderer.lightProbeUsage;
            newRenderer.reflectionProbeUsage =
                oldRenderer.reflectionProbeUsage;
            newRenderer.motionVectorGenerationMode =
                oldRenderer.motionVectorGenerationMode;
            newRenderer.allowOcclusionWhenDynamic =
                oldRenderer.allowOcclusionWhenDynamic;

            // ---- COPY MATERIAL PROPERTY BLOCK ----
            MaterialPropertyBlock mpb = new();
            oldRenderer.GetPropertyBlock(mpb);
            newRenderer.SetPropertyBlock(mpb);

            // Ensure NOT static
            clone.isStatic = false;

            // Disable original static renderer
            oldRenderer.enabled = false;

            created.Add(clone);
        }

        Clones[goal] = created;

        Plugin.Log(
            $"Recreated {created.Count} MeshRenderers for {goal.name}"
        );
    }
    
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

        // Disable only the currently-enabled child renderers and track them
        List<MeshRenderer> disabledRenderers = new();
        foreach (Transform childTransform in goal.gameObject.transform)
        {
            GameObject go = childTransform.gameObject;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
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

        // Configure frame transform
        newFrame.transform.localPosition = Vector3.zero;
        newFrame.transform.localRotation = Quaternion.Euler(-90f, isRed ? 180 : 0, 0);
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

            Goal[] goals = Object.FindObjectsByType<Goal>(FindObjectsSortMode.None);

            foreach (Goal goal in goals)
            {
                bool isRed = goal.gameObject.name.Contains("Red");

                // Convert simple types back to Vector3 and Quaternion
                Vector3 redPos = new Vector3(payload.redPosition.x, payload.redPosition.y, payload.redPosition.z);
                Vector3 redScl = new Vector3(payload.redScale.x, payload.redScale.y, payload.redScale.z);
                Quaternion redRot = new Quaternion(payload.redRotation.x, payload.redRotation.y, payload.redRotation.z, payload.redRotation.w);

                Vector3 bluePos = new Vector3(payload.bluePosition.x, payload.bluePosition.y, payload.bluePosition.z);
                Vector3 blueScl = new Vector3(payload.blueScale.x, payload.blueScale.y, payload.blueScale.z);
                Quaternion blueRot = new Quaternion(payload.blueRotation.x, payload.blueRotation.y, payload.blueRotation.z, payload.blueRotation.w);

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

                // Check if goals are in non-default position
                bool isNonDefaultPosition = (isRed && redPos != new Vector3(0, 0, -40.92f)) ||
                                    (!isRed && bluePos != new Vector3(0, 0, 40.92f));
                
                bool isNonDefaultScale = (isRed && redScl != new Vector3(0.9f, 0.9f, 0.8f) || !isRed && blueScl != new Vector3(0.9f, 0.9f, 0.8f));

                if (isNonDefaultPosition || isNonDefaultScale)
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

// --Goal Blue [Active: True, Layer: 0Position: (0.00, 0.00, 40.92), Components: Transform, Goal, NetworkObject, GoalController]
// ----Bottom Curve [Active: False, Layer: 0Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Brace Curve [Active: False, Layer: 0Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Circle [Active: False, Layer: 0Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Frame [Active: True, Layer: 18Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Front Curve [Active: False, Layer: 0Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Goal Player Collider [Active: True, Layer: 16Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Goal Post Collider [Active: True, Layer: 11Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider, CapsuleCollider, CapsuleCollider, CapsuleCollider]
// ----Goal Trigger [Active: True, Layer: 15Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider, GoalTrigger]
// ----Net [Active: True, Layer: 0Position: (0.00, 0.00, 40.92), Components: Transform, MeshCollider, SkinnedMeshRenderer, Cloth]
// ----Net Collider [Active: True, Layer: 14Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Top Curve [Active: False, Layer: 0Position: (0.00, 0.00, 40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// --Goal Red [Active: True, Layer: 0Position: (0.00, 0.00, -40.92), Components: Transform, Goal, NetworkObject, GoalController]
// ----Bottom Curve [Active: False, Layer: 0Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Brace Curve [Active: False, Layer: 0Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Circle [Active: False, Layer: 0Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Frame [Active: True, Layer: 18Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Front Curve [Active: False, Layer: 0Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Goal Player Collider [Active: True, Layer: 16Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Goal Post Collider [Active: True, Layer: 11Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider, CapsuleCollider, CapsuleCollider, CapsuleCollider]
// ----Goal Trigger [Active: True, Layer: 15Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider, GoalTrigger]
// ----Net [Active: True, Layer: 0Position: (0.00, 0.00, -40.92), Components: Transform, MeshCollider, SkinnedMeshRenderer, Cloth]
// ----Net Collider [Active: True, Layer: 14Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]
// ----Top Curve [Active: False, Layer: 0Position: (0.00, 0.00, -40.92), Components: Transform, MeshFilter, MeshRenderer, MeshCollider]

[System.Serializable]
public class GoalPositionPayload
{
    public Vec3 redPosition;
    public QuatData redRotation;
    public Vec3 redScale;
    public Vec3 bluePosition;
    public QuatData blueRotation;
    public Vec3 blueScale;
}

[System.Serializable]
public struct Vec3
{
    public float x, y, z;
}

[System.Serializable]
public struct QuatData
{
    public float x, y, z, w;
}