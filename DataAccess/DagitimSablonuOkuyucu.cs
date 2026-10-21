#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// ADIM 3 — Kullanıcının manuel doldurduğu "Sınav Haftası Dağıtım Şablonu" okuyucusu.
    /// ──────────────────────────────────────────────────────────
    /// Adım 2'de üretilen şablonun "Sınav Haftası Dağıtım Şablonu" sayfası, kullanıcı
    /// tarafından masaüstünde elle doldurulup geri yüklenir. Bu sınıf o sayfayı okuyup
    /// salonsuz yerleşimi (takvim + şube atamaları) çıkarır. Salon ataması Adım 4'te
    /// uygulamanın kendi salon havuzuyla yapılır.
    ///
    /// Sayfa düzeni:
    ///  • Gün blokları 8 sütundur: [Bölüm | Sınıf | Saat | Ders Adı | A | B | C | D].
    ///    (4. şube kolonu eski dosyalarda "INT" başlıklıdır; sistemde "D şubesi" olarak işlenir.)
    ///  • Bloklar 1., 9., 17. ... sütunlarda başlar. Her bloğun TARİH satırında (varsayılan
    ///    satır 2) birleştirilmiş tarih hücresi bulunur.
    ///  • Tarih satırının bir altı = kolon başlığı; onun altı = veri satırları.
    ///  • Saat her bloğun kendi 3. sütununda tutulur (dikey birleşikse ileri-doldurulur).
    ///  • Ders Adı boşsa o satırda sınav yoktur.
    /// </summary>
    public static class DagitimSablonuOkuyucu
    {
        public class Sonuc
        {
            public Dictionary<DateTime, List<string>> Takvim { get; } = new Dictionary<DateTime, List<string>>();
            public List<SinavAtama> Sablon { get; } = new List<SinavAtama>();
            public List<Ders> Dersler { get; } = new List<Ders>();
            public List<string> Uyarilar { get; } = new List<string>();
        }

        private const int BlokGenisligi = 8;
        private const string VarsayilanSayfa = "Sınav Haftası Dağıtım Şablonu";

        /// <summary>Doldurulmuş dağıtım şablonunu okur.</summary>
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

            // Olası blok başlangıç kolonları: 1, 9, 17, ...
            var blokKolonlari = new List<int>();
            for (int c = 1; c <= sonKolon; c += BlokGenisligi) blokKolonlari.Add(c);

            // ── Tarih (bant) satırlarını bul: ≥1 blok kolonunda tarih içeren satırlar ──
            var tarihSatirlari = new List<int>();
            for (int r = 1; r <= sonSatir; r++)
            {
                int tarihSayisi = blokKolonlari.Count(c => HucreTarih(ws, r, c).HasValue);
                if (tarihSayisi >= 1) tarihSatirlari.Add(r);
            }

            if (tarihSatirlari.Count == 0)
            {
                sonuc.Uyarilar.Add("Tarih başlığı içeren satır bulunamadı — sayfa formatı beklenenden farklı.");
                return sonuc;
            }

            for (int bi = 0; bi < tarihSatirlari.Count; bi++)
            {
                int tarihSatiri = tarihSatirlari[bi];
                int veriBaslangic = tarihSatiri + 2; // tarih + kolon başlığı sonrası
                int veriBitis = (bi + 1 < tarihSatirlari.Count) ? tarihSatirlari[bi + 1] - 1 : sonSatir;

                var gunBloklari = new List<(int Kolon, DateTime Tarih)>();
                foreach (int c in blokKolonlari)
                {
                    var t = HucreTarih(ws, tarihSatiri, c);
                    if (t.HasValue) gunBloklari.Add((c, t.Value.Date));
                }

                BantiIsle(ws, veriBaslangic, veriBitis, gunBloklari, sonuc);
            }

            foreach (var gun in sonuc.Takvim.Keys.ToList())
                sonuc.Takvim[gun] = sonuc.Takvim[gun].Distinct().OrderBy(s => s).ToList();

            return sonuc;
        }

        private static void BantiIsle(ExcelWorksheet ws, int r0, int r1,
                                      List<(int Kolon, DateTime Tarih)> gunBloklari, Sonuc sonuc)
        {
            // Her blok kendi saatini taşır → blok bazlı ileri-doldurma
            var aktifSaat = new Dictionary<int, string>();

            for (int r = r0; r <= r1; r++)
            {
                foreach (var (kolon, tarih) in gunBloklari)
                {
                    // Saat (blok içi 3. sütun) — ileri-doldurulur
                    var saatHam = ws.Cells[r, kolon + 2].Value;
                    if (saatHam != null && !string.IsNullOrWhiteSpace(saatHam.ToString()))
                    {
                        string s = SaatFormatla(saatHam);
                        if (!string.IsNullOrEmpty(s)) aktifSaat[kolon] = s;
                    }

                    string dersAdi = TemizleDersAdi(ws.Cells[r, kolon + 3].Value);
                    if (string.IsNullOrWhiteSpace(dersAdi)) continue; // ders yoksa sınav yok
                    if (OrnekSatirMi(dersAdi)) continue;              // şablondaki "(örn)" rehber satırı — atla

                    aktifSaat.TryGetValue(kolon, out string saat);
                    if (string.IsNullOrEmpty(saat))
                    {
                        sonuc.Uyarilar.Add($"{tarih:dd.MM.yyyy} — '{dersAdi}': saat girilmemiş, satır atlandı.");
                        continue;
                    }

                    string bolum = BolumEsle(ws.Cells[r, kolon].Value?.ToString());
                    int sinif = SinifCoz(ws.Cells[r, kolon + 1].Value);

                    // A / B / C / D şube kontenjanları
                    // (4. kolon eski şablonlarda "INT" başlığıyla gelir — "D şubesi" olarak okunur.)
                    var subeler = new List<(string Sube, int Kontenjan)>();
                    string[] subeAdlari = SubeEtiketi.Harfler;
                    var notlar = new List<string>();
                    for (int i = 0; i < 4; i++)
                    {
                        var ham = ws.Cells[r, kolon + 4 + i].Value;
                        if (ham == null || string.IsNullOrWhiteSpace(ham.ToString())) continue;
                        int? kont = SayiCoz(ham);
                        if (kont.HasValue) subeler.Add((subeAdlari[i], kont.Value));
                        else notlar.Add(ham.ToString().Trim());
                    }

                    if (subeler.Count == 0)
                    {
                        subeler.Add(("A", 0));
                        string not = notlar.Count > 0 ? string.Join("; ", notlar) : "kontenjan girilmemiş";
                        sonuc.Uyarilar.Add($"{tarih:dd.MM.yyyy} {saat} — {bolum} / {dersAdi}: {not} (salon atanmayabilir).");
                    }

                    if (!sonuc.Takvim.TryGetValue(tarih, out var saatler))
                    {
                        saatler = new List<string>();
                        sonuc.Takvim[tarih] = saatler;
                    }
                    if (!saatler.Contains(saat)) saatler.Add(saat);

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
                            Saat = saat
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
            return paket.Workbook.Worksheets.FirstOrDefault(w => w.Name == sayfaAdi)
                ?? paket.Workbook.Worksheets.FirstOrDefault(w =>
                       string.Equals(w.Name?.Trim(), sayfaAdi, StringComparison.OrdinalIgnoreCase))
                ?? paket.Workbook.Worksheets.FirstOrDefault(w =>
                   {
                       string n = AsciiNorm(w.Name);
                       return n.Contains("dagitim") && n.Contains("sablon");
                   });
        }

        /// <summary>Türkçe karakterleri ASCII'ye indirger, küçük harfe çevirir (harf/rakam dışını atar).</summary>
        private static string AsciiNorm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case 'ı': case 'İ': case 'I': case 'i': case 'î': case 'Î': sb.Append('i'); break;
                    case 'ş': case 'Ş': sb.Append('s'); break;
                    case 'ğ': case 'Ğ': sb.Append('g'); break;
                    case 'ç': case 'Ç': sb.Append('c'); break;
                    case 'ö': case 'Ö': sb.Append('o'); break;
                    case 'ü': case 'Ü': sb.Append('u'); break;
                    case 'â': case 'Â': sb.Append('a'); break;
                    default:
                        if (c >= 'A' && c <= 'Z') sb.Append((char)(c + 32));
                        else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static DateTime? HucreTarih(ExcelWorksheet ws, int r, int c)
        {
            var v = ws.Cells[r, c].Value;
            if (v is DateTime dt) return dt;
            if (v is double d)
            {
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

        /// <summary>Saat hücresi → "HH:mm" ("13.00" / "13:00" / gerçek saat değeri); çözülemezse null.</summary>
        private static string SaatFormatla(object v) => SaatYardimci.HucredenSaat(v);

        private static int? SayiCoz(object v)
        {
            if (v == null) return null;
            if (v is int iv) return iv;
            if (v is double dv) return (int)Math.Round(dv);
            var eslesmeler = Regex.Matches(v.ToString(), @"\d+");
            if (eslesmeler.Count == 0) return null;
            return int.Parse(eslesmeler[eslesmeler.Count - 1].Value);
        }

        private static int SinifCoz(object v)
        {
            if (v == null) return 0;
            if (v is int i) return i;
            if (v is double d) return (int)Math.Round(d);
            var m = Regex.Match(v.ToString(), @"\d+");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        private static string TemizleDersAdi(object v)
        {
            if (v == null) return null;
            string s = v.ToString().Replace("\r", " ").Replace("\n", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        /// <summary>Şablonda rehber amaçlı bırakılan "(örn)" / "örnek" satırlarını tanır.</summary>
        private static bool OrnekSatirMi(string dersAdi)
        {
            string a = (dersAdi ?? "").ToLowerInvariant();
            return a.Contains("(örn") || a.Contains("(orn") || a.Contains("örnek") || a.Contains("ornek");
        }

        private static string BolumEsle(string ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return "ÜNİVERSİTE ORTAK";
            string b = ham.Trim().ToUpperInvariant();
            if (Regex.IsMatch(b, @"^\d{1,2}[:.]\d{2}")) return "ÜNİVERSİTE ORTAK";
            if (b.Contains("TÜM BÖLÜM") || b.Contains("TUM BOLUM")) return "ORTAK DERSLER";
            if (b.Contains("ORTAK")) return "ÜNİVERSİTE ORTAK";
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
