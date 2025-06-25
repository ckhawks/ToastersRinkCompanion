using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering; // Still needed for RenderQueue, if used elsewhere, but not specifically for fade now

namespace ToastersRinkCompanion.collectibles;

// Removed custom SurfaceType and BlendMode enums that were causing issues and only for fade

public static class CollectibleRenderer
{
    private static GameObject toasterPrefab;
    private static AssetBundle _loadedAssetBundle;
    private static GameObject billboardTextPrefab; 
    
    // TODO when we move this to the serverside, we should raycast to the ground point (which won't always be 0) on the server, and then send that position to the clients to spawn it there
    
    // Y-offset for the collectible model from its ground XZ position
    private const float CollectibleBaseYOffset = 0.5f; 
    // Y-offset for the text billboard from the collectible's base XZ position
    private const float TextBillboardYOffset = 0.7f; // This positions the TEXT relative to the ground
    
    private const float BASE_CAMERA_PUSH_DISTANCE = 0.5f; // Base push, before object size
    
    // Configuration for text layout when duplicating in code
    // These values are highly dependent on your TMP_Text prefab's Canvas Scale and Font Size.
    // TUNE THESE CAREFULLY!
    private const float TEXT_LINE_SPACING = 0.15f; // Vertical distance between lines (tune this for your prefab)

    // Dictionary for managing random textures
    private static Dictionary<string, string> texturePaths = new Dictionary<string, string>
    {
        { "Default", null}, // Use null for default (no custom texture)
        { "Fade", "fade.png"},
        { "Damascus", "damascus.jpg"},
        { "Fire", "fire.png"},
        { "Flesh", "flesh.png"},
        { "Kryptek Mandrake", "Kryptek Mandrake.jpg"},
        { "Rainbow Gradient", "rainbow_gradient.jpg"},
        { "Red Splatter", "red-splatter.jpg" },
        { "RGB Digital", "rgb-digital.png"},
        { "Case Hardened", "case_hardened.png"},
        { "Burnt", "burnt.jpg"},
    };

    public class CollectibleInfo
    {
        public GameObject CollectibleGameObject;
        public GameObject TextBillboardGameObject; // The root Canvas GameObject
        public TMP_Text MainTextMeshProComponent;
        public TMP_Text SecondaryTextMeshProComponent; // Reference to the cloned text
        public RarityHelper.Rarity CurrentRarity; // Stored rarity
        public string ChosenTextureName; // New: Stored texture name
        public bool IsUpsideDown;
        public float OriginalCollectibleHeight; // Height after initial scaling
        public float SpawnTime; // When this collectible was spawned
        public float DespawnDelay = 10.0f; // How long it should last (e.g., 30 seconds)
        public float RandomScaleFactor; // Store the applied random scale
        public string SerialNumber; // New: Stored serial number
        public List<Material> CopiedMaterials = new List<Material>(); // To track materials we need to destroy
        public float MaxXZBoundsLength; // For dynamic text push distance
        // Removed FadeDuration and FadeStartTime
    }
    
    // Converted to List<CollectibleInfo>
    private static List<CollectibleInfo> activeCollectibles = new List<CollectibleInfo>();
    
    static readonly FieldInfo _bladeHandleField = typeof(Stick)
        .GetField("bladeHandle", 
            BindingFlags.Instance | BindingFlags.NonPublic);  
    
    public static void InitializeTextPrefab()
    {
        if (billboardTextPrefab != null) return;
        billboardTextPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/collectibles/collectiblenamecanvas.prefab");
        if (billboardTextPrefab == null)
        {
            Debug.LogError("Failed to load collectiblenamecanvas.prefab! Text will not be displayed.");
        }
        else
        {
            // The prefab should have ONE TMP_Text child configured.
            TMP_Text[] components = billboardTextPrefab.GetComponentsInChildren<TMP_Text>();
            if (components.Length != 1)
            {
                Debug.LogWarning($"Expected collectiblenamecanvas.prefab to have exactly one TMP_Text component, but found {components.Length}. Ensure correct prefab setup for text cloning.");
            }
        }
    }
    
