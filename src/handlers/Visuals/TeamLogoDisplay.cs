using System;
using System.Collections;
using Newtonsoft.Json;
using ToastersRinkCompanion.modifiers;
using UnityEngine;

namespace ToastersRinkCompanion.handlers;

public static class TeamLogoDisplay
{
    // Red goal is at -Z, Blue goal is at +Z
    private static readonly Vector3 RedPosition = new Vector3(0f, 3f, -52.4f);
    private static readonly Vector3 BluePosition = new Vector3(0f, 3f, 52.4f);
    private static readonly float LogoSize = 4.5f;

    private static GameObject _redLogoBoard;
    private static GameObject _blueLogoBoard;
    private static Texture2D _redTexture;
    private static Texture2D _blueTexture;

    public static void RegisterHandlers()
    {
        JsonMessageRouter.RegisterHandler("team_logo", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                var payload = JsonConvert.DeserializeObject<TeamLogoPayload>(payloadJson);
                if (payload == null || string.IsNullOrEmpty(payload.acronym))
                {
                    Plugin.LogError("team_logo: invalid payload");
                    return;
                }

                Plugin.Log($"TeamLogoDisplay: received team_logo for {payload.team} -> {payload.acronym}");

                EISTeamData.EnsureFetched();

                // If EIS data isn't loaded yet, wait for it via coroutine
                if (!EISTeamData.HasData)
                {
                    var hook = MonoBehaviourSingleton<UIManager>.Instance?.GameState;
                    if (hook != null)
                    {
                        hook.StartCoroutine(WaitForDataThenShowLogo(payload));
                    }
                    return;
                }

                FetchAndShowLogo(payload);
            }
            catch (Exception e)
            {
                Plugin.LogError($"team_logo handler error: {e}");
            }
        });

        JsonMessageRouter.RegisterHandler("team_logo_clear", (sender, payloadJson) =>
        {
            if (!MessagingHandler.connectedToToastersRink) return;

            try
            {
                var payload = JsonConvert.DeserializeObject<TeamLogoClearPayload>(payloadJson);
                if (payload == null) return;

                if (payload.team == "blue" || payload.team == "all")
                {
                    DestroyBoard(ref _blueLogoBoard, ref _blueTexture);
                }
                if (payload.team == "red" || payload.team == "all")
                {
                    DestroyBoard(ref _redLogoBoard, ref _redTexture);
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"team_logo_clear handler error: {e}");
            }
        });
    }

    private static IEnumerator WaitForDataThenShowLogo(TeamLogoPayload payload)
    {
        float waited = 0f;
        while (!EISTeamData.HasData && waited < 10f)
        {
            yield return new WaitForSeconds(0.5f);
            waited += 0.5f;
        }

        if (!EISTeamData.HasData)
        {
            Plugin.LogError("TeamLogoDisplay: timed out waiting for EIS data");
            yield break;
        }

        FetchAndShowLogo(payload);
    }

    private static void FetchAndShowLogo(TeamLogoPayload payload)
    {
        EISTeamData.GetLogoAsync(payload.acronym, tex =>
        {
            if (tex == null)
            {
                Plugin.LogError($"TeamLogoDisplay: no logo found for {payload.acronym}");
                return;
            }

            Color backingColor = new Color(0.12f, 0.12f, 0.12f);
            if (!string.IsNullOrEmpty(payload.hexColor) &&
                ColorUtility.TryParseHtmlString("#" + payload.hexColor, out var parsed))
            {
                backingColor = parsed;
            }

            if (payload.team == "blue")
            {
                ShowLogo(ref _blueLogoBoard, ref _blueTexture, tex, BluePosition, 0f, backingColor);
            }
            else if (payload.team == "red")
            {
                ShowLogo(ref _redLogoBoard, ref _redTexture, tex, RedPosition, 180f, backingColor);
            }
        });
    }

    private static void ShowLogo(ref GameObject board, ref Texture2D storedTex, Texture2D tex,
        Vector3 position, float yRotation, Color backingColor)
    {
        // Tear down existing board for this side
        if (board != null)
        {
            UnityEngine.Object.Destroy(board);
            board = null;
        }

        storedTex = tex;

        float aspectRatio = (float)tex.width / tex.height;
        float quadWidth = LogoSize * aspectRatio;

        // Parent object
        board = new GameObject("EISTeamLogo");
        board.transform.position = position;
        board.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        // Dark backing board (visible when arena geometry is hidden)
        var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backing.name = "LogoBacking";
        backing.transform.SetParent(board.transform);
        backing.transform.localRotation = Quaternion.identity;
        backing.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        backing.transform.localScale = new Vector3(quadWidth + 0.3f, LogoSize + 0.3f, 0.08f);
        UnityEngine.Object.Destroy(backing.GetComponent<Collider>());
        var backingShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        if (backingShader != null)
        {
            var backingMat = new Material(backingShader);
            backingMat.color = backingColor;
            backingMat.renderQueue = 2500;
            backing.GetComponent<MeshRenderer>().material = backingMat;
        }

        // Image quad
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "LogoImage";
        quad.transform.SetParent(board.transform);
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localScale = new Vector3(quadWidth, LogoSize, 1f);
        UnityEngine.Object.Destroy(quad.GetComponent<Collider>());

        var texShader = Shader.Find("Unlit/Texture")
            ?? Shader.Find("UI/Default");
        if (texShader != null)
        {
            var mat = new Material(texShader);
            mat.mainTexture = tex;
            // Render before transparent geometry (glass) so it appears behind the glass
            mat.renderQueue = 2500;
            quad.GetComponent<MeshRenderer>().material = mat;
        }

        Plugin.Log($"TeamLogoDisplay: showing logo at {position}");
    }

    private static void DestroyBoard(ref GameObject board, ref Texture2D tex)
    {
        if (board != null)
        {
            UnityEngine.Object.Destroy(board);
            board = null;
        }
        tex = null;
    }

    public static void Cleanup()
    {
        DestroyBoard(ref _redLogoBoard, ref _redTexture);
        DestroyBoard(ref _blueLogoBoard, ref _blueTexture);
    }

    public class TeamLogoPayload
    {
        public string team; // "red" or "blue"
        public string acronym; // EIS team acronym for logo lookup
        public string hexColor; // EIS team hex color (e.g. "FF0000")
    }

    public class TeamLogoClearPayload
    {
        public string team; // "red", "blue", or "all"
    }
}
