# AR Energi Angin

Aplikasi AR edukasi konversi energi angin menjadi listrik untuk iPhone, dibangun dengan Unity, AR Foundation, dan ARKit. Project UTS IF-203 — Augmented & Virtual Reality.

## Fitur

- Mendeteksi permukaan horizontal dan menempatkan satu model turbin.
- Empat tingkat angin ilustratif dengan kontrol Jeda/Lanjutkan.
- Penjelasan alur energi dan pilihan bagian Rotor, Generator, serta Menara.
- Mode tampilan Ringkas, Detail, dan Fokus AR.
- Atur ulang penempatan dan pemulihan saat izin kamera atau tracking berubah.

Tingkat angin dan putaran rotor hanya ilustrasi, bukan pengukuran nyata. Penanda generator menunjuk rumah mesin, bukan komponen internalnya. Tidak ada backend.

## Toolchain

| Komponen | Versi / konfigurasi |
| --- | --- |
| Unity Editor | 6000.6.6f1 + iOS Build Support |
| AR Foundation / ARKit | 6.7.1 |
| Universal Render Pipeline | 17.6.0 |
| XR Management | 4.6.1 |
| Input System | 1.20.0 |
| uGUI | 2.6.0 |
| Test Framework | 1.8.0 |
| Target | iOS 15.0 minimum, ARM64, IL2CPP, Metal, portrait |

Versi package dikunci melalui `Packages/manifest.json` dan `Packages/packages-lock.json`.

## Membuka project

1. Clone repository ini dan tambahkan folder hasil clone ke Unity Hub.
2. Buka dengan Unity 6000.6.6f1 yang memiliki modul iOS Build Support. Import pertama akan membuat ulang `Library/` dan membutuhkan ruang disk serta waktu tambahan.
3. Buka scene `Assets/Scenes/WindEnergyAR.unity`.
4. Pilih iOS sebagai active build target melalui Build Profiles.

Scene utama memakai `Turbine.prefab` dan `ExperienceCanvas.prefab`. Helper setup P1/P2/P3/P4 adalah alat pengembangan historis; jangan menjalankannya ulang untuk membuka project final.

Game View dan automated tests tidak membuktikan kamera/tracking ARKit nyata. Gunakan iPhone yang mendukung ARKit untuk pengujian fisik.

## Build ke iPhone

1. Di Build Profiles, gunakan target iOS dan scene aktif `WindEnergyAR`.
2. Export melalui antarmuka Unity ke `Builds/iOS/` di dalam folder repository, atau folder output lain di luar repository.
3. Buka hasil export di Xcode, pilih signing team milik sendiri dan perangkat fisik, lalu Build/Run.
4. Selesaikan trust/Developer Mode jika diperlukan dan izinkan akses kamera di iPhone.

Sertifikat, provisioning profile, signing team, dan kredensial tidak disertakan. Bundle identifier bawaan adalah `com.daffafauzan.utsarenergiangin`; sesuaikan melalui Player Settings bila identitas aplikasi sendiri diperlukan.

Helper `WindEnergy.Editor.ProjectBuild.ExportIos` masih memakai layout workspace akademik (`../../builds/ios/` relatif terhadap project). Untuk clone standalone, gunakan Build Profiles agar lokasi output dipilih secara eksplisit.

## Pengujian dan batas verifikasi

- Pilih target iOS terlebih dahulu, lalu jalankan EditMode dan PlayMode melalui Window > General > Test Runner.
- Untuk batch tests, gunakan `-buildTarget iOS`, `-runTests`, `-testPlatform`, `-testResults`, dan `-logFile`; jangan menambahkan `-quit`. Periksa XML hasil, bukan hanya exit code.
- Hasil P6 yang tersimpan: 40/40 EditMode dan 69/69 PlayMode lulus. Salinan project juga lulus kedua suite setelah target iOS dipilih secara eksplisit; percobaan awal dengan target default StandaloneOSX gagal satu assertion konfigurasi.
- Source/config pada publikasi awal dibandingkan dengan hash sumber yang diuji pada P6. Tes Unity tidak dijalankan ulang untuk publikasi repository ini.
- Build P5 pernah terpasang dan diluncurkan di iPhone 13. Skenario reset/placement, izin kamera, tracking recovery, dan background/foreground diterima berdasarkan konfirmasi pengguna, bukan klaim pengamatan video baru.
- Signed build P6 berhasil, tetapi install perangkat timeout sehingga install/launch build P6 belum terverifikasi.
- Stabilitas tiga penempatan masing-masing 60 detik belum diuji. Kontak alas, retention pilihan/jeda, kenyamanan fisik, dan run acceptance terpadu belum seluruhnya ditutup.

Editor tests memakai input/provider terkontrol; hasilnya bukan pengukuran akurasi atau stabilitas ARKit fisik. Publikasi source tidak menandakan seluruh acceptance perangkat telah selesai.

## Isi repository

- `Assets/`: scene, prefab, scripts, tests, model, material, UI, native camera bridge, dan file `.meta`.
- `Packages/`: manifest dan lockfile dependency.
- `ProjectSettings/`: konfigurasi Unity.

Cache Unity, UserSettings, hasil build/export, rekaman video, laporan PDF/DOCX, paket submission, dan raw device logs tidak disertakan. `.gitignore` membatasi item root ke source project dan dokumentasi ini; tambahkan exception secara eksplisit jika kelak ada file root lain yang memang ingin dipublikasikan.

## Aset pihak ketiga

- Kenney City Kit Industrial 2.0: CC0, lihat `Assets/Art/Kenney/License.txt`.
- Liberation Sans: SIL Open Font License, lihat `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` dan `Assets/Resources/Licenses/LiberationSans-OFL.txt`.
- File bawaan Unity/TextMesh Pro mempertahankan pemberitahuan lisensinya masing-masing.

Lisensi aset pihak ketiga tidak otomatis menjadi lisensi untuk seluruh source project. Tidak ada lisensi open-source baru yang ditetapkan untuk kode project melalui publikasi ini.
