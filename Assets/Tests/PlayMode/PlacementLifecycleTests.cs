using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.ARFoundation;

namespace WindEnergy.Tests.PlayMode
{
    public class PlacementProbeController : ARExperienceController
    {
        public bool Candidate = true;
        public bool RemoveSucceeds = true;
        public readonly List<TaskCompletionSource<ARAnchor>> Requests = new();
        public readonly List<ARAnchor> Removed = new();
        public Pose LastPose;
        protected override bool TryGetPlacementPose(out Pose pose)
        {
            pose = new Pose(new Vector3(1, 2, 3), Quaternion.Euler(0, 35, 0));
            return Candidate;
        }
        protected override Task<ARAnchor> CreateAnchorAsync(Pose pose)
        {
            LastPose = pose;
            var source = new TaskCompletionSource<ARAnchor>();
            Requests.Add(source);
            return source.Task;
        }
        protected override bool RemoveAnchor(ARAnchor anchor)
        {
            Removed.Add(anchor);
            if (RemoveSucceeds) Destroy(anchor.gameObject);
            return RemoveSucceeds;
        }
    }

    public class PlacementLifecycleTests
    {
        private GameObject host;
        private GameObject prefab;
        private PlacementProbeController controller;
        private readonly List<GameObject> anchors = new();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Placement test host");
            controller = host.AddComponent<PlacementProbeController>();
            prefab = new GameObject("Test content");
            prefab.SetActive(false);
            typeof(ARExperienceController).GetField("contentPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, prefab);
            Ready();
        }

        private void Ready()
        {
            controller.State.SetAvailability(Availability.Ready);
            controller.State.SetTracking(true, false, true);
            controller.State.SetCandidate(true);
        }

        private ARAnchor Anchor()
        {
            var go = new GameObject("Controlled inactive anchor");
            go.SetActive(false);
            anchors.Add(go);
            return go.AddComponent<ARAnchor>();
        }

        private static IEnumerator Finished(Task<bool> task)
        {
            var deadline = Time.realtimeSinceStartup + 3;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "placement task timed out");
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        [UnityTest]
        public IEnumerator LostCandidateBeforeTapDoesNotCreateAnchor()
        {
            controller.Candidate = false;
            var result = controller.PlaceAsync();
            yield return Finished(result);
            Assert.That(result.Result, Is.False);
            Assert.That(controller.Requests, Is.Empty);
            Assert.That(controller.State.CanPlace, Is.False);
        }

        [UnityTest]
        public IEnumerator LateAnchorAfterResetIsReleased()
        {
            var result = controller.PlaceAsync();
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            controller.ResetExperience();
            var anchor = Anchor();
            controller.Requests[0].SetResult(anchor);
            yield return Finished(result);
            Assert.That(result.Result, Is.False);
            Assert.That(controller.Removed, Is.EqualTo(new[] { anchor }));
            Assert.That(controller.State.HasPlacement, Is.False);
            yield return null;
            Assert.That(anchor == null, Is.True, "stale anchor and content must not remain");
        }

        [UnityTest]
        public IEnumerator SuspendInvalidatesPendingPlacement()
        {
            var result = controller.PlaceAsync();
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            host.SendMessage("OnApplicationPause", true);
            var anchor = Anchor();
            controller.Requests[0].SetResult(anchor);
            yield return Finished(result);
            Assert.That(result.Result, Is.False);
            Assert.That(controller.State.HasPlacement, Is.False);
            Assert.That(controller.Removed, Is.EqualTo(new[] { anchor }));
        }

