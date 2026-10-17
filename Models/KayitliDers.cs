#nullable disable
namespace SinavProgrami.Models
{
    /// <summary>
    /// KayitliDersler tablosuna karşılık gelen model sınıfı.
    /// Donem alanı DB uyumluluğu için korundu ancak UI'da ve filtrelerde kullanılmıyor.
    /// </summary>
    public class KayitliDers
    {
        public int DersID { get; set; }
        public string Donem { get; set; } = "";     // DB uyumu için, UI'da kullanılmıyor
        public string BolumAdi { get; set; }
        public int SinifSeviyesi { get; set; }
        public string DersAdi { get; set; }
        public string Sube { get; set; } = "A";
        public int Kontenjan { get; set; }

        public override string ToString()
        {
            return $"{DersAdi} — Şube {Sube} ({BolumAdi}, {SinifSeviyesi}. Sınıf, {Kontenjan} kişi)";
        }
    }
}
