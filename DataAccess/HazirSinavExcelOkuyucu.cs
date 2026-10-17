#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// "Hazır Sınav Dağıtım Excel" (BAHAR FİNAL sayfası) okuyucusu.
    ///
    /// Bu sınıf, sınavların günlere ve saatlere HAZIR dağıtılmış halini Excel'den okur.
    /// Eski sistemdeki Sınav Dağıtım Algoritması / Ortak Ders Havuzu / Şablon-Saat üretimi
    /// KALDIRILDIĞI için, takvim (gün+saat) ve ders yerleşimi artık DOĞRUDAN buradan gelir.
    ///
    /// Sayfa yapısı (BAHAR FİNAL):
    ///  • Sayfa yatay "bantlara" bölünmüştür. Her bandın ilk satırında (tarih satırı)
    ///    gün blokları için TARİH hücreleri bulunur (birleştirilmiş). Örn. band1 satır 2,
    ///    band2 satır 35.
    ///  • Her gün bloğu 7 sütundur: [Bölüm | Sınıf | Ders Adı | A | B | C | D].
    ///    (4. şube kolonu eski dosyalarda "INT" başlığını taşır; sistemde "D şubesi" olarak işlenir.)
    ///  • Tarih satırının bir altı = başlık satırı; onun altındaki satırlar veri satırlarıdır.
    ///  • Sol taraftaki A sütununda SAAT bilgisi bulunur (dikey birleştirilmiş, ileri-doldurulur).
    ///  • A/B/C/D sütunları şube kontenjanlarıdır ("87", "a-140", "a1-92" gibi değerler
    ///    olabilir → içindeki son sayı kontenjan olarak alınır). Sayı içermeyen değerler
    ///    ("danışman ofisi", "sınıf yaz gözetmen yazmaya gerek yok" vb.) not olarak işlenir.
    /// </summary>
    public static class HazirSinavExcelOkuyucu
    {
        /// <summary>Okuma sonucu — takvim, şablon (yerleşim), ders listesi ve uyarılar.</summary>
        public class Sonuc
        {
            /// <summary>Gün → o günün saatleri (takvim). Eskiden takvim panelinden gelirdi.</summary>
            public Dictionary<DateTime, List<string>> Takvim { get; } = new Dictionary<DateTime, List<string>>();

            /// <summary>Salonsuz yerleşim: her şube ayrı bir SinavAtama (Gün+Saat dolu, salon boş).</summary>
            public List<SinavAtama> Sablon { get; } = new List<SinavAtama>();

            /// <summary>Manuel/uygulamalı ders gridleri için ders listesi (her şube ayrı Ders).</summary>
            public List<Ders> Dersler { get; } = new List<Ders>();

            /// <summary>Atlanan/dikkat çeken satırlar (ders yok, salon gerekmiyor notu vb.).</summary>
            public List<string> Uyarilar { get; } = new List<string>();
        }

        /// <summary>Gün bloklarının 7'şer sütunluk standart başlangıç kolonları (B, I, P, W, AD).</summary>
        private static readonly int[] BlokBaslangicKolonlari = { 2, 9, 16, 23, 30 };

        private const string VarsayilanSayfa = "BAHAR FİNAL";

        /// <summary>
        /// Excel dosyasındaki BAHAR FİNAL sayfasını okur ve yapılandırılmış sonucu döndürür.
        /// </summary>
        public static Sonuc Oku(string dosyaYolu, string sayfaAdi = VarsayilanSayfa)
        {
            var sonuc = new Sonuc();
            using var paket = new ExcelPackage(new FileInfo(dosyaYolu));

            var ws = BulSayfa(paket, sayfaAdi);
            if (ws?.Dimension == null)
            {
                sonuc.Uyarilar.Add($"'{sayfaAdi}' sayfası bulunamadı veya boş.");
                return sonuc;
            }

            int sonSatir = ws.Dimension.End.Row;
            int sonKolon = ws.Dimension.End.Column;

            // ── 1) Yatay bantları bul: en az 2 blok-başlangıç kolonunda TARİH içeren satırlar ──
            var tarihSatirlari = new List<int>();
            for (int r = 1; r <= sonSatir; r++)
            {
                int tarihSayisi = BlokBaslangicKolonlari.Count(c => c <= sonKolon && HucreTarihMi(ws, r, c));
                if (tarihSayisi >= 2) tarihSatirlari.Add(r);
            }

            if (tarihSatirlari.Count == 0)
            {
                sonuc.Uyarilar.Add("Tarih başlığı içeren bant bulunamadı — sayfa formatı beklenenden farklı.");
                return sonuc;
            }

            // ── 2) Her bandı ayrı işle ──
            for (int bi = 0; bi < tarihSatirlari.Count; bi++)
            {
                int tarihSatiri = tarihSatirlari[bi];
                int veriBaslangic = tarihSatiri + 2; // tarih satırı + başlık satırı sonrası
                int veriBitis = (bi + 1 < tarihSatirlari.Count) ? tarihSatirlari[bi + 1] - 1 : sonSatir;

                // Bu bandın gün blokları: tarih satırında tarih içeren kolonlar
                var gunBloklari = new List<(int Kolon, DateTime Tarih)>();
                foreach (int c in BlokBaslangicKolonlari)
                {
                    if (c > sonKolon) continue;
                    var t = HucreTarih(ws, tarihSatiri, c);
                    if (t.HasValue) gunBloklari.Add((c, t.Value.Date));
                }

                BantiIsle(ws, veriBaslangic, veriBitis, gunBloklari, sonuc);
            }

            // ── 3) Takvimdeki saatleri sırala ──
            foreach (var gun in sonuc.Takvim.Keys.ToList())
                sonuc.Takvim[gun] = sonuc.Takvim[gun].Distinct().OrderBy(s => s).ToList();

            return sonuc;
        }

        private static void BantiIsle(ExcelWorksheet ws, int r0, int r1,
                                      List<(int Kolon, DateTime Tarih)> gunBloklari, Sonuc sonuc)
        {
            string aktifSaat = null;
            for (int r = r0; r <= r1; r++)
            {
                // Saat sütunu (A) — ileri-doldurulur (dikey birleşik hücreler)
                var saatHam = ws.Cells[r, 1].Value;
                if (saatHam != null && !string.IsNullOrWhiteSpace(saatHam.ToString()))
                {
                    string s = SaatFormatla(saatHam);
                    if (!string.IsNullOrEmpty(s)) aktifSaat = s;
                }
                if (string.IsNullOrEmpty(aktifSaat)) continue;

                foreach (var (kolon, tarih) in gunBloklari)
                {
                    var dersHam = ws.Cells[r, kolon + 2].Value;
                    string dersAdi = TemizleDersAdi(dersHam);
                    if (string.IsNullOrWhiteSpace(dersAdi)) continue; // ders yoksa sınav yok

                    string bolumHam = ws.Cells[r, kolon].Value?.ToString();
                    string bolum = BolumEsle(bolumHam);
                    int sinif = SinifCoz(ws.Cells[r, kolon + 1].Value);

                    // A / B / C / D şube kontenjanları
                    // (4. kolon eski dosyalarda "INT" başlığıyla yazılıdır — artık "D" şubesi.)
                    var subeler = new List<(string Sube, int Kontenjan)>();
                    string[] subeAdlari = SubeEtiketi.Harfler;
                    var salonGerekmezNotu = new List<string>();
                    for (int i = 0; i < 4; i++)
                    {
                        var ham = ws.Cells[r, kolon + 3 + i].Value;
                        if (ham == null || string.IsNullOrWhiteSpace(ham.ToString())) continue;
                        int? kont = SayiCoz(ham);
                        if (kont.HasValue)
                            subeler.Add((subeAdlari[i], kont.Value));
                        else
                            salonGerekmezNotu.Add(ham.ToString().Trim());
                    }

                    // Hiç sayısal kontenjan yoksa: dersi yine de yerleştir (salon gerekmeyebilir)
                    if (subeler.Count == 0)
                    {
                        subeler.Add(("A", 0));
                        string not = salonGerekmezNotu.Count > 0 ? string.Join("; ", salonGerekmezNotu) : "kontenjan girilmemiş";
                        sonuc.Uyarilar.Add($"{tarih:dd.MM.yyyy} {aktifSaat} — {bolum} / {dersAdi}: {not} (salon atanmayabilir).");
                    }

                    // Takvime gün+saat ekle
                    if (!sonuc.Takvim.TryGetValue(tarih, out var saatler))
                    {
                        saatler = new List<string>();
                        sonuc.Takvim[tarih] = saatler;
                    }
                    if (!saatler.Contains(aktifSaat)) saatler.Add(aktifSaat);

                    // Her şube için SinavAtama (şablon) + Ders (liste) üret
                    foreach (var (sube, kont) in subeler)
                    {
                        sonuc.Sablon.Add(new SinavAtama
                        {
                            DersAdi = dersAdi,
                            BolumAdi = bolum,
                            SinifSeviyesi = sinif,
                            Sube = sube,
                            Kontenjan = kont,
                            OgretimElemani = "",
                            Gun = tarih,
                            Saat = aktifSaat
                        });

                        sonuc.Dersler.Add(new Ders
                        {
                            DersAdi = dersAdi,
                            BolumAdi = bolum,
                            SinifSeviyesi = sinif,
                            Sube = sube,
                            Kontenjan = kont
                        });
                    }
                }
            }
        }

        // ══════════════════════════ YARDIMCILAR ══════════════════════════

        private static ExcelWorksheet BulSayfa(ExcelPackage paket, string sayfaAdi)
        {
            // Önce birebir ad, sonra büyük/küçük harf duyarsız, sonra "içeren"
            return paket.Workbook.Worksheets.FirstOrDefault(w => w.Name == sayfaAdi)
                ?? paket.Workbook.Worksheets.FirstOrDefault(w =>
                       string.Equals(w.Name?.Trim(), sayfaAdi, StringComparison.OrdinalIgnoreCase))
                ?? paket.Workbook.Worksheets.FirstOrDefault(w =>
                       (w.Name ?? "").ToUpperInvariant().Contains("FİNAL")
                       || (w.Name ?? "").ToUpperInvariant().Contains("FINAL"));
        }

        private static bool HucreTarihMi(ExcelWorksheet ws, int r, int c) => HucreTarih(ws, r, c).HasValue;

        /// <summary>Hücreyi DateTime olarak çözer (DateTime, OADate double veya metin destekli).</summary>
        private static DateTime? HucreTarih(ExcelWorksheet ws, int r, int c)
        {
            var v = ws.Cells[r, c].Value;
            if (v is DateTime dt) return dt;
            if (v is double d)
            {
                // Yalnızca makul tarih aralığındaki (saat kesri değil) OADate değerleri
                if (d > 40000 && d < 80000)
                {
                    try { return DateTime.FromOADate(d); } catch { return null; }
                }
                return null;
            }
            if (v is string s && DateTime.TryParse(s, new CultureInfo("tr-TR"), DateTimeStyles.None, out var p)
                && p.Year > 2000)
                return p;
            return null;
        }

        /// <summary>Saat hücresini "HH:mm" biçimine çevirir (DateTime, TimeSpan, kesir veya metin).</summary>
        private static string SaatFormatla(object v)
        {
            switch (v)
            {
                case DateTime dt:
                    return SaatYardimci.Saat(dt);
                case TimeSpan ts:
                    return SaatYardimci.GunKesrindenSaat(ts.TotalDays);
                case double d:
                    if (d >= 0 && d < 1)
                        return SaatYardimci.GunKesrindenSaat(d);   // 13:00 → 12:59 kesme hatası yok
                    if (d > 40000) // OADate → saat kısmı
                        return SaatYardimci.GunKesrindenSaat(d - Math.Floor(d));
                    return null;
            }
            string s = v?.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return null;
            var m = Regex.Match(s, @"(\d{1,2}):(\d{2})");
            if (m.Success)
                return $"{int.Parse(m.Groups[1].Value):00}:{m.Groups[2].Value}";
            return null;
        }

        /// <summary>Kontenjan hücresinden tam sayı çıkarır ("a-140" → 140, "a1-92" → 92, "87" → 87).</summary>
        private static int? SayiCoz(object v)
        {
            if (v == null) return null;
            if (v is int iv) return iv;
            if (v is double dv) return (int)Math.Round(dv);
            string s = v.ToString();
            var eslesmeler = Regex.Matches(s, @"\d+");
            if (eslesmeler.Count == 0) return null;
            // Son sayı grubunu al (şube etiketi + sayı biçimlerini kapsar)
            return int.Parse(eslesmeler[eslesmeler.Count - 1].Value);
        }

        /// <summary>Sınıf hücresini int'e çevirir; çözülemezse 0 (ortak ders / seviyesiz).</summary>
        private static int SinifCoz(object v)
        {
            if (v == null) return 0;
            if (v is int i) return i;
            if (v is double d) return (int)Math.Round(d);
            var m = Regex.Match(v.ToString(), @"\d+");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        /// <summary>Çok satırlı/fazla boşluklu ders adını tek satıra indirger.</summary>
        private static string TemizleDersAdi(object v)
        {
            if (v == null) return null;
            string s = v.ToString().Replace("\r", " ").Replace("\n", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        /// <summary>Excel kısa bölüm kodunu (BİL/EEM/INS/MAK/MEK/MEM) sistemdeki tam ada eşler.</summary>
        private static string BolumEsle(string ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return "ÜNİVERSİTE ORTAK";
            string b = ham.Trim().ToUpperInvariant();

            // Zaman/tarih gibi hatalı hücreler veya ortak ifadeler
            if (Regex.IsMatch(b, @"^\d{1,2}[:.]\d{2}")) return "ÜNİVERSİTE ORTAK";
            if (b.Contains("TÜM BÖLÜM") || b.Contains("TUM BOLUM")) return "ORTAK DERSLER";
            if (b.Contains("ORTAK")) return "ÜNİVERSİTE ORTAK";

            // Kısa kod eşlemesi
            if (b.Contains("BİL") || b.Contains("BIL")) return "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ";
            if (b.Contains("EEM") || b.Contains("ELEKTR")) return "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ";
            if (b.Contains("İNŞ") || b.Contains("INS") || b.Contains("İNS")) return "İNŞAAT MÜHENDİSLİĞİ BÖLÜMÜ";
            if (b.Contains("MEK") || b.Contains("MEKATRON")) return "MEKATRONİK MÜHENDİSLİĞİ BÖLÜMÜ";
            if (b.Contains("MEM") || b.Contains("METALURJ")) return "METALURJİ VE MALZEME MÜHENDİSLİĞİ BÖLÜMÜ";
            if (b.Contains("MAK") || b.Contains("MAKİN") || b.Contains("MAKIN")) return "MAKİNE MÜHENDİSLİĞİ BÖLÜMÜ";

            return ham.Trim();
        }
    }
}
