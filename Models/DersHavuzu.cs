#nullable disable
using System;
using System.Collections.Generic;

namespace SinavProgrami.Models
{
    /// <summary>
    /// Havuzun tek bir üyesi: belirli bir bölümün belirli bir şubesi.
    /// Havuzlama artık şube seviyesinde yapılır (örn. sadece BİLGİSAYAR Şube A + İNŞAAT Şube B).
    /// </summary>
    public class HavuzUyesi
    {
        public string Bolum { get; set; }
        public string Sube { get; set; } = "A";
        public int SinifSeviyesi { get; set; }

        public override string ToString() => $"{Bolum} — {SinifSeviyesi}. Sınıf Şube {Sube}";
    }

    /// <summary>
    /// Ortak Dersleri Tek Salonda Birleştirme (Havuz) tanımı.
    /// Seçilen şubeler, salon ataması sırasında tek bir kontenjanmış gibi birleştirilir.
    /// </summary>
    public class DersHavuzu
    {
        public Guid HavuzId { get; set; } = Guid.NewGuid();
        public string DersAdi { get; set; }

        /// <summary>Havuzdaki bölümlerin özet listesi (geriye dönük uyumluluk + görüntüleme).</summary>
        public List<string> Bolumler { get; set; } = new List<string>();

        /// <summary>Şube seviyesinde havuz üyeleri. Doluysa eşleştirme bu liste üzerinden yapılır.</summary>
        public List<HavuzUyesi> Uyeler { get; set; } = new List<HavuzUyesi>();

        public override string ToString()
        {
            int uyeSayisi = (Uyeler != null && Uyeler.Count > 0) ? Uyeler.Count : (Bolumler?.Count ?? 0);
            return $"{DersAdi} — {uyeSayisi} üye";
        }
    }
}