    public static void ShowCollectible(Player player)
    {
        Stick stick = player.Stick;
        if (stick == null) return;
        
        LoadPrefab();
        InitializeTextPrefab();
        
        GameObject spawnedCollectible = Object.Instantiate(toasterPrefab);
        
        // --- Apply Random Scale ---
        float randomScaleMultiplier = Random.Range(0.1f, 5.0f); // Wide range for testing
        // Apply random scale on top of the prefab's default scale
        spawnedCollectible.transform.localScale = spawnedCollectible.transform.localScale * randomScaleMultiplier;
        
        // --- Get Bounding Box Info (after scaling) ---
        Renderer collectibleRenderer = spawnedCollectible.GetComponentInChildren<Renderer>();
        float originalHeight = 0f;
        float maxXZBoundsLength = 0f;
        if (collectibleRenderer != null)
        {
            // Use bounds.extents for half-lengths
            originalHeight = collectibleRenderer.bounds.size.y;
            maxXZBoundsLength = Mathf.Max(collectibleRenderer.bounds.extents.x, collectibleRenderer.bounds.extents.z);
        }
        else
        {
            Debug.LogWarning("Collectible has no Renderer! Cannot determine bounds for adjustments. Using fallbacks.");
            originalHeight = 1.0f * randomScaleMultiplier; // Fallback height
            maxXZBoundsLength = 0.5f * randomScaleMultiplier; // Fallback max XZ extent
        }
        
        // --- Apply Random Texture (using renderer.materials to get copies) ---
        List<string> textureKeys = new List<string>(texturePaths.Keys);
        string randomTextureDisplayName = textureKeys[Random.Range(0, textureKeys.Count)];
        string randomTextureFileName = texturePaths[randomTextureDisplayName];

        Texture2D chosenTexture = null;
        if (randomTextureFileName != null) // Only load if a specific texture is chosen
        {
             chosenTexture = LoadTexture2D(randomTextureFileName);
        }
        
        MeshRenderer[] renderers = spawnedCollectible.GetComponentsInChildren<MeshRenderer>();
        List<Material> copiedMaterials = new List<Material>(); // To store materials for later cleanup

        foreach (MeshRenderer renderer in renderers)
        {
            Material[] currentMaterials = renderer.materials; 
            for (int i = 0; i < currentMaterials.Length; i++)
            {
                Material mat = currentMaterials[i];

                mat.shader = Shader.Find("Universal Render Pipeline/Lit");

                if (chosenTexture != null && mat.name.Contains("Skinnable"))
                {
                    mat.SetTexture("_BaseMap", chosenTexture);
                }
                copiedMaterials.Add(mat);
            }
            renderer.materials = currentMaterials;
        }
        
        GameObject bladeHandle = (GameObject)_bladeHandleField.GetValue(stick);
        
        // --- Position on the ground plane (X/Z from bladeHandle, Y=0 world) ---
        Vector3 spawnPositionXZ = new Vector3(bladeHandle.transform.position.x, 0, bladeHandle.transform.position.z);
        Vector3 collectibleOffsetBasedOnGround = new Vector3(0, CollectibleBaseYOffset, 0); 
        
        // --- Random Upside Down Trait ---
        bool shouldBeUpsideDown = Random.Range(0, 2) == 1; 

        // Choose a random rarity
        RarityHelper.Rarity[] allRarities = (RarityHelper.Rarity[])System.Enum.GetValues(typeof(RarityHelper.Rarity));
        RarityHelper.Rarity assignedRarity = allRarities[Random.Range(0, allRarities.Length)];

        // Choose a random object name
        string[] objectNames = { "Toaster", "Lamp", "Gnome", "Mug", "Book", "Duck", "Sword", "Shield" }; 
        string assignedObjectName = objectNames[Random.Range(0, objectNames.Length)];
        
        // Generate random serial number
        string randomSerialNumber = GenerateRandomAlphanumericString(8);

        // Store info for the new collectible
        CollectibleInfo newCollectibleInfo = new CollectibleInfo 
        { 
            CollectibleGameObject = spawnedCollectible,
            SpawnTime = Time.time,
            DespawnDelay = 10.0f, // Still has a despawn delay
            IsUpsideDown = shouldBeUpsideDown,
            OriginalCollectibleHeight = originalHeight,
            RandomScaleFactor = randomScaleMultiplier,
            CurrentRarity = assignedRarity,
            ChosenTextureName = randomTextureDisplayName,
            SerialNumber = randomSerialNumber,
            CopiedMaterials = copiedMaterials,
            MaxXZBoundsLength = maxXZBoundsLength,
            // Removed FadeDuration and FadeStartTime assignments
        };

        if (shouldBeUpsideDown)
        {
            spawnedCollectible.transform.rotation = Quaternion.Euler(90, 0, 180); 
            spawnedCollectible.transform.position = spawnPositionXZ + collectibleOffsetBasedOnGround + new Vector3(0, originalHeight, 0);
        }
        else
        {
            spawnedCollectible.transform.rotation = Quaternion.Euler(-90, 0, 0);
            spawnedCollectible.transform.position = spawnPositionXZ + collectibleOffsetBasedOnGround;
        }
        
        // --- Spawn Billboard Text (Root Canvas) ---
        GameObject textBillboardRoot = Object.Instantiate(billboardTextPrefab);
        
        TMP_Text mainTextComponent = textBillboardRoot.GetComponentInChildren<TMP_Text>();
        
        if (mainTextComponent == null)
        {
            Debug.LogError("CollectibleBillboardText prefab is missing its initial TMP_Text component!");
            Object.Destroy(spawnedCollectible);
            Object.Destroy(textBillboardRoot);
            return;
        }
        
        GameObject secondaryTextGameObject = Object.Instantiate(mainTextComponent.gameObject, textBillboardRoot.transform);
        TMP_Text secondaryTextComponent = secondaryTextGameObject.GetComponent<TMP_Text>();

        // --- Text Stacking Setup ---
        RectTransform mainRect = mainTextComponent.rectTransform;
        RectTransform secondaryRect = secondaryTextComponent.rectTransform;

        mainRect.pivot = new Vector2(0.5f, 1f);
        mainRect.anchorMin = new Vector2(0.5f, 1f);
        mainRect.anchorMax = new Vector2(0.5f, 1f);
        mainRect.anchoredPosition = new Vector2(0, 0);

        secondaryRect.pivot = new Vector2(0.5f, 1f);
        secondaryRect.anchorMin = new Vector2(0.5f, 1f);
        secondaryRect.anchorMax = new Vector2(0.5f, 1f);
        
        // Set preliminary text to get preferredHeight
        string mainLineText = $"{assignedRarity} {randomTextureDisplayName} {assignedObjectName}";
        if (shouldBeUpsideDown) {
            mainLineText += " (Upside Down)";
        }
        mainTextComponent.text = mainLineText;
        Canvas.ForceUpdateCanvases(); 
        mainTextComponent.ForceMeshUpdate();

        float mainTextRenderedHeight = mainTextComponent.preferredHeight; 
        secondaryRect.anchoredPosition = new Vector2(0, -mainTextRenderedHeight - TEXT_LINE_SPACING); 
        secondaryRect.sizeDelta = mainRect.sizeDelta; 
        secondaryTextComponent.fontSize = mainTextComponent.fontSize * 0.75f;
        
        mainTextComponent.enableAutoSizing = false;
        secondaryTextComponent.enableAutoSizing = false;


        // --- Set Final Text Content and Colors ---
        mainTextComponent.color = RarityHelper.GetColor(assignedRarity);
        mainTextComponent.alignment = TextAlignmentOptions.Center;

        if (secondaryTextComponent != null)
        {
            secondaryTextComponent.text = $"Owner: {player.Username.Value}, Serial: {randomSerialNumber}, Scale: {randomScaleMultiplier}";
            secondaryTextComponent.color = Color.white;
            secondaryTextComponent.alignment = TextAlignmentOptions.Center;
        }

        // Store text references in CollectibleInfo
        newCollectibleInfo.TextBillboardGameObject = textBillboardRoot;
        newCollectibleInfo.MainTextMeshProComponent = mainTextComponent;
        newCollectibleInfo.SecondaryTextMeshProComponent = secondaryTextComponent;

        activeCollectibles.Add(newCollectibleInfo);
    }

