#nullable disable
using System;
using System.Collections.Generic;

namespace SinavProgrami.Models
{
    /// <summary>
    /// Bir salona atanan öğrenci grubunun bilgisini tutar.
    /// </summary>
    public class SalonAtama
    {
        public string SalonKodu { get; set; }
        public int SalonKapasitesi { get; set; }
        public int AtananOgrenciSayisi { get; set; }

        /// <summary>
        /// Bu salona atanan ŞUBE etiketi (örn. "A", "B", "A+B" havuzda).
        /// Şeffaflık kuralı: salon planında hangi şubenin hangi salonda olduğu görünür.
        /// </summary>
        public string Sube { get; set; } = "";

        /// <summary>Bu salona atanan gözetmenin adı (her salon için 1 gözetmen).</summary>
        public string GozetmenAdSoyad { get; set; } = "";

        public override string ToString()
        {
            string subeEk = string.IsNullOrWhiteSpace(Sube) ? "" : $" Şb.{Sube}";
            string temel = $"{SalonKodu}({AtananOgrenciSayisi}/{SalonKapasitesi}{subeEk})";
            return string.IsNullOrWhiteSpace(GozetmenAdSoyad) ? temel : $"{temel} [{GozetmenAdSoyad}]";
        }
    }

    /// <summary>
    /// Algoritma çıktısı: Bir dersin hangi gün, hangi saat, hangi salonlara atandığını tutar.
    /// </summary>
    public class SinavAtama
    {
        public string DersAdi { get; set; }
        public string BolumAdi { get; set; }
        public int SinifSeviyesi { get; set; }
        public int Kontenjan { get; set; }
        public string Sube { get; set; } = "A";
        public string OgretimElemani { get; set; } = "";
        public DateTime Gun { get; set; }
        public string Saat { get; set; }
        public List<SalonAtama> AtananSalonlar { get; set; } = new List<SalonAtama>();

        /// <summary>
        /// Excel çıktılarında kullanılan ders etiketi: "DERS ADI (ŞUBE)".
        /// Şube bilgisi yoksa boş parantez yazılmaz — yalnızca ders adı döner.
        /// Birden çok şube tek satırda birleşmişse "DERS ADI (A+B)" biçiminde gelir.
        /// </summary>
        public string DersEtiketi
            => string.IsNullOrWhiteSpace(Sube) ? (DersAdi ?? "") : $"{DersAdi} ({Sube.Trim()})";

        /// <summary>
        /// Atanan salonları okunabilir formatta döndürür.
        /// Örn: "D101(60/60), D102(40/50)"
        /// </summary>
        public string AtananSalonlarText
        {
            get
            {
                return string.Join(", ", AtananSalonlar);
            }
        }
    }
}
