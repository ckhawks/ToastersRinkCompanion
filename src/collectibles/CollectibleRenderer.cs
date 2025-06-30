using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object; // Still needed for RenderQueue, if used elsewhere, but not specifically for fade now

namespace ToastersRinkCompanion.collectibles;

// Removed custom SurfaceType and BlendMode enums that were causing issues and only for fade

public static class CollectibleRenderer
{
    // old
    private static GameObject toasterPrefab;

    // TODO when we move this to the serverside, we should raycast to the ground point (which won't always be 0) on the server, and then send that position to the clients to spawn it there

    // Y-offset for the collectible model from its ground XZ position
    private const float CollectibleBaseYOffset = 0.35f;

    // Y-offset for the text billboard from the collectible's base XZ position
    private const float TextBillboardYOffset = 0.7f; // This positions the TEXT relative to the ground

    private const float BASE_CAMERA_PUSH_DISTANCE = 0.5f; // Base push, before object size

    // Configuration for text layout when duplicating in code
    // These values are highly dependent on your TMP_Text prefab's Canvas Scale and Font Size.
    // TUNE THESE CAREFULLY!
    private const float TEXT_LINE_SPACING = 0.15f; // Vertical distance between lines (tune this for your prefab)


    private class ItemShowCollectibleMetadata
    {
        public CollectibleItem CollectibleItem;
        public GameObject displayRoot;
        public GameObject TextBillboardRoot;
        public List<Material> CopiedMaterials;
        public float SpawnTime;
        public float IntroDuration = 1f;
        public float OutroDuration = 1f;
        public float DespawnDelay = 30.0f;
        public float MaxXZBoundsLength; // For dynamic text push distance
        public float OriginalCollectibleHeight; // Height after initial scaling
        public bool IsUpsideDown;
    }
    
    private static List<ItemShowCollectibleMetadata> activeItemShowDisplays = new List<ItemShowCollectibleMetadata>();

    private class SpawnedCollectibleInfo2
    {
        public CollectibleItem CollectibleItem;
        public GameObject CollectibleGameObject;
        public GameObject TextBillboardGameObject; // The root Canvas GameObject
        public bool IsUpsideDown;
        public float OriginalCollectibleHeight; // Height after initial scaling
        public float SpawnTime; // When this collectible was spawned
        public float DespawnDelay = 10.0f; // How long it should last (e.g., 30 seconds)
        public List<Material> CopiedMaterials = new List<Material>(); // To track materials we need to destroy
        public float MaxXZBoundsLength; // For dynamic text push distance
    }

    // Converted to List<CollectibleInfo>
    private static List<SpawnedCollectibleInfo2> activeCollectibles = new List<SpawnedCollectibleInfo2>();
    
