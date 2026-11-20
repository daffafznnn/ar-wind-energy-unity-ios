using UnityEngine;
using UnityEngine.UI;

namespace WindEnergy
{
    public sealed class P3ProbeControls : MonoBehaviour
    {
        [SerializeField] private ARExperienceController controller;
        [SerializeField] private Button windButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button tabButton;
        [SerializeField] private Button partButton;

        private bool Ready => controller != null &&
            (controller.State.Phase == ExperiencePhase.Wind || controller.State.Phase == ExperiencePhase.Parts);

        private void OnEnable()
        {
            windButton.onClick.AddListener(CycleWind);
            pauseButton.onClick.AddListener(TogglePause);
            tabButton.onClick.AddListener(ToggleTab);
            partButton.onClick.AddListener(CyclePart);
            controller.State.Changed += Render;
            Render();
        }

        private void OnDisable()
        {
            windButton.onClick.RemoveListener(CycleWind);
            pauseButton.onClick.RemoveListener(TogglePause);
            tabButton.onClick.RemoveListener(ToggleTab);
            partButton.onClick.RemoveListener(CyclePart);
            controller.State.Changed -= Render;
        }

        private void CycleWind()
        {
            if (Ready && controller.State.Tab == LearningTab.Wind)
                controller.State.SetWind((WindLevel)(((int)controller.State.Wind + 1) % 4));
        }

        private void TogglePause()
        {
            if (Ready && controller.State.Tab == LearningTab.Wind) controller.State.TogglePause();
        }

        private void ToggleTab()
        {
            if (Ready) controller.State.SetTab(controller.State.Tab == LearningTab.Wind ? LearningTab.Parts : LearningTab.Wind);
        }

        private void CyclePart()
        {
            if (Ready && controller.State.Tab == LearningTab.Parts)
                controller.State.SelectPart((TurbinePart)(((int)controller.State.Part + 1) % 3));
        }

        public void Render()
        {
            var state = controller.State;
            var wind = state.Tab == LearningTab.Wind;
            windButton.interactable = Ready && wind;
            pauseButton.interactable = Ready && wind && state.Wind != WindLevel.None;
            tabButton.interactable = Ready;
            partButton.interactable = Ready && !wind;
            var windLabel = state.Wind switch
            {
                WindLevel.None => "Tanpa angin",
                WindLevel.Low => "Rendah",
                WindLevel.Medium => "Sedang",
                _ => "Tinggi"
            };
            windButton.GetComponentInChildren<Text>().text = "Angin: " + windLabel;
            pauseButton.GetComponentInChildren<Text>().text = state.ManualPaused ? "Lanjutkan" : "Jeda";
            tabButton.GetComponentInChildren<Text>().text = wind ? "Buka Bagian" : "Kembali ke Angin";
            partButton.GetComponentInChildren<Text>().text = "Bagian: " + (state.Part == TurbinePart.Tower ? "Menara" : state.Part.ToString());
        }
    }
}
