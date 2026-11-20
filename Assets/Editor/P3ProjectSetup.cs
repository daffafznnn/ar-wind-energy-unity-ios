using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WindEnergy.Editor
{
    public static class P3ProjectSetup
    {
        public static void Configure()
        {
            const string prefabPath = "Assets/Prefabs/Turbine.prefab";
            if (File.Exists(prefabPath)) throw new IOException("Refusing to replace an existing turbine prefab.");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/WindEnergyAR.unity");
            Directory.CreateDirectory("Assets/Art/Materials");
            Directory.CreateDirectory("Assets/Prefabs");
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Kenney/windmill.fbx");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Kenney/Textures/colormap.png");
            if (source == null || texture == null) throw new InvalidOperationException("Selected Kenney assets missing.");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Turbine" };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, "Assets/Art/Materials/Turbine.mat");
            var selection = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Selection" };
            selection.SetColor("_BaseColor", new Color32(23, 90, 70, 255));
            AssetDatabase.CreateAsset(selection, "Assets/Art/Materials/Selection.mat");
            var root = new GameObject("Turbine");
            var visual = new GameObject("ModelVisualRoot");
            visual.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, visual.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Kenney Windmill";
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterial = material;
            }
            var scale = 0.30f / bounds.size.y;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localRotation = Quaternion.Euler(0, 90, 0);
            model.transform.localPosition = Vector3.up * (-bounds.min.y * scale);
            var rotor = model.transform.Find("blades");
            if (rotor == null) throw new InvalidOperationException("Inspected blades transform missing.");
            var rotorMarker = Marker("Rotor marker", new Vector3(0.325f, 1.676353f, -0.000809f), model.transform, visual.transform, selection);
            var generatorMarker = Marker("Generator housing marker", new Vector3(-0.07f, 1.825f, 0), model.transform, visual.transform, selection);
            var towerMarker = Marker("Tower marker", new Vector3(0.085f, 0.85f, 0), model.transform, visual.transform, selection);
            var view = root.AddComponent<TurbineView>();
            var fields = new SerializedObject(view);
            fields.FindProperty("rotor").objectReferenceValue = rotor;
            fields.FindProperty("modelVisualRoot").objectReferenceValue = visual;
            fields.FindProperty("rotorAxis").vector3Value = Vector3.right;
            fields.FindProperty("rotorMarker").objectReferenceValue = rotorMarker;
            fields.FindProperty("generatorMarker").objectReferenceValue = generatorMarker;
            fields.FindProperty("towerMarker").objectReferenceValue = towerMarker;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            var controller = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ARExperienceController>(true)).Single();
            var controllerFields = new SerializedObject(controller);
            controllerFields.FindProperty("contentPrefab").objectReferenceValue = prefab;
            controllerFields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new IOException("P3 scene save failed.");
            Debug.Log($"P3_CONFIGURED: source scale={scale:F9}; initial height=0.30m; rotor axis=local X; existing Canvas unchanged.");
        }

        private static GameObject Marker(string name, Vector3 sourcePosition, Transform model, Transform visual, Material material)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(visual, false);
            marker.transform.position = model.TransformPoint(sourcePosition);
            marker.transform.localScale = Vector3.one * 0.009f;
            marker.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);
            return marker;
        }

        public static void InspectImport()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Kenney/windmill.fbx");
            if (model == null) throw new InvalidOperationException("Windmill FBX not imported.");
            var instance = UnityEngine.Object.Instantiate(model);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                var mesh = t.GetComponent<MeshFilter>()?.sharedMesh;
                Debug.Log($"P3_NODE {t.name}: localPosition={t.localPosition:F6}; rotation={t.localEulerAngles:F6}; scale={t.localScale:F6}; worldPosition={t.position:F6}; meshBounds={mesh?.bounds}; triangles={mesh?.triangles.Length / 3}");
                if (mesh != null)
                {
                    Debug.Log($"P3_MESH {t.name}: min={mesh.bounds.min:F6}; max={mesh.bounds.max:F6}");
                    if (t.name.ToLowerInvariant().Contains("blades"))
                        Debug.Log("P3_BLADE_VERTICES " + string.Join(";", mesh.vertices.Distinct().Select(v => v.ToString("F6"))));
                }
            }
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                Debug.Log($"P3_RENDERER {renderer.name}: boundsMin={renderer.bounds.min:F6}; boundsMax={renderer.bounds.max:F6}; materials={string.Join(",", renderer.sharedMaterials.Select(m => m.name + ":" + m.shader.name))}");
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
