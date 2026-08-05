using System.Text;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Locates the puck's visible body mesh for the cosmetic prop modifiers.
/// Up to b1149 the mesh sat at the fixed child path puck/Puck; the b1153 puck
/// remodel moved it, so search the subtree by component instead of by name.
/// </summary>
public static class PuckVisuals
{
    public const string BallVisualName = "sphere";
    public const string CubeVisualName = "cube";

    private static bool _hierarchyDumped;

    /// <summary>
    /// Returns the largest mesh under the puck, skipping our own cosmetic
    /// primitives and the elevation indicator's plane. Null if nothing matches.
    /// </summary>
    public static MeshRenderer FindPuckMeshRenderer(Puck puck)
    {
        if (puck == null) return null;

        MeshRenderer best = null;
        float bestVolume = 0f;

        foreach (MeshRenderer renderer in puck.gameObject.GetComponentsInChildren<MeshRenderer>(true))
        {
            string objectName = renderer.gameObject.name;
            if (objectName == BallVisualName || objectName == CubeVisualName) continue;

            // The elevation indicator's plane is a puck child but not the puck body.
            if (renderer.GetComponentInParent<PuckElevationIndicator>() != null) continue;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            Vector3 size = filter.sharedMesh.bounds.size;
            float volume = size.x * size.y * size.z;
            if (best == null || volume > bestVolume)
            {
                best = renderer;
                bestVolume = volume;
            }
        }

        if (best == null) DumpHierarchy(puck);
        return best;
    }

    /// <summary>
    /// Size of the puck body mesh expressed in the puck root's local space, so
    /// a cosmetic child parented to the root matches it regardless of where the
    /// mesh sits in the hierarchy or what scale its own transform carries.
    /// </summary>
    public static Vector3 GetBodySizeInRootSpace(Puck puck, MeshRenderer renderer)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        Bounds bounds = filter != null && filter.sharedMesh != null
            ? filter.sharedMesh.bounds
            : renderer.localBounds;

        Vector3 rootScale = puck.gameObject.transform.lossyScale;
        Vector3 meshScale = renderer.transform.lossyScale;

        return Vector3.Scale(bounds.size, new Vector3(
            rootScale.x != 0f ? meshScale.x / rootScale.x : 1f,
            rootScale.y != 0f ? meshScale.y / rootScale.y : 1f,
            rootScale.z != 0f ? meshScale.z / rootScale.z : 1f));
    }

    /// <summary>
    /// Centre of the puck body mesh in the puck root's local space.
    /// </summary>
    public static Vector3 GetBodyCenterInRootSpace(Puck puck, MeshRenderer renderer)
    {
        return puck.gameObject.transform.InverseTransformPoint(renderer.bounds.center);
    }

    /// <summary>
    /// Dumps the puck subtree once per session so the next prefab change can be
    /// diagnosed from the client log instead of guesswork.
    /// </summary>
    private static void DumpHierarchy(Puck puck)
    {
        if (_hierarchyDumped) return;
        _hierarchyDumped = true;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[PuckVisuals] No puck body mesh found. Puck hierarchy:");
        AppendTransform(builder, puck.gameObject.transform, 0);
        Plugin.LogError(builder.ToString());
    }

    private static void AppendTransform(StringBuilder builder, Transform transform, int depth)
    {
        builder.Append(' ', depth * 2);
        builder.Append(transform.name);

        Component[] components = transform.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null) continue;
            builder.Append(i == 0 ? " [" : ", ");
            builder.Append(components[i].GetType().Name);
        }

        if (components.Length > 0) builder.Append(']');
        builder.AppendLine();

        foreach (Transform child in transform)
        {
            AppendTransform(builder, child, depth + 1);
        }
    }
}
