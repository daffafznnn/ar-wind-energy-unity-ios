using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WindEnergy.Tests.EditMode
{
    public class TurbinePrefabTests
    {
        [Test]
        public void PrefabUsesSelectedKenneyMeshesAndUrpMaterials()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Turbine.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<TurbineView>(), Is.Not.Null);
            var data = new SerializedObject(prefab.GetComponent<TurbineView>());
            foreach (var field in new[] { "rotor", "modelVisualRoot", "rotorMarker", "generatorMarker", "towerMarker" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(data.FindProperty("rotorAxis").vector3Value, Is.EqualTo(Vector3.right));
            var rotor = (Transform)data.FindProperty("rotor").objectReferenceValue;
            Assert.That(rotor.name, Is.EqualTo("blades"));
            Assert.That(rotor.localPosition.x, Is.EqualTo(0.240357f).Within(0.00001f));
            Assert.That(rotor.localPosition.y, Is.EqualTo(1.676353f).Within(0.00001f));
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            var model = prefab.transform.Find("ModelVisualRoot/Kenney Windmill");
            Assert.That(model, Is.Not.Null);
            var meshes = model.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.Length, Is.EqualTo(2));
            Assert.That(meshes.Sum(m => m.sharedMesh.triangles.Length / 3), Is.EqualTo(456));
            foreach (var mesh in meshes)
                Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.EqualTo("Assets/Art/Kenney/windmill.fbx"));
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo("Assets/Art/Materials/Turbine.mat"));
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial.GetTexture("_BaseMap")), Is.EqualTo("Assets/Art/Kenney/Textures/colormap.png"));
            }
        }

        [Test]
        public void PrefabBaseIsAtAnchorAndInitialHeightIsThirtyCentimeters()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Turbine.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                var model = instance.transform.Find("ModelVisualRoot/Kenney Windmill");
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.min.y, Is.EqualTo(0).Within(0.00001f));
                Assert.That(bounds.size.y, Is.EqualTo(0.30f).Within(0.00001f));
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
                var rotor = model.Find("blades");
                Assert.That(Vector3.Dot(rotor.right, Vector3.back), Is.GreaterThan(0.999f));
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
