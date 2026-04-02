using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ToastersRinkCompanion.handlers;

public static class MemeDisplay
{
    private static GameObject _memeBoard;
    private static GameObject _memeQuad;
    private static Texture2D _currentTexture;
    private static int _currentMemeId;
    private static MemeDisplayPayload _currentPayload;
    private static bool _textureReady; // texture downloaded but board may not be shown
    private static TextMesh _infoText; // submitter + like/dislike counts
    private static TextMesh _likeCountText;
    private static TextMesh _dislikeCountText;

    // Position behind the glass, outside the boards
    private static readonly Vector3 BoardPosition = new Vector3(-22.4f, 3.5f, 0f);
    private static readonly float BoardHeight = 4f; // meters tall, width scales with aspect ratio

    // Button visual positions — decorative only, colliders are server-side
    private static readonly float ButtonSize = 0.6f;
    private static readonly float ButtonOffsetZ = 3.5f;
    private static readonly float ButtonY = 1.8f;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterHandler("meme_display", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                var payload = JsonConvert.DeserializeObject<MemeDisplayPayload>(payloadJson);
                if (payload == null || string.IsNullOrEmpty(payload.url))
                {
                    Plugin.LogError("meme_display: invalid payload");
                    return;
                }

                _currentPayload = payload;

                // If same meme already downloaded, just show/hide based on phase
                if (payload.id == _currentMemeId && _textureReady)
                {
                    ShowIfWarmup();
                    return;
                }

                _currentMemeId = payload.id;
                _textureReady = false;

                var monoBehaviourHook = MonoBehaviourSingleton<UIManager>.Instance?.GameState;
                if (monoBehaviourHook == null)
                {
                    Plugin.LogError("meme_display: UIGameState.Instance is null");
                    return;
                }

                monoBehaviourHook.StartCoroutine(LoadMemeTexture(payload));
            }
            catch (Exception e)
            {
                Plugin.LogError($"meme_display handler error: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("meme_clear", (sender, payloadJson) =>
        {
            Cleanup();
        });

        JsonMessageRouter.RegisterHandler("meme_react_update", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                var payload = JsonConvert.DeserializeObject<MemeReactUpdatePayload>(payloadJson);
                if (payload == null) return;

                if (_currentPayload != null && payload.id == _currentPayload.id)
                {
                    _currentPayload.likes = payload.likes;
                    _currentPayload.dislikes = payload.dislikes;
                    UpdateCountLabels();
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"meme_react_update handler error: {e}");
            }
        });
    }

    /// <summary>
    /// Called from game state change patch. Shows board on warmup, hides otherwise.
    /// </summary>
    public static void OnGamePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        if (newPhase == GamePhase.Warmup)
        {
            ShowIfWarmup();
        }
        else if (oldPhase == GamePhase.Warmup && newPhase != GamePhase.Warmup)
        {
            HideBoard();
        }
    }

    private static void ShowIfWarmup()
    {
        if (!_textureReady || _currentTexture == null) return;

        // Check if currently in warmup
        var gm = GameManager.Instance;
        if (gm == null || gm.GameState.Value.Phase != GamePhase.Warmup) return;

        // Already showing
        if (_memeBoard != null) return;

        CreateBoard();
    }

    private static void HideBoard()
    {
        if (_memeBoard != null)
        {
            UnityEngine.Object.Destroy(_memeBoard);
            _memeBoard = null;
            _memeQuad = null;
        }
    }

    private static IEnumerator LoadMemeTexture(MemeDisplayPayload payload)
    {
        Plugin.Log($"MemeDisplay: downloading meme #{payload.id} from {payload.url}");

        Texture2D downloadedTexture = null;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(payload.url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Plugin.LogError($"MemeDisplay: download failed: {request.error}");
                yield break;
            }

            downloadedTexture = DownloadHandlerTexture.GetContent(request);
        }

        if (downloadedTexture == null)
        {
            Plugin.LogError("MemeDisplay: downloaded texture is null");
            yield break;
        }

        // Clean up old texture
        if (_currentTexture != null)
        {
            UnityEngine.Object.Destroy(_currentTexture);
        }

        _currentTexture = downloadedTexture;
        _textureReady = true;

        string submittedBy = !string.IsNullOrEmpty(payload.uploaded_by_name)
            ? payload.uploaded_by_name
            : payload.uploaded_by_steam_id;
        Plugin.Log($"MemeDisplay: meme #{payload.id} by {submittedBy} ready ({payload.likes} likes, {payload.dislikes} dislikes)");

        // Show immediately if we're in warmup
        ShowIfWarmup();
    }

    private static void CreateBoard()
    {
        // Clean up existing board first
        HideBoard();

        float aspectRatio = (float)_currentTexture.width / _currentTexture.height;
        float quadWidth = BoardHeight * aspectRatio;

        // Create parent object for the whole meme board
        _memeBoard = new GameObject("DailyMemeBoard");
        _memeBoard.transform.position = BoardPosition;

        // Create the backing board (thin cube behind the image)
        var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backing.name = "MemeBacking";
        backing.transform.SetParent(_memeBoard.transform);
        backing.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        backing.transform.localPosition = new Vector3(-0.05f, 0f, 0f);
        backing.transform.localScale = new Vector3(quadWidth + 0.3f, BoardHeight + 0.3f, 0.08f);
        UnityEngine.Object.Destroy(backing.GetComponent<Collider>());

        // Dark material for the backing
        var backingRenderer = backing.GetComponent<MeshRenderer>();
        var backingShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        if (backingShader != null)
        {
            var backingMat = new Material(backingShader);
            backingMat.color = new Color(0.15f, 0.15f, 0.15f);
            backingRenderer.material = backingMat;
        }

        // Create the image quad facing +X
        _memeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _memeQuad.name = "MemeImage";
        _memeQuad.transform.SetParent(_memeBoard.transform);
        _memeQuad.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        _memeQuad.transform.localPosition = Vector3.zero;
        _memeQuad.transform.localScale = new Vector3(quadWidth, BoardHeight, 1f);
        UnityEngine.Object.Destroy(_memeQuad.GetComponent<Collider>());

        // Apply the texture — Unlit/Texture works, URP Unlit does not render properly
        var texShader = Shader.Find("Unlit/Texture")
            ?? Shader.Find("UI/Default");
        if (texShader != null)
        {
            var mat = new Material(texShader);
            mat.mainTexture = _currentTexture;
            _memeQuad.GetComponent<MeshRenderer>().material = mat;
        }

        // Create visual like button (to the right of the board in local X)
        var likeBtn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        likeBtn.name = "MemeLikeButtonVisual";
        likeBtn.transform.SetParent(_memeBoard.transform, false);
        likeBtn.transform.localPosition = new Vector3(0.15f, ButtonY - BoardPosition.y, ButtonOffsetZ);
        likeBtn.transform.localScale = new Vector3(ButtonSize / 3f, ButtonSize, ButtonSize);
        UnityEngine.Object.Destroy(likeBtn.GetComponent<Collider>());
        var likeMat = new Material(backingShader ?? Shader.Find("Standard"));
        likeMat.color = new Color(0.2f, 0.7f, 0.2f); // green
        likeBtn.GetComponent<MeshRenderer>().material = likeMat;

        // Create visual dislike button (to the left of the board in local X)
        var dislikeBtn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dislikeBtn.name = "MemeDislikeButtonVisual";
        dislikeBtn.transform.SetParent(_memeBoard.transform, false);
        dislikeBtn.transform.localPosition = new Vector3(0.15f, ButtonY - BoardPosition.y, -ButtonOffsetZ);
        dislikeBtn.transform.localScale = new Vector3(ButtonSize / 3f, ButtonSize, ButtonSize);
        UnityEngine.Object.Destroy(dislikeBtn.GetComponent<Collider>());
        var dislikeMat = new Material(backingShader ?? Shader.Find("Standard"));
        dislikeMat.color = new Color(0.7f, 0.2f, 0.2f); // red
        dislikeBtn.GetComponent<MeshRenderer>().material = dislikeMat;

        // Like count label — positioned in front of button (positive X = toward rink)
        var likeLabelPos = likeBtn.transform.localPosition + new Vector3(ButtonSize / 3f / 2f + 0.01f, 0f, 0f);
        _likeCountText = CreateWorldText(_memeBoard.transform,
            $"{_currentPayload?.likes ?? 0}", Color.white,
            likeLabelPos, Vector3.one, 48, 0.12f);

        // Dislike count label
        var dislikeLabelPos = dislikeBtn.transform.localPosition + new Vector3(ButtonSize / 3f / 2f + 0.01f, 0f, 0f);
        _dislikeCountText = CreateWorldText(_memeBoard.transform,
            $"{_currentPayload?.dislikes ?? 0}", Color.white,
            dislikeLabelPos, Vector3.one, 48, 0.12f);

        // Info text below the meme
        string submittedBy = _currentPayload?.uploaded_by_name ?? "Unknown";
        _infoText = CreateWorldText(_memeBoard.transform,
            $"From: {submittedBy}", new Color(0.85f, 0.85f, 0.85f),
            new Vector3(0f, -(BoardHeight / 2f) - 0.3f, 0f), Vector3.one,
            32, 0.1f, TextAnchor.UpperCenter);

        // Helper text
        CreateWorldText(_memeBoard.transform,
            "Hit the buttons with a puck or stick to vote!", new Color(0.55f, 0.55f, 0.55f),
            new Vector3(0f, -(BoardHeight / 2f) - 0.7f, 0f), Vector3.one,
            24, 0.08f, TextAnchor.UpperCenter);

        Plugin.Log($"MemeDisplay: board shown for meme #{_currentMemeId}");
    }

    private static TextMesh CreateWorldText(Transform parent, string text, Color color,
        Vector3 localPos, Vector3 localScale, int fontSize = 48, float charSize = 0.15f,
        TextAnchor anchor = TextAnchor.MiddleCenter, bool shadow = true)
    {
        var go = new GameObject("WorldText");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        go.transform.localScale = localScale;

        // Shadow text (slightly behind and offset)
        if (shadow)
        {
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.transform.localPosition = new Vector3(0.01f, -0.01f, 0.005f);
            var shadowTm = shadowGo.AddComponent<TextMesh>();
            shadowTm.text = text;
            shadowTm.fontSize = fontSize;
            shadowTm.characterSize = charSize;
            shadowTm.anchor = anchor;
            shadowTm.alignment = TextAlignment.Center;
            shadowTm.color = new Color(0f, 0f, 0f, 0.6f);

            // Swap shadow to depth-respecting shader
            ApplyDepthTestedTextMaterial(shadowGo.GetComponent<MeshRenderer>());
        }

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = fontSize;
        tm.characterSize = charSize;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        // Swap to depth-respecting shader (GUI/Text Shader has ZTest Always hardcoded)
        ApplyDepthTestedTextMaterial(go.GetComponent<MeshRenderer>());

        return tm;
    }

    private static void ApplyDepthTestedTextMaterial(MeshRenderer renderer)
    {
        if (renderer == null || renderer.material == null) return;

        // GUI/Text Shader has ZTest Always hardcoded, so we must swap shaders.
        // Use Sprites/Default which supports alpha and respects depth.
        // Color comes from TextMesh vertex colors, not the material.
        var depthShader = Shader.Find("Sprites/Default");
        if (depthShader == null) return;

        var oldMat = renderer.material;
        var mat = new Material(depthShader);
        mat.mainTexture = oldMat.mainTexture; // preserve font atlas
        mat.color = Color.white; // let TextMesh vertex colors control tint
        mat.renderQueue = 2999;
        renderer.material = mat;
    }

    private static void UpdateTextWithShadow(TextMesh tm, string text)
    {
        if (tm == null) return;
        tm.text = text;
        // Update shadow child if it exists
        var shadow = tm.transform.Find("Shadow");
        if (shadow != null)
        {
            var shadowTm = shadow.GetComponent<TextMesh>();
            if (shadowTm != null) shadowTm.text = text;
        }
    }

    private static void UpdateCountLabels()
    {
        if (_currentPayload == null) return;
        UpdateTextWithShadow(_likeCountText, $"{_currentPayload.likes}");
        UpdateTextWithShadow(_dislikeCountText, $"{_currentPayload.dislikes}");
    }

    private static void DestroyBoard()
    {
        HideBoard();

        if (_currentTexture != null)
        {
            UnityEngine.Object.Destroy(_currentTexture);
            _currentTexture = null;
        }
    }

    public static void Cleanup()
    {
        DestroyBoard();
        _currentMemeId = 0;
        _currentPayload = null;
        _textureReady = false;
    }

    /// <summary>
    /// Send a like or dislike reaction for the current meme.
    /// </summary>
    public static void SendReaction(string reaction)
    {
        if (_currentMemeId == 0) return;

        JsonMessageRouter.SendMessage(
            "meme_react",
            Unity.Netcode.NetworkManager.ServerClientId,
            new { reaction }
        );
    }

    // Harmony patch to hook game phase changes
    [HarmonyPatch(typeof(LevelController), "Event_Everyone_OnGameStateChanged")]
    public static class MemeDisplayGamePhaseChangedPatch
    {
        static void Postfix(Dictionary<string, object> eventParams)
        {
            try
            {
                GamePhase newPhase = ((GameState)eventParams["newGameState"]).Phase;
                GamePhase oldPhase = ((GameState)eventParams["oldGameState"]).Phase;

                if (oldPhase != newPhase)
                {
                    OnGamePhaseChanged(oldPhase, newPhase);
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"MemeDisplay phase change patch error: {e}");
            }
        }
    }

    // Payload classes
    public class MemeDisplayPayload
    {
        public int id;
        public string url;
        public string active_until;
        public string uploaded_by_steam_id;
        public string uploaded_by_name;
        public int likes;
        public int dislikes;
    }

    public class MemeReactUpdatePayload
    {
        public int id;
        public int likes;
        public int dislikes;
    }
}
