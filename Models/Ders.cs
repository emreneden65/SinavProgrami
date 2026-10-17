#nullable disable
namespace SinavProgrami.Models
{
    /// <summary>
    /// Algoritmaya giren ders bilgilerini tutan model sınıfı.
    /// Dönem alanı kaldırıldı — artık dönem filtreleme yapılmıyor.
    /// </summary>
    public class Ders
    {
        public int DersID { get; set; }
        public string BolumAdi { get; set; }
        public int SinifSeviyesi { get; set; }
        public string DersAdi { get; set; }
        public string Sube { get; set; } = "A";
        public int Kontenjan { get; set; }
        public string OgretimElemani { get; set; } = "";

        /// <summary>
        /// Dersin kodu (örn. "BIL214"). Sınav Listesi'nden çekilir.
        /// Sınıf seviyesi ve "Çekilmiş Dersler" blok grubu (kod numarasının ilk rakamı)
        /// bu alandan türetilir. Örn. BIL214 → 2. sınıf / "2 ile başlayanlar" bloğu.
        /// </summary>
        public string DersKodu { get; set; } = "";

        public override string ToString()
        {
            return $"{DersAdi} — Şube {Sube} ({BolumAdi} - {SinifSeviyesi}. Sınıf, {Kontenjan} öğrenci)";
        }
    }
}