    private static void Update()
    {
        if (activeCollectibles.Count == 0) return;
        
        float bobbingSpeed = 1.0f;
        float bobbingHeight = 0.15f;
        float rotationSpeed = 30.0f;
        
        Player localPlayer = PlayerManager.Instance.GetLocalPlayer();
        Camera playerCamera = localPlayer?.PlayerCamera?.CameraComponent;
        
        if (playerCamera == null)
        {
            Debug.LogWarning("Local player camera not found! Cannot billboard text or despawn collectibles.");
            return;
        }
        
        List<CollectibleInfo> collectiblesToDespawn = new List<CollectibleInfo>();

        foreach (CollectibleInfo collectibleInfo in activeCollectibles)
        {
            // --- Despawn Logic ---
            if (Time.time - collectibleInfo.SpawnTime >= collectibleInfo.DespawnDelay)
            {
                collectiblesToDespawn.Add(collectibleInfo);
                continue;
            }
            
            // Removed Fade Logic
            
            GameObject collectible = collectibleInfo.CollectibleGameObject;
            GameObject textBillboard = collectibleInfo.TextBillboardGameObject;

            // Calculate bobbing motion
            float bobOffset = Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;

            // Determine the base Y position for the collectible (fixed Y from ground)
            Vector3 groundPosition = new Vector3(collectible.transform.position.x, 0, collectible.transform.position.z);
            Vector3 currentCollectibleBasePos = groundPosition + new Vector3(0, CollectibleBaseYOffset, 0); 
        
            // Adjust position based on upside down trait and bobbing
            if (collectibleInfo.IsUpsideDown)
            {
                collectible.transform.position = currentCollectibleBasePos + new Vector3(0, collectibleInfo.OriginalCollectibleHeight + bobOffset, 0);
            }
            else
            {
                collectible.transform.position = currentCollectibleBasePos + new Vector3(0, CollectibleBaseYOffset + bobOffset, 0);
            }
            
            // Calculate spinning motion
            collectible.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            
            // --- Update Billboard Text Position and Rotation ---
            Vector3 textGroundPosition = new Vector3(collectible.transform.position.x, 0, collectible.transform.position.z);
            
            Vector3 directionToCamera = playerCamera.transform.position - textGroundPosition;
            directionToCamera.y = 0; // Flatten to XZ plane
            
            // Calculate dynamic push offset: Base amount + half of the object's largest XZ dimension
            float dynamicPushDistance = BASE_CAMERA_PUSH_DISTANCE + collectibleInfo.MaxXZBoundsLength;
            Vector3 pushOffset = directionToCamera.normalized * dynamicPushDistance;
            
            textBillboard.transform.position = textGroundPosition + new Vector3(0, TextBillboardYOffset, 0) + pushOffset;

            textBillboard.transform.rotation = playerCamera.transform.rotation;
        }

        foreach (CollectibleInfo infoToDespawn in collectiblesToDespawn)
        {
            RemoveCollectible(infoToDespawn);
        }
    }

