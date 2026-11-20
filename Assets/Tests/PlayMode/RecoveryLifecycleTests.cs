using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace WindEnergy.Tests.PlayMode
{
    public class RecoveryProbeController : PlacementProbeController
    {
        public CameraAuthorizationStatus Authorization = CameraAuthorizationStatus.Authorized;
        public ARSessionState Session = ARSessionState.SessionTracking;
        protected override CameraAuthorizationStatus ReadCameraAuthorization() => Authorization;
        protected override ARSessionState ReadSessionState() => Session;
    }

    public class RecoveryLifecycleTests
    {
        private GameObject host, canvas;
        private RecoveryProbeController controller;
        private Camera camera;
        private ARAnchor anchor;
        private ExperienceUI ui;
        private TurbineView turbine;
        private Transform visual, rotor;
        private ExperienceState State => controller.State;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            host = new GameObject("Recovery test host");
            host.SetActive(false);
            camera = host.AddComponent<Camera>();
            camera.enabled = false;
            controller = host.AddComponent<RecoveryProbeController>();
            SetField("arCamera", camera);
            SetField("contentPrefab", UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Turbine.prefab"));
            host.SetActive(true);
            controller.RefreshAvailability();
            State.SetCandidate(true);
            var placement = controller.PlaceAsync();
            var root = new GameObject("Controlled recovery anchor");
            root.SetActive(false);
            anchor = root.AddComponent<ARAnchor>();
            anchor.enabled = false;
            SetAnchorTracking(TrackingState.Tracking);
            root.SetActive(true);
            controller.Requests[0].SetResult(anchor);
            var deadline = Time.realtimeSinceStartup + 3;
            while (!placement.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(placement.IsCompletedSuccessfully && placement.Result, Is.True);
            controller.RefreshAvailability();
            turbine = anchor.GetComponentInChildren<TurbineView>();
            visual = turbine.transform.Find("ModelVisualRoot");
            rotor = visual.Find("Kenney Windmill/blades");
            canvas = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab"));
            ui = canvas.GetComponent<ExperienceUI>();
            ui.Bind(State, controller.Place, controller.ResetExperience);
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.Wind));
            Assert.That(visual.gameObject.activeSelf, Is.True);
        }

        private void SetField(string name, object value) =>
            typeof(ARExperienceController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);

        private void SetAnchorTracking(TrackingState tracking)
        {
            // Supply provider facts at the package boundary; application tracking logic stays real.
            var setter = typeof(ARTrackable<XRAnchor, ARAnchor>).GetMethod("SetSessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(anchor, new object[] { new XRAnchor(new TrackableId(1, 2), Pose.identity, tracking, IntPtr.Zero) });
        }

        [TestCase(LearningTab.Wind, false)]
        [TestCase(LearningTab.Wind, true)]
        [TestCase(LearningTab.Parts, false)]
        [TestCase(LearningTab.Parts, true)]
        public void RecoveryPreservesLearningChoices(LearningTab tab, bool paused)
        {
            State.SetWind(WindLevel.High);
            State.SelectPart(TurbinePart.Generator);
            if (paused) State.TogglePause();
            State.SetTab(tab);
            ui.ExpandDetails();
            ui.EnterFocus();
            controller.Session = ARSessionState.SessionInitializing;
            SetAnchorTracking(TrackingState.Limited);
            controller.RefreshAvailability();
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.TrackingLost));
            Assert.That(State.CanAnimate, Is.False);
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(anchor.gameObject.activeSelf, Is.True);
            Assert.That(ui.PanelMode, Is.EqualTo(ExperiencePanelMode.Compact));
            Assert.That(ui.buttons[2].gameObject.activeInHierarchy, Is.True);
            controller.Session = ARSessionState.SessionTracking;
            SetAnchorTracking(TrackingState.Tracking);
            controller.RefreshAvailability();
            Assert.That(State.Wind, Is.EqualTo(WindLevel.High));
            Assert.That(State.Part, Is.EqualTo(TurbinePart.Generator));
            Assert.That(State.Tab, Is.EqualTo(tab));
            Assert.That(State.ManualPaused, Is.EqualTo(paused));
            Assert.That(State.CanAnimate, Is.EqualTo(tab == LearningTab.Wind && !paused));
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Assert.That(anchor.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            if (tab == LearningTab.Parts)
                Assert.That(visual.Find("Generator housing marker").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void OutOfFrameDoesNotChangeTracking()
        {
            camera.transform.SetPositionAndRotation(new Vector3(100, 100, 100), Quaternion.identity);
            Assert.That(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), new Bounds(anchor.transform.position, Vector3.one)), Is.False);
            host.SendMessage("Update");
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.Wind));
            Assert.That(State.CanAnimate, Is.True);
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Assert.That(ui.buttons[2].gameObject.activeInHierarchy, Is.False);
        }

        [TestCase("OnApplicationPause", true, false)]
        [TestCase("OnApplicationFocus", false, true)]
        public void PermissionReturnRechecksAvailability(string callback, bool background, bool foreground)
        {
            host.SendMessage(callback, background);
            controller.Authorization = CameraAuthorizationStatus.Denied;
            var observed = new List<ExperiencePhase>();
            Action observe = () => observed.Add(State.Phase);
            State.Changed += observe;
            try { host.SendMessage(callback, foreground); }
            finally { State.Changed -= observe; }
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.CameraDenied));
            Assert.That(observed, Has.No.Member(ExperiencePhase.Wind), "Resume must read permission before publishing a visible/animating state.");
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(ui.buttons[0].gameObject.activeInHierarchy, Is.False);
            controller.Authorization = CameraAuthorizationStatus.Authorized;
            controller.RefreshAvailability();
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.Wind));
            Assert.That(visual.gameObject.activeSelf, Is.True);
        }

        [TestCase("OnApplicationPause", true, false)]
        [TestCase("OnApplicationFocus", false, true)]
        public void BackgroundStopsAnimation(string callback, bool background, bool foreground)
        {
            turbine.Advance(0.1f);
            var stopped = rotor.localRotation;
            host.SendMessage(callback, background);
            turbine.Advance(1);
            Assert.That(State.CanAnimate, Is.False);
            Assert.That(rotor.localRotation, Is.EqualTo(stopped));
            Assert.That(visual.gameObject.activeSelf, Is.False);
            host.SendMessage(callback, foreground);
            turbine.Advance(0.1f);
            Assert.That(Quaternion.Angle(stopped, rotor.localRotation), Is.GreaterThan(0.1f));
        }

        [TestCase(CameraAuthorizationStatus.Denied, ARSessionState.SessionTracking, ExperiencePhase.CameraDenied)]
        [TestCase(CameraAuthorizationStatus.Undetermined, ARSessionState.SessionTracking, ExperiencePhase.Initializing)]
        [TestCase(CameraAuthorizationStatus.Authorized, ARSessionState.Unsupported, ExperiencePhase.Unsupported)]
        public void UnavailableRetainsNoPlacementControls(CameraAuthorizationStatus authorization, ARSessionState session, ExperiencePhase phase)
        {
            ui.EnterFocus();
            controller.Authorization = authorization;
            controller.Session = session;
            controller.RefreshAvailability();
            Assert.That(State.Phase, Is.EqualTo(phase));
            Assert.That(ui.PanelMode, Is.EqualTo(ExperiencePanelMode.Compact));
            Assert.That(ui.sheet.gameObject.activeInHierarchy, Is.True);
            Assert.That(ui.buttons[0].gameObject.activeInHierarchy, Is.False);
            Assert.That(ui.focusDock.activeInHierarchy, Is.False);
            Assert.That(State.CanPlace, Is.False);
            Assert.That(State.CanAnimate, Is.False);
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(ui.description.text, Is.Not.Empty);
        }

        [Test]
        public void PermissionGrantWaitsForCurrentTracking()
        {
            controller.Authorization = CameraAuthorizationStatus.Denied;
            controller.RefreshAvailability();
            controller.Authorization = CameraAuthorizationStatus.Authorized;
            controller.Session = ARSessionState.SessionInitializing;
            SetAnchorTracking(TrackingState.Limited);
            var observed = new List<ExperiencePhase>();
            Action observe = () => observed.Add(State.Phase);
            State.Changed += observe;
            try { controller.RefreshAvailability(); }
            finally { State.Changed -= observe; }
            Assert.That(observed, Has.No.Member(ExperiencePhase.Wind), "A grant must not reveal content using cached session/anchor tracking.");
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.TrackingLost));
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(State.CanAnimate, Is.False);
        }

        [Test]
        public void PauseAndFocusMustBothReturnBeforeAnimationResumes()
        {
            host.SendMessage("OnApplicationPause", true);
            host.SendMessage("OnApplicationFocus", false);
            host.SendMessage("OnApplicationPause", false);
            Assert.That(State.CanAnimate, Is.False);
            Assert.That(visual.gameObject.activeSelf, Is.False);
            host.SendMessage("OnApplicationFocus", true);
            Assert.That(State.CanAnimate, Is.True);
            Assert.That(visual.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void MissingAnchorDoesNotCreateReplacementWithoutReset()
        {
            UnityEngine.Object.DestroyImmediate(anchor.gameObject);
            controller.RefreshAvailability();
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.TrackingLost));
            Assert.That(State.CanPlace, Is.False);
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            ui.buttons[2].onClick.Invoke();
            Assert.That(State.HasPlacement, Is.False);
            Assert.That(State.Wind, Is.EqualTo(WindLevel.Medium));
            Assert.That(State.Tab, Is.EqualTo(LearningTab.Wind));
            Assert.That(State.Phase, Is.EqualTo(ExperiencePhase.Scanning));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (anchor != null) UnityEngine.Object.DestroyImmediate(anchor.gameObject);
            yield return null;
        }
    }
}
