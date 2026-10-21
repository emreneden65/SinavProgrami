#nullable disable
namespace SinavProgrami.Models
{
    /// <summary>
    /// Gözetmen (Araştırma Görevlisi) modeli.
    /// Excel'den yüklenir; Ağırlık arayüzden düzenlenebilir.
    /// </summary>
    public class Gozetmen
    {
        /// <summary>Fakülte listesindeki kısa kod (B1, E4, MA2...). Basit formatta boş kalabilir.</summary>
        public string Kod { get; set; } = "";

        public string AdSoyad { get; set; }
        public string Bolum { get; set; }

        /// <summary>
        /// Görev payı: görev sayısı ağırlıkla orantılıdır
        /// (ağırlık 1,0 olan 14 sınava giriyorsa 0,5 olan 7 sınava girer).
        /// </summary>
        public double Agirlik { get; set; } = 1.0;

        public int AtananSinavSayisi { get; set; } = 0;

        /// <summary>Ağırlıklı eşit dağılım skoru — en düşük skorlu gözetmen önce seçilir.</summary>
        public double Skor => AtananSinavSayisi / (Agirlik <= 0 ? 1.0 : Agirlik);

        public override string ToString() => $"{AdSoyad} ({Bolum})";
    }
}
