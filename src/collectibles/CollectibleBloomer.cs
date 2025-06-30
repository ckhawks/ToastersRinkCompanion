// using HarmonyLib;
// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.Universal;
//
// namespace ToastersRinkCompanion.collectibles;
//
// public static class CollectibleBloomer
// {
//     // private static float bloomIntensity = 2.5f; // Adjust this in your mod's config or dynamically
//     private static float bloomIntensity = 10f;
//     // private static float bloomThreshold = 1.5f; // Adjust this
//     private static float bloomThreshold = 0.1f;
//     private static Color bloomTint = Color.white; // Adjust this
//     
//     public static void MakeBloomer()
//     {
//          // 1. Find all Volume components in the scene
//         // Depending on the game, there might be global volumes, local volumes, or both.
//         // It's usually best to target global volumes.
//         Volume[] volumes = Object.FindObjectsOfType<Volume>();
//
//         if (volumes.Length == 0)
//         {
//             Plugin.LogWarning("[BloomMod] No Volume components found in the scene.");
//             return;
//         }
//
//         bool bloomActivated = false;
//
//         foreach (Volume volume in volumes)
//         {
//             // We're typically looking for a Global Volume for scene-wide effects
//             if (volume.isGlobal)
//             {
//                 Plugin.Log($"[BloomMod] Found Global Volume: {volume.name}");
//
//                 // Get or create a runtime mutable profile clone
//                 // This is crucial! You should *not* modify the original asset directly
//                 // because it would permanently change the game's assets.
//                 // Instead, create an instance clone of the profile.
//                 VolumeProfile profile = volume.sharedProfile; // Use sharedProfile for the base
//                 if (profile == null)
//                 {
//                     Plugin.LogWarning($"[BloomMod] Global Volume '{volume.name}' has no sharedProfile. Skipping.");
//                     continue;
//                 }
//
//                 // If you need to make changes unique to this mod instance
//                 // or if the game might unload/reload the scene, cloning is safer.
//                 // If you modify sharedProfile, it affects all instances using that profile.
//                 // For a mod, sharedProfile might be acceptable if you just want to enable an effect.
//                 // volume.profile = Instantiate(volume.sharedProfile); // Option to clone if needed
//                 // profile = volume.profile; // Now work with the cloned profile
//
//
//                 // 2. Try to get the Bloom override from the profile
//                 Bloom bloom;
//                 if (!profile.TryGet(out bloom))
//                 {
//                     // 3. If Bloom doesn't exist, add it
//                     bloom = profile.Add<Bloom>(true); // 'true' makes it active by default
//                     Debug.Log($"[BloomMod] Added Bloom override to profile: {profile.name}");
//                 }
//                 else
//                 {
//                     Debug.Log($"[BloomMod] Found existing Bloom override in profile: {profile.name}");
//                 }
//
//                 // 4. Enable and configure Bloom parameters
//                 bloom.active = true; // Ensure the Bloom override itself is active
//
//                 // Override parameters. Use 'true' for 'overrideState' to enable them.
//                 bloom.intensity.overrideState = true;
//                 bloom.intensity.value = bloomIntensity;
//
//                 bloom.threshold.overrideState = true;
//                 bloom.threshold.value = bloomThreshold;
//
//                 bloom.tint.overrideState = true;
//                 bloom.tint.value = bloomTint;
//
//                 // Optionally, other settings:
//                 // bloom.scatter.overrideState = true;
//                 // bloom.scatter.value = 0.7f;
//                 // bloom.dirtTexture.overrideState = true;
//                 // bloom.dirtTexture.value = YourDirtTexture; // If you have one
//                 // bloom.dirtIntensity.overrideState = true;
//                 // bloom.dirtIntensity.value = 1.0f;
//
//                 Debug.Log($"[BloomMod] Activated Bloom on Volume '{volume.name}' with Intensity: {bloomIntensity}, Threshold: {bloomThreshold}");
//                 bloomActivated = true;
//                 // If you only want to affect the first global volume found, break here
//                 // break;
//             }
//         }
//
//         if (!bloomActivated)
//         {
//             Debug.LogWarning("[BloomMod] Could not activate Bloom on any global volume. Check scene setup.");
//         }
//     }
//
//     // [HarmonyPatch(typeof(SpectatorCamera), "OnNetworkPostSpawn")]
//     // public static class SpectatorCameraOnNetworkPostSpawn
//     // {
//     //     [HarmonyPostfix]
//     //     public static void Postfix(SpectatorCamera __instance)
//     //     {
//     //         if (__instance.CameraComponent == null) return;
//     //         
//     //         UniversalAdditionalCameraData cameraData = __instance.CameraComponent.GetUniversalAdditionalCameraData();
//     //         if (cameraData != null)
//     //         {
//     //             // Ensure post-processing is enabled for this camera
//     //             if (!cameraData.renderPostProcessing)
//     //             {
//     //                 cameraData.renderPostProcessing = true;
//     //                 Debug.Log("[BloomMod] Enabled Post Processing on Camera: " + __instance.CameraComponent.name);
//     //             }
//     //
//     //             // Also check the Volume Layer Mask
//     //             // This determines which Volumes this camera will consider.
//     //             // It should typically include the layer your Global Volume is on, or 'Everything'.
//     //             // If your Global Volume is on a specific layer, ensure this mask includes it.
//     //             // cameraData.volumeLayerMask = LayerMask.GetMask("YourVolumeLayerName"); // Example
//     //             // OR to be safe, set it to everything:
//     //             // cameraData.volumeLayerMask = ~0; // All layers
//     //         }
//     //         else
//     //         {
//     //             Debug.LogWarning("[BloomMod] Camera does not have UniversalAdditionalCameraData. Is this URP?");
//     //         }
//     //
//     //         // Ensure the camera is rendering in HDR (High Dynamic Range)
//     //         // Bloom needs HDR values to work correctly.
//     //         if (!__instance.CameraComponent.allowHDR)
//     //         {
//     //             __instance.CameraComponent.allowHDR = true;
//     //             Debug.Log("[BloomMod] Enabled HDR on Camera: " + __instance.CameraComponent.name);
//     //         }
//     //     }
//     // }
//     //
//     // [HarmonyPatch(typeof(PlayerCamera), "OnNetworkPostSpawn")]
//     // public static class PlayerCameraOnNetworkPostSpawn
//     // {
//     //     [HarmonyPostfix]
//     //     public static void Postfix(SpectatorCamera __instance)
//     //     {
//     //         if (__instance.CameraComponent == null) return;
//     //         
//     //         UniversalAdditionalCameraData cameraData = __instance.CameraComponent.GetUniversalAdditionalCameraData();
//     //         if (cameraData != null)
//     //         {
//     //             // Ensure post-processing is enabled for this camera
//     //             if (!cameraData.renderPostProcessing)
//     //             {
//     //                 cameraData.renderPostProcessing = true;
//     //                 Debug.Log("[BloomMod] Enabled Post Processing on Camera: " + __instance.CameraComponent.name);
//     //             }
//     //
//     //             // Also check the Volume Layer Mask
//     //             // This determines which Volumes this camera will consider.
//     //             // It should typically include the layer your Global Volume is on, or 'Everything'.
//     //             // If your Global Volume is on a specific layer, ensure this mask includes it.
//     //             // cameraData.volumeLayerMask = LayerMask.GetMask("YourVolumeLayerName"); // Example
//     //             // OR to be safe, set it to everything:
//     //             // cameraData.volumeLayerMask = ~0; // All layers
//     //         }
//     //         else
//     //         {
//     //             Debug.LogWarning("[BloomMod] Camera does not have UniversalAdditionalCameraData. Is this URP?");
//     //         }
//     //
//     //         // Ensure the camera is rendering in HDR (High Dynamic Range)
//     //         // Bloom needs HDR values to work correctly.
//     //         if (!__instance.CameraComponent.allowHDR)
//     //         {
//     //             __instance.CameraComponent.allowHDR = true;
//     //             Debug.Log("[BloomMod] Enabled HDR on Camera: " + __instance.CameraComponent.name);
//     //         }
//     //     }
//     // }
// }