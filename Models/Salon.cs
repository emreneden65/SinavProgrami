#nullable disable
namespace SinavProgrami.Models
{
    /// <summary>
    /// Veritabanındaki Salonlar tablosuna karşılık gelen model sınıfı.
    /// </summary>
    public class Salon
    {
        public int ID { get; set; }
        public string SalonKodu { get; set; }
        public int Kapasite { get; set; }

        /// <summary>"NORMAL" veya "UYGULAMALI" (lab, teknik resim sınıfı vb.)</summary>
        public string SalonTipi { get; set; } = "NORMAL";

        /// <summary>Salon uygulamalı (lab/teknik resim) salonu mu?</summary>
        public bool UygulamaliMi =>
            string.Equals(SalonTipi?.Trim(), "UYGULAMALI", System.StringComparison.OrdinalIgnoreCase);

        public override string ToString()
        {
            return $"{SalonKodu} (Kapasite: {Kapasite}{(UygulamaliMi ? ", Uygulamalı" : "")})";
        }
    }
}
