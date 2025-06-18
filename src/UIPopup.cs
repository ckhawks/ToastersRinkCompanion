using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace ToastersRinkCompanion;

public static class UIPopup
{
    // private static VisualElement prompt; 
    
    // --- CURSOR MANAGEMENT ---
    // Static fields to store the cursor's state before we change it.
    private static bool _previousCursorVisible;
    private static CursorLockMode _previousCursorLockState;
    
    static readonly FieldInfo _gameTimeLabelField = typeof(UIGameState)
        .GetField("gameTimeLabel", 
            BindingFlags.Instance | BindingFlags.NonPublic);  
    
    /// <summary>
    /// Kicks off the process to download an image and show the popup.
    /// </summary>
    public static void Show(ulong fromClientId, string imageUrl, string note)
    {
        if (Application.isBatchMode)
            return;

        // We need a MonoBehaviour instance to start a coroutine.
        // UIGameState.Instance is a perfect candidate.
        var monoBehaviourHook = UIGameState.Instance;
        if (monoBehaviourHook == null)
        {
            Plugin.LogError("Cannot show popup, UIGameState.Instance is null.");
            return;
        }

        // Start the coroutine on the MonoBehaviour instance.
        monoBehaviourHook.StartCoroutine(
            LoadAndShowPopupCoroutine(fromClientId, imageUrl, note)
        );
    }
    
    /// <summary>
    /// Coroutine to download the image and then build/show the UI.
    /// </summary>
    private static IEnumerator LoadAndShowPopupCoroutine(
        ulong fromClientId,
        string imageUrl,
        string note
    )
    {
        Plugin.Log($"Starting download for: {imageUrl}");
        Texture2D downloadedTexture = null;

        // 1. DOWNLOAD THE IMAGE
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (
                request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError
            )
            {
                Plugin.LogError($"Image download failed: {request.error}");
                yield break; // Stop execution if the download fails
            }
            else
            {
                downloadedTexture = DownloadHandlerTexture.GetContent(request);
            }
        }

        // --- At this point, the image is downloaded and in 'downloadedTexture' ---
        Plugin.Log("Download complete. Building UI...");

        // 2. BUILD THE UI (This is your original code, moved here)
        VisualElement wrapperContainer = new VisualElement();
        wrapperContainer.style.flexDirection = FlexDirection.Row;
        wrapperContainer.style.width =
            new StyleLength(new Length(100, LengthUnit.Percent));
        wrapperContainer.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        wrapperContainer.style.position = Position.Absolute;
        wrapperContainer.style.left = 0;
        wrapperContainer.style.top = 0;
        wrapperContainer.style.alignItems = Align.Center;
        wrapperContainer.style.justifyContent = Justify.Center;

        VisualElement container = new VisualElement();
        container.style.paddingBottom = 16;
        container.style.paddingTop = 16;
        container.style.paddingRight = 16;
        container.style.paddingLeft = 16;
        container.style.backgroundColor =
            new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        container.style.display = DisplayStyle.Flex;
        container.style.flexDirection = FlexDirection.Column;
        container.style.maxWidth = 400;
        container.style.minWidth = 400;
        container.style.minHeight = 400;
        container.style.maxHeight = 400;
        wrapperContainer.Add(container);

        Label titleLabel = new Label();
        Player sendingPlayer = PlayerManager.Instance.GetPlayerByClientId(fromClientId);
        titleLabel.text = $"NOTICE from {sendingPlayer.Username.Value}";
        titleLabel.style.fontSize = 24;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        titleLabel.style.color = Color.white;
        container.Add(titleLabel);

        // *** MODIFIED PART: Use the downloaded texture ***
        Image image = new Image();
        image.sprite = Sprite.Create(
            downloadedTexture,
            new Rect(0, 0, downloadedTexture.width, downloadedTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        image.scaleMode = ScaleMode.ScaleToFit;
        image.tintColor = Color.white; // no tint
        image.style.width = 300;
        image.style.height = 300;
        image.style.marginTop = 10;
        image.style.marginBottom = 10;
        container.Add(image);

        Label noteLabel = new Label();
        noteLabel.text = note; // Display the note, not the URL
        container.Add(noteLabel);

        Button closeButton = new Button();
        closeButton.text = "Agree";
        closeButton.style.backgroundColor =
            new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        closeButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        closeButton.style.width = 250;
        closeButton.style.minWidth = 250;
        closeButton.style.maxWidth = 250;
        closeButton.style.height = 50;
        closeButton.style.minHeight = 50;
        closeButton.style.maxHeight = 50;
        closeButton.style.paddingTop = 12;
        closeButton.style.paddingBottom = 12;
        closeButton.style.paddingLeft = 16;
        closeButton.style.paddingRight = 16;

        closeButton.RegisterCallback<MouseEnterEvent>(evt => {
            closeButton.style.backgroundColor = Color.white;
            closeButton.style.color = Color.black;
        });
        closeButton.RegisterCallback<MouseLeaveEvent>(evt => {
            closeButton.style.backgroundColor =
                new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            closeButton.style.color = Color.white;
        });

        // *** MODIFIED PART: The click handler now needs to destroy the texture ***
        closeButton.RegisterCallback<ClickEvent>(evt => {
            // Pass both the container and the texture to the Hide method
            Hide(wrapperContainer, downloadedTexture);
        });
        container.Add(closeButton);

        // 3. ADD THE COMPLETED UI TO THE SCREEN
        if (_gameTimeLabelField == null)
        {
            Plugin.LogError($"_gameTimeLabelField is null");
            yield break;
        }

        Label gameTimeLabel =
            (Label)_gameTimeLabelField.GetValue(UIGameState.Instance);
        if (gameTimeLabel == null)
        {
            Plugin.LogError($"gameTimeLabel is null");
            yield break;
        }

        // --- CURSOR MANAGEMENT: SAVE AND SET ---
        // 1. Save the current state before changing it.
        _previousCursorVisible = UnityEngine.Cursor.visible;
        _previousCursorLockState = UnityEngine.Cursor.lockState;
        
        // 2. Set the cursor to be visible and unlocked for UI interaction.
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        
        VisualElement root = gameTimeLabel.parent.parent.parent;
        root.Add(wrapperContainer);
        Plugin.Log($"Popup is now visible.");
    }
    
    /// <summary>
    /// Hides the container and cleans up the downloaded texture to prevent memory leaks.
    /// </summary>
    public static void Hide(VisualElement container, Texture2D textureToDestroy)
    {
        if (container == null) return;

        container.style.display = DisplayStyle.None;
        container.RemoveFromHierarchy();
        
        // --- CURSOR MANAGEMENT: RESTORE ---
        // Restore the cursor to the state it was in before the popup appeared.
        UnityEngine.Cursor.visible = _previousCursorVisible;
        UnityEngine.Cursor.lockState = _previousCursorLockState;

        // IMPORTANT: Destroy the texture object to free up memory
        if (textureToDestroy != null)
        {
            Object.Destroy(textureToDestroy);
        }
    }
}