using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace WindEnergy.Tests.EditMode
{
    public class ConfigurationTests
    {
        private const string ScenePath = "Assets/Scenes/WindEnergyAR.unity";

        [Test]
        public void ManifestUsesPinnedVersions()
        {
            var versions = new Dictionary<string, string>
            {
                ["com.unity.xr.arfoundation"] = "6.7.1",
                ["com.unity.xr.arkit"] = "6.7.1",
                ["com.unity.xr.management"] = "4.6.1",
                ["com.unity.render-pipelines.universal"] = "17.6.0",
                ["com.unity.inputsystem"] = "1.20.0",
                ["com.unity.ugui"] = "2.6.0",
                ["com.unity.test-framework"] = "1.8.0"
            };
            var manifest = File.ReadAllText("Packages/manifest.json");
            var resolved = PackageInfo.GetAllRegisteredPackages();
            foreach (var pair in versions)
            {
                Assert.That(Regex.IsMatch(manifest, "\"" + Regex.Escape(pair.Key) + "\"\\s*:\\s*\"" + Regex.Escape(pair.Value) + "\""), Is.True, pair.Key);
                Assert.That(resolved.Single(package => package.name == pair.Key).version, Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void IosSettingsMatchPlan()
        {
            {
                Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(BuildTarget.iOS));
                Assert.That(PlayerSettings.iOS.targetOSVersionString, Is.EqualTo("15.0"));
                Assert.That(PlayerSettings.GetArchitecture(NamedBuildTarget.iOS), Is.EqualTo(1), "ARM64");
                Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS), Is.EqualTo(ScriptingImplementation.IL2CPP));
                Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS), Is.EqualTo(new[] { GraphicsDeviceType.Metal }));
                Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
                Assert.That(PlayerSettings.productName, Is.EqualTo("AR Energi Angin"));
                Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS), Is.EqualTo("com.daffafauzan.utsarenergiangin"));
                Assert.That(PlayerSettings.iOS.cameraUsageDescription, Is.EqualTo("Kamera digunakan untuk menampilkan turbin AR pada permukaan meja."));
                Assert.That(PlayerSettings.iOS.microphoneUsageDescription, Is.Empty);
                var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
                Assert.That(settings.FindProperty("activeInputHandler").intValue, Is.EqualTo(1));
            }
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
            Assert.That(general, Is.Not.Null, "iOS XR settings");
            Assert.That(general.InitManagerOnStart, Is.True);
            Assert.That(general.Manager, Is.Not.Null);
            Assert.That(general.Manager.activeLoaders.Select(loader => loader.GetType().FullName), Is.EqualTo(new[] { "UnityEngine.XR.ARKit.ARKitLoader" }));
            Assert.That(general.Manager.automaticLoading, Is.True);
            Assert.That(general.Manager.automaticRunning, Is.True);
        }

        [Test]
        public void SceneWiresSingleTurbine()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controllers = roots.SelectMany(root => root.GetComponentsInChildren<ARExperienceController>(true)).ToArray();
                Assert.That(controllers.Length, Is.EqualTo(1));
                var controller = controllers[0];
                var origin = roots.SelectMany(root => root.GetComponentsInChildren<XROrigin>(true)).Single();
                Assert.That(origin.GetComponent<ARPlaneManager>().requestedDetectionMode,
                    Is.EqualTo(UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal));
                Assert.That(origin.GetComponent<ARRaycastManager>().enabled, Is.True);
                Assert.That(origin.GetComponent<ARAnchorManager>().enabled, Is.True);
                var serialized = new SerializedObject(controller);
                foreach (var name in new[] { "planeManager", "raycastManager", "anchorManager", "arCamera", "reticle", "contentPrefab" })
                    Assert.That(serialized.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
                var prefab = (GameObject)serialized.FindProperty("contentPrefab").objectReferenceValue;
                Assert.That(PrefabUtility.IsPartOfPrefabAsset(prefab), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo("Assets/Prefabs/Turbine.prefab"));
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
                var buttons = roots.SelectMany(root => root.GetComponentsInChildren<UnityEngine.UI.Button>(true)).ToArray();
                Assert.That(buttons.Length, Is.EqualTo(13));
                var view = roots.SelectMany(root => root.GetComponentsInChildren<ExperienceUI>(true)).Single();
                Assert.That(view.controller, Is.SameAs(controller));
                Assert.That(view.buttons.Concat(new[]{view.detailButton,view.focusButton,view.restoreButton,view.focusPauseButton}), Is.EquivalentTo(buttons));
                Assert.That(view.focusDock,Is.Not.Null);Assert.That(view.detailContent,Is.Not.Null);Assert.That(view.sheetControls,Is.Not.Null);
                var uiSerialized = new SerializedObject(view);
                Assert.That(uiSerialized.FindProperty("focusStatus").objectReferenceValue,Is.Not.Null);
                var ticks = uiSerialized.FindProperty("windTicks");
                Assert.That(ticks.arraySize,Is.EqualTo(2));
                for(int i=0;i<2;i++) Assert.That(new SerializedObject(ticks.GetArrayElementAtIndex(i).objectReferenceValue).FindProperty("m_text").stringValue,Is.EqualTo(i==0?"Tanpa angin":"Tinggi"));
                Assert.That(view.windSlider.wholeNumbers, Is.True);
                Assert.That(view.windSlider.maxValue, Is.EqualTo(3));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<P3ProbeControls>(true)), Is.Empty);
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<PlacementProbeUI>(true)), Is.Empty);
                foreach(var button in buttons) Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero, "P4 binds once at runtime");
                var input = roots.SelectMany(root => root.GetComponentsInChildren<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(true)).Single();
                Assert.That(input.enabled, Is.True);
                Assert.That(input.actionsAsset, Is.Not.Null);
                foreach(var action in new[]{input.point,input.leftClick,input.move,input.submit,input.cancel,input.scrollWheel})
                    Assert.That(action.action,Is.Not.Null,"Input references survive scene serialization");
                Assert.That(input.submit.action.bindings.Any(b=>b.effectivePath=="<Keyboard>/space"),Is.True);
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Single().renderMode,
                    Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void FinalUiSpritesHaveNineSliceBorders()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Button.png");
            Assert.That(sprite.border, Is.EqualTo(new Vector4(8,8,8,8)));
            var panel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panel.png");
            Assert.That(panel.border, Is.EqualTo(new Vector4(20,20,20,20)));
        }

        [Test]
        public void SceneUsesArRenderer()
        {
            Assert.That(File.Exists(ScenePath), Is.True, "Saved AR scene must exist");
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path), Is.EqualTo(new[] { ScenePath }));
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Rendering/ARPipeline.asset");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Rendering/ARRenderer.asset");
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.EqualTo(pipeline));
            for (var index = 0; index < QualitySettings.names.Length; index++)
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(index), Is.EqualTo(pipeline), QualitySettings.names[index]);
            var serializedPipeline = new SerializedObject(pipeline);
            var rendererIndex = serializedPipeline.FindProperty("m_DefaultRendererIndex").intValue;
            Assert.That(serializedPipeline.FindProperty("m_RendererDataList").GetArrayElementAtIndex(rendererIndex).objectReferenceValue, Is.EqualTo(renderer));
            Assert.That(renderer.renderingMode, Is.EqualTo(RenderingMode.Forward));
            Assert.That(renderer.rendererFeatures.OfType<ARBackgroundRendererFeature>().Count(feature => feature.isActive), Is.EqualTo(1));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<ARSession>(true)).Count(), Is.EqualTo(1));
                var origin = roots.SelectMany(root => root.GetComponentsInChildren<XROrigin>(true)).Single();
                Assert.That(origin.Camera, Is.Not.Null);
                Assert.That(origin.Camera.GetComponent<ARCameraManager>().enabled, Is.True);
                Assert.That(origin.Camera.GetComponent<ARCameraBackground>().enabled, Is.True);
                Assert.That(origin.Camera.GetComponent<TrackedPoseDriver>().enabled, Is.True);
                Assert.That(origin.Camera.GetUniversalAdditionalCameraData().scriptableRenderer, Is.SameAs(pipeline.GetRenderer(rendererIndex)));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
