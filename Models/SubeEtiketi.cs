#nullable disable
using System;
using System.Collections.Generic;

namespace SinavProgrami.Models
{
    /// <summary>
    /// ŞUBE ETİKETİ NORMALİZASYONU — tek merkez.
    /// ─────────────────────────────────────────────────────────────
    /// Sistemdeki 4. şube kolonu tarihsel olarak "INT" (International) etiketiyle
    /// tutuluyordu. Artık tüm okuma/yazma katmanlarında bu şube <b>"D"</b> olarak
    /// adlandırılır (A / B / C / D).
    ///
    /// Bu sınıf hem GERİYE DÖNÜK UYUMU sağlar (eski Excel dosyalarındaki "INT",
    /// "INT." , "int" değerleri okunurken otomatik "D"ye çevrilir) hem de
    /// Excel hücrelerinde SAYI olarak gelen şube değerlerini (1/2/3/4 → A/B/C/D)
    /// harf formatına dönüştürür.
    ///
    /// Kullanım:
    ///   SubeEtiketi.Normalize("INT")  → "D"
    ///   SubeEtiketi.Normalize("4")    → "D"
    ///   SubeEtiketi.Normalize(4.0)    → "D"
    ///   SubeEtiketi.Normalize("a1")   → "A"
    ///   SubeEtiketi.Normalize("")     → "A"   (varsayılan)
    /// </summary>
    public static class SubeEtiketi
    {
        /// <summary>Standart şube harfleri — kolon sırası bu diziyle aynıdır.</summary>
        public static readonly string[] Harfler = { "A", "B", "C", "D" };

        /// <summary>4. şubenin yeni adı. Eski adı "INT" idi.</summary>
        public const string DordunculSube = "D";

        /// <summary>Varsayılan şube (etiket okunamazsa).</summary>
        public const string Varsayilan = "A";

        /// <summary>
        /// Ham şube değerini (metin, int, double, boş) standart harfe indirger.
        /// Sayısal değerler 1→A, 2→B, 3→C, 4→D olarak eşlenir.
        /// Tanınmayan/boş değerler için <paramref name="varsayilan"/> döner.
        /// </summary>
        public static string Normalize(object ham, string varsayilan = Varsayilan)
        {
            if (ham == null) return varsayilan;

            // Excel hücresi sayı tipinde gelebilir (1, 2, 3, 4)
            if (ham is int i) return SayidanHarf(i, varsayilan);
            if (ham is double d) return SayidanHarf((int)Math.Round(d), varsayilan);
            if (ham is decimal m) return SayidanHarf((int)Math.Round(m), varsayilan);

            string s = ham.ToString();
            if (string.IsNullOrWhiteSpace(s)) return varsayilan;

            // Yalnızca harf/rakam bırak: "INT.", "Şb. A", "a-1" gibi yazımları kapsar
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            string t = sb.ToString();

            if (t.Length == 0) return varsayilan;

            // ── ESKİ ETİKET: INT / INTERNATIONAL → D ──
            if (t.StartsWith("INT")) return DordunculSube;
            if (t.StartsWith("ING") || t.StartsWith("ENG")) return DordunculSube; // İngilizce şube

            // ── HARF ETİKETİ ──
            if (t[0] == 'A') return "A";
            if (t[0] == 'B') return "B";
            if (t[0] == 'C') return "C";
            if (t[0] == 'D') return "D";

            // ── SAYISAL ETİKET: "1", "2", "3", "4" (metin olarak gelmiş) ──
            if (char.IsDigit(t[0]))
            {
                // Baştaki rakam grubunu al ("04" → 4, "4. şube" → 4)
                int j = 0, deger = 0;
                while (j < t.Length && char.IsDigit(t[j]))
                {
                    deger = deger * 10 + (t[j] - '0');
                    j++;
                    if (deger > 99) break;
                }
                return SayidanHarf(deger, varsayilan);
            }

            return varsayilan;
        }

        /// <summary>1→A, 2→B, 3→C, 4→D. Aralık dışı değerler için varsayılan döner.</summary>
        public static string SayidanHarf(int sayi, string varsayilan = Varsayilan)
        {
            if (sayi >= 1 && sayi <= Harfler.Length) return Harfler[sayi - 1];
            // 0 tabanlı yazılmış dosyalar için tolerans (0→A, 3→D)
            if (sayi == 0) return Harfler[0];
            return varsayilan;
        }

        /// <summary>Harf → kolon indeksi (A=0, B=1, C=2, D=3). Bulunamazsa -1.</summary>
        public static int HarfIndeksi(string harf)
        {
            string h = Normalize(harf, null);
            if (h == null) return -1;
            return Array.IndexOf(Harfler, h);
        }

        /// <summary>
        /// Bir metnin geçerli bir şube etiketi olup olmadığını söyler.
        /// ("A", "B", "C", "D" ve eski "INT" kabul edilir; "25", "danışman ofisi" reddedilir.)
        /// </summary>
        public static bool GecerliEtiketMi(string ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return false;
            string t = ham.Trim().ToUpperInvariant();
            return t == "A" || t == "B" || t == "C" || t == "D" || t == "INT";
        }

        /// <summary>Şube harflerini boş sayaçlarla hazır bir sözlük üretir (A/B/C/D → 0).</summary>
        public static Dictionary<string, int> BosSayaclar()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in Harfler) d[h] = 0;
            return d;
        }
    }
}