    public static GameObject CreateCollectibleDisplayInWorld(CollectibleItem collectibleItem, Vector3 position)
    {
        try
        { 
            CollectiblePrefabs.InitializeTextPrefab();
            CollectiblePrefabs.LoadCollectiblesParticlesPrefab();

            GameObject root = new GameObject();
            root.transform.position = position;
            root.transform.localScale = Vector3.one * 100;

            GameObject collectiblePrefab = CollectiblePrefabs.LoadPrefab(collectibleItem.ItemName);
            GameObject collectibleDisplay = Object.Instantiate(collectiblePrefab, root.transform);
            collectibleDisplay.transform.localScale =
                Vector3.one * collectibleItem.ScaleFactor * .68f; // because the empty scales to 100,100,100 at max size
            if (Collectible.HasTrait(collectibleItem, "Wide"))
                collectibleDisplay.transform.localScale = new Vector3(collectibleDisplay.transform.localScale.x,
                    collectibleDisplay.transform.localScale.y * 2.5f, collectibleDisplay.transform.localScale.z);
            collectibleDisplay.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            if (Collectible.HasTrait(collectibleItem, "Upside Down"))
                collectibleDisplay.transform.localRotation = Quaternion.Euler(90, 0, 0);
            collectibleDisplay.transform.localPosition = Vector3.zero; // make it follow parent
            
            Renderer collectibleRenderer = collectibleDisplay.GetComponentInChildren<Renderer>();
            collectibleRenderer = collectibleRenderer == null ? collectibleDisplay.GetComponent<Renderer>() : collectibleRenderer;
            float originalHeight = 0f;
            float maxXZBoundsLength = 0f;
            if (collectibleRenderer != null)
            {
                // We use bounds here and not local bounds because at this point in time, this IS big. It only becomes small in Update()
                // Use bounds.extents for half-lengths
                originalHeight = collectibleRenderer.bounds.size.y;
                maxXZBoundsLength = Mathf.Max(collectibleRenderer.bounds.extents.x,
                    collectibleRenderer.bounds.extents.z);
            }
            // else
            // {
            //     Debug.LogWarning(
            //         "Collectible has no Renderer! Cannot determine bounds for adjustments. Using fallbacks.");
            //     originalHeight = 1.0f * collectible.ScaleFactor; // Fallback height
            //     maxXZBoundsLength = 0.5f * collectible.ScaleFactor; // Fallback max XZ extent
            // }
            
            Texture2D patternTexture = collectibleItem.PatternName != null
                ? CollectiblePrefabs.LoadTexture2D(collectibleItem.PatternName)
                : null; // this will be null if default/unpatterned

            MeshRenderer[] renderers = collectibleDisplay.GetComponentsInChildren<MeshRenderer>();
            List<Material> copiedMaterials = new List<Material>(); // To store materials for later cleanup

            foreach (MeshRenderer renderer in renderers)
            {
                Material[] currentMaterials = renderer.materials;
                foreach (var mat in currentMaterials)
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");

                    if (patternTexture != null && mat.name.Contains("Skinnable"))
                    {
                        mat.SetTexture("_BaseMap", patternTexture);
                        mat.SetColor("_BaseColor", Color.white);
                        
                        // To enable emission, set the emission map, and set intensity:
                        mat.EnableKeyword("_EMISSION"); // Enable the emission keyword
                        mat.SetTexture("_EmissionMap", patternTexture); // Set the emission map
                        
                        mat.SetColor("_EmissionColor", Color.white * 1f); // Set emission color and intensity.
                        // if (Collectible.HasTrait(collectibleItem, "Ascended"))
                        // {
                        //     mat.SetColor("_EmissionColor", Color.white * 3.5f); // Set emission color and intensity.
                        // }
                        // else
                        // {
                        //     mat.SetColor("_EmissionColor", Color.white * 1f); // Set emission color and intensity.
                        // }
                        // A value > 1 can make it glow.
                        // If you want actual HDR, you'd use a different approach for intensity.
                    }

                    copiedMaterials.Add(mat);
                }

                renderer.materials = currentMaterials;
            }

            if (Collectible.HasTrait(collectibleItem, "Holographic"))
            {
                GameObject holographicObject = Object.Instantiate(CollectiblePrefabs.holographicPrefab, collectibleDisplay.transform);
                MeshFilter holographicMeshFilter = holographicObject.GetComponent<MeshFilter>();
                if (holographicMeshFilter == null)
                {
                    Plugin.LogError($"Holographic mesh filter not found!");
                }
                holographicMeshFilter.mesh = collectibleDisplay.GetComponent<MeshFilter>().mesh;
                MeshRenderer holographicMeshRenderer = holographicObject.GetComponent<MeshRenderer>();
                if (holographicMeshRenderer == null)
                {
                    Plugin.LogError($"Holographic mesh renderer not found!");
                }
                holographicMeshRenderer.material.SetFloat("_ExtrusionAmount", 0.00001f);
                holographicMeshRenderer.material.SetFloat("_StartTime", Time.time);
                holographicObject.transform.localScale = Vector3.one;
                Plugin.Log($"Added holographic renderer");
            }

            if (Collectible.HasTrait(collectibleItem, "Glistening"))
            {
                GameObject glisteningObject = Object.Instantiate(CollectiblePrefabs.glisteningPrefab, collectibleDisplay.transform);
                ParticleSystem glisteningParticleSystem = glisteningObject.GetComponent<ParticleSystem>();
                var shapeModule = glisteningParticleSystem.shape;
                shapeModule.mesh = collectibleDisplay.GetComponent<MeshFilter>().mesh;
                glisteningObject.transform.localScale = collectibleDisplay.transform.localScale;
                glisteningObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                Plugin.Log($"Added glistening particles");
            }
            
            if (Collectible.HasTrait(collectibleItem, "Flaming"))
            {
                GameObject flamingObject = Object.Instantiate(CollectiblePrefabs.flamingPrefab, collectibleDisplay.transform);
                ParticleSystem flamingParticleSystem = flamingObject.GetComponent<ParticleSystem>();
                var shapeModule = flamingParticleSystem.shape;
                shapeModule.mesh = collectibleDisplay.GetComponent<MeshFilter>().mesh;
                var emissionModule = flamingParticleSystem.emission;
                emissionModule.enabled = true;
                flamingObject.transform.localScale = collectibleDisplay.transform.localScale;
                flamingObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                Plugin.Log($"Added flaming particles");
            }
            
            if (Collectible.HasTrait(collectibleItem, "Smoking"))
            {
                GameObject smokingObject = Object.Instantiate(CollectiblePrefabs.smokingPrefab, collectibleDisplay.transform);
                ParticleSystem smokingParticleSystem = smokingObject.GetComponent<ParticleSystem>();
                var shapeModule = smokingParticleSystem.shape;
                shapeModule.mesh = collectibleDisplay.GetComponent<MeshFilter>().mesh;
                var emissionModule = smokingParticleSystem.emission;
                emissionModule.enabled = true;
                smokingObject.transform.localScale = collectibleDisplay.transform.localScale;
                smokingObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                Plugin.Log($"Added smoking particles");
            }
            
            // Add particles
            if (!(collectibleItem.RarityName is "Common" or "Uncommon"))
            {
                GameObject particlesGameObject = GameObject.Instantiate(CollectiblePrefabs.particlesPrefab, root.transform);
                particlesGameObject.transform.rotation = quaternion.Euler(-90, 0, 0);
                particlesGameObject.name = "CollectibleParticles";
                ParticleSystem particleSystem = particlesGameObject.GetComponent<ParticleSystem>();
                var mainModule = particleSystem.main;
                mainModule.loop = false;
                mainModule.playOnAwake = false;
                mainModule.duration = 30f;
                mainModule.startDelay = 0f;
                mainModule.startColor = CollectiblesConstants.GetColor(collectibleItem.RarityName);
                var shapeModule = particleSystem.shape;
                shapeModule.scale = new Vector3(1, 1f, originalHeight + 0.4f);
                var emissionModule = particleSystem.emission;
                switch (collectibleItem.RarityName)
                {
                    case "Mythic":
                        emissionModule.rateOverTime = 25f;
                        break;
                    case "Legendary":
                        emissionModule.rateOverTime = 20f;
                        break;
                    case "Epic":
                        emissionModule.rateOverTime = 15f;
                        break;
                    case "Rare":
                        emissionModule.rateOverTime = 10f;
                        break;
                }

                var rotationOverLifetimeModule = particleSystem.rotationOverLifetime;
                rotationOverLifetimeModule.xMultiplier = 20;
                rotationOverLifetimeModule.yMultiplier = 45;
                rotationOverLifetimeModule.enabled = true;
            }
            
            GameObject textScalingParent = new GameObject();
            // textScalingParent.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f); // TODO this might be unneeded
            textScalingParent.transform.parent = root.transform;

            GameObject textBillboardRoot =
                Object.Instantiate(CollectiblePrefabs.billboardTextPrefab, textScalingParent.transform);
            textBillboardRoot.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);
            // Update() will set -> textBillboardRoot position, rotation, needs access to empty for position base
            textBillboardRoot.transform.rotation = Quaternion.Euler(0, 90, 90);

