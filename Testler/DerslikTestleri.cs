using Microsoft.Data.Sqlite;
using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 3 — Derslikler veritabanında tutulmaz; her okul kendi derslik Excel'ini
/// "Derslik Listesi Yükle" ile yükler (liste oturum kaydında saklanır).
/// </summary>
static class DerslikTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 3 — Derslikler veritabanında değil, kullanıcı yüklüyor");

        t.Test("Veritabanında derslik tablosu yok; eski kurulumdaki salonlar siliniyor", () =>
        {
            string eskiYol = DatabaseHelper.VeritabaniYolu;
            string geciciDb = Path.Combine(Path.GetTempPath(), $"eski_kurulum_{Guid.NewGuid():N}.db");
            try
            {
                // Eski sürümün bıraktığı veritabanı: içinde 2 salon kayıtlı
                using (var c = new SqliteConnection($"Data Source={geciciDb}"))
                {
                    c.Open();
                    var cmd = c.CreateCommand();
                    cmd.CommandText = "CREATE TABLE Salonlar (ID INTEGER PRIMARY KEY, Salon_Kodu TEXT, Kapasite INTEGER, Salon_Tipi TEXT);" +
                                      "INSERT INTO Salonlar (Salon_Kodu, Kapasite, Salon_Tipi) VALUES ('T4-204', 60, 'NORMAL'), ('T1-104', 40, 'UYGULAMALI');";
                    cmd.ExecuteNonQuery();
                }

                DatabaseHelper.VeritabaniniAyarla(geciciDb);
                DatabaseHelper.VeritabaniniHazirla();

                using (var c = new SqliteConnection($"Data Source={geciciDb}"))
                {
                    c.Open();
                    var cmd = c.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE '%Salon%'";
                    Dogrula(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "Veritabanında hâlâ salon tablosu var.");
                }
            }
            finally
            {
                DatabaseHelper.VeritabaniniAyarla(eskiYol);
                SqliteConnection.ClearAllPools();
                try { File.Delete(geciciDb); } catch { }
            }
        });

        t.Test("İlk açılışta derslik listesi boş; durum satırı yüklemeyi istiyor", () =>
        {
            OturumKaydi.Sil();
            using var form = FormuAc();
            Dogrula(Alan<List<Salon>>(form, "_derslikler").Count == 0, "Program kendi derslik listesiyle açıldı.");
            Dogrula(Alan<Label>(form, "lblDbDurum").Text.Contains("Derslik listesi yüklenmedi"),
                "Durum satırı derslik yüklenmesini istemiyor.");
            form.Close();
        });

        t.Test("Derslik Excel'i okunuyor (normal + laboratuvar bölümü)", () =>
        {
            string yol = DerslikExceliYaz();
            try
            {
                var okuma = DerslikListesiOkuyucu.Oku(yol);
                Dogrula(okuma.Salonlar.Count == 3, $"Okunan derslik sayısı {okuma.Salonlar.Count}, beklenen 3.");
                Dogrula(okuma.Salonlar.Single(s => s.SalonKodu == "A-101").Kapasite == 62, "'62(31)' kapasitesi 62 okunmadı.");
                Dogrula(okuma.Salonlar.Single(s => s.SalonKodu == "LAB-1").UygulamaliMi, "Laboratuvar uygulamalı sayılmadı.");
            }
            finally { File.Delete(yol); }
        });

        t.Test("Yüklenen liste uygulamaya geçiyor, veritabanına yazılmıyor", () =>
        {
            OturumKaydi.Sil();
            using var form = FormuAc();
            Cagir(form, "DerslikListesiniUygula", OrnekDerslikler(3));
            Dogrula(Alan<List<Salon>>(form, "_derslikler").Count == 3, "Derslik listesi uygulanmadı.");
            Dogrula(Alan<Label>(form, "lblDbDurum").Text.StartsWith("✅ 3 derslik yüklü"), "Durum satırı güncellenmedi.");

            using (var c = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                c.Open();
                var cmd = c.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE '%Salon%'";
                Dogrula(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "Derslikler veritabanına yazıldı.");
            }
            form.Close();
        });

        t.Test("Derslik listesi kapatıp açınca geri geliyor (Madde 1 ile)", () =>
        {
            OturumKaydi.Sil();
            using (var form = FormuAc())
            {
                Cagir(form, "DerslikListesiniUygula", OrnekDerslikler(3));
                form.Close();
            }
            using var yeniden = FormuAc();
            var derslikler = Alan<List<Salon>>(yeniden, "_derslikler");
            Dogrula(derslikler.Count == 3 && derslikler.Any(s => s.UygulamaliMi), "Derslik listesi geri gelmedi.");
            Dogrula(Alan<Label>(yeniden, "lblDbDurum").Text.StartsWith("✅ 3 derslik yüklü"), "Durum satırı geri gelmedi.");
            yeniden.Close();
        });

        t.Test("Şablonun gün sayfalarında yalnızca yüklenen derslikler var (uygulamalı sarı)", () =>
        {
            var gun = SablonGunSayfasi(OrnekDerslikler(3));
            var basliklar = SalonBasliklari(gun.Sayfa);
            Dogrula(basliklar.SequenceEqual(new[] { "T4-501", "LAB-1", "Z-101" }),
                $"Salon sütunları beklenen gibi değil: {string.Join(", ", basliklar)}");
            Dogrula(Dolgu(gun.Sayfa, 11) == "FFFFF2CC", "Uygulamalı derslik sarı değil.");
            Dogrula(Dolgu(gun.Sayfa, 10) == null && Dolgu(gun.Sayfa, 12) == null, "Normal derslik boyalı.");
            Dogrula(Equals(gun.Sayfa.Cells[2, 10].Value, 60d) || Equals(gun.Sayfa.Cells[2, 10].Value, 60),
                "Kapasite satırı yazılmadı.");
            gun.Paket.Dispose();
            File.Delete(gun.Yol);
        });

        t.Test("Şablondakinden çok derslik varsa ek sütunlar da biçimli ekleniyor", () =>
        {
            var gun = SablonGunSayfasi(OrnekDerslikler(40));
            var basliklar = SalonBasliklari(gun.Sayfa);
            Dogrula(basliklar.Count == 40, $"Salon sütunu sayısı {basliklar.Count}, beklenen 40.");
            int son = 10 + 39;
            Dogrula(gun.Sayfa.Cells[3, son].Style.Border.Left.Style != OfficeOpenXml.Style.ExcelBorderStyle.None,
                "Eklenen sütunlarda kenarlık yok.");
            gun.Paket.Dispose();
            File.Delete(gun.Yol);
        });
    }

    /// <summary>T4 normal, uygulamalı lab ve T1-dışı normal derslik karışımı.</summary>
    private static List<Salon> OrnekDerslikler(int adet)
    {
        var liste = new List<Salon>
        {
            new() { SalonKodu = "T4-501", Kapasite = 60, SalonTipi = "NORMAL" },
            new() { SalonKodu = "LAB-1", Kapasite = 30, SalonTipi = "UYGULAMALI" },
            new() { SalonKodu = "Z-101", Kapasite = 45, SalonTipi = "NORMAL" },
        };
        for (int i = liste.Count; i < adet; i++)
            liste.Add(new Salon { SalonKodu = $"Z-{200 + i}", Kapasite = 40, SalonTipi = "NORMAL" });
        return liste.Take(adet).ToList();
    }

    private static (ExcelPackage Paket, ExcelWorksheet Sayfa, string Yol) SablonGunSayfasi(List<Salon> derslikler)
    {
        var gun = new DateTime(2026, 10, 26);
        var takvim = new Dictionary<DateTime, List<string>> { [gun] = new() { "09:00", "11:00" } };
        var dersler = new List<Ders> { new() { BolumAdi = "BİLGİSAYAR", DersKodu = "BIL214", DersAdi = "VERİ YAPILARI", Sube = "A", Kontenjan = 80, SinifSeviyesi = 2 } };
        string yol = Path.Combine(Path.GetTempPath(), $"sablon_derslik_{Guid.NewGuid():N}.xlsx");
        CekilmisDerslerExportHelper.Olustur(dersler, takvim, yol, out _, null, derslikler);
        var paket = new ExcelPackage(new FileInfo(yol));
        return (paket, paket.Workbook.Worksheets[$"{gun:dd.MM.yyyy}"] ?? throw new Exception("Gün sayfası yok."), yol);
    }

    /// <summary>Gün sayfasının 1. satırındaki salon başlıkları (J sütunundan itibaren).</summary>
    private static List<string> SalonBasliklari(ExcelWorksheet ws)
    {
        var liste = new List<string>();
        for (int c = 10; c <= ws.Dimension.End.Column; c++)
        {
            string v = ws.Cells[1, c].Text?.Trim();
            if (!string.IsNullOrEmpty(v)) liste.Add(v);
        }
        return liste;
    }

    private static string Dolgu(ExcelWorksheet ws, int kolon)
    {
        var f = ws.Cells[3, kolon].Style.Fill;
        return f.PatternType == OfficeOpenXml.Style.ExcelFillStyle.None ? null : f.BackgroundColor.Rgb;
    }

    /// <summary>Okuyucunun beklediği düzende küçük bir derslik Excel'i üretir.</summary>
    private static string DerslikExceliYaz()
    {
        string yol = Path.Combine(Path.GetTempPath(), $"derslik_{Guid.NewGuid():N}.xlsx");
        using var p = new ExcelPackage();
        var ws = p.Workbook.Worksheets.Add("Derslikler");
        ws.Cells[1, 1].Value = "DERSLİK";
        ws.Cells[1, 2].Value = "SINAV DÜZEYİNDEKİ ÖĞR. SAYISI";
        ws.Cells[1, 3].Value = "DERS DÜZEYİNDEKİ ÖĞR. SAYISI";
        ws.Cells[2, 1].Value = "A-101"; ws.Cells[2, 2].Value = "62(31)"; ws.Cells[2, 3].Value = 155;
        ws.Cells[3, 1].Value = "A-102"; ws.Cells[3, 2].Value = 48;
        ws.Cells[4, 1].Value = "BİLGİSAYAR LABARATUVARLARI";
        ws.Cells[4, 1, 4, 3].Merge = true;
        ws.Cells[5, 1].Value = "LAB-1"; ws.Cells[5, 2].Value = 30;
        ws.Cells[6, 1].Value = "TOPLAM"; ws.Cells[6, 2].Value = 140;
        p.SaveAs(new FileInfo(yol));
        return yol;
    }
}
