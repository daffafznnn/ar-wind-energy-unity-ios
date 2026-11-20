using System;

namespace WindEnergy
{
    public enum Availability { Checking, Ready, CameraDenied, Unsupported }
    public enum ExperiencePhase { Initializing, Scanning, Ready, Placing, Wind, Parts, TrackingLost, CameraDenied, Unsupported }

    public enum WindLevel { None = 0, Low = 1, Medium = 2, High = 3 }
    public enum LearningTab { Wind, Parts }
    public enum TurbinePart { Rotor, Generator, Tower }

    public sealed class ExperienceState
    {
        private Availability availability;
        private bool sessionTracking;
        private bool anchorTracking;
        private bool foreground = true;
        private bool candidate;
        private int sequence;
        private int pendingTicket;

        public event Action Changed;
        public WindLevel Wind { get; private set; } = WindLevel.Medium;
        public LearningTab Tab { get; private set; }
        public TurbinePart Part { get; private set; }
        public bool ManualPaused { get; private set; }
        public bool CanAnimate => Phase == ExperiencePhase.Wind && Wind != WindLevel.None && !ManualPaused;

        public void SetWind(WindLevel value)
        {
            if (!Enum.IsDefined(typeof(WindLevel), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (Wind == value) return;
            Wind = value;
            Changed?.Invoke();
        }

        public void SetTab(LearningTab value)
        {
            if (!Enum.IsDefined(typeof(LearningTab), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (Tab == value) return;
            Tab = value;
            Changed?.Invoke();
        }

        public void SelectPart(TurbinePart value)
        {
            if (!Enum.IsDefined(typeof(TurbinePart), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (Part == value) return;
            Part = value;
            Changed?.Invoke();
        }

        public void TogglePause()
        {
            if (Wind == WindLevel.None) return;
            ManualPaused = !ManualPaused;
            Changed?.Invoke();
        }
        public bool HasPlacement { get; private set; }
        public bool PlacementFailed { get; private set; }
        public bool CanPlace => SessionReady && candidate && !HasPlacement && pendingTicket == 0;
        private bool SessionReady => availability == Availability.Ready && sessionTracking && foreground;

        public ExperiencePhase Phase
        {
            get
            {
                if (availability == Availability.CameraDenied) return ExperiencePhase.CameraDenied;
                if (availability == Availability.Unsupported) return ExperiencePhase.Unsupported;
                if (availability == Availability.Checking) return ExperiencePhase.Initializing;
                if (HasPlacement)
                {
                    if (!SessionReady || !anchorTracking) return ExperiencePhase.TrackingLost;
                    return Tab == LearningTab.Parts ? ExperiencePhase.Parts : ExperiencePhase.Wind;
                }
                if (pendingTicket != 0) return ExperiencePhase.Placing;
                return CanPlace ? ExperiencePhase.Ready : ExperiencePhase.Scanning;
            }
        }

        public void SetAvailability(Availability value)
        {
            if (!Enum.IsDefined(typeof(Availability), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (availability == value) return;
            availability = value;
            if (value != Availability.Ready) InvalidatePending();
            Changed?.Invoke();
        }

        public void SetTracking(bool sessionTracking, bool anchorTracking, bool foreground)
        {
            if (this.sessionTracking == sessionTracking && this.anchorTracking == anchorTracking && this.foreground == foreground) return;
            this.sessionTracking = sessionTracking;
            this.anchorTracking = anchorTracking;
            this.foreground = foreground;
            if (!SessionReady) InvalidatePending();
            Changed?.Invoke();
        }

        public void SetCandidate(bool valid)
        {
            valid = valid && SessionReady && !HasPlacement;
            if (candidate == valid) return;
            candidate = valid;
            Changed?.Invoke();
        }

        public bool TryBeginPlacement(out int ticket)
        {
            ticket = 0;
            if (!CanPlace) return false;
            ticket = pendingTicket = checked(++sequence);
            PlacementFailed = false;
            Changed?.Invoke();
            return true;
        }

        public bool CompletePlacement(int ticket, bool success)
        {
            if (ticket == 0 || ticket != pendingTicket) return false;
            pendingTicket = 0;
            HasPlacement = success && SessionReady;
            PlacementFailed = !HasPlacement;
            if (HasPlacement) candidate = false;
            Changed?.Invoke();
            return HasPlacement;
        }

        public void Reset()
        {
            InvalidatePending();
            HasPlacement = false;
            PlacementFailed = false;
            anchorTracking = false;
            Wind = WindLevel.Medium;
            Tab = LearningTab.Wind;
            Part = TurbinePart.Rotor;
            ManualPaused = false;
            Changed?.Invoke();
        }

        private void InvalidatePending()
        {
            pendingTicket = 0;
            candidate = false;
        }
    }
}
