using System;
using NUnit.Framework;

namespace WindEnergy.Tests.EditMode
{
    public class ExperienceStateTests
    {
        private static ExperienceState Ready()
        {
            var state = new ExperienceState();
            state.SetAvailability(Availability.Ready);
            state.SetTracking(true, false, true);
            state.SetCandidate(true);
            return state;
        }

        [Test]
        public void CandidateRequiresReadySession()
        {
            var state = new ExperienceState();
            state.SetCandidate(true);
            Assert.That(state.CanPlace, Is.False);
            state.SetAvailability(Availability.Ready);
            Assert.That(state.CanPlace, Is.False);
            state.SetTracking(true, false, true);
            state.SetCandidate(true);
            Assert.That(state.CanPlace, Is.True);
            state.SetTracking(false, false, true);
            Assert.That(state.CanPlace, Is.False);
        }

        [Test]
        public void DuplicateBeginIsRejected()
        {
            var state = Ready();
            Assert.That(state.TryBeginPlacement(out var ticket), Is.True);
            Assert.That(state.TryBeginPlacement(out _), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.Placing));
            Assert.That(state.CompletePlacement(ticket, true), Is.True);
            Assert.That(state.HasPlacement, Is.True);
            Assert.That(state.TryBeginPlacement(out _), Is.False);
        }

