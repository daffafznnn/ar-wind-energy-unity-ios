using System;
using NUnit.Framework;
using UnityEngine.XR.ARFoundation;

namespace WindEnergy.Tests.EditMode
{
    public class CameraAuthorizationTests
    {
        [TestCase(CameraAuthorizationStatus.Undetermined, ARSessionState.SessionTracking, Availability.Checking)]
        [TestCase(CameraAuthorizationStatus.Restricted, ARSessionState.SessionTracking, Availability.CameraDenied)]
        [TestCase(CameraAuthorizationStatus.Denied, ARSessionState.SessionTracking, Availability.CameraDenied)]
        [TestCase(CameraAuthorizationStatus.Authorized, ARSessionState.Ready, Availability.Ready)]
        [TestCase(CameraAuthorizationStatus.Authorized, ARSessionState.SessionInitializing, Availability.Ready)]
        [TestCase(CameraAuthorizationStatus.Authorized, ARSessionState.SessionTracking, Availability.Ready)]
        [TestCase(CameraAuthorizationStatus.Authorized, ARSessionState.CheckingAvailability, Availability.Checking)]
        [TestCase(CameraAuthorizationStatus.Undetermined, ARSessionState.Unsupported, Availability.Unsupported)]
        [TestCase(CameraAuthorizationStatus.Denied, ARSessionState.Unsupported, Availability.Unsupported)]
        [TestCase(CameraAuthorizationStatus.Authorized, ARSessionState.Unsupported, Availability.Unsupported)]
        public void AuthorizationAndSessionDetermineAvailability(CameraAuthorizationStatus authorization,
            ARSessionState session, Availability expected)
        {
            Assert.That(CameraAuthorization.Evaluate(authorization, session), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidAuthorizationCannotBecomeReady()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CameraAuthorization.Evaluate((CameraAuthorizationStatus)99, ARSessionState.SessionTracking));
        }
    }
}
