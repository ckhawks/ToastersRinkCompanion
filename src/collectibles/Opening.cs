using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToastersRinkCompanion.collectibles;

public static class Opening
{
    private const float BASE_CAMERA_PUSH_DISTANCE = 0.5f; // Base push, before object size
    private const float TEXT_LINE_SPACING = 0.15f; // Vertical distance between lines (tune this for your prefab)
    
    private static List<GameObject> spawnedCases;
    private static List<CaseOpeningMetadata> caseOpeningMetadatas = new List<CaseOpeningMetadata>();

    private class CaseOpeningMetadata
    {
        public CollectibleItem CollectibleItem;
        public GameObject CollectibleDisplay;
        public GameObject emptyParent;
        public GameObject displayRoot;
        public GameObject TextBillboardRoot;
        public List<Material> CopiedMaterials;
        public float SpawnTime;
        public List<GameObject> particleObjects;
        public float DespawnDelay = 15.0f;
    }
    
    public static void PlayCaseOpening(OpenCasePayload payload)
    {
        try
        {
            CollectiblePrefabs.LoadCasePrefab();
            CollectiblePrefabs.InitializeTextPrefab();
            CollectiblePrefabs.LoadCollectiblesParticlesPrefab();
            
            Player localPlayer = PlayerManager.Instance.GetLocalPlayer();
            bool isForSelf = localPlayer != null &&
                             localPlayer.SteamId.Value.ToString().Equals(payload.CollectibleItem.CurrentOwnerSteamId);
            
            Player ownerPlayer =
                PlayerManager.Instance.GetPlayerBySteamId(
                    new FixedString32Bytes(payload.CollectibleItem.CurrentOwnerSteamId));
            
            GameObject caseObject = Object.Instantiate(CollectiblePrefabs.casePrefab);
            caseObject.transform.localScale /= 3f; // Scale the case down to normal size
            caseObject.transform.position = payload.Position;
            caseObject.transform.rotation = payload.Rotation;
            foreach(Material mat in caseObject.GetComponentInChildren<MeshRenderer>().sharedMaterials)
            {
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            // Set the case bottom text to say the owner name
            GameObject caseBottom = caseObject.transform.Find("CaseBottom").gameObject;
            GameObject topCollectibleNameCanvas = caseBottom.transform.Find("CollectibleNameCanvas").gameObject;
            GameObject ownerCollectibleNameText = topCollectibleNameCanvas.transform.Find("CollectibleNameText").gameObject;
            TMP_Text ownerNameText = ownerCollectibleNameText.GetComponent<TMP_Text>();
            ownerNameText.text = ownerPlayer.Username.Value.ToString();

            GameObject emptyParent = caseObject.transform.Find("Empty").gameObject;
            GameObject collectiblePrefab = CollectiblePrefabs.LoadPrefab(payload.CollectibleItem.ItemName);
            GameObject collectibleDisplay = Object.Instantiate(collectiblePrefab, emptyParent.transform);
            collectibleDisplay.transform.localScale = Vector3.one * 2 * payload.CollectibleItem.ScaleFactor; // because the empty scales to 100,100,100 at max size
            if (Collectible.HasTrait(payload.CollectibleItem, "Wide"))
                collectibleDisplay.transform.localScale = new Vector3(collectibleDisplay.transform.localScale.x, collectibleDisplay.transform.localScale.y * 2.5f, collectibleDisplay.transform.localScale.z);
            collectibleDisplay.transform.localRotation = Quaternion.Euler(0, 0, 0);
            collectibleDisplay.transform.localPosition = Vector3.zero; // make it follow parent
            
            Renderer collectibleRenderer = collectibleDisplay.GetComponentInChildren<Renderer>();
            collectibleRenderer = collectibleRenderer == null ? collectibleDisplay.GetComponent<Renderer>() : collectibleRenderer;
            float originalHeight = 0f;                                                           
            float maxXZBoundsLength = 0f;                                                        
            if (collectibleRenderer != null)                                                     
            {                                                                                    
                // Use bounds.extents for half-lengths           
                Plugin.Log($"collectibleRenderer is found!");
                originalHeight = collectibleRenderer.localBounds.size.z * 2 * payload.CollectibleItem.ScaleFactor;                    
                Plugin.Log($"collectibleRenderer bounds: {collectibleRenderer.localBounds}!");
                maxXZBoundsLength = Mathf.Max(collectibleRenderer.localBounds.extents.x,              
                    collectibleRenderer.localBounds.extents.z);                                       
            }

            if (Collectible.HasTrait(payload.CollectibleItem, "Upside Down"))
            {
                collectibleDisplay.transform.localRotation = Quaternion.Euler(180, 0, 0);
                Plugin.Log($"Setting upside down and localY to {originalHeight}");
                collectibleDisplay.transform.localPosition = new Vector3(0, 0, originalHeight);
            }

            
            
            Texture2D patternTexture = payload.CollectibleItem.PatternName != null ?
                CollectiblePrefabs.LoadTexture2D(payload.CollectibleItem.PatternName) : null; // this will be null if default/unpatterned

            MeshRenderer[] renderers = collectibleDisplay.GetComponentsInChildren<MeshRenderer>();
            List<Material> copiedMaterials = new List<Material>(); // To store materials for later cleanup
            
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
                        mat.SetColor("_BaseColor", Color.white);
                        
                        // To enable emission, set the emission map, and set intensity:
                        mat.EnableKeyword("_EMISSION"); // Enable the emission keyword
                        mat.SetTexture("_EmissionMap", patternTexture); // Set the emission map
                        mat.SetColor("_EmissionColor", Color.white * 1f); // Set emission color and intensity.
                        
                        // if (Collectible.HasTrait(payload.CollectibleItem, "Ascended"))
                        // {
                        //     mat.SetColor("_EmissionColor", Color.white * 3.5f); // Set emission color and intensity.
                        // }
                        // else
                        // {
                        //     mat.SetColor("_EmissionColor", Color.white * 1f); // Set emission color and intensity.
                        // }
                    }

                    copiedMaterials.Add(mat);
                }

                renderer.materials = currentMaterials;
            }
            
