namespace WindEnergy
{
    public static class ExperienceCopy
    {
        public const string PlacementFailureMessage = "Turbin belum bisa ditempatkan. Arahkan kamera ke meja dan coba lagi.";
        public const string Disclaimer = "Tingkat angin dan putaran rotor hanya ilustrasi, bukan pengukuran nyata.";
        public static readonly string[] WindNames = { "Tanpa angin", "Rendah", "Sedang", "Tinggi" };
        public static readonly string[] PartNames = { "Rotor", "Generator", "Menara" };
        public static readonly string[] PartTitles = { "Rotor dan bilah", "Generator", "Menara" };
        public static readonly string[] PartDescriptions = {
            "Angin membuat bilah berputar. Gerak rotor diteruskan menuju generator.",
            "Generator mengubah energi mekanis menjadi energi listrik. Letaknya di dalam rumah mesin di atas menara.",
            "Menara menopang rotor dan rumah mesin agar berada di atas permukaan tanah."
        };
        public static readonly string[] PartNotes = {
            "Ditandai pada model: rotor dan bilah.",
            "Ditandai: rumah mesin, bukan komponen di dalamnya.",
            "Ditandai pada model: struktur menara."
        };
    }
}
