using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WindEnergy.Tests.PlayMode
{
    public class TurbineViewTests
    {
        private GameObject host;
        private GameObject visual;
        private Transform rotor;
        private GameObject[] markers;
        private TurbineView view;
        private ExperienceState state;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Turbine test");
            host.SetActive(false);
            host.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 35, 0));
            visual = new GameObject("ModelVisualRoot");
            visual.transform.SetParent(host.transform, false);
            rotor = new GameObject("Rotor").transform;
            rotor.SetParent(visual.transform, false);
            rotor.localPosition = new Vector3(0, 0.2f, 0);
            rotor.localRotation = Quaternion.Euler(12, 23, 34);
            markers = new GameObject[3];
            for (var i = 0; i < markers.Length; i++)
            {
                markers[i] = new GameObject(((TurbinePart)i).ToString());
                markers[i].transform.SetParent(visual.transform, false);
            }
            view = host.AddComponent<TurbineView>();
            Set("rotor", rotor);
            Set("modelVisualRoot", visual);
            Set("rotorAxis", Vector3.right);
            Set("rotorMarker", markers[0]);
            Set("generatorMarker", markers[1]);
            Set("towerMarker", markers[2]);
            host.SetActive(true);
            state = new ExperienceState();
            state.SetAvailability(Availability.Ready);
            state.SetTracking(true, false, true);
            state.SetCandidate(true);
            state.TryBeginPlacement(out var ticket);
            state.CompletePlacement(ticket, true);
            state.SetTracking(true, true, true);
        }

        private void Set(string name, object value) =>
            typeof(TurbineView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);

        [Test]
        public void NoWindStopsRotor()
        {
            state.SetWind(WindLevel.None);
            view.Apply(state);
            var initial = rotor.localRotation;
            view.Advance(1);
            Assert.That(Quaternion.Angle(initial, rotor.localRotation), Is.LessThan(0.001f));
            state.SetWind(WindLevel.Low);
            view.Apply(state);
            view.Advance(1);
            Assert.That(Quaternion.Angle(initial, rotor.localRotation), Is.EqualTo(30).Within(0.001f));
        }

        [Test]
        public void AdvanceRotatesRotorNotRoot()
        {
            var position = host.transform.position;
            var rotation = host.transform.rotation;
            var initial = rotor.localRotation;
            var pivot = rotor.position;
            view.Apply(state);
            view.Advance(0.5f);
            var expected = initial * Quaternion.AngleAxis(35, Vector3.right);
            Assert.That(Quaternion.Angle(expected, rotor.localRotation), Is.LessThan(0.001f));
            Assert.That(host.transform.position, Is.EqualTo(position));
            Assert.That(host.transform.rotation, Is.EqualTo(rotation));
            Assert.That(rotor.position, Is.EqualTo(pivot));
            view.Apply(state);
            Assert.That(Quaternion.Angle(expected, rotor.localRotation), Is.LessThan(0.001f), "Apply must preserve angle");
            view.Advance(0.5f);
            Assert.That(Quaternion.Angle(initial * Quaternion.AngleAxis(70, Vector3.right), rotor.localRotation), Is.LessThan(0.001f));
        }

        [TestCase(TurbinePart.Rotor)]
        [TestCase(TurbinePart.Generator)]
        [TestCase(TurbinePart.Tower)]
        public void PartsSelectsCorrectMarker(TurbinePart part)
        {
            state.SetTab(LearningTab.Parts);
            state.SelectPart(part);
            view.Apply(state);
            Assert.That(visual.activeSelf, Is.True);
            for (var i = 0; i < markers.Length; i++) Assert.That(markers[i].activeSelf, Is.EqualTo(i == (int)part));
            var initial = rotor.localRotation;
            view.Advance(1);
            Assert.That(rotor.localRotation, Is.EqualTo(initial));
            state.SetTab(LearningTab.Wind);
            view.Apply(state);
            foreach (var marker in markers) Assert.That(marker.activeSelf, Is.False);
        }

        [Test]
        public void TrackingLossHidesOnlyVisualRootAndStopsRotor()
        {
            view.Apply(state);
            state.SetTracking(false, false, true);
            view.Apply(state);
            Assert.That(visual.activeSelf, Is.False);
            Assert.That(host.activeSelf, Is.True);
            var initial = rotor.localRotation;
            view.Advance(1);
            Assert.That(rotor.localRotation, Is.EqualTo(initial));
            state.SetTracking(true, true, true);
            view.Apply(state);
            Assert.That(visual.activeSelf, Is.True);
            view.Advance(1);
            Assert.That(Quaternion.Angle(initial, rotor.localRotation), Is.EqualTo(70).Within(0.001f));
        }

        [Test]
        public void ManualPauseStopsAndResumesWithoutResettingAngle()
        {
            view.Apply(state);
            view.Advance(0.25f);
            var initial = rotor.localRotation;
            state.TogglePause();
            view.Apply(state);
            view.Advance(1);
            Assert.That(rotor.localRotation, Is.EqualTo(initial));
            state.TogglePause();
            view.Apply(state);
            view.Advance(0.25f);
            Assert.That(Quaternion.Angle(initial, rotor.localRotation), Is.EqualTo(17.5f).Within(0.001f));
        }

        [TestCase(WindLevel.None, 0)]
        [TestCase(WindLevel.Low, 30)]
        [TestCase(WindLevel.Medium, 70)]
        [TestCase(WindLevel.High, 120)]
        public void UsesApprovedIllustrativeSpeeds(WindLevel level, float expected) =>
            Assert.That(TurbineView.DegreesPerSecond(level), Is.EqualTo(expected));

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidSpeedLevelIsRejected(int value) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => TurbineView.DegreesPerSecond((WindLevel)value));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);
    }
}