            List<GameObject> particleObjects = new List<GameObject>();
            
            
            if (Collectible.HasTrait(payload.CollectibleItem, "Holographic"))
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

            if (Collectible.HasTrait(payload.CollectibleItem, "Glistening"))
            {
                GameObject glisteningObject = Object.Instantiate(CollectiblePrefabs.glisteningPrefab, collectibleDisplay.transform);
                ParticleSystem glisteningParticleSystem = glisteningObject.GetComponent<ParticleSystem>();
                var shapeModule = glisteningParticleSystem.shape;
                shapeModule.mesh = collectibleDisplay.GetComponent<MeshFilter>().mesh;
                glisteningObject.transform.localScale = collectibleDisplay.transform.localScale / 3;
                glisteningObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                particleObjects.Add(glisteningObject);
                Plugin.Log($"Added glistening particles");
            }
            
            if (Collectible.HasTrait(payload.CollectibleItem, "Flaming"))
            {
                GameObject flamingObject = Object.Instantiate(CollectiblePrefabs.flamingPrefab, collectibleDisplay.transform);
                ParticleSystem flamingParticleSystem = flamingObject.GetComponent<ParticleSystem>();
                var shapeModule = flamingParticleSystem.shape;
                shapeModule.mesh = collectibleDisplay.GetComponent<MeshFilter>().mesh;
                var emissionModule = flamingParticleSystem.emission;
                emissionModule.enabled = true;
                flamingObject.transform.localScale = collectibleDisplay.transform.localScale / 3;
                flamingObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                particleObjects.Add(flamingObject);
                Plugin.Log($"Added flaming particles");
            }
            
            if (Collectible.HasTrait(payload.CollectibleItem, "Smoking"))
            {
                GameObject smokingObject = Object.Instantiate(CollectiblePrefabs.smokingPrefab, collectibleDisplay.transform);
                ParticleSystem smokingParticleSystem = smokingObject.GetComponent<ParticleSystem>();
                var shapeModule = smokingParticleSystem.shape;
                shapeModule.mesh = collectibleDisplay.GetComponent<MeshFilter>().mesh;
                var emissionModule = smokingParticleSystem.emission;
                emissionModule.enabled = true;
                smokingObject.transform.localScale = collectibleDisplay.transform.localScale / 3;
                smokingObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                particleObjects.Add(smokingObject);
                Plugin.Log($"Added smoking particles");
            }
            
