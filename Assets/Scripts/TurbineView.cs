using UnityEngine;

namespace WindEnergy
{
    public sealed class TurbineView : MonoBehaviour
    {
        [SerializeField] private Transform rotor;
        [SerializeField] private GameObject modelVisualRoot;
        [SerializeField] private Vector3 rotorAxis = Vector3.right;
        [SerializeField] private GameObject rotorMarker;
        [SerializeField] private GameObject generatorMarker;
        [SerializeField] private GameObject towerMarker;

        private Quaternion initialRotation;
        private float angle;
        private ExperienceState state;

        private void Awake() => initialRotation = rotor.localRotation;
        private void Update() => Advance(Time.deltaTime);

        public void Apply(ExperienceState state)
        {
            this.state = state;
            var visible = state.Phase == ExperiencePhase.Wind || state.Phase == ExperiencePhase.Parts;
            modelVisualRoot.SetActive(visible);
            var parts = visible && state.Tab == LearningTab.Parts;
            rotorMarker.SetActive(parts && state.Part == TurbinePart.Rotor);
            generatorMarker.SetActive(parts && state.Part == TurbinePart.Generator);
            towerMarker.SetActive(parts && state.Part == TurbinePart.Tower);
        }

        public void Advance(float deltaTime)
        {
            if (state == null || !state.CanAnimate || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            angle = Mathf.Repeat(angle + DegreesPerSecond(state.Wind) * deltaTime, 360);
            rotor.localRotation = initialRotation * Quaternion.AngleAxis(angle, rotorAxis);
        }

        public static float DegreesPerSecond(WindLevel level) => level switch
        {
            WindLevel.None => 0,
            WindLevel.Low => 30,
            WindLevel.Medium => 70,
            WindLevel.High => 120,
            _ => throw new System.ArgumentOutOfRangeException(nameof(level))
        };
    }
}
