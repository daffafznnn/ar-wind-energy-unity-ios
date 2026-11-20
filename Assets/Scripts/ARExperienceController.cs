using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace WindEnergy
{
    public class ARExperienceController : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private GameObject reticle;
        [SerializeField] private GameObject contentPrefab;
        private readonly List<ARRaycastHit> hits = new();
        private readonly List<ARAnchor> cleanupPending = new();
        private ARAnchor currentAnchor;
        private GameObject content;
        private TurbineView turbineView;
        private bool paused;
        private bool focused = true;
        private CameraAuthorizationStatus? lastAuthorization;
        private ARSessionState? lastSession;

        public ExperienceState State { get; } = new ExperienceState();
        public bool CleanupBlocked => cleanupPending.Count != 0;
        private bool Foreground => !paused && focused;

        private void OnEnable()
        {
            State.Changed += ApplyContent;
            ARSession.stateChanged += SessionChanged;
            RefreshAvailability();
        }

        private void Start() => RefreshAvailability();

        protected virtual CameraAuthorizationStatus ReadCameraAuthorization() => CameraAuthorization.Read();
        protected virtual ARSessionState ReadSessionState() => ARSession.state;

        private void SessionChanged(ARSessionStateChangedEventArgs args) => RefreshAvailability();

        public void RefreshAvailability()
        {
            if (arCamera == null) return;
            var authorization = ReadCameraAuthorization();
            var session = ReadSessionState();
            var availability = CameraAuthorization.Evaluate(authorization, session);
            // Close access first on denial; refresh tracking before opening it on a grant.
            if (availability != Availability.Ready) State.SetAvailability(availability);
            SyncTracking();
            if (availability == Availability.Ready) State.SetAvailability(availability);
            if (Debug.isDebugBuild && (lastAuthorization != authorization || lastSession != session))
            {
                Debug.Log($"P2_CAMERA_PERMISSION: {authorization}; ar={session}; availability={availability}");
                lastAuthorization = authorization;
                lastSession = session;
            }
        }

        private void SyncTracking()
        {
            State.SetTracking(ReadSessionState() == ARSessionState.SessionTracking,
                currentAnchor != null && currentAnchor.trackingState == TrackingState.Tracking, Foreground);
        }

        private void Update()
        {
            if (arCamera == null) return;
            RefreshAvailability();
            if (planeManager == null || raycastManager == null) return;
            var pose = default(Pose);
            var valid = Foreground && !State.HasPlacement && !CleanupBlocked && TryGetPlacementPose(out pose);
            State.SetCandidate(valid);
            if (reticle != null)
            {
                reticle.SetActive(State.CanPlace);
                if (State.CanPlace) reticle.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
            ApplyContent();
            foreach (var plane in planeManager.trackables)
                foreach (var renderer in plane.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = !State.HasPlacement && Foreground;
        }

        private void ApplyContent()
        {
            if (content == null) return;
            if (turbineView != null)
            {
                content.SetActive(true);
                turbineView.Apply(State);
            }
            else content.SetActive(State.Phase == ExperiencePhase.Wind || State.Phase == ExperiencePhase.Parts);
        }

        protected virtual bool TryGetPlacementPose(out Pose pose)
        {
            pose = default;
            if (arCamera == null || planeManager == null || raycastManager == null ||
                !planeManager.enabled || !raycastManager.enabled || ReadSessionState() != ARSessionState.SessionTracking) return false;
            var point = arCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.58f, 0));
            if (!raycastManager.Raycast(new Vector2(point.x, point.y), hits, TrackableType.PlaneWithinPolygon)) return false;
            foreach (var hit in hits)
            {
                var plane = planeManager.GetPlane(hit.trackableId);
                if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp || plane.trackingState != TrackingState.Tracking) continue;
                var heading = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up);
                if (heading.sqrMagnitude < 0.001f) heading = Vector3.forward;
                pose = new Pose(hit.pose.position, Quaternion.LookRotation(heading, Vector3.up));
                return true;
            }
            return false;
        }

        public async void Place()
        {
            try { await PlaceAsync(); }
            catch (Exception error) { Debug.LogWarning($"P2_PLACE_HANDLER_FAILED: {error.GetType().Name}"); }
        }

        public async Task<bool> PlaceAsync()
        {
            if (!isActiveAndEnabled || !Foreground || CleanupBlocked || contentPrefab == null) return false;
            var valid = TryGetPlacementPose(out var pose);
            State.SetCandidate(valid);
            if (!State.TryBeginPlacement(out var ticket)) return false;
            ARAnchor created = null;
            try
            {
                created = await CreateAnchorAsync(pose);
                if (created == null)
                {
                    State.CompletePlacement(ticket, false);
                    return false;
                }
                if (this == null || !isActiveAndEnabled || !State.CompletePlacement(ticket, true))
                {
                    ReleaseAnchor(created);
                    return false;
                }
                currentAnchor = created;
                content = Instantiate(contentPrefab, created.transform, false);
                content.transform.localPosition = Vector3.zero;
                content.transform.localRotation = Quaternion.identity;
                turbineView = content.GetComponent<TurbineView>();
                ApplyContent();
                Debug.Log("P3_PLACED: one anchored turbine; device tracking not validated by this marker alone.");
                return true;
            }
            catch (Exception error)
            {
                if (created != null) ReleaseAnchor(created);
                if (currentAnchor == created && currentAnchor != null)
                {
                    currentAnchor = null;
                    if (content != null) Destroy(content);
                    State.Reset();
                }
                else State.CompletePlacement(ticket, false);
                Debug.LogWarning($"P2_PLACEMENT_FAILED: {error.GetType().Name}");
                return false;
            }
        }

        protected virtual async Task<ARAnchor> CreateAnchorAsync(Pose pose)
        {
            if (anchorManager == null || !anchorManager.enabled || anchorManager.subsystem == null) return null;
            var result = await anchorManager.TryAddAnchorAsync(pose);
            return result.status.IsSuccess() ? result.value : null;
        }

        protected virtual bool RemoveAnchor(ARAnchor anchor) =>
            anchorManager != null && anchorManager.enabled && anchorManager.TryRemoveAnchor(anchor);

        private void ReleaseAnchor(ARAnchor anchor)
        {
            if (anchor == null) return;
            var removed = false;
            try { removed = RemoveAnchor(anchor); }
            catch (Exception error) { Debug.LogWarning($"P2_ANCHOR_REMOVE_EXCEPTION: {error.GetType().Name}"); }
            if (removed) cleanupPending.Remove(anchor);
            else if (!cleanupPending.Contains(anchor))
            {
                cleanupPending.Add(anchor);
                Debug.LogWarning("P2_ANCHOR_REMOVE_FAILED: reset to retry cleanup.");
            }
        }

        public void ResetExperience()
        {
            State.Reset();
            if (content != null)
            {
                content.SetActive(false);
                Destroy(content);
                content = null;
                turbineView = null;
            }
            foreach (var pending in cleanupPending.ToArray())
            {
                if (pending == null) cleanupPending.Remove(pending);
                else ReleaseAnchor(pending);
            }
            var previous = currentAnchor;
            currentAnchor = null;
            ReleaseAnchor(previous);
            if (reticle != null) reticle.SetActive(false);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            // Refresh permission before foreground can reveal or animate retained content.
            if (!value) RefreshAvailability();
            else SyncTracking();
            ApplyContent();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (value) RefreshAvailability();
            else SyncTracking();
            ApplyContent();
        }

        private void OnDisable()
        {
            State.Changed -= ApplyContent;
            ARSession.stateChanged -= SessionChanged;
            State.SetTracking(false, false, false);
            ResetExperience();
        }
    }
}
