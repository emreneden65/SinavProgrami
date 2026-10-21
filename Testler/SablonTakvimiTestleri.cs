using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 0 — "Sınav Haftası Dağıtım Şablonu" sayfasında seçilen tüm sınav günleri
/// hafta sınırında bölünmeden TEK BANTTA yan yana yazılır.
/// </summary>
static class SablonTakvimiTestleri
{
    private static readonly List<string> Saatler = new() { "09:00", "11:00", "13:00", "15:00", "17:00" };
    private const int BlokGenisligi = 8;

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 0 — Şablon takvimi tek bant");

        t.Test("Perşembe başlayan 7 günlük vize tek satırda ve geri okunuyor", () =>
            GunleriKontrolEt(SinavTipi.GunleriUret(new DateTime(2026, 10, 22), SinavDonemTipi.Vize)));

        t.Test("10 günlük final tek satırda; şablonda olmayan 8–10. bloklar biçimli", () =>
            GunleriKontrolEt(SinavTipi.GunleriUret(new DateTime(2026, 10, 26), SinavDonemTipi.Final)));
    }

    private static void GunleriKontrolEt(List<DateTime> gunler)
    {
        var takvim = gunler.ToDictionary(g => g, g => new List<string>(Saatler));
        var dersler = new List<Ders>
        {
            new() { BolumAdi = "BİLGİSAYAR", DersKodu = "BIL214", DersAdi = "VERİ YAPILARI", Sube = "A", Kontenjan = 80, SinifSeviyesi = 2 }
        };
        // Her güne 09:00'da bir ders — geri okumada gün kaybı olursa yakalanır
        var dagitim = gunler.Select((g, i) => new SinavAtama
        {
            DersAdi = $"DERS {i + 1}", BolumAdi = "BİLGİSAYAR", SinifSeviyesi = 3,
            Sube = "A", Kontenjan = 50, Gun = g, Saat = "09:00"
        }).ToList();

        string yol = Path.Combine(Path.GetTempPath(), $"sablon_test_{Guid.NewGuid():N}.xlsx");
        try
        {
            CekilmisDerslerExportHelper.Olustur(dersler, takvim, yol, out _, dagitim);

            using (var paket = new ExcelPackage(new FileInfo(yol)))
            {
                var ws = paket.Workbook.Worksheets["Sınav Haftası Dağıtım Şablonu"];
                Dogrula(ws != null, "Şablon sayfası bulunamadı.");

                // Bütün tarihler 2. satırda, 1., 9., 17. ... sütunlarda, sırayla
                for (int i = 0; i < gunler.Count; i++)
                {
                    int kolon = 1 + i * BlokGenisligi;
                    Dogrula(Tarih(ws.Cells[2, kolon].Value) == gunler[i],
                        $"{gunler[i]:dd.MM} tarihi 2. satır {kolon}. sütunda değil.");
                    Dogrula(ws.Cells[2, kolon].Merge,
                        $"{gunler[i]:dd.MM} tarih hücresi birleştirilmemiş (### görünür).");
                    Dogrula(Equals(ws.Cells[3, kolon].Value, "Bölüm"),
                        $"{gunler[i]:dd.MM} bloğunda kolon başlıkları yok.");
                    Dogrula(Math.Abs(ws.Column(kolon + 3).Width - ws.Column(4).Width) < 0.01,
                        $"{gunler[i]:dd.MM} bloğunun 'Ders Adı' sütun genişliği 1. bloktan farklı.");
                }

                // 2. satırın altında başka tarih satırı (ikinci bant) olmamalı
                for (int r = 3; r <= ws.Dimension.End.Row; r++)
                    for (int c = 1; c <= ws.Dimension.End.Column; c += BlokGenisligi)
                        Dogrula(Tarih(ws.Cells[r, c].Value) == null, $"{r}. satırda ikinci bir bant tarihi var.");
            }

            var okuma = DagitimSablonuOkuyucu.Oku(yol);
            Dogrula(okuma.Takvim.Count == gunler.Count,
                $"Geri okunan gün sayısı {okuma.Takvim.Count}, beklenen {gunler.Count}.");
            Dogrula(okuma.Sablon.Count == gunler.Count,
                $"Geri okunan sınav sayısı {okuma.Sablon.Count}, beklenen {gunler.Count}.");
        }
        finally
        {
            try { File.Delete(yol); } catch { }
        }
    }

    /// <summary>Kaydedilmiş dosyada tarih hücresi Excel seri numarası (double) olarak okunur.</summary>
    private static DateTime? Tarih(object v) => v switch
    {
        DateTime d => d.Date,
        double d when d > 40000 && d < 80000 => DateTime.FromOADate(d).Date,
        _ => null
    };
}