            TMP_Text mainTextComponent = textBillboardRoot.GetComponentInChildren<TMP_Text>();

            if (mainTextComponent == null)
            {
                Debug.LogError("CollectibleBillboardText prefab is missing its initial TMP_Text component!");
                // Object.Destroy(spawnedCollectible);
                Object.Destroy(textBillboardRoot);
                return null;
            }

            GameObject secondaryTextGameObject =
                Object.Instantiate(mainTextComponent.gameObject, textBillboardRoot.transform);
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
            string mainLineText = collectibleItem.FullName;
            mainTextComponent.text = mainLineText;
            Canvas.ForceUpdateCanvases();
            mainTextComponent.ForceMeshUpdate();

            float mainTextRenderedHeight = mainTextComponent.preferredHeight;
            secondaryRect.anchoredPosition = new Vector2(0, -mainTextRenderedHeight - TEXT_LINE_SPACING);
            secondaryRect.sizeDelta = mainRect.sizeDelta;
            secondaryTextComponent.fontSize = mainTextComponent.fontSize * 0.5f;

            mainTextComponent.enableAutoSizing = false;
            secondaryTextComponent.enableAutoSizing = false;


            // --- Set Final Text Content and Colors ---
            mainTextComponent.color = CollectiblesConstants.GetColor(collectibleItem.RarityName);
            mainTextComponent.alignment = TextAlignmentOptions.Center;

