using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 8 — Sınav Programı Formu başlık bloğu: bölüm kısaltması yerine tam bölüm adı;
/// "TEKNOLOJİ FAKÜLTESİ", bölüm + sınıf ve dönem satırlarının üçü de 11 punto.
/// </summary>
static class FormBaslikTestleri
{
    private static readonly (string Kisa, string Tam)[] Bolumler =
    {
        ("BİL", "BİLGİSAYAR MÜHENDİSLİĞİ"),
        ("EEM", "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ"),
        ("INS", "İNŞAAT MÜHENDİSLİĞİ"),
        ("MAK", "MAKİNE MÜHENDİSLİĞİ"),
        ("MEK", "MEKATRONİK MÜHENDİSLİĞİ"),
        ("MEM", "METALURJİ VE MALZEME MÜHENDİSLİĞİ"),
    };

    private const string Baslik = "2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI";

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 8 — Form başlığında tam bölüm adı, hepsi 11 punto");

        t.Test("Kısaltmalar ve farklı yazımlar tam bölüm adına çevriliyor", () =>
        {
            foreach (var (kisa, tam) in Bolumler)
                Dogrula(BolumYardimci.TamAd(kisa) == tam, $"{kisa} → '{BolumYardimci.TamAd(kisa)}'");
            Dogrula(BolumYardimci.TamAd("ELM") == "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ", "ELM tanınmadı.");
            Dogrula(BolumYardimci.TamAd("İNŞ") == "İNŞAAT MÜHENDİSLİĞİ", "İNŞ tanınmadı.");
            Dogrula(BolumYardimci.TamAd("BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ") == "BİLGİSAYAR MÜHENDİSLİĞİ", "Tam ad yazımı tanınmadı.");
            Dogrula(BolumYardimci.TamAd("Metalurji ve Malzeme Mühendisliği") == "METALURJİ VE MALZEME MÜHENDİSLİĞİ", "Küçük harfli tam ad tanınmadı.");
            Dogrula(BolumYardimci.TamAd("ÜNİVERSİTE ORTAK") == "ÜNİVERSİTE ORTAK", "Tanınmayan bölüm değiştirildi.");
        });

        foreach (var (sayfa, donemSatiri) in new[]
                 {
                     ("Sınav Programı Formu", Baslik),
                     ("Sınav Programı Formu (Gözetmenli)", Baslik)   // Madde 9: "(GÖZETMENLİ)" eki kaldırıldı
                 })
        {
            t.Test($"'{sayfa}': her bölüm bloğunda tam ad yazıyor, kısaltma yok", () => FormIle(sayfa, ws =>
            {
                var bloklar = BaslikBloklari(ws);
                var bolumSatirlari = bloklar.Select(b => ws.Cells[b + 1, 1].Text).ToList();
                foreach (var (kisa, tam) in Bolumler)
                {
                    Dogrula(bolumSatirlari.Contains($"{tam} 1. SINIF"), $"'{tam} 1. SINIF' satırı yok. Satırlar: {string.Join(" | ", bolumSatirlari)}");
                    Dogrula(!bolumSatirlari.Contains($"{kisa} 1. SINIF"), $"Kısaltma kalmış: '{kisa} 1. SINIF'.");
                }
                Dogrula(bolumSatirlari.Contains("BİLGİSAYAR MÜHENDİSLİĞİ TEKNİK SEÇMELİ DERSLER"), "Teknik seçmeli bloğunda tam ad yok.");
            }));

            t.Test($"'{sayfa}': fakülte, bölüm ve dönem satırlarının üçü de 11 punto kalın", () => FormIle(sayfa, ws =>
            {
                foreach (int ust in BaslikBloklari(ws))
                {
                    Dogrula(ws.Cells[ust + 2, 1].Text == donemSatiri, $"Satır {ust + 2}: dönem satırı '{ws.Cells[ust + 2, 1].Text}'.");
                    for (int r = ust; r <= ust + 2; r++)
                    {
                        var font = ws.Cells[r, 1].Style.Font;
                        Dogrula(Math.Abs(font.Size - 11f) < 0.01 && font.Bold,
                            $"Satır {r} ('{ws.Cells[r, 1].Text}'): {font.Size} punto, kalın={font.Bold}.");
                    }
                }
            }));
        }
    }

    /// <summary>"TEKNOLOJİ FAKÜLTESİ" yazan satırlar = her bölüm/sınıf bloğunun ilk satırı.</summary>
    private static List<int> BaslikBloklari(ExcelWorksheet ws)
    {
        var satirlar = Enumerable.Range(1, ws.Dimension.End.Row).Where(r => ws.Cells[r, 1].Text == "TEKNOLOJİ FAKÜLTESİ").ToList();
        Dogrula(satirlar.Count == Bolumler.Length + 1, $"{satirlar.Count} başlık bloğu bulundu, beklenen {Bolumler.Length + 1}.");
        return satirlar;
    }

    /// <summary>6 bölümün 1. sınıfı + BİL teknik seçmeli sınavlarıyla final Excel'ini üretir.</summary>
    private static void FormIle(string sayfa, Action<ExcelWorksheet> govde)
    {
        string yol = Path.Combine(Path.GetTempPath(), $"form_{Guid.NewGuid():N}.xlsx");
        try
        {
            var gun = new DateTime(2026, 10, 22);
            var sonuclar = Bolumler.Select((b, i) => Sinav(b.Kisa, 1, gun.AddDays(i))).ToList();
            sonuclar.Add(Sinav("BİL", 0, gun.AddDays(6)));
            ExcelMatrisExportHelper.MatrisExcelOlustur(sonuclar, new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 } }, yol, Baslik,
                new List<Gozetmen> { new() { AdSoyad = "ALİ VELİ", Bolum = "Bilgisayar Mühendisliği" } });
            using var paket = new ExcelPackage(new FileInfo(yol));
            govde(paket.Workbook.Worksheets[sayfa] ?? throw new Exception($"'{sayfa}' sayfası yok."));
        }
        finally { try { File.Delete(yol); } catch { } }
    }

    private static SinavAtama Sinav(string bolum, int sinif, DateTime gun) => new()
    {
        DersAdi = $"{bolum} DERSİ", BolumAdi = bolum, SinifSeviyesi = sinif, Sube = "A", Kontenjan = 40, Gun = gun, Saat = "09:00",
        AtananSalonlar = new() { new() { SalonKodu = "T4-204", SalonKapasitesi = 60, AtananOgrenciSayisi = 40, GozetmenAdSoyad = "ALİ VELİ" } }
    };
}
