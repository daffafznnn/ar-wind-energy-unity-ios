using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace WindEnergy.Editor
{
    public static class P2ProjectSetup
    {
        public static void Configure()
        {
            const string scenePath = "Assets/Scenes/WindEnergyAR.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.SelectMany(root => root.GetComponentsInChildren<ARExperienceController>(true)).Any())
                throw new InvalidOperationException("P2 setup refuses to replace an existing controller.");
            Directory.CreateDirectory("Assets/P2Probe");
            AssetDatabase.Refresh();
            var origin = roots.SelectMany(root => root.GetComponentsInChildren<XROrigin>(true)).Single();
            var planes = origin.gameObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            var raycasts = origin.gameObject.AddComponent<ARRaycastManager>();
            var anchors = origin.gameObject.AddComponent<ARAnchorManager>();
            var controller = new GameObject("AR Experience").AddComponent<ARExperienceController>();
            var orange = Material("ProbeOrange", new Color32(255, 179, 71, 255));
            var blue = Material("ProbeBlue", new Color32(50, 160, 255, 255));
            var white = Material("ReticleWhite", Color.white);
            var prefab = Probe(orange, blue);
            var reticle = new GameObject("Placement Reticle");
            var ring = reticle.AddComponent<LineRenderer>();
            ring.sharedMaterial = white;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = 0.003f;
            ring.positionCount = 40;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.055f, 0.002f, Mathf.Sin(angle) * 0.055f));
            }
            reticle.SetActive(false);
            var serialized = new SerializedObject(controller);
            Set(serialized, "planeManager", planes);
            Set(serialized, "raycastManager", raycasts);
            Set(serialized, "anchorManager", anchors);
            Set(serialized, "arCamera", origin.Camera);
            Set(serialized, "reticle", reticle);
            Set(serialized, "contentPrefab", prefab);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CreateUI(controller);
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException("P2 scene save failed.");
            AssetDatabase.SaveAssets();
            Debug.Log("P2_SCENE_SAVED: development probe only, not final turbine or final UI.");
        }

        private static Material Material(string name, Color color)
        {
            var path = "Assets/P2Probe/" + name + ".mat";
            if (File.Exists(path)) throw new IOException("Refusing to replace " + path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Probe(Material body, Material marker)
        {
            var root = new GameObject("P2 Probe - not final turbine");
            Box(root.transform, "Body", new Vector3(0, 0.06f, 0), new Vector3(0.08f, 0.12f, 0.08f), body);
            Box(root.transform, "Heading marker", new Vector3(0, 0.14f, 0.035f), new Vector3(0.04f, 0.04f, 0.11f), marker);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/P2Probe/PlacementProbe.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void CreateUI(ARExperienceController controller)
        {
            var canvas = new GameObject("P2 Probe Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0;
            var safe = Rect("Safe Area", canvas.transform);
            safe.anchorMin = Vector2.zero;
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            var panel = Rect("Development Probe Panel", safe);
            panel.anchorMin = new Vector2(0, 0);
            panel.anchorMax = new Vector2(1, 0);
            panel.pivot = new Vector2(0.5f, 0);
            panel.sizeDelta = new Vector2(0, 520);
            panel.gameObject.AddComponent<Image>().color = new Color32(15, 27, 40, 245);
            var title = Label("Probe Title", panel, "PROBE P2 · BUKAN MODEL FINAL", 32);
            Bounds(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -90), new Vector2(-40, -20));
            var status = Label("Probe Status", panel, "Memeriksa kamera dan kesiapan AR…", 42);
            Bounds(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -300), new Vector2(-40, -100));
            var place = Button("Place Probe", panel, "Tempatkan probe", new Color32(255, 179, 71, 255), new Color32(15, 27, 40, 255));
            Bounds((RectTransform)place.transform, new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(40, 40), new Vector2(-12, 180));
            var reset = Button("Reset Probe", panel, "Atur ulang", new Color32(49, 69, 90, 255), Color.white);
            Bounds((RectTransform)reset.transform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(12, 40), new Vector2(-40, 180));
            UnityEventTools.AddPersistentListener(place.onClick, controller.Place);
            UnityEventTools.AddPersistentListener(reset.onClick, controller.ResetExperience);
            place.interactable = false;
            var ui = canvas.gameObject.AddComponent<PlacementProbeUI>();
            var serialized = new SerializedObject(ui);
            Set(serialized, "controller", controller);
            Set(serialized, "safeAreaRoot", safe);
            Set(serialized, "status", status);
            Set(serialized, "placeButton", place);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var input = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
        }

        private static Text Label(string name, Transform parent, string text, int size)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(string name, Transform parent, string text, Color background, Color foreground)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Label("Label", rect, text, 36);
            label.color = foreground;
            label.alignment = TextAnchor.MiddleCenter;
            Bounds(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            return button;
        }

        private static void Bounds(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = lower;
            rect.offsetMax = upper;
        }

        private static void Set(SerializedObject serialized, string name, UnityEngine.Object value) =>
            serialized.FindProperty(name).objectReferenceValue = value;
    }
}
