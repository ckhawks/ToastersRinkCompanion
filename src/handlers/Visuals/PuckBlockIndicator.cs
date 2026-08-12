using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Local-player readout for the puck-block bind. The outline in
/// <see cref="PuckBlockOutline"/> deliberately skips your own body, so without this
/// you have no way to tell whether a press actually engaged.
///
/// A shield sits centred just above the HUD stamina bar: yellow while you are
/// blocking, grey while on cooldown, gone otherwise. It is drawn with Painter2D
/// rather than shipped as a sprite, so there is no asset to load and it stays sharp
/// at any resolution.
///
/// The state machine is driven from two sides. Your key press moves it optimistically
/// into <c>Pending</c> so the shield reacts on the same frame, and the server's
/// <c>puck_block_state</c> message is what actually confirms or corrects it. A press
/// the server refuses -- cooldown, no stamina, goalie, modifier off -- simply never
/// gets confirmed, and <c>Pending</c> times out back to idle.
/// </summary>
public static class PuckBlockIndicator
{
    /// <summary>
    /// Mirrored from PuckBodyBlock on the server. The server remains the authority on
    /// when a block starts and ends, and every transition here is corrected by the
    /// state message. Keep in sync.
    ///
    /// There is deliberately no duration readout. How long a block lasts is set by
    /// stamina, not by the max-hold ceiling, so a countdown against that ceiling would
    /// promise time the player usually does not have -- it looked like the block was
    /// cutting out early. The stamina bar directly below is the honest gauge.
    /// </summary>
    private const float WindupTime = 0.3f;

    private const float CooldownTime = 0.35f;

    /// <summary>
    /// How long past the windup we keep showing the pending state before concluding
    /// the press was refused. Covers the round trip to the server.
    /// </summary>
    private const float PendingGrace = 0.5f;

    /// <summary>
    /// Shares the outline's configured color, so your shield and the silhouette other
    /// players see are always the same ability rather than two settings to keep aligned.
    /// </summary>
    private static Color AbilityColor => PuckBlockOutline.AbilityColor;

    private static readonly Color CooldownColor = new(0.55f, 0.55f, 0.55f);

    /// <summary>
    /// The shield is the Feather "shield" glyph, authored in a 24x24 box. Everything is
    /// expressed in those units and multiplied by <see cref="Scale"/>, so resizing the
    /// icon is a one-number change and the proportions cannot drift.
    /// </summary>
    private const float Scale = 4.5f;

    private const float IconSize = 24f * Scale;

    /// <summary>Lowest point of the glyph inside its box, for parking it above the bar.</summary>
    private const float GlyphBottom = 22f * Scale;

    /// <summary>Clearance between the point of the shield and the stamina bar.</summary>
    private const float BarGap = 10f;

    /// <summary>
    /// Size during the windup, as a fraction of the active shield. It grows across the
    /// windup but stops short of full, so a charging block never reads as an engaged
    /// one -- the snap to full size is the moment you are actually blocking.
    /// </summary>
    private const float WindupScaleStart = 0.55f;

    private const float WindupScaleEnd = 0.8f;

    private enum State
    {
        Idle,
        Pending,
        Active,
        Cooldown,
    }

    private static State _state = State.Idle;
    private static float _stateEnteredAt;

    /// <summary>
    /// Whether the bind is physically down. A press made during a cooldown is queued
    /// server-side and engages the moment it lapses, so the shield has to know the key
    /// is still held in order to follow it straight into the windup.
    /// </summary>
    private static bool _isHeld;

    private static VisualElement _shield;
    private static VisualElement _staminaBar;
    private static Color _shieldColor = AbilityColor;

    public static void RegisterHandlers()
    {
        EventManager.AddEventListener("Event_OnClientDisconnected",
            new Action<Dictionary<string, object>>(_ => Reset()));
    }

    // ------------------------------------------------------------------- inputs

    /// <summary>Called from <see cref="PuckBlockInput"/> on every key edge.</summary>
    public static void OnLocalInput(bool down)
    {
        try
        {
            if (down)
            {
                // Goalies have nothing to toggle -- they already block with their body --
                // so do not flash a shield that will only time out.
                if (IsLocalGoalie()) return;

                _isHeld = true;

                // Held through a cooldown: leave the grey shield up and let the cooldown
                // roll into the windup on its own.
                if (_state != State.Idle) return;

                Setup();
                SetState(State.Pending);
                return;
            }

            _isHeld = false;

            // Releasing during the windup means no block ever started, so no cooldown.
            if (_state == State.Pending) SetState(State.Idle);
            else if (_state == State.Active) SetState(State.Cooldown);
        }
        catch (Exception e)
        {
            Plugin.LogError($"PuckBlockIndicator input failed: {e.Message}");
        }
    }

    /// <summary>
    /// Called from <see cref="PuckBlockOutline"/> when a state message arrives for the
    /// local client. This is the authoritative edge.
    /// </summary>
    public static void OnServerState(bool blocking)
    {
        try
        {
            Setup();

            if (blocking)
            {
                SetState(State.Active);
                return;
            }

            // A block we knew about ended: show the cooldown. If we were not showing
            // anything, a stale release is nothing to report.
            if (_state == State.Active || _state == State.Pending) SetState(State.Cooldown);
        }
        catch (Exception e)
        {
            Plugin.LogError($"PuckBlockIndicator state failed: {e.Message}");
        }
    }

    /// <summary>Drops the shield outright. Used on disconnect so it cannot stick on.</summary>
    public static void Reset()
    {
        _isHeld = false;
        SetState(State.Idle);
    }

    private static void SetState(State next)
    {
        _state = next;
        _stateEnteredAt = Time.unscaledTime;

        if (next == State.Idle && _shield != null) _shield.style.display = DisplayStyle.None;
    }

