using System.Text.RegularExpressions;
using OfficeOpenXml;
using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 9 — (1) "(GÖZETMENLİ)" ekleri Excel'den kalktı (sekme adı aynen kaldı);
/// (2) tüm Excel çıktılarında saat 13.00 biçiminde; geri yüklenen dosyalarda 13.00 da 13:00 da okunur.
/// </summary>
static class SaatBicimiTestleri
{
    private const string Baslik = "2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI";
    private static readonly Regex IkiNoktaliSaat = new(@"\b\d{1,2}:\d{2}\b");
    private static readonly DateTime Gun = new(2026, 10, 22);

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 9 — '(GÖZETMENLİ)' kalktı, saatler 13.00");

        t.Test("Final / taslak Excel'inde hiçbir hücrede '(GÖZETMENLİ)' yok; sekme adı aynen duruyor", () => FinalIle(paket =>
        {
            var hucreler = TumHucreler(paket);
            Dogrula(!hucreler.Any(h => h.Metin.Contains("(GÖZETMENLİ)")), "Hâlâ '(GÖZETMENLİ)' yazan hücre var.");
            // Excel sekme adını 31 karakterde keser: gerçek ad "Sınav Programı Formu (Gözetmenl"
            var gozetmenliSayfa = paket.Workbook.Worksheets["Sınav Programı Formu (Gözetmenli)"];
            Dogrula(gozetmenliSayfa != null, "Gözetmenli form sekmesinin adı değişmiş.");
            Dogrula(hucreler.Any(h => h.Sayfa == gozetmenliSayfa.Name && h.Metin == Baslik),
                "Gözetmenli formda başlık sade formla aynı değil.");
        }));

        t.Test("Final / taslak Excel'inin bütün sayfalarında saat 13.00 biçiminde (13:00 yok)", () => FinalIle(paket =>
        {
            var hucreler = TumHucreler(paket);
            var ikiNoktali = hucreler.Where(h => IkiNoktaliSaat.IsMatch(h.Metin)).ToList();
            Dogrula(ikiNoktali.Count == 0, $"'13:00' biçimi kalmış: {string.Join(" | ", ikiNoktali.Take(5).Select(h => $"{h.Sayfa}: {h.Metin}"))}");
            foreach (var sayfa in new[] { "Sınav Programı Matrisi", "Slot Bazlı Sınav Planı", "Sınav Programı Formu",
                                          paket.Workbook.Worksheets["Sınav Programı Formu (Gözetmenli)"].Name, "Görev Çizelgesi" })
                Dogrula(hucreler.Any(h => h.Sayfa == sayfa && h.Metin.Contains("13.00")), $"'{sayfa}' sayfasında 13.00 görünmüyor.");
        }));

