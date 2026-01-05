using System.Collections.Generic;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class FuckGoals
{
    private static GameObject goalFrameBluePrefab;
    private static GameObject goalFrameRedPrefab;
    private static AssetBundle _loadedAssetBundle;
    
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
            newPos.z = isRed ? -5f : 5f;
            
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
            newFrame.transform.localRotation = Quaternion.Euler(-90f, isRed ? 180 : 0, 0);
            Plugin.Log($"original frame scale {newFrame.transform.localScale}");
            Vector3 s = newFrame.transform.localScale; // prefabs import with a scale of 100,100,100
            newFrame.transform.localScale = new Vector3(s.x * 0.90f, s.z * 0.80f, s.y * 0.90f); // -> 90, 90, 80
            Plugin.Log($"new frame scale {newFrame.transform.localScale}");
            // newFrame.transform.localScale = new Vector3(100, 100, 100);
            newFrame.transform.localScale = new Vector3(92, 100, 92);
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