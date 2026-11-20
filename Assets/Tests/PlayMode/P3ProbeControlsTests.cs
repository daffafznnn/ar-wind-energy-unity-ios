using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WindEnergy.Tests.PlayMode
{
    public class P3ProbeControlsTests
    {
        private GameObject host;
        private GameObject ui;
        private ARExperienceController controller;
        private P3ProbeControls controls;
        private Button wind;
        private Button pause;
        private Button tab;
        private Button part;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Controlled AR state");
            controller = host.AddComponent<ARExperienceController>();
            ui = new GameObject("Temporary UI");
            ui.SetActive(false);
            wind = Button("Wind"); pause = Button("Pause"); tab = Button("Tab"); part = Button("Part");
            controls = ui.AddComponent<P3ProbeControls>();
            Set("controller", controller); Set("windButton", wind); Set("pauseButton", pause);
            Set("tabButton", tab); Set("partButton", part);
            ui.SetActive(true);
        }

        private Button Button(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(ui.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            return go.GetComponent<Button>();
        }

        private void Set(string field, object value) => typeof(P3ProbeControls)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controls, value);

        private void Place()
        {
            controller.State.SetAvailability(Availability.Ready);
            controller.State.SetTracking(true, false, true);
            controller.State.SetCandidate(true);
            controller.State.TryBeginPlacement(out var ticket);
            controller.State.CompletePlacement(ticket, true);
            controller.State.SetTracking(true, true, true);
            controls.Render();
        }

        [Test]
        public void ControlsRejectClicksBeforePlacementAndDuringTrackingLoss()
        {
            controls.Render();
            Assert.That(wind.interactable, Is.False);
            wind.onClick.Invoke(); tab.onClick.Invoke(); part.onClick.Invoke(); pause.onClick.Invoke();
            Assert.That(controller.State.Wind, Is.EqualTo(WindLevel.Medium));
            Assert.That(controller.State.Tab, Is.EqualTo(LearningTab.Wind));
            Assert.That(controller.State.Part, Is.EqualTo(TurbinePart.Rotor));
            Assert.That(controller.State.ManualPaused, Is.False);
            Place();
            controller.State.SetTracking(false, false, true);
            controls.Render();
            wind.onClick.Invoke();
            Assert.That(wind.interactable, Is.False);
            Assert.That(controller.State.Wind, Is.EqualTo(WindLevel.Medium));
        }

        [Test]
        public void WindCyclesAllFourLevelsAndZeroDisablesPause()
        {
            Place();
            foreach (var expected in new[] { WindLevel.High, WindLevel.None, WindLevel.Low, WindLevel.Medium })
            {
                wind.onClick.Invoke();
                Assert.That(controller.State.Wind, Is.EqualTo(expected));
                Assert.That(pause.interactable, Is.EqualTo(expected != WindLevel.None));
            }
            pause.onClick.Invoke();
            Assert.That(controller.State.ManualPaused, Is.True);
            Assert.That(pause.GetComponentInChildren<Text>().text, Is.EqualTo("Lanjutkan"));
        }

        [Test]
        public void PartsControlsSelectEachMarkerWithoutLosingPause()
        {
            Place();
            Assert.That(part.interactable, Is.False);
            pause.onClick.Invoke();
            tab.onClick.Invoke();
            Assert.That(controller.State.Tab, Is.EqualTo(LearningTab.Parts));
            Assert.That(wind.interactable, Is.False);
            Assert.That(pause.interactable, Is.False);
            Assert.That(part.interactable, Is.True);
            foreach (var expected in new[] { TurbinePart.Generator, TurbinePart.Tower, TurbinePart.Rotor })
            {
                part.onClick.Invoke();
                Assert.That(controller.State.Part, Is.EqualTo(expected));
            }
            tab.onClick.Invoke();
            Assert.That(controller.State.ManualPaused, Is.True);
            Assert.That(controller.State.CanAnimate, Is.False);
        }

        [Test]
        public void ReenableDoesNotDuplicateClickListeners()
        {
            Place();
            controls.enabled = false;
            controls.enabled = true;
            wind.onClick.Invoke();
            Assert.That(controller.State.Wind, Is.EqualTo(WindLevel.High));
            tab.onClick.Invoke();
            Assert.That(controller.State.Tab, Is.EqualTo(LearningTab.Parts));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ui);
            Object.DestroyImmediate(host);
        }
    }
}
