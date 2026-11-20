using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.ARFoundation;

namespace WindEnergy.Tests.PlayMode
{
    public class CameraAuthorizationProbeController : ARExperienceController
    {
        public CameraAuthorizationStatus Authorization = CameraAuthorizationStatus.Undetermined;
        protected override CameraAuthorizationStatus ReadCameraAuthorization() => Authorization;
        protected override ARSessionState ReadSessionState() => ARSessionState.SessionTracking;
    }

    public class CameraAuthorizationLifecycleTests
    {
        private GameObject host;
        private CameraAuthorizationProbeController controller;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Camera authorization test");
            var camera = host.AddComponent<Camera>();
            controller = host.AddComponent<CameraAuthorizationProbeController>();
            typeof(ARExperienceController).GetField("arCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, camera);
        }

        [UnityTest]
        public IEnumerator UndeterminedIsCheckingNotDeniedAndCannotPlace()
        {
            yield return null;
            controller.RefreshAvailability();
            controller.State.SetCandidate(true);
            Assert.That(controller.State.Phase, Is.EqualTo(ExperiencePhase.Initializing));
            Assert.That(controller.State.CanPlace, Is.False);
        }

        [UnityTest]
        public IEnumerator DeniedCannotPlaceDespiteTrackingSession()
        {
            controller.Authorization = CameraAuthorizationStatus.Denied;
            yield return null;
            controller.RefreshAvailability();
            controller.State.SetCandidate(true);
            Assert.That(controller.State.Phase, Is.EqualTo(ExperiencePhase.CameraDenied));
            Assert.That(controller.State.CanPlace, Is.False);
        }

        [UnityTest]
        public IEnumerator DelayedGrantRecoversWithoutSessionChangeOrResume()
        {
            yield return null;
            controller.State.SetAvailability(Availability.CameraDenied);
            controller.Authorization = CameraAuthorizationStatus.Authorized;
            host.SendMessage("Update");
            controller.State.SetCandidate(true);
            Assert.That(controller.State.Phase, Is.EqualTo(ExperiencePhase.Ready));
            Assert.That(controller.State.CanPlace, Is.True);
        }

        [UnityTest]
        public IEnumerator ResumeRechecksPermissionRevocationAndGrant()
        {
            controller.Authorization = CameraAuthorizationStatus.Authorized;
            yield return null;
            controller.RefreshAvailability();
            controller.State.SetCandidate(true);
            Assert.That(controller.State.CanPlace, Is.True);
            host.SendMessage("OnApplicationPause", true);
            controller.Authorization = CameraAuthorizationStatus.Denied;
            host.SendMessage("OnApplicationPause", false);
            controller.State.SetCandidate(true);
            Assert.That(controller.State.Phase, Is.EqualTo(ExperiencePhase.CameraDenied));
            Assert.That(controller.State.CanPlace, Is.False);
            host.SendMessage("OnApplicationFocus", false);
            controller.Authorization = CameraAuthorizationStatus.Authorized;
            host.SendMessage("OnApplicationFocus", true);
            controller.State.SetCandidate(true);
            Assert.That(controller.State.CanPlace, Is.True);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);
    }
}
