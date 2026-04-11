using System;
using System.Collections.Generic;
using HarmonyLib;
using ToastersRinkCompanion.modifiers;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

/// <summary>
/// Drives the in-world fresnel glow overlay applied to players. Two distinct modes:
///
/// <list type="bullet">
/// <item><b>Stars</b> (Warmup / GameOver / PostGame): highlights the three star
/// players from the last match with gold/silver/bronze.</item>
/// <item><b>TeamCelebration</b> (RedScore / BlueScore): highlights every player
/// on the scoring team with that team's color. Team color is pulled from the
/// optional ToasterReskinLoader API via <see cref="TeamColorAccess"/>, falling
/// back to sensible defaults when TRL isn't installed.</item>
/// </list>
///
/// The local player is skipped to avoid first-person camera clipping.
///
/// Asset bundle: <c>assetbundles/fresnel</c>.
/// Material: <c>assets/toaster's rink/coloredfresnelmaterial.mat</c> (shader exposes <c>_Color</c>).
///
/// Puck player models are NOT rigged — the body is a collection of ~60 individual
/// <c>MeshRenderer</c>s whose transforms are driven each frame by animation logic.
/// So for each source renderer we instantiate a child GameObject carrying a
/// <c>MeshFilter</c> (sharing the source mesh) and a <c>MeshRenderer</c> (wearing
/// a unique fresnel material instance). Because the overlay is a child of the
/// source's own GameObject, it inherits the parent's animated transform for free.
/// </summary>
public static class StarPlayerGlow
{
    private enum GlowMode
    {
        None,
        Stars,
        BlueCelebration,
        RedCelebration,
    }

    /// <summary>
    /// The most recent celebration mode (BlueCelebration/RedCelebration), remembered
    /// across the RedScore/BlueScore → Replay phase transition so the replay glow
    /// can use the same team color as the preceding score announcement.
    /// </summary>
    private static GlowMode _lastCelebrationMode = GlowMode.None;

    private static AssetBundle _loadedAssetBundle;
    private static GameObject _fresnelPrefab;
    private static Material _sharedFresnelMaterial;

    // Per-player list of overlay GameObjects
    private static readonly Dictionary<string, List<GameObject>> _activeOverlays = new();
    // Material instances we created so we can destroy them on cleanup
    private static readonly List<Material> _ownedMaterials = new();

    // Stars colors
    private static readonly Color Gold = new Color32(0xFF, 0xD7, 0x00, 0xFF);
    private static readonly Color Silver = new Color32(0xED, 0xED, 0xED, 0xFF);
    private static readonly Color Bronze = new Color32(0xCD, 0x7F, 0x32, 0xFF);

    private static GlowMode _currentMode = GlowMode.None;

    public static void RegisterEvents()
    {
        MatchStarsStore.OnStarsChanged += OnStarsChanged;
        TeamColorAccess.OnTeamColorsChanged += OnTeamColorsChanged;
    }