            // Create audio shiz nizz
            AudioSource audioSourceBase = collectibleDisplay.AddComponent<AudioSource>();
            audioSourceBase.resource = CollectiblePrefabs.LoadAudioClip("case_open_base.mp3");
            audioSourceBase.loop = false;
            audioSourceBase.playOnAwake = false;
            audioSourceBase.reverbZoneMix = 0.1f;
            audioSourceBase.maxDistance = 15f;
            audioSourceBase.volume = isForSelf ? 0.6f : 0.3f;
            audioSourceBase.spatialBlend = 0.8f;
            AudioSource audioSourceExtra = collectibleDisplay.AddComponent<AudioSource>();
            audioSourceExtra.resource = payload.CollectibleItem.RarityName == "Common" && payload.CollectibleItem.Value < 200
                ? CollectiblePrefabs.LoadAudioClip("case_open_crickets.mp3")
                : CollectiblePrefabs.LoadAudioClip("case_open_swag.mp3");
            audioSourceExtra.loop = false;
            audioSourceExtra.playOnAwake = false;
            audioSourceExtra.reverbZoneMix = 0.1f;
            audioSourceExtra.maxDistance = 15f;
            audioSourceExtra.volume = isForSelf ? 0.6f : 0.3f;
            audioSourceExtra.spatialBlend = 0.8f;
            
            // Add particles
            if (!(payload.CollectibleItem.RarityName is "Common" or "Uncommon"))
            {
                GameObject particlesGameObject = GameObject.Instantiate(CollectiblePrefabs.particlesPrefab, emptyParent.transform);
                particlesGameObject.name = "CollectibleParticles";
                ParticleSystem particleSystem = particlesGameObject.GetComponent<ParticleSystem>();
                var mainModule = particleSystem.main;
                mainModule.loop = false;
                mainModule.playOnAwake = false;
                mainModule.duration = 7f;
                mainModule.startDelay = 0f;
                mainModule.startColor = CollectiblesConstants.GetColor(payload.CollectibleItem.RarityName);
                var shapeModule = particleSystem.shape;
                shapeModule.scale = new Vector3(1, 1f, 1);
                var emissionModule = particleSystem.emission;
                switch (payload.CollectibleItem.RarityName)
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
            textScalingParent.transform.SetParent(emptyParent.transform);
            textScalingParent.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
            
            GameObject textBillboardRoot = Object.Instantiate(CollectiblePrefabs.billboardTextPrefab, textScalingParent.transform);
            textBillboardRoot.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);
            // Update() will set -> textBillboardRoot position, rotation, needs access to empty for position base
            textBillboardRoot.transform.rotation = Quaternion.Euler(0, 90, 90);
        
            TMP_Text mainTextComponent = textBillboardRoot.GetComponentInChildren<TMP_Text>();
            