        [Test]
        public void ResetRejectsOldTicket()
        {
            var state = Ready();
            Assert.That(state.TryBeginPlacement(out var oldTicket), Is.True);
            state.Reset();
            Assert.That(state.CanPlace, Is.False, "reset discards the old candidate");
            state.SetCandidate(true);
            Assert.That(state.TryBeginPlacement(out var newTicket), Is.True);
            Assert.That(state.CompletePlacement(oldTicket, true), Is.False);
            Assert.That(state.HasPlacement, Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.Placing));
            Assert.That(state.CompletePlacement(newTicket, true), Is.True);
        }

        [Test]
        public void SuspensionRejectsPendingAndRequiresFreshCandidate()
        {
            var state = Ready();
            Assert.That(state.TryBeginPlacement(out var ticket), Is.True);
            state.SetTracking(true, false, false);
            Assert.That(state.CompletePlacement(ticket, true), Is.False);
            state.SetTracking(true, false, true);
            Assert.That(state.CanPlace, Is.False);
            state.SetCandidate(true);
            Assert.That(state.TryBeginPlacement(out _), Is.True);
        }

        [Test]
        public void FailureSurvivesRaycastRefreshUntilRetry()
        {
            var state = Ready();
            Assert.That(state.TryBeginPlacement(out var ticket), Is.True);
            Assert.That(state.CompletePlacement(ticket, false), Is.False);
            state.SetCandidate(false);
            state.SetCandidate(true);
            Assert.That(state.PlacementFailed, Is.True);
            Assert.That(state.TryBeginPlacement(out _), Is.True);
            Assert.That(state.PlacementFailed, Is.False);
        }

        [Test]
        public void AvailabilityChangeInvalidatesPendingAndRejectsBadEnum()
        {
            var state = Ready();
            Assert.That(state.TryBeginPlacement(out var ticket), Is.True);
            state.SetAvailability(Availability.CameraDenied);
            Assert.That(state.CompletePlacement(ticket, true), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.CameraDenied));
            state.Reset();
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.CameraDenied));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetAvailability((Availability)99));
        }

        [Test]
        public void TrackingLossPreservesExistingPlacementForRecovery()
        {
            var state = Ready();
            state.TryBeginPlacement(out var ticket);
            Assert.That(state.CompletePlacement(ticket, true), Is.True);
            state.SetTracking(true, true, true);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.Wind));
            state.SetTracking(false, false, true);
            Assert.That(state.HasPlacement, Is.True);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.TrackingLost));
            state.SetTracking(true, true, true);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.Wind));
        }

        private static ExperienceState Placed()
        {
            var state = Ready();
            Assert.That(state.TryBeginPlacement(out var ticket), Is.True);
            Assert.That(state.CompletePlacement(ticket, true), Is.True);
            state.SetTracking(true, true, true);
            return state;
        }

        [Test]
        public void DefaultLearningValues()
        {
            var state = new ExperienceState();
            Assert.That(state.Wind, Is.EqualTo(WindLevel.Medium));
            Assert.That(state.Tab, Is.EqualTo(LearningTab.Wind));
            Assert.That(state.Part, Is.EqualTo(TurbinePart.Rotor));
            Assert.That(state.ManualPaused, Is.False);
            Assert.That(state.CanAnimate, Is.False);
            Assert.That(Placed().CanAnimate, Is.True);
        }

        [Test]
        public void NoWindNeverAnimates()
        {
            var state = Placed();
            state.SetWind(WindLevel.None);
            Assert.That(state.CanAnimate, Is.False);
            state.TogglePause();
            Assert.That(state.ManualPaused, Is.False);
            state.SetWind(WindLevel.Low);
            state.TogglePause();
            state.SetWind(WindLevel.None);
            state.TogglePause();
            Assert.That(state.ManualPaused, Is.True);
            Assert.That(state.CanAnimate, Is.False);
            state.SetWind(WindLevel.High);
            Assert.That(state.CanAnimate, Is.False);
        }

        [Test]
        public void PartsPauseDoesNotEraseManualPause()
        {
            var state = Placed();
            state.SetTab(LearningTab.Parts);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.Parts));
            Assert.That(state.CanAnimate, Is.False);
            Assert.That(state.ManualPaused, Is.False);
            state.SetTab(LearningTab.Wind);
            Assert.That(state.CanAnimate, Is.True);
            state.TogglePause();
            state.SetTab(LearningTab.Parts);
            state.SelectPart(TurbinePart.Generator);
            state.SetWind(WindLevel.High);
            state.SetTab(LearningTab.Wind);
            Assert.That(state.CanAnimate, Is.False);
            Assert.That(state.ManualPaused, Is.True);
            Assert.That(state.Part, Is.EqualTo(TurbinePart.Generator));
            Assert.That(state.Wind, Is.EqualTo(WindLevel.High));
            state.TogglePause();
            Assert.That(state.CanAnimate, Is.True);
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidWindIsRejected(int value)
        {
            var state = Placed();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetWind((WindLevel)value));
            Assert.That(state.Wind, Is.EqualTo(WindLevel.Medium));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void InvalidTabIsRejected(int value)
        {
            var state = Placed();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetTab((LearningTab)value));
            Assert.That(state.Tab, Is.EqualTo(LearningTab.Wind));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void InvalidPartIsRejected(int value)
        {
            var state = Placed();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectPart((TurbinePart)value));
            Assert.That(state.Part, Is.EqualTo(TurbinePart.Rotor));
        }

        [Test]
        public void ResetRestoresLearningDefaultsWithoutChangingAvailability()
        {
            var state = Placed();
            state.SetWind(WindLevel.High);
            state.SetTab(LearningTab.Parts);
            state.SelectPart(TurbinePart.Tower);
            state.TogglePause();
            state.SetAvailability(Availability.CameraDenied);
            state.Reset();
            Assert.That(state.Wind, Is.EqualTo(WindLevel.Medium));
            Assert.That(state.Tab, Is.EqualTo(LearningTab.Wind));
            Assert.That(state.Part, Is.EqualTo(TurbinePart.Rotor));
            Assert.That(state.ManualPaused, Is.False);
            Assert.That(state.HasPlacement, Is.False);
            Assert.That(state.CanAnimate, Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExperiencePhase.CameraDenied));
        }

        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void AnimationRequiresSessionAnchorAndForeground(bool session, bool anchor, bool foreground)
        {
            var state = Placed();
            Assert.That(state.CanAnimate, Is.True);
            state.SetTracking(session, anchor, foreground);
            Assert.That(state.CanAnimate, Is.False);
            state.SetTracking(true, true, true);
            Assert.That(state.CanAnimate, Is.True);
            state.SetAvailability(Availability.CameraDenied);
            Assert.That(state.CanAnimate, Is.False);
        }

        [Test]
        public void LearningChangesNotifyOnceOnlyWhenValueChanges()
        {
            var state = Placed();
            var changes = 0;
            state.Changed += () => changes++;
            state.SetWind(WindLevel.Medium);
            state.SetTab(LearningTab.Wind);
            state.SelectPart(TurbinePart.Rotor);
            Assert.That(changes, Is.Zero);
            state.SetWind(WindLevel.High);
            state.SetTab(LearningTab.Parts);
            state.SelectPart(TurbinePart.Tower);
            state.TogglePause();
            Assert.That(changes, Is.EqualTo(4));
            state.SetWind(WindLevel.None);
            state.TogglePause();
            Assert.That(changes, Is.EqualTo(5));
        }

        [Test]
        public void ChangeEventDoesNotRepeatForUnchangedFrame()
        {
            var state = Ready();
            var changes = 0;
            state.Changed += () => changes++;
            state.SetCandidate(true);
            state.SetTracking(true, false, true);
            Assert.That(changes, Is.Zero);
            state.SetCandidate(false);
            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
