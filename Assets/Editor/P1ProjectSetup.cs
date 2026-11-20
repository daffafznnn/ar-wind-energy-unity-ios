using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

namespace WindEnergy.Setup
{
    public static class P1ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/WindEnergyAR.unity";

        public static void Configure()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("P1 setup refuses to overwrite the existing AR scene.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new InvalidOperationException("Open with -buildTarget iOS before P1 setup.");

            Directory.CreateDirectory("Assets/Rendering");
            Directory.CreateDirectory("Assets/XR");
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            ConfigurePlayer();
            ConfigureLoader();
            ConfigureRendering();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("P1_CONFIGURATION_SAVED: iOS settings, ARKit loader, URP assets, and AR scene. No device build performed.");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.productName = "AR Energi Angin";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.daffafauzan.utsarenergiangin");
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.iOS.cameraUsageDescription = "Kamera digunakan untuk menampilkan turbin AR pada permukaan meja.";
            PlayerSettings.iOS.microphoneUsageDescription = "";
            PlayerSettings.iOS.locationUsageDescription = "";
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLoader()
        {
            if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey, out var targets))
            {
                targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(targets, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, targets, true);
            }
            if (!targets.HasSettingsForBuildTarget(BuildTargetGroup.iOS))
                targets.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.iOS);
            if (!targets.HasManagerSettingsForBuildTarget(BuildTargetGroup.iOS))
                targets.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.iOS);
            var general = targets.SettingsForBuildTarget(BuildTargetGroup.iOS);
            general.InitManagerOnStart = true;
            general.Manager.automaticLoading = true;
            general.Manager.automaticRunning = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.ARKit.ARKitLoader", BuildTargetGroup.iOS))
                throw new InvalidOperationException("ARKit loader could not be assigned.");
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(targets);
        }

        private static void ConfigureRendering()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Rendering/ARRenderer.asset");
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "ARRenderer";
                renderer.renderingMode = RenderingMode.Forward;
                AssetDatabase.CreateAsset(renderer, "Assets/Rendering/ARRenderer.asset");
                var background = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                background.name = "AR Camera Background";
                AssetDatabase.AddObjectToAsset(background, renderer);
                renderer.rendererFeatures.Add(background);
                EditorUtility.SetDirty(renderer);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Rendering/ARPipeline.asset");
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "ARPipeline";
                pipeline.msaaSampleCount = 1;
                AssetDatabase.CreateAsset(pipeline, "Assets/Rendering/ARPipeline.asset");
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var previousQuality = QualitySettings.GetQualityLevel();
            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(previousQuality, false);
        }

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            var origin = new GameObject("XR Origin", typeof(XROrigin)).GetComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);
            var cameraObject = new GameObject("AR Camera", typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.GetUniversalAdditionalCameraData().SetRenderer(0);
            var pose = cameraObject.GetComponent<TrackedPoseDriver>();
            var position = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotation = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            var tracking = new InputAction("Tracking State", binding: "<XRHMD>/trackingState", expectedControlType: "Integer");
            tracking.AddBinding("<HandheldARInputDevice>/trackingState");
            pose.positionInput = new InputActionProperty(position);
            pose.rotationInput = new InputActionProperty(rotation);
            pose.trackingStateInput = new InputActionProperty(tracking);
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 0f;
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("AR scene could not be saved.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