            if (mainTextComponent == null)
            {
                Debug.LogError("CollectibleBillboardText prefab is missing its initial TMP_Text component!");
                Object.Destroy(collectibleDisplay);
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
            string mainLineText = payload.CollectibleItem.FullName;
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
            mainTextComponent.color = CollectiblesConstants.GetColor(payload.CollectibleItem.RarityName);
            mainTextComponent.alignment = TextAlignmentOptions.Center;

            if (secondaryTextComponent != null)
            {
                string ownerName = ownerPlayer == null ? "Unknown" : ownerPlayer.Username.Value.ToString();
                
                secondaryTextComponent.text =
                    $"Value: T${payload.CollectibleItem.Value} — Owner: {ownerName}\nSerial: {payload.CollectibleItem.Serial} — Scale: x{payload.CollectibleItem.ScaleFactor}";
                secondaryTextComponent.color = Color.white;
                secondaryTextComponent.alignment = TextAlignmentOptions.Center;
            }

            CaseOpeningMetadata caseOpeningMetadata = new CaseOpeningMetadata
            {
                CollectibleItem = payload.CollectibleItem,
                CollectibleDisplay = collectibleDisplay,
                emptyParent = emptyParent,
                CopiedMaterials = copiedMaterials,
                DespawnDelay = 15f,
                displayRoot = caseObject,
                SpawnTime = Time.time,
                TextBillboardRoot = textBillboardRoot,
                particleObjects = particleObjects,
            };
            
            caseOpeningMetadatas.Add(caseOpeningMetadata);
            
            Animator caseAnimator = caseObject.GetComponent<Animator>();
            if (caseAnimator != null)
            {
                caseAnimator.Play("OpenAction");
                audioSourceBase.Play();
                audioSourceExtra.Play();
            }
            else
            {
                Debug.LogWarning("Pelican Case has no Animator component!");
            }
        }
        catch (Exception e)
        {
            Plugin.LogError($"Error while PlayCaseOpening: {e}");
        }
    }

    private static void Update()
    {
        if (caseOpeningMetadatas.Count == 0) return;
        
        List<CaseOpeningMetadata> caseOpeningMetadatasToRemove = new List<CaseOpeningMetadata>(); // TODO need to store the start time so we can destroy
        
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
        
        foreach (CaseOpeningMetadata caseOpeningMetadata in caseOpeningMetadatas)
        {
            GameObject emptyParent = caseOpeningMetadata.emptyParent;
            GameObject textBillboardRoot = caseOpeningMetadata.TextBillboardRoot;
            
            if (Time.time - caseOpeningMetadata.SpawnTime > caseOpeningMetadata.DespawnDelay)
            {
                caseOpeningMetadatasToRemove.Add(caseOpeningMetadata);
            }

            if (emptyParent == null || emptyParent.transform == null)
            {
                Plugin.LogError($"emptyParent is null");
                return;
            }

            foreach (GameObject particleObject in caseOpeningMetadata.particleObjects)
            {
                particleObject.transform.localScale = new Vector3(
                    Mathf.Lerp(0, caseOpeningMetadata.CollectibleDisplay.transform.localScale.x / 3f, emptyParent.transform.localScale.x / 100),
                    Mathf.Lerp(0, caseOpeningMetadata.CollectibleDisplay.transform.localScale.y / 3f, emptyParent.transform.localScale.y / 100),
                    Mathf.Lerp(0, caseOpeningMetadata.CollectibleDisplay.transform.localScale.z / 3f, emptyParent.transform.localScale.z / 100));
                ParticleSystem particleSystem = particleObject.GetComponent<ParticleSystem>();
                var emissionModule = particleSystem.emission;
                if (particleObject.transform.localScale.x < 0.01)
                {
                    emissionModule.enabled = false;
                }
                else
                {
                    emissionModule.enabled = true;
                }
            }
            
            // If there are particles attached, scale them with the thing
            Transform particlesSearch = emptyParent.transform.Find("CollectibleParticles");
            if (particlesSearch != null)
            {
                GameObject particlesGameObject = particlesSearch.gameObject;
                particlesGameObject.transform.localScale = emptyParent.transform.localScale / 100; // The particles game object ignores parents scale and it's localScale is the only scale it uses
            }
            
            // --- Update Billboard Text Position and Rotation ---
            Vector3 textGroundPosition =
                new Vector3(emptyParent.transform.position.x, emptyParent.transform.position.y, emptyParent.transform.position.z);
            
            Vector3 directionToCamera = currentCamera.transform.position - textGroundPosition;
            directionToCamera.y = 0; // Flatten to XZ plane
            
            // Calculate dynamic push offset: Base amount + half of the object's largest XZ dimension
            // float dynamicPushDistance = BASE_CAMERA_PUSH_DISTANCE + collectibleInfo.MaxXZBoundsLength; // TODO add XZ bounds back
            float dynamicPushDistance = BASE_CAMERA_PUSH_DISTANCE;
            Vector3 pushOffset = directionToCamera.normalized * dynamicPushDistance;
            
            // textBillboardRoot.transform.position =
            //     textGroundPosition + new Vector3(0, TextBillboardYOffset, 0) + pushOffset;
            textBillboardRoot.transform.position =
                textGroundPosition + new Vector3(0, -0.2f, 0) + pushOffset;
            
            textBillboardRoot.transform.rotation = currentCamera.transform.rotation;
        }
        
        foreach (CaseOpeningMetadata remove in caseOpeningMetadatasToRemove)
        {
            if (remove.displayRoot != null) Object.Destroy(remove.displayRoot);
            if (remove.TextBillboardRoot != null) Object.Destroy(remove.TextBillboardRoot);

            foreach (Material mat in remove.CopiedMaterials)
            {
                if (mat != null) Object.Destroy(mat);
            }

            remove.CopiedMaterials.Clear();

            caseOpeningMetadatas.Remove(remove);
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
}