        [UnityTest]
        public IEnumerator FailedAnchorAllowsRetry()
        {
            var result = controller.PlaceAsync();
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            controller.Requests[0].SetResult(null);
            yield return Finished(result);
            Assert.That(result.Result, Is.False);
            Assert.That(controller.State.PlacementFailed, Is.True);
            var retry = controller.PlaceAsync();
            Assert.That(controller.State.PlacementFailed, Is.False);
            Assert.That(controller.Requests.Count, Is.EqualTo(2));
            var anchor = Anchor();
            controller.Requests[1].SetResult(anchor);
            yield return Finished(retry);
            Assert.That(retry.Result, Is.True);
            Assert.That(anchor.transform.childCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DuplicateTapCreatesOneContentAndResetReleasesIt()
        {
            var first = controller.PlaceAsync();
            var duplicate = controller.PlaceAsync();
            yield return Finished(duplicate);
            Assert.That(duplicate.Result, Is.False);
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            var anchor = Anchor();
            controller.Requests[0].SetResult(anchor);
            yield return Finished(first);
            Assert.That(first.Result, Is.True);
            Assert.That(anchor.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.LastPose.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(controller.LastPose.rotation, Is.EqualTo(Quaternion.Euler(0, 35, 0)));
            controller.ResetExperience();
            Assert.That(controller.State.HasPlacement, Is.False);
            yield return null;
            Assert.That(anchor == null, Is.True);
        }

        [UnityTest]
        public IEnumerator StaleResultDoesNotCancelNewPlacement()
        {
            var old = controller.PlaceAsync();
            controller.ResetExperience();
            Ready();
            var current = controller.PlaceAsync();
            Assert.That(controller.Requests.Count, Is.EqualTo(2));
            var stale = Anchor();
            controller.Requests[0].SetResult(stale);
            yield return Finished(old);
            Assert.That(old.Result, Is.False);
            Assert.That(controller.State.Phase, Is.EqualTo(ExperiencePhase.Placing));
            var active = Anchor();
            controller.Requests[1].SetResult(active);
            yield return Finished(current);
            Assert.That(current.Result, Is.True);
            Assert.That(active.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.Removed, Is.EqualTo(new[] { stale }));
        }

        [UnityTest]
        public IEnumerator FailedRemovalBlocksPlacementUntilResetRetries()
        {
            var result = controller.PlaceAsync();
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            var anchor = Anchor();
            controller.Requests[0].SetResult(anchor);
            yield return Finished(result);
            var content = anchor.transform.GetChild(0).gameObject;
            controller.RemoveSucceeds = false;
            LogAssert.Expect(LogType.Warning, "P2_ANCHOR_REMOVE_FAILED: reset to retry cleanup.");
            controller.ResetExperience();
            Assert.That(content.activeSelf, Is.False);
            Ready();
            var blocked = controller.PlaceAsync();
            yield return Finished(blocked);
            Assert.That(blocked.Result, Is.False);
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            controller.RemoveSucceeds = true;
            controller.ResetExperience();
            Ready();
            var retry = controller.PlaceAsync();
            Assert.That(controller.Requests.Count, Is.EqualTo(2));
            controller.Requests[1].SetResult(null);
            yield return Finished(retry);
        }

        [UnityTest]
        public IEnumerator DisabledControllerRejectsLateResult()
        {
            var result = controller.PlaceAsync();
            Assert.That(controller.Requests.Count, Is.EqualTo(1));
            controller.enabled = false;
            var anchor = Anchor();
            controller.Requests[0].SetResult(anchor);
            yield return Finished(result);
            Assert.That(result.Result, Is.False);
            Assert.That(controller.Removed, Is.EqualTo(new[] { anchor }));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator TurbineBindingKeepsRootAliveAndShowsParts()
        {
            var turbine = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Turbine.prefab");
            Assert.That(turbine, Is.Not.Null);
            typeof(ARExperienceController).GetField("contentPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, turbine);
            var result = controller.PlaceAsync();
            var anchor = Anchor();
            anchor.enabled = false;
            anchor.gameObject.SetActive(true);
            controller.Requests[0].SetResult(anchor);
            yield return Finished(result);
            Assert.That(result.Result, Is.True);
            var content = anchor.transform.GetChild(0);
            var visual = content.Find("ModelVisualRoot");
            controller.State.SetTracking(true, true, true);
            Assert.That(content.gameObject.activeSelf, Is.True);
            Assert.That(visual.gameObject.activeSelf, Is.True);
            var rotor = visual.Find("Kenney Windmill/blades");
            var initial = rotor.localRotation;
            yield return new WaitForSeconds(0.05f);
            Assert.That(Quaternion.Angle(initial, rotor.localRotation), Is.GreaterThan(0.1f),
                $"phase={controller.State.Phase}; canAnimate={controller.State.CanAnimate}; active={content.gameObject.activeInHierarchy}");
            controller.State.SetTab(LearningTab.Parts);
            controller.State.SelectPart(TurbinePart.Generator);
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Assert.That(visual.Find("Generator housing marker").gameObject.activeSelf, Is.True);
            initial = rotor.localRotation;
            yield return null;
            Assert.That(rotor.localRotation, Is.EqualTo(initial));
            controller.State.SetTracking(false, false, true);
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(anchor.gameObject.activeSelf, Is.True);
            Assert.That(content.gameObject.activeSelf, Is.True);
            controller.State.SetTracking(true, true, true);
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Assert.That(visual.Find("Generator housing marker").gameObject.activeSelf, Is.True);
            controller.ResetExperience();
            yield return null;
            Assert.That(anchor == null, Is.True);
        }
#endif

        [TearDown]
        public void TearDown()
        {
            if (controller != null) controller.RemoveSucceeds = true;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(prefab);
            foreach (var anchor in anchors) if (anchor != null) Object.DestroyImmediate(anchor);
            anchors.Clear();
        }
    }
}
