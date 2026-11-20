using UnityEngine;
using UnityEngine.UI;

namespace WindEnergy
{
    public sealed class PlacementProbeUI : MonoBehaviour
    {
        [SerializeField] private ARExperienceController controller;
        [SerializeField] private RectTransform safeAreaRoot;
        [SerializeField] private Text status;
        [SerializeField] private Button placeButton;

        private void Update()
        {
            if (controller == null) return;
            var safe = Screen.safeArea;
            if (Screen.width > 0 && Screen.height > 0 && safeAreaRoot != null)
            {
                safeAreaRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                safeAreaRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                safeAreaRoot.offsetMin = safeAreaRoot.offsetMax = Vector2.zero;
            }
            placeButton.interactable = controller.State.CanPlace && !controller.CleanupBlocked;
            status.text = controller.CleanupBlocked ? "Anchor belum terhapus. Tekan Atur ulang untuk mencoba lagi." :
                controller.State.PlacementFailed ? "Penempatan gagal. Arahkan ke meja dan coba lagi." :
                controller.State.Phase switch
                {
                    ExperiencePhase.Ready => "Permukaan siap. Tempatkan turbin pada lingkaran.",
                    ExperiencePhase.Placing => "Menyiapkan anchor…",
                    ExperiencePhase.Wind => "Tingkat angin dan putaran hanya ilustrasi, bukan pengukuran nyata.",
                    ExperiencePhase.Parts => controller.State.Part switch
                    {
                        TurbinePart.Generator => "Penanda menunjuk rumah mesin, bukan generator internal yang terlihat.",
                        TurbinePart.Tower => "Menara menopang rotor dan rumah mesin.",
                        _ => "Rotor dan bilah menerima energi gerak angin. Animasi dijeda saat membaca."
                    },
                    ExperiencePhase.TrackingLost => "Tracking terganggu. Arahkan kamera kembali ke meja, atau atur ulang.",
                    ExperiencePhase.CameraDenied => "Izin kamera belum diberikan. Aktifkan Kamera di Pengaturan aplikasi.",
                    ExperiencePhase.Unsupported => "AR belum tersedia pada perangkat ini.",
                    ExperiencePhase.Initializing => "Memeriksa kamera dan kesiapan AR…",
                    _ => "Arahkan kamera ke meja bertekstur. Gerakkan ponsel perlahan."
                };
        }
    }
}