    [HarmonyPatch(typeof(SynchronizedObjectManager), "Update")]
    public static class SynchronizedObjectManagerUpdate
    {
        [HarmonyPostfix]
        public static void Postfix(SynchronizedObjectManager __instance)
        {
            Update();
        }
    }

    private static void LoadPrefab()
    {
        if (toasterPrefab != null) return;
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/collectibles");
        toasterPrefab = PrefabHelper.LoadPrefab(_loadedAssetBundle, "assets/collectibles/models/toaster.fbx");
        if (toasterPrefab == null)
        {
            Debug.LogError("Failed to load toaster.fbx prefab!");
        }
    }
    
    private static Texture2D LoadTexture2D(string textureName)
    {
        if (_loadedAssetBundle == null) _loadedAssetBundle = PrefabHelper.LoadAssetBundle("assetbundles/collectibles");
        return PrefabHelper.LoadTexture2D(_loadedAssetBundle, $"assets/collectibles/skintextures/{textureName}");
    }
    
    public static void RemoveCollectible(CollectibleInfo info)
    {
        if (activeCollectibles.Contains(info))
        {
            if (info.CollectibleGameObject != null) Object.Destroy(info.CollectibleGameObject);
            if (info.TextBillboardGameObject != null) Object.Destroy(info.TextBillboardGameObject);
            
            foreach (Material mat in info.CopiedMaterials)
            {
                if (mat != null) Object.Destroy(mat);
            }
            info.CopiedMaterials.Clear();
            
            activeCollectibles.Remove(info);
        }
    }

    private static string GenerateRandomAlphanumericString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] stringChars = new char[length];
        for (int i = 0; i < length; i++)
        {
            stringChars[i] = chars[Random.Range(0, chars.Length)];
        }
        return new string(stringChars).ToLower();
    }
    
    // Removed SetMaterialBlendModeToFade method
}