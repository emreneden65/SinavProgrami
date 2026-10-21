using System.Text.RegularExpressions;
using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 6 — Gözetmenli taslak / final Excel'inde (ve uygulamadaki matriste) salonlar
/// 'Salon Atanmış Dosya' ile aynı sırada: T4 binası önce, sonra salon kodu.
/// </summary>
static class SalonSirasiTestleri
{
    private static readonly string[] BeklenenSira = { "T4-204", "T4-209", "T4-330", "T1-107", "T1-201" };

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 6 — Gözetmenli taslakta salonlar T4 bloğundan başlıyor");

        t.Test("Gözetmenli taslağın 'Slot Bazlı Sınav Planı' sayfası T4 ile başlıyor", () => TaslakIle((paket, _, _) =>
        {
            var ws = paket.Workbook.Worksheets[GozetmenliSablonOkuyucu.SayfaAdi];
            var sira = BaslikSalonlari(ws);
            Dogrula(sira.SequenceEqual(BeklenenSira), $"Salon sırası: {string.Join(", ", sira)}");
        }));

        t.Test("Taslaktaki bütün sayfalarda ve hücrelerde T4 salonları T1'den önce", () => TaslakIle((paket, _, _) =>
        {
            foreach (var ws in paket.Workbook.Worksheets)
            {
                if (ws.Dimension == null) continue;
                // Salon başlık satırları
                for (int r = 1; r <= ws.Dimension.End.Row; r++)
                {
                    var kodlar = SatirdakiSalonlar(ws, r);
                    if (kodlar.Count < 2) continue;
                    int sonT4 = kodlar.FindLastIndex(k => k.StartsWith("T4")), ilkT1 = kodlar.FindIndex(k => k.StartsWith("T1"));
                    Dogrula(sonT4 < 0 || ilkT1 < 0 || sonT4 < ilkT1,
                        $"'{ws.Name}' satır {r}: {string.Join(", ", kodlar)}");
                }
                // Hücre içi salon listeleri ("T4-204(40), T1-107(30)" / "T4-204-330, T1-107")
                foreach (var hucre in ws.Cells[ws.Dimension.Address])
                {
                    string m = hucre.Text ?? "";
                    int i4 = m.IndexOf("T4-"), i1 = m.IndexOf("T1-");
                    Dogrula(i4 < 0 || i1 < 0 || i4 < i1, $"'{ws.Name}' {hucre.Address}: \"{m}\"");
                }
            }
        }));

        t.Test("Sıra değişse de düzenlenmiş taslak geri yüklenince gözetmenler doğru salona oturuyor", () => TaslakIle((_, yol, kaynak) =>
        {
            var hedef = Sonuclar();
            foreach (var sa in hedef.SelectMany(s => s.AtananSalonlar)) sa.GozetmenAdSoyad = "";
            int guncellenen = GozetmenliSablonOkuyucu.Uygula(hedef, GozetmenliSablonOkuyucu.Oku(yol));
            Dogrula(guncellenen == hedef.Count, $"{guncellenen}/{hedef.Count} satır güncellendi.");
            // Geri yükleme salon listesini Excel'deki sütun sırasıyla kurar → salon koduna göre karşılaştır
            foreach (var (k, h) in kaynak.Zip(hedef))
            {
                var okunan = h.AtananSalonlar.ToDictionary(s => s.SalonKodu, s => s.GozetmenAdSoyad);
                Dogrula(okunan.Count == k.AtananSalonlar.Count, $"'{k.DersAdi}': salon sayısı değişti.");
                foreach (var s in k.AtananSalonlar)
                    Dogrula(okunan.TryGetValue(s.SalonKodu, out var ad) && ad == s.GozetmenAdSoyad,
                        $"'{k.DersAdi}' {s.SalonKodu}: beklenen '{s.GozetmenAdSoyad}', okunan '{ad}'.");
            }
        }));

