#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using OfficeOpenXml;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// ADIM 1 — "Sınav Listesi" ham ders listesi okuyucusu.
    /// ──────────────────────────────────────────────────────────
    /// Fakülteden gelen düz ders listesini (her satır bir şube) okur ve
    /// TÜM dersleri çeker. Eski <see cref="ExcelImportHelper"/> aksine:
    ///   • İkinci Öğretim satırları ATLANMAZ (Adım 2'de aynı harf kolonuna toplanır).
    ///   • Kod numarası 0 ile başlayan dersler ATLANMAZ ("0 ile başlayanlar" bloğu vardır).
    ///   • Ders Kodu bilgisi modelde korunur (Sınıf ve blok grubu bundan türetilir).
    ///
    /// Beklenen sütunlar (başlık sırası değişebilir, esnek eşleştirilir):
    ///   Bölüm | Ünvanı Adı Soyadı | Öğretim Türü | DERSİN KODU |
    ///   VERİLEN DERSİN ADI | Dersin Şubesi | Öğrenci Sayısı
    /// </summary>
    public static class SinavListesiOkuyucu
    {
        public class Sonuc
        {
            /// <summary>Okunan tüm dersler (her şube ayrı bir Ders — Normal ve İÖ dahil).</summary>
            public List<Ders> Dersler { get; } = new List<Ders>();

            /// <summary>Atlanan satırlar / uyarılar.</summary>
            public List<string> Uyarilar { get; } = new List<string>();

            /// <summary>Okunan toplam ham satır sayısı (başlık hariç).</summary>
            public int OkunanSatir { get; set; }
        }

        /// <summary>Sınav Listesi Excel'ini okuyup tüm dersleri döndürür.</summary>
        public static Sonuc Oku(string dosyaYolu)
        {
            var sonuc = new Sonuc();

            using (var paket = new ExcelPackage(new System.IO.FileInfo(dosyaYolu)))
            {
                var ws = paket.Workbook.Worksheets.FirstOrDefault(s => s.Dimension != null);
                if (ws == null)
                    throw new Exception("Excel dosyasında dolu bir sayfa bulunamadı.");

                int satirSayisi = ws.Dimension.Rows;
                int sutunSayisi = ws.Dimension.Columns;

                // ── Başlık satırını bul (içinde 'kod' ve 'bolum' geçen satır) ──
                int baslikSatiri = -1;
                var map = new Dictionary<string, int>();
                for (int r = 1; r <= Math.Min(satirSayisi, 10); r++)
                {
                    var gecici = new Dictionary<string, int>();
                    for (int c = 1; c <= sutunSayisi; c++)
                    {
                        string h = Normalize(HucreMetni(ws, r, c));
                        if (h.Length == 0) continue;
                        Esle(gecici, h, c);
                    }
                    if (gecici.ContainsKey("dersinkodu") && gecici.ContainsKey("bolum"))
                    {
                        baslikSatiri = r;
                        map = gecici;
                        break;
                    }
                }

                if (baslikSatiri < 0)
                    throw new Exception(
                        "Başlık satırı bulunamadı. 'Bölüm' ve 'Dersin Kodu' sütunları beklenir.\n" +
                        "Bulunan ilk satır: " + string.Join(" | ", Enumerable.Range(1, sutunSayisi)
                            .Select(c => HucreMetni(ws, 1, c))));

                foreach (var gerekli in new[] { "bolum", "dersinkodu", "dersadi" })
                {
                    if (!map.ContainsKey(gerekli))
                        throw new Exception($"'{gerekli}' sütunu eşleştirilemedi. Bulunan başlıklar: " +
                            string.Join(" | ", Enumerable.Range(1, sutunSayisi).Select(c => HucreMetni(ws, baslikSatiri, c))));
                }

                for (int r = baslikSatiri + 1; r <= satirSayisi; r++)
                {
                    try
                    {
                        string bolumRaw = HucreMetni(ws, r, map["bolum"]);
                        string kod = HucreMetni(ws, r, map["dersinkodu"]);
                        string dersAdi = HucreMetni(ws, r, map["dersadi"]);

                        // Tamamen boş satırı sessizce atla
                        if (string.IsNullOrWhiteSpace(bolumRaw) && string.IsNullOrWhiteSpace(kod) &&
                            string.IsNullOrWhiteSpace(dersAdi))
                            continue;

                        sonuc.OkunanSatir++;

                        if (string.IsNullOrWhiteSpace(kod))
                        {
                            sonuc.Uyarilar.Add($"Satır {r}: Ders kodu boş — atlandı.");
                            continue;
                        }

                        // Sınıf seviyesi = kod numarasının ilk rakamı (0–9 kabul; 0 grubu geçerli)
                        int? ilkRakam = null;
                        foreach (char ch in kod)
                            if (char.IsDigit(ch)) { ilkRakam = ch - '0'; break; }

                        if (ilkRakam == null)
                        {
                            sonuc.Uyarilar.Add($"Satır {r}: Kod '{kod}' içinde rakam yok, sınıf/grup belirlenemedi — atlandı.");
                            continue;
                        }

                        string sube = map.ContainsKey("sube") ? HucreMetni(ws, r, map["sube"]) : "";
                        if (string.IsNullOrWhiteSpace(sube)) sube = "A";

                        int kontenjan = 0;
                        if (map.ContainsKey("kontenjan"))
                        {
                            string kStr = HucreMetni(ws, r, map["kontenjan"]);
                            string temiz = new string(kStr.Where(char.IsDigit).ToArray());
                            int.TryParse(temiz, out kontenjan);
                        }

                        string ogrTuru = map.ContainsKey("ogretimturu") ? HucreMetni(ws, r, map["ogretimturu"]) : "";
                        string ogrEleman = map.ContainsKey("ogretimelemani") ? HucreMetni(ws, r, map["ogretimelemani"]) : "";

                        sonuc.Dersler.Add(new Ders
                        {
                            BolumAdi = BolumNormalize(bolumRaw),
                            DersKodu = kod.Trim().ToUpperInvariant(),
                            DersAdi = dersAdi.Trim().ToUpperInvariant(),
                            Sube = SubeEtiketi.Normalize(sube),
                            Kontenjan = kontenjan,
                            SinifSeviyesi = ilkRakam.Value,
                            OgretimElemani = ogrEleman.Trim()
                        });
                    }
                    catch (Exception ex)
                    {
                        sonuc.Uyarilar.Add($"Satır {r}: Okuma hatası — {ex.Message}");
                    }
                }
            }

            return sonuc;
        }

        // ─────────────────────────────────────────────
        //  YARDIMCILAR
        // ─────────────────────────────────────────────

        private static string HucreMetni(ExcelWorksheet ws, int row, int col)
            => ws.Cells[row, col].Value?.ToString()?.Trim() ?? "";

        /// <summary>Başlığı boşluksuz, küçük harfli, Türkçe karakterleri sadeleştirilmiş yapar.</summary>
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var map = new Dictionary<char, char>
            {
                {'ı','i'},{'İ','i'},{'ş','s'},{'Ş','s'},{'ğ','g'},{'Ğ','g'},
                {'ç','c'},{'Ç','c'},{'ö','o'},{'Ö','o'},{'ü','u'},{'Ü','u'},
                {'â','a'},{'Â','a'},{'î','i'},{'Î','i'},{'û','u'},{'Û','u'}
            };
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToLowerInvariant())
            {
                char lc = char.ToLowerInvariant(c);
                if (map.TryGetValue(c, out char m)) sb.Append(m);
                else if (map.TryGetValue(lc, out char m2)) sb.Append(m2);
                else if (char.IsLetterOrDigit(lc)) sb.Append(lc);
            }
            return sb.ToString();
        }

        /// <summary>Normalize edilmiş başlığı bilinen alan anahtarına eşler.</summary>
        private static void Esle(Dictionary<string, int> map, string h, int col)
        {
            if (!map.ContainsKey("bolum") && (h.Contains("bolum") || h.Contains("program") || h.Contains("department")))
                map["bolum"] = col;
            else if (!map.ContainsKey("ogretimturu") && (h.Contains("ogretimturu") || h == "ogretim" || h.Contains("turu")))
                map["ogretimturu"] = col;
            else if (!map.ContainsKey("dersinkodu") && (h.Contains("dersinkodu") || h.Contains("derskodu") || h.Contains("dersinkod") || h == "kod" || h.Contains("code")))
                map["dersinkodu"] = col;
            else if (!map.ContainsKey("dersadi") && (h.Contains("dersinadi") || h.Contains("verilendersinadi") || h.Contains("dersadi") || h.Contains("course") || h.Contains("dersad")))
                map["dersadi"] = col;
            else if (!map.ContainsKey("sube") && (h.Contains("sube") || h.Contains("grup") || h.Contains("section") || h.Contains("branch")))
                map["sube"] = col;
            else if (!map.ContainsKey("kontenjan") && (h.Contains("ogrencisayisi") || h.Contains("kontenjan") || h.Contains("kapasite") || h.Contains("ogrenci") || h.Contains("sayisi") || h.Contains("count")))
                map["kontenjan"] = col;
            else if (!map.ContainsKey("ogretimelemani") && (h.Contains("adsoyad") || h.Contains("unvani") || h.Contains("ogretimelemani") || h.Contains("ogretimuyesi") || h.Contains("hoca") || h.Contains("sorumlu")))
                map["ogretimelemani"] = col;
        }

        /// <summary>Bölüm adını standart tam ada dönüştürür (bilinmeyen ise ham hali korunur).</summary>
        private static string BolumNormalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            string b = raw.ToUpperInvariant();
            if (b.Contains("BİLG") || b.Contains("BILG")) return "BİLGİSAYAR MÜHENDİSLİĞİ";
            if (b.Contains("ELEKTR")) return "ELEKTRİK VE ELEKTRONİK MÜHENDİSLİĞİ";
            if (b.Contains("İNŞA") || b.Contains("INSA")) return "İNŞAAT MÜHENDİSLİĞİ";
            if (b.Contains("MAKİN") || b.Contains("MAKIN") || b.Contains("MAKINA") || b.Contains("MAKİNA")) return "MAKİNE MÜHENDİSLİĞİ";
            if (b.Contains("MEKATRON")) return "MEKATRONİK MÜHENDİSLİĞİ";
            if (b.Contains("METALURJ") || b.Contains("MALZEME")) return "METALURJİ VE MALZEME MÜHENDİSLİĞİ";
            return raw.Trim();
        }
    }
}
