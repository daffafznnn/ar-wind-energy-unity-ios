using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WindEnergy.Editor
{
    public static class P3VisualEvidence
    {
        public static void CaptureControls()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/WindEnergyAR.unity");
            var roots = scene.GetRootGameObjects();
            var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single();
            var controller = roots.SelectMany(r => r.GetComponentsInChildren<ARExperienceController>(true)).Single();
            foreach (var root in roots) if (root != canvas.gameObject) root.SetActive(false);
            var camera = new GameObject("UI evidence camera (unsaved)").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(170, 181, 174, 255);
            camera.nearClipPlane = 0.01f;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var target = new RenderTexture(1170, 2532, 24);
            target.Create();
            camera.targetTexture = target;
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../evidence/test-runs/p3/controls-visual"));
            Directory.CreateDirectory(output);
            controller.State.SetAvailability(Availability.Ready);
            controller.State.SetTracking(true, false, true);
            controller.State.SetCandidate(true);
            CaptureState("ready");
            controller.State.TryBeginPlacement(out var ticket);
            controller.State.CompletePlacement(ticket, true);
            controller.State.SetTracking(true, true, true);
            CaptureState("wind");
            controller.State.SetTab(LearningTab.Parts);
            controller.State.SelectPart(TurbinePart.Generator);
            CaptureState("generator");
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("P3_CONTROLS_VISUAL: 3 Editor UI renders; no live camera or device claim; scene not saved.");

            void CaptureState(string name)
            {
                canvas.GetComponent<P3ProbeControls>().Render();
                canvas.GetComponent<PlacementProbeUI>().SendMessage("Update");
                Canvas.ForceUpdateCanvases();
                Save(camera, target, Path.Combine(output, name + ".png"));
                foreach (var label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>())
                {
                    if (label.preferredHeight > label.rectTransform.rect.height + 1)
                        throw new InvalidOperationException("Text height overflow: " + label.name + " in " + name);
                }
            }
        }

        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/WindEnergyAR.unity");
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Turbine.prefab");
            var turbine = UnityEngine.Object.Instantiate(prefab);
            var view = turbine.GetComponent<TurbineView>();
            var rotor = turbine.transform.Find("ModelVisualRoot/Kenney Windmill/blades");
            var camera = new GameObject("Evidence camera (unsaved)").AddComponent<Camera>();
            camera.transform.position = new Vector3(0.40f, 0.28f, -0.65f);
            camera.transform.LookAt(new Vector3(0, 0.15f, 0));
            camera.orthographic = true;
            camera.orthographicSize = 0.185f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(230, 234, 230, 255);
            camera.allowHDR = false;
            var light = new GameObject("Evidence light (unsaved)").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.7f;
            light.transform.rotation = Quaternion.Euler(45, -25, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white * 0.65f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Evidence reference plane (unsaved)";
            ground.transform.localScale = Vector3.one * 0.1f;
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.SetColor("_BaseColor", new Color32(160, 175, 163, 255));
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            var state = new ExperienceState();
            state.SetAvailability(Availability.Ready);
            state.SetTracking(true, false, true);
            state.SetCandidate(true);
            state.TryBeginPlacement(out var ticket);
            state.CompletePlacement(ticket, true);
            state.SetTracking(true, true, true);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../evidence/test-runs/p3/visual"));
            Directory.CreateDirectory(output);
            var target = new RenderTexture(720, 960, 24);
            target.Create();
            camera.targetTexture = target;
            foreach (var angle in new[] { 0, 60, 120 })
            {
                view.Apply(state);
                rotor.localRotation = Quaternion.AngleAxis(angle, Vector3.right);
                Save(camera, target, Path.Combine(output, $"rotor-{angle}.png"));
            }
            rotor.localRotation = Quaternion.identity;
            state.SetTab(LearningTab.Parts);
            foreach (TurbinePart part in Enum.GetValues(typeof(TurbinePart)))
            {
                state.SelectPart(part);
                view.Apply(state);
                Save(camera, target, Path.Combine(output, $"part-{part}.png"));
            }
            camera.transform.position = new Vector3(-0.4f, 0.25f, 0.65f);
            camera.transform.LookAt(new Vector3(0, 0.15f, 0));
            state.SetTab(LearningTab.Wind);
            view.Apply(state);
            Save(camera, target, Path.Combine(output, "rear.png"));
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("P3_VISUAL_EVIDENCE: 7 real Editor renders; fixed-angle captures, not physical AR evidence. Scene not saved.");
        }

        private static void Save(Camera camera, RenderTexture target, string path)
        {
            if (File.Exists(path)) throw new IOException("Refusing to overwrite evidence " + path);
            camera.Render();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