        t.Test("Şablon Excel'inde saat 13.00; geri yüklenince 13:00 olarak okunuyor", () =>
        {
            string yol = GeciciYol();
            try
            {
                var takvim = new Dictionary<DateTime, List<string>> { [Gun] = new() { "09:00", "13:00" } };
                var dagitim = new List<SinavAtama> { new() { DersAdi = "FİZİK II", BolumAdi = "BİLGİSAYAR", SinifSeviyesi = 1, Sube = "A", Kontenjan = 40, Gun = Gun, Saat = "13:00" } };
                CekilmisDerslerExportHelper.Olustur(new List<Ders>(), takvim, yol, out _, dagitim,
                    new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 } });
                using (var paket = new ExcelPackage(new FileInfo(yol)))
                {
                    var hucreler = TumHucreler(paket);
                    Dogrula(!hucreler.Any(h => IkiNoktaliSaat.IsMatch(h.Metin)), "Şablonda '13:00' biçimi kalmış.");
                    Dogrula(hucreler.Any(h => h.Metin == "13.00") && hucreler.Any(h => h.Metin == "09.00"), "Şablonda 13.00 / 09.00 yok.");
                }
                var okuma = DagitimSablonuOkuyucu.Oku(yol);
                Dogrula(okuma.Sablon.Count == 1 && okuma.Sablon[0].Saat == "13:00", $"Geri okunan saat: '{okuma.Sablon.FirstOrDefault()?.Saat}'.");
            }
            finally { try { File.Delete(yol); } catch { } }
        });

        t.Test("Salon atanmış Excel'de saat 13.00; geri yüklenince 13:00 olarak okunuyor", () =>
        {
            string yol = GeciciYol();
            try
            {
                var satir = new GunlukSalonDagitici.GunSatir
                {
                    Gun = Gun, Saat = "13:00", Bolum = "BİL", Sinif = 1, DersAdi = "FİZİK II", Toplam = 40,
                    Subeler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["A"] = 40 },
                    SalonDagilimi = new Dictionary<string, int> { ["T4-204"] = 40 },
                    SalonSubeDagilimi = new Dictionary<string, Dictionary<string, int>> { ["T4-204"] = new() { ["A"] = 40 } }
                };
                var takvim = new Dictionary<DateTime, List<string>> { [Gun] = new() { "09:00", "13:00" } };
                GunSalonMatrisExportHelper.YazVeKaydet(new() { satir }, new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 } },
                    new List<SinavAtama>(), takvim, yol);
                using (var paket = new ExcelPackage(new FileInfo(yol)))
                {
                    var ws = paket.Workbook.Worksheets[$"{Gun:dd.MM.yyyy}"];
                    var saatHucresi = Enumerable.Range(3, ws.Dimension.End.Row - 2).Select(r => ws.Cells[r, 4])
                                                .First(c => c.Value is double d && Math.Abs(d - 13.0 / 24) < 1e-6);
                    Dogrula(saatHucresi.Style.Numberformat.Format == SaatYardimci.ExcelSaatBicimi,
                        $"Saat hücresi biçimi '{saatHucresi.Style.Numberformat.Format}'.");
                    Dogrula(!TumHucreler(paket).Any(h => h.Metin.Contains(":") && IkiNoktaliSaat.IsMatch(h.Metin) && h.Sayfa != ws.Name),
                        "Salon atanmış dosyanın şablon sayfasında '13:00' biçimi kalmış.");
                }
                var okuma = GunSalonMatrisOkuyucu.Oku(yol);
                Dogrula(okuma.Atamalar.Count == 1 && okuma.Atamalar[0].Saat == "13:00", $"Geri okunan saat: '{okuma.Atamalar.FirstOrDefault()?.Saat}'.");
            }
            finally { try { File.Delete(yol); } catch { } }
        });

        t.Test("Elle yazılan saatler okunuyor: 13.00, 13:00, 9.30, Excel'in çevirdiği 13 / 13,3 / 1300", () =>
        {
            var beklenen = new (object Deger, string Saat)[]
            {
                ("13.00", "13:00"), ("13:00", "13:00"), ("9.30", "09:30"), (" 09.00 ", "09:00"),
                (13.0 / 24, "13:00"), (13d, "13:00"), (13.3d, "13:30"), (1300d, "13:00"), (930d, "09:30"),
                (new DateTime(1899, 12, 30, 13, 0, 0), "13:00"), (TimeSpan.FromHours(15), "15:00"),
            };
            foreach (var (deger, saat) in beklenen)
                Dogrula(SaatYardimci.HucredenSaat(deger) == saat, $"{deger} → '{SaatYardimci.HucredenSaat(deger)}', beklenen {saat}.");
            foreach (var gecersiz in new object[] { "22.10.2026", "25.00", "13.75", "abc", "", null, 2500d })
                Dogrula(SaatYardimci.HucredenSaat(gecersiz) == null, $"'{gecersiz}' saat sanıldı.");
        });

        t.Test("Uygulama içinde saatler 13:00 kalıyor (yalnızca Excel'de 13.00)", () =>
        {
            Dogrula(SaatYardimci.ExcelMetni("13:00") == "13.00" && SaatYardimci.ExcelMetni("9:00") == "09.00", "Excel metni yanlış.");
            OturumKaydi.Sil();
            using var form = FormuAc();
            var saatler = Alan<Dictionary<DateTime, List<string>>>(form, "_takvimSlotlari").Values.SelectMany(s => s).Distinct().ToList();
            Dogrula(saatler.Count > 0 && saatler.All(s => s.Contains(':')), $"Uygulamadaki saatler: {string.Join(", ", saatler)}");
            form.Close();
        });
    }

    private static string GeciciYol() => Path.Combine(Path.GetTempPath(), $"saat_{Guid.NewGuid():N}.xlsx");

    private static List<(string Sayfa, string Metin)> TumHucreler(ExcelPackage paket)
        => paket.Workbook.Worksheets.Where(w => w.Dimension != null)
                .SelectMany(w => w.Cells[w.Dimension.Address].Select(c => (w.Name, c.Text ?? ""))).ToList();

    /// <summary>13:00'te gözetmen atanmış bir sınavla final / taslak Excel'ini üretir.</summary>
    private static void FinalIle(Action<ExcelPackage> govde)
    {
        string yol = GeciciYol();
        try
        {
            var sonuclar = new List<SinavAtama>
            {
                new() { DersAdi = "FİZİK II", BolumAdi = "BİL", SinifSeviyesi = 1, Sube = "A", Kontenjan = 40, Gun = Gun, Saat = "13:00",
                        AtananSalonlar = new() { new() { SalonKodu = "T4-204", SalonKapasitesi = 60, AtananOgrenciSayisi = 40, GozetmenAdSoyad = "ALİ VELİ" } } }
            };
            ExcelMatrisExportHelper.MatrisExcelOlustur(sonuclar, new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 } }, yol, Baslik,
                new List<Gozetmen> { new() { AdSoyad = "ALİ VELİ", Bolum = "Bilgisayar Mühendisliği" } });
            using var paket = new ExcelPackage(new FileInfo(yol));
            govde(paket);
        }
        finally { try { File.Delete(yol); } catch { } }
    }
}
