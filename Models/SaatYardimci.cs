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
    }
}
