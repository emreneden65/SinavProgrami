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
    /// Salon atanmış gün-sayfası Excel'ini (GunSalonMatrisExportHelper çıktısı, kullanıcı elle
    /// düzenlemiş olabilir) geri okur ve gözetmen ataması için <see cref="SinavAtama"/> listesi
    /// üretir (AtananSalonlar dolu).
    ///
    /// Gün sayfası düzeni:
    ///   row1: col10+ salon kodları,  row2: col1-9 başlıklar + col10+ kapasiteler,
    ///   row3+: Bölüm | SINIF | Tarih | Saat | Dersler | A | B | C | D | [salon hücreleri]
    /// </summary>
    public static class GunSalonMatrisOkuyucu
    {
        private const int SalonBaslangicKolon = 10;

        public class Sonuc
        {
            public List<SinavAtama> Atamalar { get; } = new List<SinavAtama>();
            public List<string> Uyarilar { get; } = new List<string>();
            public int SayfaSayisi { get; set; }
        }

        /// <summary>Ders satırındaki tek bir salon hücresinin ham okunuşu.</summary>
        private class SalonHucresi
        {
            public string SalonKodu;
            public int Kapasite;
            /// <summary>Hücrede sayı yazılıysa öğrenci sayısı; yoksa null (kolon toplamından türetilir).</summary>
            public int? Sayi;
            /// <summary>Hücredeki şube harfleri ("A", ya da "A+B" için [A,B]).</summary>
            public List<string> Harfler = new List<string>();
        }

        public static Sonuc Oku(string dosyaYolu)
        {
            var sonuc = new Sonuc();
            using var paket = new ExcelPackage(new FileInfo(dosyaYolu));

            var gunSayfalari = paket.Workbook.Worksheets.Where(GunSayfasiMi).ToList();
            if (gunSayfalari.Count == 0)
            {
                sonuc.Uyarilar.Add("Dosyada 'Sınav Günü' matris sayfası bulunamadı.");
                return sonuc;
            }

            foreach (var ws in gunSayfalari)
            {
                sonuc.SayfaSayisi++;
                int sonSatir = ws.Dimension.End.Row;
                int sonKolon = ws.Dimension.End.Column;

                // Salon kodları (row1) + kapasiteleri (row2)
                var salonKodlari = new Dictionary<int, string>();
                var salonKapasite = new Dictionary<int, int>();
                for (int c = SalonBaslangicKolon; c <= sonKolon; c++)
                {
                    string kod = ws.Cells[1, c].Value?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(kod)) continue;
                    salonKodlari[c] = kod;
                    salonKapasite[c] = SayiCoz(ws.Cells[2, c].Value) ?? 0;
                }
                if (salonKodlari.Count == 0) continue;

                for (int r = 3; r <= sonSatir; r++)
                {
                    string dersAdi = TemizAd(ws.Cells[r, 5].Value?.ToString());
                    if (string.IsNullOrWhiteSpace(dersAdi)) continue;

                    string bolum = ws.Cells[r, 1].Value?.ToString()?.Trim() ?? "";
                    int sinif = SayiCoz(ws.Cells[r, 2].Value) ?? 0;
                    DateTime gun = HucreTarih(ws.Cells[r, 3].Value) ?? default;
                    string saat = SaatCoz(ws.Cells[r, 4].Value);

                    // ── Salon hücreleri ──
                    // Yeni format: hücrede YALNIZCA şube etiketi ("A", "A+B", "D") bulunur —
                    // sayı gerekmez. Öğrenci sayısı, satırın A/B/C/D kolon toplamlarından
                    // türetilir. Eski formatlar ("25", "25 (A)") geriye dönük desteklenir.
                    var hucreler = new List<SalonHucresi>();

                    foreach (var kv in salonKodlari)
                    {
                        object hucre = ws.Cells[r, kv.Key].Value;
                        string ham = hucre?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(ham)) continue;

                        hucreler.Add(new SalonHucresi
                        {
                            SalonKodu = kv.Value,
                            Kapasite = salonKapasite.TryGetValue(kv.Key, out int k) ? k : 0,
                            Sayi = SayiCoz(hucre),
                            Harfler = SubeHarfleriCoz(ham)
                        });
                    }

                    // Harf kolon toplamları (A=6, B=7, C=8, D=9)
                    // 9. kolon eski dosyalarda "INT" başlıklıdır — "D şubesi" olarak okunur.
                    var harfToplamlari = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["A"] = SayiCoz(ws.Cells[r, 6].Value) ?? 0,
                        ["B"] = SayiCoz(ws.Cells[r, 7].Value) ?? 0,
                        ["C"] = SayiCoz(ws.Cells[r, 8].Value) ?? 0,
                        ["D"] = SayiCoz(ws.Cells[r, 9].Value) ?? 0
                    };

                    // ── ŞUBELERİ AYRI SATIRLARA BÖL ──
                    // Excel'de bir ders satırı A/B/C/D şubelerini birlikte taşıyabilir.
                    // Çıktıda "DERS (A+B)" gibi birleşik etiket İSTENMEDİĞİ için burada
                    // her şube KENDİ SinavAtama satırına ayrılır; salonlar da o şubeye
                    // ait olanlarla sınırlanır. Bir salon iki şubeyi birden barındırıyorsa
                    // (hücrede "A+B" yazıyorsa) her iki satırda da görünür — gözetmen
                    // algoritması K1 kuralıyla o salona yine tek gözetmen atar.
                    var mevcutHarfler = SubeEtiketi.Harfler
                        .Where(h => harfToplamlari[h] > 0 || hucreler.Any(x => x.Harfler.Contains(h)))
                        .ToList();

                    // Şube harfi yazılmamış hücreler (eski format: sadece "25").
                    var harfsizHucreler = hucreler.Where(x => x.Harfler.Count == 0).ToList();

                    if (mevcutHarfler.Count == 0)
                        mevcutHarfler.Add(SubeEtiketi.Varsayilan);   // hiç bilgi yok → tek şube "A"

                    if (harfsizHucreler.Count > 0 && mevcutHarfler.Count > 1)
                        sonuc.Uyarilar.Add($"{ws.Name} satır {r} — '{dersAdi}': " +
                            string.Join(", ", harfsizHucreler.Select(x => x.SalonKodu)) +
                            " salonlarında şube harfi yazılmamış, birden çok şube olduğu için bağlanamadı.");

                    int uretilenSatir = 0;
                    foreach (string harf in mevcutHarfler)
                    {
                        var kendiHucreleri = hucreler.Where(x => x.Harfler.Contains(harf)).ToList();
                        // Harfsiz hücreler yalnızca satırda TEK şube varsa ona bağlanır;
                        // aksi halde hangi şubeye ait olduğu dosyadan anlaşılamaz.
                        if (mevcutHarfler.Count == 1) kendiHucreleri.AddRange(harfsizHucreler);

                        // Sayı yazılmamış salonların öğrenci sayısı: şubenin kolon toplamı,
                        // aynı şubeyi taşıyan salonlara eşit paylaştırılır (bölünmüş şube).
                        var sayisizlar = kendiHucreleri.Where(x => !x.Sayi.HasValue).ToList();
                        int pay = 0, kalanPay = 0;
                        if (sayisizlar.Count > 0 && harfToplamlari[harf] > 0)
                        {
                            pay = harfToplamlari[harf] / sayisizlar.Count;
                            kalanPay = harfToplamlari[harf] - pay * (sayisizlar.Count - 1);
                        }

                        var salonlar = new List<SalonAtama>();
                        int sayisizIdx = 0;
                        foreach (var h in kendiHucreleri)
                        {
                            int ogrenci;
                            if (h.Sayi.HasValue) ogrenci = h.Sayi.Value;
                            else
                            {
                                ogrenci = (sayisizIdx == sayisizlar.Count - 1) ? kalanPay : pay;
                                sayisizIdx++;
                            }

                            salonlar.Add(new SalonAtama
                            {
                                SalonKodu = h.SalonKodu,
                                SalonKapasitesi = h.Kapasite,
                                AtananOgrenciSayisi = ogrenci,
                                Sube = harf          // artık salon da tek şube etiketi taşır
                            });
                        }

                        int kontenjan = harfToplamlari[harf] > 0
                            ? harfToplamlari[harf]
                            : salonlar.Sum(s => s.AtananOgrenciSayisi);

                        sonuc.Atamalar.Add(new SinavAtama
                        {
                            DersAdi = dersAdi,
                            BolumAdi = bolum,
                            SinifSeviyesi = sinif,
                            Kontenjan = kontenjan,
                            Sube = harf,
                            Gun = gun,
                            Saat = saat,
                            AtananSalonlar = salonlar
                        });
                        uretilenSatir++;

                        if (salonlar.Count == 0)
                            sonuc.Uyarilar.Add($"{ws.Name} satır {r} — '{dersAdi}' (Şube {harf}): salon atanmamış (gözetmen atanamaz).");
                    }

                    if (uretilenSatir == 0)
                        sonuc.Uyarilar.Add($"{ws.Name} satır {r} — '{dersAdi}': şube çözümlenemedi, satır atlandı.");
                }
            }

            return sonuc;
        }

        // ── Yardımcılar ──
        private static bool GunSayfasiMi(ExcelWorksheet w)
        {
            if (w?.Dimension == null) return false;
            bool tarih = false, dersler = false;
            int sonK = Math.Min(w.Dimension.End.Column, 12);
            for (int c = 1; c <= sonK; c++)
            {
                string v = (w.Cells[2, c].Value?.ToString() ?? "").Trim().ToUpperInvariant();
                if (v == "TARİH" || v == "TARIH") tarih = true;
                if (v == "DERSLER") dersler = true;
            }
            return tarih && dersler;
        }

        /// <summary>
        /// Hücre metnindeki şube etiketlerini (A / B / C / D) çıkarır.
        /// "A" → [A], "A+B" → [A,B], "25 (A)" → [A] (eski format uyumu).
        /// Eski dosyalardaki "INT" etiketi otomatik olarak "D"ye çevrilir.
        /// </summary>
        private static List<string> SubeHarfleriCoz(string ham)
        {
            var sonuc = new List<string>();
            if (string.IsNullOrWhiteSpace(ham)) return sonuc;

            string metin = ham.ToUpperInvariant().Replace("İ", "I");
            foreach (var token in Regex.Split(metin, @"[^A-ZÇĞÖŞÜ]+"))
            {
                string t = token.Trim();
                if (!SubeEtiketi.GecerliEtiketMi(t)) continue;
                string harf = SubeEtiketi.Normalize(t);   // "INT" → "D"
                if (!sonuc.Contains(harf)) sonuc.Add(harf);
            }
            return sonuc;
        }

        private static int? SayiCoz(object v)
        {
            if (v == null) return null;
            if (v is int i) return i;
            if (v is double d) return (int)Math.Round(d);
            var m = Regex.Matches(v.ToString(), @"\d+");
            return m.Count > 0 ? int.Parse(m[0].Value) : (int?)null;
        }

        private static DateTime? HucreTarih(object v)
        {
            if (v is DateTime dt) return dt.Date;
            if (v is double d && d > 40000 && d < 80000) { try { return DateTime.FromOADate(d).Date; } catch { } }
            if (v is string s && DateTime.TryParse(s, new CultureInfo("tr-TR"), DateTimeStyles.None, out var p)) return p.Date;
            return null;
        }

        /// <summary>Saat hücresi → "HH:mm" ("13.00" / "13:00" / gerçek saat değeri); çözülemezse metin olduğu gibi.</summary>
        private static string SaatCoz(object v)
            => SaatYardimci.HucredenSaat(v) ?? (v?.ToString()?.Trim() ?? "");

        private static string TemizAd(string ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) return ad;
            // "(ortak)" gibi ekleri temizle
            return Regex.Replace(ad, @"\s*\(ortak\)\s*$", "", RegexOptions.IgnoreCase).Trim();
        }
    }
}
