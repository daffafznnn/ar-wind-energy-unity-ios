using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WindEnergy.Editor
{
    public static class P3ProbeSetup
    {
        public static void Configure()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/WindEnergyAR.unity");
            var roots = scene.GetRootGameObjects();
            var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single();
            if (canvas.GetComponent<P3ProbeControls>() != null) throw new InvalidOperationException("Temporary controls already exist.");
            var controller = roots.SelectMany(r => r.GetComponentsInChildren<ARExperienceController>(true)).Single();
            var panel = (RectTransform)canvas.transform.Find("Safe Area/Development Probe Panel");
            panel.sizeDelta = new Vector2(0, 760);
            var title = panel.Find("Probe Title").GetComponent<Text>();
            title.text = "UJI P3 · UI SEMENTARA";
            title.fontSize = 32;
            Bounds(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -80), new Vector2(-40, -20));
            var status = panel.Find("Probe Status").GetComponent<Text>();
            status.fontSize = 36;
            Bounds(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -220), new Vector2(-40, -90));
            var place = panel.Find("Place Probe").GetComponent<Button>();
            place.GetComponentInChildren<Text>().text = "Tempatkan turbin";
            place.GetComponentInChildren<Text>().fontSize = 32;
            var wind = CreateButton("P3 Wind", panel, "Angin: Sedang", false, 360);
            var pause = CreateButton("P3 Pause", panel, "Jeda", true, 360);
            var tab = CreateButton("P3 Tab", panel, "Buka Bagian", false, 200);
            var part = CreateButton("P3 Part", panel, "Bagian: Rotor", true, 200);
            var controls = canvas.gameObject.AddComponent<P3ProbeControls>();
            var fields = new SerializedObject(controls);
            fields.FindProperty("controller").objectReferenceValue = controller;
            fields.FindProperty("windButton").objectReferenceValue = wind;
            fields.FindProperty("pauseButton").objectReferenceValue = pause;
            fields.FindProperty("tabButton").objectReferenceValue = tab;
            fields.FindProperty("partButton").objectReferenceValue = part;
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log("P3_TEMPORARY_CONTROLS_SAVED: four controls; final P4 UI not implemented.");
        }

        private static Button CreateButton(string name, Transform parent, string text, bool right, float y)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Bounds(rect, new Vector2(right ? 0.5f : 0, 0), new Vector2(right ? 1 : 0.5f, 0),
                new Vector2(right ? 12 : 40, y), new Vector2(right ? -40 : -12, y + 140));
            var image = go.GetComponent<Image>();
            image.color = new Color32(49, 69, 90, 255);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = false;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 1, 1);
            colors.pressedColor = new Color(0.65f, 0.85f, 0.8f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f);
            button.colors = colors;
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(go.transform, false);
            Bounds(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 32;
            label.text = text;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        private static void Bounds(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = lower; rect.offsetMax = upper;
        }
    }
}