    /// <summary>
    /// PlayerBody.ApplyCustomizations runs after the body mesh children are fully
    /// built AND after Player.PlayerBody is linked (see PlayerBody.OnPlayerReferenceChanged
    /// in the decompiled source). That makes it the correct hook — the earlier
    /// Event_Everyone_OnPlayerBodySpawned fires on an empty shell with no Player link.
    /// ToasterMattePlayers uses this same hook point.
    /// </summary>
    [HarmonyPatch(typeof(PlayerBody), nameof(PlayerBody.ApplyCustomizations))]
    public static class StarGlowApplyCustomizationsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerBody __instance)
        {
            try
            {
                OnPlayerBodyReady(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.LogError($"[StarPlayerGlow] ApplyCustomizations postfix error: {e}");
            }
        }
    }

    private static void OnPlayerBodyReady(PlayerBody playerBody)
    {
        if (_currentMode != GlowMode.Stars &&
            _currentMode != GlowMode.BlueCelebration &&
            _currentMode != GlowMode.RedCelebration) return;

        if (playerBody == null || playerBody.Player == null) return;

        var player = playerBody.Player;
        bool isReplay = player.IsReplay != null && player.IsReplay.Value;
        string user = player.Username.Value.ToString();

        // Resolve the effective team. Replay clones' Player.GameState may not have
        // synced yet at customization time — fall back to the original (live) player
        // via the +1337 OwnerClientId offset.
        PlayerTeam effectiveTeam = player.Team;
        if (isReplay && effectiveTeam == PlayerTeam.None)
        {
            ulong originalClientId = player.OwnerClientId - 1337UL;
            var original = PlayerManager.Instance?.GetPlayerByClientId(originalClientId);
            if (original != null) effectiveTeam = original.Team;
        }

        Plugin.Log($"[StarPlayerGlow] OnPlayerBodyReady: player={user} team={player.Team} " +
                   $"effectiveTeam={effectiveTeam} isReplay={isReplay} isLocal={player.IsLocalPlayer} " +
                   $"mode={_currentMode}");

        // Stars mode: only glow if this specific player is a star
        if (_currentMode == GlowMode.Stars)
        {
            if (player.SteamId == null) return;
            string steamId = player.SteamId.Value.ToString();
            int rank = MatchStarsStore.RankBySteamId(steamId);
            if (rank == 0) return;

            if (!isReplay && player.IsLocalPlayer) return;

            if (!EnsureAssetsLoaded()) return;
            Color color = rank switch { 1 => Gold, 2 => Silver, _ => Bronze };
            ApplyToPlayer(player, color);
            return;
        }

        // Celebration modes
        PlayerTeam targetTeam = _currentMode == GlowMode.BlueCelebration ? PlayerTeam.Blue : PlayerTeam.Red;
        if (effectiveTeam != targetTeam) return;

        if (!isReplay && player.IsLocalPlayer) return;

        if (!EnsureAssetsLoaded()) return;
        Color celColor = _currentMode == GlowMode.BlueCelebration
            ? TeamColorAccess.BlueTeamColor
            : TeamColorAccess.RedTeamColor;
        ApplyToPlayer(player, celColor);
    }

    private static void OnStarsChanged()
    {
        Plugin.Log($"[StarPlayerGlow] OnStarsChanged: currentMode={_currentMode} hasStars={MatchStarsStore.HasStars}");

        // If we haven't seen a phase change yet, infer mode from the current phase.
        if (_currentMode == GlowMode.None)
        {
            var inferred = InferModeFromCurrentPhase();
            Plugin.Log($"[StarPlayerGlow] OnStarsChanged: inferred mode={inferred}");
            if (inferred != GlowMode.None)
                _currentMode = inferred;
        }

        if (_currentMode == GlowMode.Stars)
            ApplyStars();
    }

    private static void OnTeamColorsChanged()
    {
        // Re-apply with the new team colors if we're currently in a celebration mode.
        if (_currentMode == GlowMode.BlueCelebration || _currentMode == GlowMode.RedCelebration)
            ApplyCelebration(_currentMode);
    }

    private static GlowMode InferModeFromCurrentPhase()
    {
        var gm = NetworkBehaviourSingleton<GameManager>.Instance;
        if (gm == null) return GlowMode.None;

        var phase = gm.GameState.Value.Phase;
        return phase switch
        {
            GamePhase.Warmup => GlowMode.Stars,
            GamePhase.GameOver => GlowMode.Stars,
            GamePhase.PostGame => GlowMode.Stars,
            GamePhase.BlueScore => GlowMode.BlueCelebration,
            GamePhase.RedScore => GlowMode.RedCelebration,
            // Replay inherits whatever scoring phase came before it
            GamePhase.Replay => _lastCelebrationMode,
            _ => GlowMode.None,
        };
    }

    // ---------------------------------------------------------------
    // Asset loading
    // ---------------------------------------------------------------

    private static bool EnsureAssetsLoaded()
    {
        if (_sharedFresnelMaterial != null) return true;

        try
        {
            if (_loadedAssetBundle == null)
            {
                _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/fresnel");
                if (_loadedAssetBundle != null)
                {
                    // Dump the contents so we can verify the exact asset paths if anything fails
                    var names = _loadedAssetBundle.GetAllAssetNames();
                    Plugin.Log($"[StarPlayerGlow] fresnel bundle loaded, {names.Length} assets:");
                    foreach (var n in names) Plugin.Log($"[StarPlayerGlow]   - {n}");
                }
            }

            if (_loadedAssetBundle == null)
            {
                Plugin.LogError("[StarPlayerGlow] Failed to load fresnel asset bundle");
                return false;
            }

            // Load the PREFAB and extract the material from its renderer — loading a
            // .mat directly from a bundle can leave the shader reference dangling
            // because shaders follow a different serialization path.
            _fresnelPrefab = PrefabHelper.LoadPrefab(
                _loadedAssetBundle, "assets/toaster's rink/coloredfresnelrenderer.prefab");

            if (_fresnelPrefab == null)
            {
                Plugin.LogError("[StarPlayerGlow] Failed to load ColoredFresnelRenderer prefab");
                return false;
            }

            var renderer = _fresnelPrefab.GetComponentInChildren<Renderer>(includeInactive: true);
            if (renderer == null)
            {
                Plugin.LogError("[StarPlayerGlow] ColoredFresnelRenderer prefab has no Renderer component");
                return false;
            }

            _sharedFresnelMaterial = renderer.sharedMaterial;
            if (_sharedFresnelMaterial == null)
            {
                Plugin.LogError("[StarPlayerGlow] Fresnel prefab's renderer has no sharedMaterial");
                return false;
            }

            Plugin.Log($"[StarPlayerGlow] Loaded fresnel material '{_sharedFresnelMaterial.name}' " +
                       $"shader='{_sharedFresnelMaterial.shader?.name}' " +
                       $"hasColor={_sharedFresnelMaterial.HasProperty("_Color")}");

            return true;
        }
        catch (System.Exception e)
        {
            Plugin.LogError($"[StarPlayerGlow] Error loading assets: {e}");
            return false;
        }
    }

    // ---------------------------------------------------------------
    // Phase tracking
    // ---------------------------------------------------------------

    [HarmonyPatch(typeof(LevelController), "Event_Everyone_OnGameStateChanged")]
    public static class StarGlowPhaseChangedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Dictionary<string, object> eventParams)
        {
            try
            {
                GamePhase newPhase = ((GameState)eventParams["newGameState"]).Phase;
                GamePhase oldPhase = ((GameState)eventParams["oldGameState"]).Phase;
                if (oldPhase == newPhase) return;

                GlowMode nextMode = newPhase switch
                {
                    GamePhase.Warmup => GlowMode.Stars,
                    GamePhase.GameOver => GlowMode.Stars,
                    GamePhase.PostGame => GlowMode.Stars,
                    GamePhase.BlueScore => GlowMode.BlueCelebration,
                    GamePhase.RedScore => GlowMode.RedCelebration,
                    // Replay inherits the team that just scored
                    GamePhase.Replay => _lastCelebrationMode,
                    _ => GlowMode.None,
                };

                // Remember celebration mode so Replay can reuse it
                if (nextMode == GlowMode.BlueCelebration || nextMode == GlowMode.RedCelebration)
                    _lastCelebrationMode = nextMode;

                if (nextMode == _currentMode)
                {
                    // Even if the mode didn't change, Replay entry spawns new player
                    // instances (the replay clones), so we need to re-run the apply
                    // to pick them up.
                    if (newPhase == GamePhase.Replay &&
                        (nextMode == GlowMode.BlueCelebration || nextMode == GlowMode.RedCelebration))
                    {
                        Plugin.Log($"[StarPlayerGlow] Replay entered with existing {nextMode}, re-applying to pick up replay clones");
                        ApplyCelebration(nextMode);
                    }
                    return;
                }

                Plugin.Log($"[StarPlayerGlow] Phase {oldPhase} -> {newPhase}, mode {_currentMode} -> {nextMode}");
                _currentMode = nextMode;

                switch (nextMode)
                {
                    case GlowMode.Stars:
                        ApplyStars();
                        break;
                    case GlowMode.BlueCelebration:
                    case GlowMode.RedCelebration:
                        ApplyCelebration(nextMode);
                        break;
                    case GlowMode.None:
                    default:
                        RemoveAll();
                        break;
                }
            }
            catch (System.Exception e)
            {
                Plugin.LogError($"[StarPlayerGlow] phase change error: {e}");
            }
        }
    }

    // ---------------------------------------------------------------
    // Stars mode
    // ---------------------------------------------------------------

    private static void ApplyStars()
    {
        Plugin.Log($"[StarPlayerGlow] ApplyStars: hasStars={MatchStarsStore.HasStars}");
        if (!MatchStarsStore.HasStars) return;
        if (!EnsureAssetsLoaded()) return;

        RemoveAll();

        var playerManager = PlayerManager.Instance;
        if (playerManager == null) { Plugin.LogError("[StarPlayerGlow] PlayerManager.Instance null"); return; }

        var players = playerManager.GetPlayers();
        if (players == null) { Plugin.LogError("[StarPlayerGlow] GetPlayers() null"); return; }

        var localPlayer = playerManager.GetLocalPlayer();
        int applied = 0;

        foreach (var player in players)
        {
            if (player == null) continue;
            if (player == localPlayer) continue;
            if (player.SteamId == null) continue;

            string steamId = player.SteamId.Value.ToString();
            int rank = MatchStarsStore.RankBySteamId(steamId);
            if (rank == 0) continue;

            Color color = rank switch
            {
                1 => Gold,
                2 => Silver,
                _ => Bronze,
            };
            ApplyToPlayer(player, color);
            applied++;
        }

        Plugin.Log($"[StarPlayerGlow] ApplyStars done: applied to {applied} players " +
                   $"(total players={players.Count}, local skipped={localPlayer != null})");
    }

    // ---------------------------------------------------------------
    // Celebration mode (RedScore / BlueScore)
    // ---------------------------------------------------------------

    private static void ApplyCelebration(GlowMode mode)
    {
        Plugin.Log($"[StarPlayerGlow] ApplyCelebration: mode={mode}");
        if (!EnsureAssetsLoaded()) return;

        RemoveAll();

        var playerManager = PlayerManager.Instance;
        if (playerManager == null) return;

        var localPlayer = playerManager.GetLocalPlayer();

        PlayerTeam targetTeam = mode == GlowMode.BlueCelebration ? PlayerTeam.Blue : PlayerTeam.Red;
        Color color = mode == GlowMode.BlueCelebration
            ? TeamColorAccess.BlueTeamColor
            : TeamColorAccess.RedTeamColor;

        // Deep diagnostic: scan the whole scene for PlayerBody components
        var allBodiesInScene = UnityEngine.Object.FindObjectsOfType<PlayerBody>();
        Plugin.Log($"[StarPlayerGlow] Scene scan: FindObjectsOfType<PlayerBody>() returned {allBodiesInScene?.Length ?? 0}");
        if (allBodiesInScene != null)
        {
            for (int i = 0; i < allBodiesInScene.Length; i++)
            {
                var pb = allBodiesInScene[i];
                if (pb == null) continue;
                string hierarchyPath = GetHierarchyPath(pb.transform);
                int mrCount = pb.GetComponentsInChildren<MeshRenderer>(true).Length;
                Plugin.Log($"[StarPlayerGlow]   PlayerBody[{i}] '{pb.name}' path='{hierarchyPath}' meshRenderers={mrCount}");
            }
        }

        // Two separate lists to walk:
        //  1) GetSpawnedPlayersByTeam(team) — live players on that team
        //  2) GetReplayPlayers() — replay clones
        var liveList = new List<Player>();
        var replayList = new List<Player>();

        try
        {
            var spawned = playerManager.GetSpawnedPlayersByTeam(targetTeam);
            if (spawned != null) liveList.AddRange(spawned);
            Plugin.Log($"[StarPlayerGlow] GetSpawnedPlayersByTeam({targetTeam}) -> {liveList.Count}");
        }
        catch (System.Exception e)
        {
            Plugin.LogError($"[StarPlayerGlow] GetSpawnedPlayersByTeam failed: {e}");
        }

        try
        {
            var replays = playerManager.GetReplayPlayers();
            if (replays != null)
            {
                Plugin.Log($"[StarPlayerGlow] GetReplayPlayers() -> {replays.Count} total");
                foreach (var rp in replays)
                {
                    if (rp == null) { Plugin.Log("[StarPlayerGlow]   replay null"); continue; }
                    string user = rp.Username.Value.ToString();
                    bool isRep = rp.IsReplay != null && rp.IsReplay.Value;
                    Plugin.Log($"[StarPlayerGlow]   replay candidate: {user} team={rp.Team} isReplay={isRep}");
                    if (rp.Team != targetTeam) continue;
                    replayList.Add(rp);
                }
            }
            else
            {
                Plugin.Log("[StarPlayerGlow] GetReplayPlayers() returned null");
            }
        }
        catch (System.Exception e)
        {
            Plugin.LogError($"[StarPlayerGlow] GetReplayPlayers failed: {e}");
        }

        int applied = 0;
        int replayCloneCount = 0;

        foreach (var player in liveList)
        {
            if (player == null) continue;
            // Skip the real local player (first-person camera clipping)
            if (player == localPlayer) continue;
            ApplyToPlayer(player, color);
            applied++;
        }

        foreach (var player in replayList)
        {
            if (player == null) continue;
            // Replay clones are third-person — never skip them, even if they
            // correspond to the local player.
            ApplyToPlayer(player, color);
            applied++;
            replayCloneCount++;
        }

        Plugin.Log($"[StarPlayerGlow] ApplyCelebration done: team={targetTeam} " +
                   $"live={liveList.Count} replay={replayList.Count} " +
                   $"applied={applied} (replayClones={replayCloneCount}) " +
                   $"color=({color.r:F2},{color.g:F2},{color.b:F2})");
    }

    // ---------------------------------------------------------------
    // Shared apply / remove
    // ---------------------------------------------------------------

    private static void ApplyToPlayer(Player player, Color glowColor)
    {
        if (player.SteamId == null) return;

        Transform searchRoot = player.PlayerBody != null ? player.PlayerBody.transform : player.transform;
        if (searchRoot == null)
        {
            Plugin.Log($"[StarPlayerGlow] ApplyToPlayer: no transform for {player.Username.Value}");
            return;
        }

        var sourceRenderers = searchRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            Plugin.Log($"[StarPlayerGlow] ApplyToPlayer: {player.Username.Value} — no MeshRenderers under '{searchRoot.name}'");
            return;
        }

        // Filter to only renderers that will actually be drawn:
        //  - component enabled
        //  - GameObject active in hierarchy
        // This skips the hidden male/female body variant that ToasterReskinLoader
        // keeps in the hierarchy but disables.
        int skippedHidden = 0;
        var visibleRenderers = new List<MeshRenderer>(sourceRenderers.Length);
        foreach (var r in sourceRenderers)
        {
            if (r == null) continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) { skippedHidden++; continue; }
            visibleRenderers.Add(r);
        }

        string steamId = player.SteamId.Value.ToString();

        // If we already have overlays for this player, destroy them first. PlayerBody.ApplyCustomizations
        // can run multiple times (notably when ToasterReskinLoader swaps male/female body variants),
        // and without this we'd stack overlays AND leak them onto now-hidden body parts whose
        // Renderer.enabled flag was toggled but whose GameObject is still active in the hierarchy.
        if (_activeOverlays.TryGetValue(steamId, out var existing))
        {
            foreach (var go in existing)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            existing.Clear();
        }

        if (!_activeOverlays.TryGetValue(steamId, out var overlayList))
        {
            overlayList = new List<GameObject>();
            _activeOverlays[steamId] = overlayList;
        }

        int created = 0;
        foreach (var src in visibleRenderers)
        {
            if (src == null) continue;

            var srcFilter = src.GetComponent<MeshFilter>();
            if (srcFilter == null || srcFilter.sharedMesh == null) continue;

            // Parent the overlay as a CHILD of the source renderer's GameObject.
            // That way the overlay inherits all the parent's animated transforms
            // (Puck drives each body piece via script every frame).
            //
            // The overlay is scaled up very slightly so it sits just outside the
            // original mesh surface. Without the nudge the two meshes share depth
            // and z-fight. Fresnel fades to transparent at head-on angles, so the
            // scale bump is invisible except at silhouette edges — exactly where
            // we want the glow anyway.
            var overlay = new GameObject("TRC_StarGlow");
            overlay.layer = src.gameObject.layer;
            overlay.transform.SetParent(src.transform, worldPositionStays: false);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one * 1.005f;

            var overlayFilter = overlay.AddComponent<MeshFilter>();
            overlayFilter.sharedMesh = srcFilter.sharedMesh;

            var overlayRenderer = overlay.AddComponent<MeshRenderer>();
            overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.enabled = true;

            // Fresh material instance so color doesn't bleed between players/ranks
            var matInstance = new Material(_sharedFresnelMaterial);
            matInstance.SetColor("_Color", glowColor);
            if (matInstance.HasProperty("_Visibility"))
                matInstance.SetFloat("_Visibility", 1f);
            overlayRenderer.sharedMaterial = matInstance;
            _ownedMaterials.Add(matInstance);

            overlayList.Add(overlay);
            created++;
        }

        bool isReplayClone = player.IsReplay != null && player.IsReplay.Value;
        Plugin.Log($"[StarPlayerGlow]   {player.Username.Value}: {created} overlay(s) from " +
                   $"{visibleRenderers.Count} visible / {sourceRenderers.Length} total MeshRenderer(s) " +
                   $"(skippedHidden={skippedHidden} layer={sourceRenderers[0].gameObject.layer} isReplay={isReplayClone})");

        // Dump the first 3 visible renderer paths so we can see the hierarchy
        for (int i = 0; i < System.Math.Min(3, visibleRenderers.Count); i++)
        {
            Plugin.Log($"[StarPlayerGlow]     [{i}] {GetHierarchyPath(visibleRenderers[i].transform)}");
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "<null>";
        var sb = new System.Text.StringBuilder();
        sb.Append(t.name);
        var p = t.parent;
        int depth = 0;
        while (p != null && depth < 10)
        {
            sb.Insert(0, p.name + "/");
            p = p.parent;
            depth++;
        }
        return sb.ToString();
    }

    private static void RemoveAll()
    {
        foreach (var kvp in _activeOverlays)
        {
            foreach (var go in kvp.Value)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
        }
        _activeOverlays.Clear();

        foreach (var mat in _ownedMaterials)
        {
            if (mat != null) UnityEngine.Object.Destroy(mat);
        }
        _ownedMaterials.Clear();
    }
}