    private static bool IsLocalGoalie()
    {
        try
        {
            var player = PlayerManager.Instance?.GetLocalPlayer();
            return player != null && player.Role == PlayerRole.Goalie;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------- frame

    private static void Tick()
    {
        if (_shield == null) return;

        switch (_state)
        {
            case State.Idle:
                _shield.style.display = DisplayStyle.None;
                return;

            case State.Pending:
                if (Time.unscaledTime - _stateEnteredAt > WindupTime + PendingGrace)
                {
                    // Never confirmed. The press was refused, or the modifier is off.
                    SetState(State.Idle);
                    return;
                }

            {
                float charge = Mathf.Clamp01((Time.unscaledTime - _stateEnteredAt) / WindupTime);

                Render(AbilityColor,
                    Mathf.Lerp(0.3f, 0.8f, Mathf.PingPong(Time.unscaledTime * 3f, 1f)),
                    Mathf.Lerp(WindupScaleStart, WindupScaleEnd, charge));
                break;
            }

            case State.Active:
                Render(AbilityColor, 1f, 1f);
                break;

            case State.Cooldown:
                if (Time.unscaledTime - _stateEnteredAt >= CooldownTime)
                {
                    // Still holding: the server engages the queued press right about
                    // now, so go straight to the windup rather than blinking off.
                    SetState(_isHeld ? State.Pending : State.Idle);
                    return;
                }

                Render(CooldownColor, 0.8f, 1f);
                break;
        }

        _shield.style.display = DisplayStyle.Flex;
        Reposition();
    }

    private static void Render(Color color, float opacity, float scale)
    {
        _shield.style.opacity = opacity;
        _shield.style.scale = new Scale(new Vector2(scale, scale));

        // Opacity is applied at render time, so only an actual color change needs the
        // path rebuilt. The windup pulse costs nothing.
        if (_shieldColor == color) return;

        _shieldColor = color;
        _shield.MarkDirtyRepaint();
    }

    /// <summary>
    /// Horizontal placement is screen centre, set once at setup -- centring on the
    /// stamina bar's own rect instead would inherit any offset that bar has.
    ///
    /// Vertical placement tracks the bar every visible frame, since the HUD rescales
    /// with resolution and UI scale. Both rects are read in panel space so the answer
    /// does not depend on where either element sits in the hierarchy.
    /// </summary>
    private static void Reposition()
    {
        if (_staminaBar == null) return;

        var bar = _staminaBar.worldBound;
        var host = _shield.parent?.worldBound ?? default;
        if (float.IsNaN(bar.yMin) || float.IsNaN(host.yMin)) return;

        _shield.style.top = bar.yMin - host.yMin - GlyphBottom - BarGap;
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>Maps a point from the glyph's 24x24 authoring box into element pixels.</summary>
    private static Vector2 P(float x, float y) => new(x * Scale, y * Scale);

    /// <summary>
    /// The Feather "shield" glyph, traced from its path data:
    /// <c>M12 22 s8-4 8-10 V5 l-8-3 -8 3 v7 c0 6 8 10 8 10 z</c>
    ///
    /// Filled with the state color, no stroke.
    /// </summary>
    private static void DrawShield(MeshGenerationContext context)
    {
        var painter = context.painter2D;

        painter.BeginPath();

        // Bottom point, then the right flank sweeping up. The SVG's "s" shorthand
        // mirrors no previous curve here, so its first control point is the start point.
        painter.MoveTo(P(12f, 22f));
        painter.BezierCurveTo(P(12f, 22f), P(20f, 18f), P(20f, 12f));

        // Right edge up to the shoulder, across the angled top, down the left edge.
        painter.LineTo(P(20f, 5f));
        painter.LineTo(P(12f, 2f));
        painter.LineTo(P(4f, 5f));
        painter.LineTo(P(4f, 12f));

        // Left flank back down to the point.
        painter.BezierCurveTo(P(4f, 18f), P(12f, 22f), P(12f, 22f));
        painter.ClosePath();

        painter.fillColor = _shieldColor;
        painter.Fill();
    }

    // -------------------------------------------------------------------- setup

    private static void Setup()
    {
        if (_shield != null) return;

        var root = MonoBehaviourSingleton<UIManager>.Instance?.RootVisualElement;
        if (root == null) return;

        // The bar supplies the vertical anchor only; the shield lives on the root so it
        // is centred on the screen and cannot disturb the HUD's own layout.
        _staminaBar = root.Q("HUDView")?.Q("StaminaProgressBar");

        _shield = new VisualElement { name = "PuckBlockIndicator" };
        _shield.style.position = Position.Absolute;
        _shield.style.display = DisplayStyle.None;
        _shield.style.width = IconSize;
        _shield.style.height = IconSize;
        // Clicks must fall through to the game, this is a readout only.
        _shield.pickingMode = PickingMode.Ignore;
        _shield.generateVisualContent += DrawShield;

        // Screen centre. The glyph is symmetric about the middle of its box, so
        // centring the element centres the shield.
        _shield.style.left = Length.Percent(50f);
        _shield.style.translate = new Translate(-IconSize * 0.5f, 0);

        // Scale about the point of the shield, so the windup grows upward out of a
        // fixed spot instead of drifting away from the stamina bar as it changes size.
        _shield.style.transformOrigin = new TransformOrigin(Length.Percent(50f), GlyphBottom);

        root.Add(_shield);

        // Fallback for a HUD without the bar: sit above where it would have been.
        if (_staminaBar == null) _shield.style.bottom = 140;

        _shield.schedule.Execute(Tick).Every(16);
    }
}