            if (secondaryTextComponent != null)
            {
                Player ownerPlayer =
                    PlayerManager.Instance.GetPlayerBySteamId(
                        new FixedString32Bytes(collectibleItem.CurrentOwnerSteamId));
                string ownerName = ownerPlayer == null ? "Unknown" : ownerPlayer.Username.Value.ToString();

                secondaryTextComponent.text =
                    $"Value: T${collectibleItem.Value} — Owner: {ownerName}\nSerial: {collectibleItem.Serial} — Scale: x{collectibleItem.ScaleFactor}";
                secondaryTextComponent.color = Color.white;
                secondaryTextComponent.alignment = TextAlignmentOptions.Center;
            }

            Plugin.Log($"Item is upside down: {Collectible.HasTrait(collectibleItem, "Upside Down")}");
            activeItemShowDisplays.Add(new ItemShowCollectibleMetadata
            {
                CollectibleItem = collectibleItem,
                DespawnDelay = 30.0f,
                displayRoot = root,
                TextBillboardRoot = textBillboardRoot,
                IntroDuration = 1f,
                OutroDuration = 0.5f,
                CopiedMaterials = copiedMaterials,
                IsUpsideDown = Collectible.HasTrait(collectibleItem, "Upside Down"),
                MaxXZBoundsLength = maxXZBoundsLength,
                OriginalCollectibleHeight = originalHeight,
                SpawnTime = Time.time,
            });

