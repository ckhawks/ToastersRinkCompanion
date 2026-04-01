using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Renders modifier-spawned objects on the game minimap as simple shapes.
/// Hooks into UIMinimap.Update via Harmony, similar to ToasterSticksOnMap.
/// </summary>
public static class MinimapObjects
{
    // ─── Reflection (same fields as ToasterSticksOnMap) ──────────────

    static readonly FieldInfo _contentField = typeof(UIMinimap)
        .GetField("content", BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly MethodInfo _worldToMinimapMethod = typeof(UIMinimap)
        .GetMethod("WorldPositionToMinimapPosition", BindingFlags.NonPublic | BindingFlags.Instance);

    // ─── TRL soft dependency ─────────────────────────────────────────

    private static Type _trlApiType;
    private static bool _trlChecked;

    private static void EnsureTRL()
    {
        if (_trlChecked) return;
        _trlChecked = true;
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "ToasterReskinLoader");
            if (asm != null)
                _trlApiType = asm.GetType("ToasterReskinLoader.ToasterReskinLoaderAPI");
        }
        catch { /* TRL not available */ }
    }

    private static readonly Color DefaultBlue = new(0.231f, 0.510f, 0.965f, 1f);
    private static readonly Color DefaultRed = new(0.820f, 0.200f, 0.200f, 1f);

    private static Color GetTeamColor(bool isRed)
    {
        if (_trlApiType != null)
        {
            try
            {
                bool enabled = (bool)_trlApiType.GetProperty("TeamColorsEnabled").GetValue(null);
                if (enabled)
                {
                    string prop = isRed ? "RedTeamColor" : "BlueTeamColor";
                    return (Color)_trlApiType.GetProperty(prop).GetValue(null);
                }
            }
            catch { /* fall through */ }
        }
        return isRed ? DefaultRed : DefaultBlue;
    }

    // ─── Colors ──────────────────────────────────────────────────────

    private static readonly Color ObstacleColor = new(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color ConeColor = new(1f, 0.65f, 0f, 1f);
    private static readonly Color JailColor = new(0f, 0f, 0f, 1f);
    private static readonly Color JailAirborneColor = new(0f, 0f, 0f, 0.35f);
    private static readonly Color RockColor = new(0.55f, 0.35f, 0.15f, 1f);

    // ─── Element pools ───────────────────────────────────────────────

    private static readonly List<VisualElement> _obstacleElements = new();
    private static readonly List<VisualElement> _coneElements = new();
    private static readonly List<VisualElement> _jailElements = new();
    private static VisualElement _rockElement;
    private static readonly List<VisualElement> _goalElements = new();

    private static VisualElement _minimapContent;

    // ─── Update throttle ─────────────────────────────────────────────

    private static float _accumulator;
    private const float UpdateInterval = 0.1f; // 10 Hz — fast enough to feel responsive

    // ─── Default goal positions ──────────────────────────────────────

    // ─── Center wall constants ───────────────────────────────────────
    // Gap is 10 units wide on X, centered on origin. Rink is 45 units wide on X.
    private const float CenterWallGapHalfWidth = 5f;
    private const float RinkHalfWidth = 22.5f;

    // ─── Harmony patch ───────────────────────────────────────────────

    [HarmonyPatch(typeof(UIMinimap), "Update")]
    public static class MinimapUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMinimap __instance)
        {
            if (Plugin.modSettings != null && !Plugin.modSettings.showMinimapObjects) return;

            _accumulator += Time.deltaTime;
            if (_accumulator < UpdateInterval) return;
            _accumulator = 0f;

            EnsureTRL();

            VisualElement content = (VisualElement)_contentField.GetValue(__instance);
            if (content == null) return;
            _minimapContent = content;

            Bounds bounds = __instance.Bounds;
            PlayerTeam team = __instance.Team;

            int obstacleIdx = 0;
            int coneIdx = 0;
            int jailIdx = 0;
            int goalIdx = 0;

            // ── Obstacles (black rectangles, rendered below everything) ──

            foreach (var go in Portals.SpawnedObjects)
                RenderObstacle(go, ref obstacleIdx, team, bounds, __instance);

            foreach (var go in Ramps.SpawnedObjects)
                RenderObstacle(go, ref obstacleIdx, team, bounds, __instance);

            foreach (var go in GoalRamps.SpawnedObjects)
                RenderObstacle(go, ref obstacleIdx, team, bounds, __instance);

            foreach (var go in SpeedBumps.SpawnedObjects)
                RenderObstacle(go, ref obstacleIdx, team, bounds, __instance);

            // Pillars — two circles at either end of the bounding box
            if (Pillars.SpawnedObject != null)
                RenderPillars(Pillars.SpawnedObject, ref obstacleIdx, team, bounds, __instance);

            foreach (var go in BigWalls.SpawnedObjects)
                RenderObstacle(go, ref obstacleIdx, team, bounds, __instance);

            // Center Wall — two halves with 10-unit gap, clamped to rink width
            if (CenterWall.SpawnedObject != null)
                RenderCenterWall(CenterWall.SpawnedObject, ref obstacleIdx, team, bounds, __instance);

            HideExcess(_obstacleElements, obstacleIdx);

            // ── Cones (orange triangles) ──

            foreach (var go in Cones.SpawnedObjects)
                RenderCone(go, ref coneIdx, team, bounds, __instance);

            HideExcess(_coneElements, coneIdx);

            // ── Jails (unfilled squares, transparent when airborne) ──

            foreach (var kvp in Jail.SpawnedJails)
                RenderJail(kvp.Value, ref jailIdx, team, bounds, __instance);

            HideExcess(_jailElements, jailIdx);

            // ── Rock (brown rectangle, only when active) ──

            RenderRock(team, bounds, __instance);

            // ── Goals (team-colored C/U shape, only when non-default position/scale) ──

            if (FuckGoals.GoalsAreNonDefault)
            {
                try
                {
                    Goal[] goals = UnityEngine.Object.FindObjectsByType<Goal>(FindObjectsSortMode.None);
                    foreach (Goal goal in goals)
                    {
                        if (goal == null) continue;
                        bool isRed = goal.gameObject.name.Contains("Red");
                        RenderGoal(goal, isRed, ref goalIdx, team, bounds, __instance);
                    }
                }
                catch { /* Goal type might not be loaded yet */ }
            }

            HideExcess(_goalElements, goalIdx);

            // ── Z-ordering: SendToBack in reverse priority order ──
            // Last call to SendToBack ends up at the very bottom.
            // Order: goals first (above obstacles), then obstacles/cones/jails/rock (very bottom).
            SendAllToBack(_goalElements, goalIdx);
            if (_rockElement != null && _rockElement.style.display == DisplayStyle.Flex)
                _rockElement.SendToBack();
            SendAllToBack(_jailElements, jailIdx);
            SendAllToBack(_coneElements, coneIdx);
            SendAllToBack(_obstacleElements, obstacleIdx);
        }
    }

    private static void SendAllToBack(List<VisualElement> pool, int activeCount)
    {
        for (int i = activeCount - 1; i >= 0; i--)
            pool[i].SendToBack();
    }

    // ─── Coordinate helpers ──────────────────────────────────────────

    private static Vector2 WorldToMinimap(UIMinimap minimap, Vector3 worldPos, PlayerTeam team, Bounds bounds)
    {
        Vector3 teamPos = team == PlayerTeam.Blue ? worldPos : -worldPos;
        return (Vector2)_worldToMinimapMethod.Invoke(minimap, new object[] { teamPos, bounds });
    }

    private static (Vector3 center, Vector3 size) GetXZBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return (go.transform.position, Vector3.one * 2f);

        Bounds merged = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            merged.Encapsulate(renderers[i].bounds);

        return (merged.center, merged.size);
    }

    /// <summary>
    /// Converts a world-space width/depth (X/Z) into minimap pixel dimensions.
    /// </summary>
    private static Vector2 WorldSizeToMinimapSize(UIMinimap minimap, Vector3 worldCenter,
        float worldWidth, float worldDepth, PlayerTeam team, Bounds bounds)
    {
        Vector3 probe1 = worldCenter + new Vector3(-worldWidth / 2f, 0, -worldDepth / 2f);
        Vector3 probe2 = worldCenter + new Vector3(worldWidth / 2f, 0, worldDepth / 2f);

        Vector2 mm1 = WorldToMinimap(minimap, probe1, team, bounds);
        Vector2 mm2 = WorldToMinimap(minimap, probe2, team, bounds);

        float pixelW = Mathf.Abs((-mm2.x) - (-mm1.x));
        float pixelH = Mathf.Abs(mm2.y - mm1.y);

        return new Vector2(Mathf.Max(pixelW, 3f), Mathf.Max(pixelH, 3f));
    }

    /// <summary>
    /// Positions a VisualElement centered at the given minimap coordinates.
    /// Uses the same translate convention as ToasterSticksOnMap.
    /// </summary>
    private static void PositionElement(VisualElement el, Vector2 mmPos, float pixelW, float pixelH)
    {
        el.style.width = pixelW;
        el.style.height = pixelH;
        el.style.translate = new Translate(-mmPos.x, mmPos.y);
    }

    // ─── Obstacle rendering (black rectangles) ──────────────────────

    private static VisualElement CreateObstacleElement()
    {
        var ve = new VisualElement();
        ve.style.position = Position.Absolute;
        ve.style.backgroundColor = new StyleColor(ObstacleColor);
        _minimapContent.Add(ve);
        return ve;
    }

    private static void RenderObstacle(GameObject go, ref int idx, PlayerTeam team, Bounds bounds, UIMinimap minimap)
    {
        if (go == null) return;

        var (center, size) = GetXZBounds(go);
        // Clamp width to rink bounds (45 units on X)
        float clampedX = Mathf.Min(size.x, RinkHalfWidth * 2f);
        Vector2 mmPos = WorldToMinimap(minimap, center, team, bounds);
        Vector2 mmSize = WorldSizeToMinimapSize(minimap, center, clampedX, size.z, team, bounds);

        VisualElement el = GetOrCreateElement(_obstacleElements, ref idx, CreateObstacleElement);
        el.style.display = DisplayStyle.Flex;
        // Reset any border radius from potential reuse of pillar elements
        el.style.borderTopLeftRadius = 0;
        el.style.borderTopRightRadius = 0;
        el.style.borderBottomLeftRadius = 0;
        el.style.borderBottomRightRadius = 0;
        PositionElement(el, mmPos, mmSize.x, mmSize.y);
    }

    // ─── Pillars (two circles at either end of bounding box) ─────────

    private static void RenderPillars(GameObject go, ref int idx, PlayerTeam team, Bounds bounds, UIMinimap minimap)
    {
        if (go == null) return;

        var (center, size) = GetXZBounds(go);

        // The pillar mesh spans along one axis. Determine which axis is longer.
        bool longerOnX = size.x >= size.z;
        // Each pillar is roughly the shorter dimension in diameter
        float pillarDiameter = longerOnX ? size.z : size.x;
        pillarDiameter = Mathf.Max(pillarDiameter, 1.5f);
        // Inset by the pillar radius so circles sit at actual pillar centers, not bounding box edges
        float halfSpan = (longerOnX ? size.x : size.z) / 2f - pillarDiameter / 2f;
        halfSpan = Mathf.Max(halfSpan, 0f);

        Vector3 offset = longerOnX ? new Vector3(halfSpan, 0, 0) : new Vector3(0, 0, halfSpan);
        Vector3 pos1 = center - offset;
        Vector3 pos2 = center + offset;

        foreach (Vector3 pillarPos in new[] { pos1, pos2 })
        {
            Vector2 mmPos = WorldToMinimap(minimap, pillarPos, team, bounds);
            Vector2 mmSize = WorldSizeToMinimapSize(minimap, pillarPos, pillarDiameter, pillarDiameter, team, bounds);
            float circleSize = Mathf.Max(mmSize.x, mmSize.y);
            circleSize = Mathf.Max(circleSize, 5f);

            VisualElement el = GetOrCreateElement(_obstacleElements, ref idx, CreateObstacleElement);
            el.style.display = DisplayStyle.Flex;
            el.style.borderTopLeftRadius = circleSize / 2f;
            el.style.borderTopRightRadius = circleSize / 2f;
            el.style.borderBottomLeftRadius = circleSize / 2f;
            el.style.borderBottomRightRadius = circleSize / 2f;
            PositionElement(el, mmPos, circleSize, circleSize);
        }
    }

    // ─── Center wall (two halves with 10-unit gap, clamped to rink width) ──

    private static void RenderCenterWall(GameObject go, ref int idx, PlayerTeam team, Bounds bounds, UIMinimap minimap)
    {
        if (go == null) return;

        var (center, size) = GetXZBounds(go);
        float wallDepth = size.z; // thickness of the wall in Z

        // Left half: from -RinkHalfWidth to -CenterWallGapHalfWidth
        float leftWidth = RinkHalfWidth - CenterWallGapHalfWidth;
        if (leftWidth > 0.5f)
        {
            float leftCenterX = -(CenterWallGapHalfWidth + leftWidth / 2f);
            Vector3 leftCenter = new Vector3(center.x + leftCenterX, center.y, center.z);
            Vector2 mmPos = WorldToMinimap(minimap, leftCenter, team, bounds);
            Vector2 mmSize = WorldSizeToMinimapSize(minimap, leftCenter, leftWidth, wallDepth, team, bounds);

            VisualElement el = GetOrCreateElement(_obstacleElements, ref idx, CreateObstacleElement);
            el.style.display = DisplayStyle.Flex;
            el.style.borderTopLeftRadius = 0;
            el.style.borderTopRightRadius = 0;
            el.style.borderBottomLeftRadius = 0;
            el.style.borderBottomRightRadius = 0;
            PositionElement(el, mmPos, mmSize.x, mmSize.y);
        }

        // Right half: from +CenterWallGapHalfWidth to +RinkHalfWidth
        float rightWidth = RinkHalfWidth - CenterWallGapHalfWidth;
        if (rightWidth > 0.5f)
        {
            float rightCenterX = CenterWallGapHalfWidth + rightWidth / 2f;
            Vector3 rightCenter = new Vector3(center.x + rightCenterX, center.y, center.z);
            Vector2 mmPos = WorldToMinimap(minimap, rightCenter, team, bounds);
            Vector2 mmSize = WorldSizeToMinimapSize(minimap, rightCenter, rightWidth, wallDepth, team, bounds);

            VisualElement el = GetOrCreateElement(_obstacleElements, ref idx, CreateObstacleElement);
            el.style.display = DisplayStyle.Flex;
            el.style.borderTopLeftRadius = 0;
            el.style.borderTopRightRadius = 0;
            el.style.borderBottomLeftRadius = 0;
            el.style.borderBottomRightRadius = 0;
            PositionElement(el, mmPos, mmSize.x, mmSize.y);
        }
    }

    // ─── Cone rendering (orange triangles) ───────────────────────────

    private static void RenderCone(GameObject go, ref int idx, PlayerTeam team, Bounds bounds, UIMinimap minimap)
    {
        if (go == null) return;

        Vector3 worldPos = go.transform.position;
        Vector2 mmPos = WorldToMinimap(minimap, worldPos, team, bounds);

        VisualElement el = GetOrCreateElement(_coneElements, ref idx, () =>
        {
            // Triangle via CSS borders: 0-size element with colored bottom border + transparent sides
            var ve = new VisualElement();
            ve.style.position = Position.Absolute;
            ve.style.width = 0;
            ve.style.height = 0;
            ve.style.borderBottomWidth = 7;
            ve.style.borderBottomColor = new StyleColor(ConeColor);
            ve.style.borderLeftWidth = 4;
            ve.style.borderLeftColor = new StyleColor(Color.clear);
            ve.style.borderRightWidth = 4;
            ve.style.borderRightColor = new StyleColor(Color.clear);
            ve.style.borderTopWidth = 0;
            _minimapContent.Add(ve);
            return ve;
        });

        el.style.display = DisplayStyle.Flex;
        el.style.translate = new Translate(-mmPos.x, mmPos.y);
    }

    // ─── Jail rendering (unfilled square, transparent when airborne) ─

    private static void RenderJail(GameObject go, ref int idx, PlayerTeam team, Bounds bounds, UIMinimap minimap)
    {
        if (go == null) return;

        var (center, size) = GetXZBounds(go);
        Vector2 mmPos = WorldToMinimap(minimap, center, team, bounds);
        Vector2 mmSize = WorldSizeToMinimapSize(minimap, center, size.x, size.z, team, bounds);
        float side = Mathf.Max(mmSize.x, mmSize.y);
        side = Mathf.Max(side, 8f);

        bool airborne = center.y > 1.0f;
        Color color = airborne ? JailAirborneColor : JailColor;

        VisualElement el = GetOrCreateElement(_jailElements, ref idx, () =>
        {
            var ve = new VisualElement();
            ve.style.position = Position.Absolute;
            ve.style.backgroundColor = new StyleColor(Color.clear);
            _minimapContent.Add(ve);
            return ve;
        });

        el.style.display = DisplayStyle.Flex;
        el.style.width = side;
        el.style.height = side;
        el.style.borderTopWidth = 2;
        el.style.borderBottomWidth = 2;
        el.style.borderLeftWidth = 2;
        el.style.borderRightWidth = 2;
        el.style.borderTopColor = new StyleColor(color);
        el.style.borderBottomColor = new StyleColor(color);
        el.style.borderLeftColor = new StyleColor(color);
        el.style.borderRightColor = new StyleColor(color);
        PositionElement(el, mmPos, side, side);
    }

    // ─── Rock rendering ──────────────────────────────────────────────

    private static void RenderRock(PlayerTeam team, Bounds bounds, UIMinimap minimap)
    {
        GameObject rock = RockEvent.SpawnedRock;

        if (rock == null)
        {
            if (_rockElement != null)
                _rockElement.style.display = DisplayStyle.None;
            return;
        }

        var (center, size) = GetXZBounds(rock);
        Vector2 mmPos = WorldToMinimap(minimap, center, team, bounds);
        Vector2 mmSize = WorldSizeToMinimapSize(minimap, center, size.x, size.z, team, bounds);
        mmSize.x = Mathf.Max(mmSize.x, 8f);
        mmSize.y = Mathf.Max(mmSize.y, 8f);

        if (_rockElement == null)
        {
            _rockElement = new VisualElement();
            _rockElement.style.position = Position.Absolute;
            _rockElement.style.backgroundColor = new StyleColor(RockColor);
            _rockElement.style.borderTopLeftRadius = new Length(50, LengthUnit.Percent);
            _rockElement.style.borderTopRightRadius = new Length(50, LengthUnit.Percent);
            _rockElement.style.borderBottomLeftRadius = new Length(50, LengthUnit.Percent);
            _rockElement.style.borderBottomRightRadius = new Length(50, LengthUnit.Percent);
            _minimapContent.Add(_rockElement);
        }

        _rockElement.style.display = DisplayStyle.Flex;
        PositionElement(_rockElement, mmPos, mmSize.x, mmSize.y);
    }

    // ─── Goal rendering (C/U shape drawn with 2px lines) ───────────────

    private const float GoalLineThickness = 2f;

    /// <summary>
    /// Renders a goal as three 2px lines forming a C/U shape: back + two sides.
    /// The opening faces the goal's forward direction.
    /// </summary>
    private static void RenderGoal(Goal goal, bool isRed, ref int idx, PlayerTeam team, Bounds bounds, UIMinimap minimap)
    {
        Vector3 worldPos = goal.transform.position;
        Vector3 scale = goal.transform.localScale;

        float goalWidth = 3.5f * scale.x;
        float goalDepth = 2.5f * scale.z;

        Vector3 forward = goal.transform.forward;
        Vector3 right = goal.transform.right;

        Color color = GetTeamColor(isRed);

        // Four corners of the goal mouth (C shape)
        // Back-left, back-right, front-left, front-right
        Vector3 backLeft = worldPos - forward * (goalDepth / 2f) - right * (goalWidth / 2f);
        Vector3 backRight = worldPos - forward * (goalDepth / 2f) + right * (goalWidth / 2f);
        Vector3 frontLeft = worldPos + forward * (goalDepth / 2f) - right * (goalWidth / 2f);
        Vector3 frontRight = worldPos + forward * (goalDepth / 2f) + right * (goalWidth / 2f);

        // Convert all corners to minimap coords
        Vector2 mmBL = WorldToMinimap(minimap, backLeft, team, bounds);
        Vector2 mmBR = WorldToMinimap(minimap, backRight, team, bounds);
        Vector2 mmFL = WorldToMinimap(minimap, frontLeft, team, bounds);
        Vector2 mmFR = WorldToMinimap(minimap, frontRight, team, bounds);

        // Back line: backLeft → backRight
        RenderGoalLine(mmBL, mmBR, color, ref idx);
        // Left side: backLeft → frontLeft
        RenderGoalLine(mmBL, mmFL, color, ref idx);
        // Right side: backRight → frontRight
        RenderGoalLine(mmBR, mmFR, color, ref idx);
    }

    /// <summary>
    /// Draws a 2px line between two minimap-space points using a rotated VisualElement.
    /// </summary>
    private static void RenderGoalLine(Vector2 mmA, Vector2 mmB, Color color, ref int idx)
    {
        // Convert to pixel-space (negate X to match translate convention)
        float ax = -mmA.x, ay = mmA.y;
        float bx = -mmB.x, by = mmB.y;

        float dx = bx - ax;
        float dy = by - ay;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        length = Mathf.Max(length, 2f);
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

        // Midpoint for positioning
        float mx = (ax + bx) / 2f;
        float my = (ay + by) / 2f;

        VisualElement el = GetOrCreateElement(_goalElements, ref idx, () =>
        {
            var ve = new VisualElement();
            ve.style.position = Position.Absolute;
            ve.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            _minimapContent.Add(ve);
            return ve;
        });

        el.style.display = DisplayStyle.Flex;
        el.style.backgroundColor = new StyleColor(color);
        el.style.width = length;
        el.style.height = GoalLineThickness;
        el.style.translate = new Translate(mx, my);
        el.style.rotate = new Rotate(Angle.Degrees(angle));
    }

    // ─── Pool helpers ────────────────────────────────────────────────

    private static VisualElement GetOrCreateElement(List<VisualElement> pool, ref int idx, Func<VisualElement> factory)
    {
        if (idx < pool.Count)
        {
            var existing = pool[idx];
            if (existing.parent == null && _minimapContent != null)
                _minimapContent.Add(existing);
            idx++;
            return existing;
        }

        var created = factory();
        pool.Add(created);
        idx++;
        return created;
    }

    private static void HideExcess(List<VisualElement> pool, int activeCount)
    {
        for (int i = activeCount; i < pool.Count; i++)
            pool[i].style.display = DisplayStyle.None;
    }

    // ─── Cleanup ─────────────────────────────────────────────────────

    public static void Clear()
    {
        foreach (var el in _obstacleElements) el.RemoveFromHierarchy();
        _obstacleElements.Clear();

        foreach (var el in _coneElements) el.RemoveFromHierarchy();
        _coneElements.Clear();

        foreach (var el in _jailElements) el.RemoveFromHierarchy();
        _jailElements.Clear();

        if (_rockElement != null)
        {
            _rockElement.RemoveFromHierarchy();
            _rockElement = null;
        }

        foreach (var el in _goalElements) el.RemoveFromHierarchy();
        _goalElements.Clear();

        _minimapContent = null;
    }
}
