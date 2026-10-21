#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SinavProgrami.Models
{
    /// <summary>Yarıyıl (dönem): Güz veya Bahar.</summary>
    public enum Yariyil
    {
        Guz = 0,
        Bahar = 1
    }

    /// <summary>
    /// Öğretim yılı + dönem (ör. 2026/2027 Güz) ve Excel başlık metinleri.
    /// Öğretim yılı başladığı takvim yılıyla tutulur: 2026 → "2026/2027".
    /// </summary>
    public static class AkademikDonem
    {
        /// <summary>Öğretim yılı Eylül'de başlar: Ekim 2026 → 2026 (2026/2027), Mart 2027 → 2026.</summary>
        public static int BuOgretimYili(DateTime bugun) => bugun.Month >= 9 ? bugun.Year : bugun.Year - 1;

        /// <summary>Seçim kutusundaki yıllar: geçen öğretim yılından iki yıl sonrasına kadar.</summary>
        public static List<int> YilSecenekleri(DateTime bugun)
        {
            int buYil = BuOgretimYili(bugun);
            return Enumerable.Range(buYil - 1, 4).ToList();
        }

        /// <summary>Uygulamada görünen biçim: "2026/2027".</summary>
        public static string YilGorunen(int baslangicYili) => $"{baslangicYili}/{baslangicYili + 1}";

        /// <summary>
        /// Elle yazılan öğretim yılını çözer; başlangıç yılını döner, geçersizse null.
        /// Kabul: "2031/2032", "2031-2032", "2031 / 2032" veya yalnız "2031".
        /// İkinci yıl birinciden tam 1 fazla olmalıdır.
        /// </summary>
        public static int? YilCoz(string metin)
        {
            var m = System.Text.RegularExpressions.Regex.Match(metin?.Trim() ?? "", @"^(\d{4})\s*(?:[/\-–]\s*(\d{4}))?$");
            if (!m.Success) return null;
            int baslangic = int.Parse(m.Groups[1].Value);
            if (baslangic < 2000 || baslangic > 2099) return null;
            if (m.Groups[2].Success && int.Parse(m.Groups[2].Value) != baslangic + 1) return null;
            return baslangic;
        }

        public static string YariyilAdi(Yariyil yariyil) => yariyil == Yariyil.Guz ? "Güz" : "Bahar";

        /// <summary>
        /// Excel çıktılarının program başlığı (resmi formdaki biçim):
        /// "2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI".
        /// </summary>
        public static string ProgramBasligi(int baslangicYili, Yariyil yariyil, SinavDonemTipi sinavTipi)
            => $"{baslangicYili}-{baslangicYili + 1} ÖĞRETİM YILI " +
               $"{(yariyil == Yariyil.Guz ? "GÜZ" : "BAHAR")} DÖNEMİ " +
               $"{SinavTipi.KisaAd(sinavTipi)} SINAV PROGRAMI";
    }
}
