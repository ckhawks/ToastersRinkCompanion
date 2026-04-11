// SharedPayloads.cs
//
// Payload shapes that are reused across many unrelated features. Feature-specific
// payloads should live as nested classes on the consuming class (see Jail.cs for
// the pattern), but these types are shared widely enough that a single top-level
// home avoids fake nesting.

using System;

namespace ToastersRinkCompanion;

/// <summary>
/// Minimal "feature is on or off" payload. Used by almost every prop toggle
/// (Pillars, BigWalls, CenterWall, Tarps, SpeedBumps, DummyX2, Ramps, GoalRamps,
/// Portals, showVersion, ...).
/// </summary>
[Serializable]
public class EnabledPayload
{
    public bool enabled;
}

/// <summary>
/// Plain-old-data 3-component vector for wire serialization. Kept separate
/// from <see cref="UnityEngine.Vector3"/> so JSON consumers don't need to
/// match Unity's field layout.
/// </summary>
[Serializable]
public struct Vec3
{
    public float x, y, z;

    public Vec3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vec3(UnityEngine.Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(x, y, z);
}
