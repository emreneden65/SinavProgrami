#nullable disable
using System;
using System.Text.RegularExpressions;

namespace SinavProgrami.Models
{
    /// <summary>
    /// Saat metinleri ("9:00", "09:00", "13:30") için ortak yardımcılar.
    /// Saatler string tutulduğundan düz metin sıralaması "13:00"ü "9:00"un önüne
    /// koyar ve Excel çıktılarında saat blokları yer değiştirir — sıralamada
    /// her zaman <see cref="Anahtar"/> kullanılmalıdır.
    /// </summary>
    public static class SaatYardimci
    {
        /// <summary>Kronolojik sıralama anahtarı; çözülemeyen saatler en sona gider.</summary>
        public static TimeSpan Anahtar(string saat)
            => TimeSpan.TryParse(saat?.Trim(), out var t) ? t : TimeSpan.MaxValue;

        /// <summary>
        /// Excel saat değeri (günün kesri, örn. 13:00 = 0.541666…) → "HH:mm".
        /// Kesir ikili kayan noktada tam temsil edilemediğinden TimeSpan.FromDays(0.541666…)
        /// 12:59:59.999 verir; dakikaları kesmek "12:59" üretiyordu. En yakın dakikaya yuvarlanır.
        /// </summary>
        public static string GunKesrindenSaat(double gunKesri)
        {
            int toplamDakika = (int)Math.Round(gunKesri * 24 * 60) % (24 * 60);
            return $"{toplamDakika / 60:00}:{toplamDakika % 60:00}";
        }

        /// <summary>Saat bileşeni taşıyan DateTime → en yakın dakikaya yuvarlanmış "HH:mm".</summary>
        public static string Saat(DateTime dt) => GunKesrindenSaat(dt.TimeOfDay.TotalDays);

        /// <summary>"9:00" / "09:00 " → "09:00". Saat bulunamazsa metin olduğu gibi (trim) döner.</summary>
        public static string Normalize(string saat)
        {
            string s = saat?.Trim() ?? "";
            var m = Regex.Match(s, @"(\d{1,2}):(\d{2})");
            return m.Success ? $"{int.Parse(m.Groups[1].Value):00}:{m.Groups[2].Value}" : s;
        }

        /// <summary>
        /// Excel çıktılarında saatin görünen biçimi: "13:00" → "13.00".
        /// (Uygulama içinde saatler "13:00" olarak tutulur; yalnızca Excel'e yazarken çevrilir.)
        /// </summary>
        public static string ExcelMetni(string saat) => Normalize(saat).Replace(':', '.');

        /// <summary>Excel'e gerçek saat değeri yazıldığında kullanılan biçim (13.00 görünür).</summary>
        public const string ExcelSaatBicimi = "hh\".\"mm;@";

        /// <summary>
        /// Geri yüklenen Excel hücresindeki saati "HH:mm"e çevirir; çözemezse null.
        /// Kabul: gerçek saat değeri (gün kesri / DateTime / TimeSpan), "13:00" ve "13.00" metni,
        /// ayrıca elle yazılan "13.00"ın Türkçe Excel'de dönüşebildiği sayılar (13 → 13:00, 13,3 → 13:30, 1300 → 13:00).
        /// </summary>
        public static string HucredenSaat(object v)
        {
            switch (v)
            {
                case DateTime dt: return Saat(dt);
                case TimeSpan ts: return GunKesrindenSaat(ts.TotalDays);
                case double d:
                    if (d >= 0 && d < 1) return GunKesrindenSaat(d);              // gerçek saat değeri
                    if (d > 40000) return GunKesrindenSaat(d - Math.Floor(d));     // tarih + saat
                    return SayidanSaat(d);
                case int i: return SayidanSaat(i);
            }

            string s = v?.ToString()?.Trim() ?? "";
            var m = Regex.Match(s, @"(\d{1,2}):(\d{2})");
            if (!m.Success) m = Regex.Match(s, @"^(\d{1,2})\.(\d{2})$");   // "13.00" — tarih metinleriyle karışmasın diye tam eşleşme
            if (!m.Success) return null;
            int saat = int.Parse(m.Groups[1].Value), dakika = int.Parse(m.Groups[2].Value);
            return saat < 24 && dakika < 60 ? $"{saat:00}:{dakika:00}" : null;
        }

        /// <summary>Hücreye elle yazılıp sayıya dönüşmüş saat: 13 / 13,3 (ondalık) veya 1300 (binlik ayırıcı).</summary>
        private static string SayidanSaat(double d)
        {
            int saat, dakika;
            if (d >= 1 && d < 24)
            {
                saat = (int)Math.Floor(d);
                dakika = (int)Math.Round((d - saat) * 100);
            }
            else if (d >= 100 && d < 2400 && d == Math.Floor(d))
            {
                saat = (int)d / 100;
                dakika = (int)d % 100;
            }
            else return null;
            return dakika < 60 ? $"{saat:00}:{dakika:00}" : null;
        }
    }
}