        t.Test("Uygulamadaki Matris görünümü de T4 ile başlıyor", () =>
        {
            OturumKaydi.Sil();
            using var form = FormuAc();
            Cagir(form, "MatrisGorunumuOlustur", Sonuclar(), Salonlar());
            var sira = Alan<DataGridView>(form, "dgvMatris").Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Tag is Salon).Select(c => ((Salon)c.Tag).SalonKodu).ToList();
            Dogrula(sira.SequenceEqual(BeklenenSira), $"Matris salon sırası: {string.Join(", ", sira)}");
            form.Close();
        });
    }

    private static List<Salon> Salonlar() => new()
    {
        // Bilerek karışık sırada
        new() { SalonKodu = "T1-201", Kapasite = 30 }, new() { SalonKodu = "T4-330", Kapasite = 60 },
        new() { SalonKodu = "T1-107", Kapasite = 62 }, new() { SalonKodu = "T4-209", Kapasite = 40, SalonTipi = "UYGULAMALI" },
        new() { SalonKodu = "T4-204", Kapasite = 60 },
    };

    private static List<SinavAtama> Sonuclar()
    {
        var gun = new DateTime(2026, 10, 22);
        SinavAtama Sinav(string ders, string bolum, string saat, params (string Kod, int Ogr, string Gozetmen)[] s) => new()
        {
            DersAdi = ders, BolumAdi = bolum, SinifSeviyesi = 1, Sube = "A", Kontenjan = s.Sum(x => x.Ogr), Gun = gun, Saat = saat,
            AtananSalonlar = s.Select(x => new SalonAtama { SalonKodu = x.Kod, SalonKapasitesi = 60, AtananOgrenciSayisi = x.Ogr, Sube = "A", GozetmenAdSoyad = x.Gozetmen }).ToList()
        };
        return new List<SinavAtama>
        {
            Sinav("FİZİK II", "BİL", "09:00", ("T1-107", 30, "ALİ VELİ"), ("T4-204", 40, "AYŞE KAYA"), ("T4-330", 20, "MEHMET YILMAZ")),
            Sinav("DİFERANSİYEL", "MAK", "11:00", ("T1-201", 25, "ALİ VELİ"), ("T4-209", 30, "AYŞE KAYA")),
        };
    }

    /// <summary>Gözetmenli taslağı üretir, açar ve gövdeye verir; sonunda siler.</summary>
    private static void TaslakIle(Action<ExcelPackage, string, List<SinavAtama>> govde)
    {
        string yol = Path.Combine(Path.GetTempPath(), $"taslak_{Guid.NewGuid():N}.xlsx");
        var sonuclar = Sonuclar();
        try
        {
            ExcelMatrisExportHelper.MatrisExcelOlustur(sonuclar, Salonlar(), yol, "", new List<Gozetmen>
            {
                new() { AdSoyad = "ALİ VELİ", Bolum = "Bilgisayar Mühendisliği" },
                new() { AdSoyad = "AYŞE KAYA", Bolum = "Makine Mühendisliği" },
                new() { AdSoyad = "MEHMET YILMAZ", Bolum = "Bilgisayar Mühendisliği" },
            });
            using var paket = new ExcelPackage(new FileInfo(yol));
            govde(paket, yol, sonuclar);
        }
        finally { try { File.Delete(yol); } catch { } }
    }

    private static readonly Regex SalonKodu = new(@"^T\d-\d+");

    /// <summary>Bir satırdaki hücrelerin başındaki salon kodları (ör. "T4-207\n(40)" → "T4-207"), soldan sağa.</summary>
    private static List<string> SatirdakiSalonlar(ExcelWorksheet ws, int r)
    {
        var liste = new List<string>();
        for (int c = 1; c <= ws.Dimension.End.Column; c++)
        {
            var m = SalonKodu.Match((ws.Cells[r, c].Text ?? "").Trim());
            if (m.Success && (ws.Cells[r, c].Text.Trim() == m.Value || ws.Cells[r, c].Text.Contains('\n')))
                liste.Add(m.Value);
        }
        return liste;
    }

    /// <summary>Sayfadaki en çok salon kodu içeren satır = salon başlık satırı.</summary>
    private static List<string> BaslikSalonlari(ExcelWorksheet ws)
        => Enumerable.Range(1, ws.Dimension.End.Row).Select(r => SatirdakiSalonlar(ws, r))
                     .OrderByDescending(l => l.Count).First();
}