            return root;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error while creating collecting object: {e.Message}");
            return null;
        }
    }
    
    public static GameObject OLDOLDOLDCreateCollectibleDisplay(CollectibleItem collectible)
    {
        try
        {
            GameObject parent = new GameObject();
            parent.transform.localScale = new Vector3(100f, 100f, 100f); // TODO if i come back to this, i fucked up this scale
            
            GameObject collectiblePrefab = CollectiblePrefabs.LoadPrefab(collectible.ItemName);
            CollectiblePrefabs.InitializeTextPrefab();

            Plugin.Log($"CreateCollectibleDisplay1");
            GameObject spawnedCollectibleDisplay = Object.Instantiate(collectiblePrefab, parent.transform);


            // spawnedCollectibleDisplay.transform.localScale = new Vector3(collectible.ScaleFactor,
            //     collectible.ScaleFactor, collectible.ScaleFactor);
            spawnedCollectibleDisplay.transform.localScale = new Vector3(100, 100, 100);
            spawnedCollectibleDisplay.transform.localRotation = Quaternion.Euler(-90, 0, 0);

            Plugin.Log($"CreateCollectibleDisplay2");
            Renderer collectibleRenderer = spawnedCollectibleDisplay.GetComponentInChildren<Renderer>();
            float originalHeight = 0f;
            float maxXZBoundsLength = 0f;
            if (collectibleRenderer != null)
            {
                // Use bounds.extents for half-lengths
                originalHeight = collectibleRenderer.bounds.size.y;
                maxXZBoundsLength = Mathf.Max(collectibleRenderer.bounds.extents.x,
                    collectibleRenderer.bounds.extents.z);
            }
            else
            {
                Debug.LogWarning(
                    "Collectible has no Renderer! Cannot determine bounds for adjustments. Using fallbacks.");
                originalHeight = 1.0f * collectible.ScaleFactor; // Fallback height
                maxXZBoundsLength = 0.5f * collectible.ScaleFactor; // Fallback max XZ extent
            }
            Plugin.Log($"CreateCollectibleDisplay3");

            Texture2D patternTexture = collectible.PatternName != null ?
                CollectiblePrefabs.LoadTexture2D(collectible.PatternName) : null; // this will be null if default/unpatterned

            MeshRenderer[] renderers = spawnedCollectibleDisplay.GetComponentsInChildren<MeshRenderer>();
            List<Material> copiedMaterials = new List<Material>(); // To store materials for later cleanup

            Plugin.Log($"CreateCollectibleDisplay4");
            foreach (MeshRenderer renderer in renderers)
            {
                Material[] currentMaterials = renderer.materials;
                for (int i = 0; i < currentMaterials.Length; i++)
                {
                    Material mat = currentMaterials[i];

                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");

                    if (patternTexture != null && mat.name.Contains("Skinnable"))
                    {
                        mat.SetTexture("_BaseMap", patternTexture);
                    }

                    copiedMaterials.Add(mat);
                }

                renderer.materials = currentMaterials;
            }
            
            Plugin.Log($"CreateCollectibleDisplay5");

            // TODO detect if has upside down trait and if so flip
            // if (collectible.Traits.)
            // {
            //     spawnedCollectible.transform.rotation = Quaternion.Euler(90, 0, 180); 
            //     spawnedCollectible.transform.position = spawnPositionXZ + collectibleOffsetBasedOnGround + new Vector3(0, originalHeight, 0);
            // }
            // else
            // {
            // spawnedCollectibleDisplay.transform.rotation = Quaternion.Euler(-90, 0, 0);
            // spawnedCollectibleDisplay.transform.position = spawnPositionXZ + collectibleOffsetBasedOnGround;
            // }

            // Store info for the new collectible
            SpawnedCollectibleInfo2 newSpawnedCollectibleInfo2 = new SpawnedCollectibleInfo2
            {
                CollectibleItem = collectible,
                CollectibleGameObject = spawnedCollectibleDisplay,
                SpawnTime = Time.time,
                DespawnDelay = 10.0f, // Still has a despawn delay
                // IsUpsideDown = shouldBeUpsideDown,
                OriginalCollectibleHeight = originalHeight,
                // CurrentRarity = assignedRarity,
                // ChosenTextureName = randomTextureDisplayName,
                // SerialNumber = randomSerialNumber,
                // CopiedMaterials = copiedMaterials,
                MaxXZBoundsLength = maxXZBoundsLength,
                // Removed FadeDuration and FadeStartTime assignments
            };

            Plugin.Log($"CreateCollectibleDisplay6");
            GameObject textBillboardRoot = Object.Instantiate(CollectiblePrefabs.billboardTextPrefab, parent.transform, false);
            // textBillboardRoot.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);
            textBillboardRoot.transform.localScale = new Vector3(100f, 100f, 100f);
            textBillboardRoot.transform.localRotation = Quaternion.Euler(0, 90, 90);

            TMP_Text mainTextComponent = textBillboardRoot.GetComponentInChildren<TMP_Text>();

            if (mainTextComponent == null)
            {
                Debug.LogError("CollectibleBillboardText prefab is missing its initial TMP_Text component!");
                Object.Destroy(spawnedCollectibleDisplay);
                Object.Destroy(textBillboardRoot);
                return null;
            }
            Plugin.Log($"CreateCollectibleDisplay7");

            GameObject secondaryTextGameObject =
                Object.Instantiate(mainTextComponent.gameObject, textBillboardRoot.transform);
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

            Plugin.Log($"CreateCollectibleDisplay8");
            // Set preliminary text to get preferredHeight
            string mainLineText = collectible.FullName;
            // if (shouldBeUpsideDown) {
            //     mainLineText += " (Upside Down)";
            // }
            mainTextComponent.text = mainLineText;
            Canvas.ForceUpdateCanvases();
            mainTextComponent.ForceMeshUpdate();

            float mainTextRenderedHeight = mainTextComponent.preferredHeight;
            secondaryRect.anchoredPosition = new Vector2(0, -mainTextRenderedHeight - TEXT_LINE_SPACING);
            secondaryRect.sizeDelta = mainRect.sizeDelta;
            secondaryTextComponent.fontSize = mainTextComponent.fontSize * 0.5f;

            mainTextComponent.enableAutoSizing = false;
            secondaryTextComponent.enableAutoSizing = false;
            Plugin.Log($"CreateCollectibleDisplay9");

            // --- Set Final Text Content and Colors ---
            mainTextComponent.color = CollectiblesConstants.GetColor(collectible.RarityName);
            mainTextComponent.alignment = TextAlignmentOptions.Center;

            if (secondaryTextComponent != null)
            {
                secondaryTextComponent.text =
                    $"Value: T${collectible.Value} — Owner: {collectible.CurrentOwnerSteamId}\nSerial: {collectible.Serial} — Scale: x{collectible.ScaleFactor}";
                secondaryTextComponent.color = Color.white;
                secondaryTextComponent.alignment = TextAlignmentOptions.Center;
            }

            Plugin.Log($"CreateCollectibleDisplay10");
            // Store text references in CollectibleInfo
            newSpawnedCollectibleInfo2.TextBillboardGameObject = textBillboardRoot;
            // newSpawnedCollectibleInfo.MainTextMeshProComponent = mainTextComponent;
            // newSpawnedCollectibleInfo.SecondaryTextMeshProComponent = secondaryTextComponent;

            activeCollectibles.Add(newSpawnedCollectibleInfo2);

            Plugin.Log($"CreateCollectibleDisplay11");
            return parent;
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error while creating collectible display: {e}");
            return null;
        }
    }

    private static void UpdateOld()
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
        
        List<SpawnedCollectibleInfo2> collectiblesToDespawn = new List<SpawnedCollectibleInfo2>();

        foreach (SpawnedCollectibleInfo2 collectibleInfo in activeCollectibles)
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

            // collectible.transform.localScale = Vector3.one;
            // Determine the base Y position for the collectible (fixed Y from ground)
            Vector3 groundPosition = new Vector3(collectible.transform.position.x, 0, collectible.transform.position.z);
            Vector3 currentCollectibleBasePos = groundPosition + new Vector3(0, CollectibleBaseYOffset, 0);

            // Adjust position based on upside down trait and bobbing
            if (collectibleInfo.IsUpsideDown)
            {
                collectible.transform.position = currentCollectibleBasePos +
                                                 new Vector3(0, collectibleInfo.OriginalCollectibleHeight + bobOffset,
                                                     0);
            }
            else
            {
                collectible.transform.position =
                    currentCollectibleBasePos + new Vector3(0, CollectibleBaseYOffset + bobOffset, 0);
            }

            // Calculate spinning motion
            collectible.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

            // --- Update Billboard Text Position and Rotation ---
            Vector3 textGroundPosition =
                new Vector3(collectible.transform.position.x, 0, collectible.transform.position.z);

            Vector3 directionToCamera = playerCamera.transform.position - textGroundPosition;
            directionToCamera.y = 0; // Flatten to XZ plane

            // Calculate dynamic push offset: Base amount + half of the object's largest XZ dimension
            float dynamicPushDistance = BASE_CAMERA_PUSH_DISTANCE + collectibleInfo.MaxXZBoundsLength;
            Vector3 pushOffset = directionToCamera.normalized * dynamicPushDistance;

            textBillboard.transform.position =
                textGroundPosition + new Vector3(0, TextBillboardYOffset, 0) + pushOffset;

            textBillboard.transform.rotation = playerCamera.transform.rotation;
        }

        foreach (SpawnedCollectibleInfo2 infoToDespawn in collectiblesToDespawn)
        {
            RemoveCollectible(infoToDespawn);
        }
    }

    public static float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
    }
    
    public static float EaseInOutElastic(float t)
    {
        float c5 = (2 * Mathf.PI) / 4.5f;
        return t == 0
            ? 0
            : t == 1
                ? 1
                : t < 0.5f
                    ? -(Mathf.Pow(2, 20 * t - 10) * Mathf.Sin((20 * t - 11.25f) * c5)) / 2
                    : (Mathf.Pow(2, -20 * t + 10) * Mathf.Sin((20 * t - 11.25f) * c5)) / 2 + 1;
    }
    
    const float bobbingSpeed = 1.0f;
    const float bobbingHeight = 0.15f;
    const float rotationSpeed = 30.0f;
    private static void UpdateItemShowDisplays()
    {
        if (activeItemShowDisplays.Count == 0) return;
        
        List<ItemShowCollectibleMetadata> itemShowDisplaysToDespawn = new List<ItemShowCollectibleMetadata>();
        
        Player localPlayer = PlayerManager.Instance.GetLocalPlayer();
        Camera currentCamera = localPlayer?.PlayerCamera?.CameraComponent;
        

        if (currentCamera == null)
        {
            currentCamera = localPlayer?.SpectatorCamera.CameraComponent;
        }

        if (currentCamera == null)
        {
            Debug.LogWarning("Local camera not found! Cannot billboard text or despawn collectibles.");
            return;
        }
        
        foreach (ItemShowCollectibleMetadata itemShowMetadata in activeItemShowDisplays)
        {
            float howManySecondsTheDisplayHasBeenAlive = Time.time - itemShowMetadata.SpawnTime;
            
            // Check if it's time for this thing to be despawned
            if (howManySecondsTheDisplayHasBeenAlive >= itemShowMetadata.DespawnDelay)
            {
                itemShowDisplaysToDespawn.Add(itemShowMetadata);
                continue;
            }

            if (howManySecondsTheDisplayHasBeenAlive < itemShowMetadata.IntroDuration)
            {
                itemShowMetadata.displayRoot.transform.localScale = Vector3.one * 100 * Mathf.Lerp(0, 1, EaseInOutQuad(howManySecondsTheDisplayHasBeenAlive) / itemShowMetadata.IntroDuration);
                // itemShowMetadata.TextBillboardRoot.transform.localScale = Vector3.one * Mathf.Lerp(0, 1, EaseInOutQuad(howManySecondsTheDisplayHasBeenAlive) / itemShowMetadata.IntroDuration);
            }
            
            if (howManySecondsTheDisplayHasBeenAlive >=
                itemShowMetadata.DespawnDelay - itemShowMetadata.OutroDuration)
            {
                itemShowMetadata.displayRoot.transform.localScale = Vector3.one * 100 * Mathf.Lerp(1, 0, EaseInOutQuad((howManySecondsTheDisplayHasBeenAlive - (itemShowMetadata.DespawnDelay - itemShowMetadata.OutroDuration)) / itemShowMetadata.OutroDuration));
                // itemShowMetadata.TextBillboardRoot.transform.localScale = Vector3.one * Mathf.Lerp(1, 0, EaseInOutQuad((howManySecondsTheDisplayHasBeenAlive - (itemShowMetadata.DespawnDelay - itemShowMetadata.OutroDuration)) / itemShowMetadata.OutroDuration));
            }
            
            // Calculate bobbing motion
            float bobOffset = Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;

            // collectible.transform.localScale = Vector3.one;
            // Determine the base Y position for the collectible (fixed Y from ground)
            Vector3 groundPosition = new Vector3(itemShowMetadata.displayRoot.transform.position.x, 0, itemShowMetadata.displayRoot.transform.position.z);
            Vector3 currentCollectibleBasePos = groundPosition + new Vector3(0, CollectibleBaseYOffset, 0);

            // Adjust position based on upside down trait and bobbing
            if (itemShowMetadata.IsUpsideDown)
            {
                itemShowMetadata.displayRoot.transform.position = currentCollectibleBasePos +
                                                                  new Vector3(0, itemShowMetadata.OriginalCollectibleHeight + bobOffset,
                                                                      0);
            }
            else
            {
                itemShowMetadata.displayRoot.transform.position =
                    currentCollectibleBasePos + new Vector3(0, CollectibleBaseYOffset + bobOffset, 0);
            }
            
            
            // TODO might need to change the line below to rotate only the collectible inside the root, not the root
            itemShowMetadata.displayRoot.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            
            // --- Update Billboard Text Position and Rotation --
            Vector3 textGroundPosition =
                new Vector3(itemShowMetadata.displayRoot.transform.position.x, 0, itemShowMetadata.displayRoot.transform.position.z);

            Vector3 directionToCamera = currentCamera.transform.position - textGroundPosition;
            directionToCamera.y = 0; // Flatten to XZ plane

            // Calculate dynamic push offset: Base amount + half of the object's largest XZ dimension
            float dynamicPushDistance = BASE_CAMERA_PUSH_DISTANCE + itemShowMetadata.MaxXZBoundsLength / 1.5f;
            Vector3 pushOffset = directionToCamera.normalized * dynamicPushDistance;

            itemShowMetadata.TextBillboardRoot.transform.position =
                textGroundPosition + new Vector3(0, TextBillboardYOffset, 0) + pushOffset;

            itemShowMetadata.TextBillboardRoot.transform.rotation = currentCamera.transform.rotation;
        }
        
        foreach (ItemShowCollectibleMetadata iscm in itemShowDisplaysToDespawn)
        {
            DestroyCollectibleDisplay(iscm);
        }
    }

    [HarmonyPatch(typeof(SynchronizedObjectManager), "Update")]
    public static class SynchronizedObjectManagerUpdate
    {
        [HarmonyPostfix]
        public static void Postfix(SynchronizedObjectManager __instance)
        {
            UpdateItemShowDisplays();
        }
    }

    
    private static void DestroyCollectibleDisplay(ItemShowCollectibleMetadata iscm)
    {
        if (activeItemShowDisplays.Contains(iscm))
        {
            if (iscm.displayRoot != null) Object.Destroy(iscm.displayRoot);
            if (iscm.TextBillboardRoot != null) Object.Destroy(iscm.TextBillboardRoot);

            foreach (Material mat in iscm.CopiedMaterials)
            {
                if (mat != null) Object.Destroy(mat);
            }

            iscm.CopiedMaterials.Clear();

            activeItemShowDisplays.Remove(iscm);
        }
    }

    private static void RemoveCollectible(SpawnedCollectibleInfo2 info2)
    {
        if (activeCollectibles.Contains(info2))
        {
            if (info2.CollectibleGameObject != null) Object.Destroy(info2.CollectibleGameObject);
            if (info2.TextBillboardGameObject != null) Object.Destroy(info2.TextBillboardGameObject);

            foreach (Material mat in info2.CopiedMaterials)
            {
                if (mat != null) Object.Destroy(mat);
            }

            info2.CopiedMaterials.Clear();

            activeCollectibles.Remove(info2);
        }
    }
}