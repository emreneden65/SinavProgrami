using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A5 — geçiş: eski arayüze (MainForm) bağlı testlerin yeni arayüzdeki karşılıkları ve
/// yedek olarak duran eski arayüzle oturum uyumu. Eski arayüz kaldırıldığında (A6) da bu testler kalır;
/// yalnızca eski formu açan son test o aşamada çıkarılır.
/// </summary>
static class YeniArayuzGecisTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A5 — geçiş (eski arayüz testlerinin karşılıkları, oturum uyumu)");

        // Madde 7 karşılığı: Final / gözetmenli Excel başlığı arayüzde seçilen dönemden oluşur
        t.Test("Final ve taslak Excel'inin başlığı seçilen öğretim yılı, dönem ve sınav türünden oluşuyor", () =>
        {
            string yol = Path.Combine(Path.GetTempPath(), $"gecis_baslik_{Guid.NewGuid():N}.xlsx");
            try
            {
                var durum = YeniArayuzSalonTestleri.SalonluYuklu();
                durum.OgretimYili = 2027;
                durum.Yariyil = Yariyil.Bahar;
                durum.SinavTipi = SinavDonemTipi.Final;
                var takvim = new TakvimServisi(durum);
                var final = new FinalServisi(durum, new HocaEslemesi(), takvim);
                final.MatrisExceliYaz(yol);

                string baslik = takvim.ProgramBasligi();
                using var paket = new ExcelPackage(new FileInfo(yol));
                var metinler = paket.Workbook.Worksheets.Where(w => w.Dimension != null)
                    .SelectMany(w => w.Cells[w.Dimension.Address].Select(c => (Sayfa: w.Name, Metin: c.Text ?? ""))).ToList();
                Dogrula(baslik.StartsWith("2027-2028 ÖĞRETİM YILI BAHAR DÖNEMİ") && baslik.Contains("FİNAL"), baslik);
                Dogrula(metinler.Count(m => m.Metin == baslik && m.Sayfa.StartsWith("Sınav Programı Formu")) >= 2, "Form başlıkları seçimi göstermiyor.");
                Dogrula(metinler.Any(m => m.Sayfa == GozetmenliSablonOkuyucu.SayfaAdi && m.Metin.Contains(baslik)), "Slot planı başlığı seçimi göstermiyor.");
            }
            finally { if (File.Exists(yol)) File.Delete(yol); }
        });

        // Madde 9 karşılığı: Excel'de saat 13.00, uygulamada 13:00
        t.Test("Salonlu Excel geri yüklenince uygulamadaki saatler 13:00 biçiminde (Excel'de 13.00)", () =>
        {
            string yol = Path.Combine(Path.GetTempPath(), $"gecis_saat_{Guid.NewGuid():N}.xlsx");
            try
            {
                var durum = YeniArayuzSalonTestleri.SalonluYuklu();
                var salon = new SalonServisi(durum, new HocaEslemesi(), new TakvimServisi(durum));
                salon.SalonluExcelYaz(yol);
                using (var paket = new ExcelPackage(new FileInfo(yol)))
                    Dogrula(paket.Workbook.Worksheets.Any(w => w.Dimension != null &&
                                w.Cells[w.Dimension.Address].Any(c => c.Text == "13.00")), "Excel'de 13.00 yazımı yok.");
                var okuma = SalonServisi.SalonluExcelOnizle(yol);
                salon.SalonluExcelUygula(okuma, yol);
                Dogrula(durum.SonSonuclar.All(a => System.Text.RegularExpressions.Regex.IsMatch(a.Saat ?? "", @"^\d\d:\d\d$")) &&
                        durum.SonSonuclar.Any(a => a.Saat == "13:00"), string.Join(",", durum.SonSonuclar.Select(a => a.Saat).Distinct()));
                var matris = salon.Matris();
                Dogrula(matris.Satirlar.All(s => !s.Saat.Contains('.')), "Matriste noktalı saat var.");
            }
            finally { if (File.Exists(yol)) File.Delete(yol); }
        });

        // Madde 3 karşılığı: derslikler veritabanına değil oturum kaydına gider
        t.Test("Derslik listesi uygulanınca veritabanı değişmiyor; oturum kaydına yazılıp geri geliyor", () =>
        {
            DatabaseHelper.VeritabaniniHazirla();
            long once = new FileInfo(DatabaseHelper.VeritabaniYolu).Length;
            var durum = new CalismaDurumu();
            new DerslikServisi(durum).Uygula(new List<Salon> { new() { SalonKodu = "T4-999", Kapasite = 33 } });
            var yonetici = new OturumYoneticisi(durum, new IsYurutucu());
            OturumKaydi.Yaz(OturumKaydi.Serilestir(yonetici.Topla()));
            var geri = YeniArayuzSablonTestleri.Yuklu(File.ReadAllText(OturumKaydi.DosyaYolu));
            Dogrula(geri.Derslikler.Count == 1 && geri.Derslikler[0].SalonKodu == "T4-999", "Derslik oturumdan geri gelmedi.");
            Dogrula(new FileInfo(DatabaseHelper.VeritabaniYolu).Length == once, "Veritabanı değişti.");
        });

        t.Test("Veri klasörü: anahtarsız açılış gerçek klasörü kullanıyor (deneme kipi kapalı)", () =>
        {
            string onceki = OturumKaydi.Klasor;
            try
            {
                VeriKlasoru.Kur(deneme: false);
                Dogrula(!VeriKlasoru.DenemeModu && OturumKaydi.Klasor == onceki, $"Deneme kipi {VeriKlasoru.DenemeModu}, klasör {OturumKaydi.Klasor}");
                Dogrula(VeriKlasoru.DenemeKlasoru == Path.Combine(VeriKlasoru.GercekKlasor, "Deneme"), VeriKlasoru.DenemeKlasoru);
            }
            finally { OturumKaydi.Klasor = onceki; }
        });

        // Yedek eski arayüz: yeni arayüzün kaydettiği oturum eski arayüzde açılabilmeli (ve tersi)
        t.Test("Yeni arayüzün kaydettiği oturum eski arayüzde açılıyor; eski arayüz kaydedince yeni arayüz aynı veriyi okuyor", () =>
        {
            var durum = YeniArayuzSalonTestleri.SalonluYuklu();
            durum.OgretimYili = 2026;
            durum.Yariyil = Yariyil.Guz;
            var goz = new GozetmenServisi(durum);
            goz.AtamaTamamlandi(goz.GozetmenleriAta());
            durum.SonDosyalar[SonDosyaTurleri.SalonluExcel] = @"C:\x\salonlu.xlsx";
            var yeniKayit = new OturumYoneticisi(durum, new IsYurutucu()).Topla();
            OturumKaydi.Yaz(OturumKaydi.Serilestir(yeniKayit));

            using (var form = FormuAc())
            {
                Dogrula(Alan<List<Ders>>(form, "_dersListesi").Count == durum.Dersler.Count, "Eski arayüz ders listesini okumadı.");
                Dogrula(Alan<List<SinavAtama>>(form, "_sablonSonuclari").Count == durum.SablonSonuclari.Count, "Eski arayüz şablonu okumadı.");
                Dogrula(Alan<List<SinavAtama>>(form, "_sonSonuclar").Count == durum.SonSonuclar.Count, "Eski arayüz salon sonuçlarını okumadı.");
                Dogrula(Alan<List<Gozetmen>>(form, "_gozetmenListesi").Count == durum.GozetmenListesi.Count, "Eski arayüz gözetmenleri okumadı.");
                Dogrula(Alan<int?>(form, "_ogretimYili") == 2026 && Alan<Yariyil?>(form, "_yariyil") == Yariyil.Guz, "Eski arayüz dönemi okumadı.");
                var eskiKilit = new AdimKilitleri().SozlugeCevir().Keys.ToDictionary(a => a, a => Alan<Button>(form, a).Enabled);
                Dogrula(eskiKilit.SequenceEqual(durum.Kilitler.SozlugeCevir()), "Eski arayüzün adım düğmeleri yeni kilitlerden farklı.");
                form.Close();   // eski arayüz kapanırken oturumu kendi biçimiyle kaydeder
            }

            var geri = YeniArayuzSablonTestleri.Yuklu(File.ReadAllText(OturumKaydi.DosyaYolu));
            var geriKayit = new OturumYoneticisi(geri, new IsYurutucu()).Topla();
            string Veri(OturumDurumu d) => OturumKaydi.Serilestir(new OturumDurumu
            {
                Derslikler = d.Derslikler, Dersler = d.Dersler, Takvim = d.Takvim, SablonSonuclari = d.SablonSonuclari,
                SablonIntNotlari = d.SablonIntNotlari, GunSalonSatirlari = d.GunSalonSatirlari, SonSonuclar = d.SonSonuclar,
                Gozetmenler = d.Gozetmenler, Butonlar = d.Butonlar, HataOranlari = d.HataOranlari, UygulamaliDersler = d.UygulamaliDersler,
                OgretimYili = d.OgretimYili, Yariyil = d.Yariyil, SinavTipi = d.SinavTipi
            });
            Dogrula(Veri(geriKayit) == Veri(yeniKayit), "Eski arayüzden geçen oturumda veri değişti.");
        });
    }
}
