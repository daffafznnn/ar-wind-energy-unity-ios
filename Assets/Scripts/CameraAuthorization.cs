using System;
using System.Runtime.InteropServices;
using UnityEngine.XR.ARFoundation;

namespace WindEnergy
{
    public enum CameraAuthorizationStatus { Undetermined, Restricted, Denied, Authorized }

    public static class CameraAuthorization
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int WindEnergyCameraAuthorizationStatus();
#endif

        public static CameraAuthorizationStatus Read()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return (CameraAuthorizationStatus)WindEnergyCameraAuthorizationStatus();
#else
            return CameraAuthorizationStatus.Authorized;
#endif
        }

        public static Availability Evaluate(CameraAuthorizationStatus authorization, ARSessionState session)
        {
            if (!Enum.IsDefined(typeof(CameraAuthorizationStatus), authorization))
                throw new ArgumentOutOfRangeException(nameof(authorization));
            if (session == ARSessionState.Unsupported) return Availability.Unsupported;
            if (authorization == CameraAuthorizationStatus.Undetermined) return Availability.Checking;
            if (authorization != CameraAuthorizationStatus.Authorized) return Availability.CameraDenied;
            return session == ARSessionState.Ready || session == ARSessionState.SessionInitializing ||
                   session == ARSessionState.SessionTracking ? Availability.Ready : Availability.Checking;
        }
    }
}
