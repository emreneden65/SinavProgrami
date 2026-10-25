using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using OfficeOpenXml;
using SinavProgrami.Models;
using SinavProgrami.Services;
using SinavProgrami.UI;
using SinavProgrami.UI.Components;
using SinavProgrami.UI.Components.DersSekmeleri;
using SinavProgrami.UI.Pages;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A2 — Derslikler ve Dersler sayfaları pencere açılmadan (bUnit) çizilir;
/// dosya seçimi sahte servisle, onaylar gerçek diyalog düğmeleriyle yapılır.
/// </summary>
static class YeniArayuzDersSayfaTestleri
{
    private const string Bil = "BİLGİSAYAR MÜHENDİSLİĞİ";

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A2 — Derslikler ve Dersler sayfaları");

        t.Test("Hücre düzenleyici: tıkla → yaz → Enter kaydediyor; Esc vazgeçiyor; hata bildirimi gösterilip eski değer kalıyor", () => Baglamla(null, (ctx, _, _) =>
        {
            var kaydedilen = new List<string>();
            string donecekHata = null;
            var hucre = ctx.Render<HucreDuzenleyici>(p => p
                .Add(x => x.Deger, "80")
                .Add(x => x.Sayi, true)
                .Add(x => x.Kaydet, v => { kaydedilen.Add(v); return donecekHata; }));

            hucre.Find(".hucre-degeri").Click();
            hucre.Find("input").Input("95");
            hucre.Find("input").KeyDown("Enter");
            Dogrula(kaydedilen.SequenceEqual(new[] { "95" }) && hucre.FindAll("input").Count == 0, "Enter ile kaydedilmedi.");

            hucre.Find(".hucre-degeri").Click();
            hucre.Find("input").Input("12");
            hucre.Find("input").KeyDown("Escape");
            Dogrula(kaydedilen.Count == 1 && hucre.Find(".hucre-degeri").TextContent.Trim() == "80", "Esc değeri kaydetti.");

            donecekHata = "Geçersiz değer";
            hucre.Find(".hucre-degeri").Click();
            hucre.Find("input").Input("abc");
            hucre.Find("input").Blur();
            var bildirim = ctx.Services.GetRequiredService<ISnackbar>();
            Dogrula(kaydedilen.Last() == "abc" && bildirim.ShownSnackbars.Any(s => s.Message == "Geçersiz değer"), "Hata bildirimi gösterilmedi.");
            Dogrula(hucre.Find(".hucre-degeri").TextContent.Trim() == "80", "Hatalı değer ekranda kaldı.");
        }));

        t.Test("Derslikler: boşken yükleme çağrısı; doluyken 4 özet kutusu, tablo ve tür filtresi", () =>
        {
            Baglamla(null, (ctx, _, _) =>
            {
                var bos = ctx.Render<Derslikler>();
                Dogrula(bos.Markup.Contains("Henüz derslik listesi yüklenmedi"), "Boş durum gösterilmedi.");
            });
            Baglamla(d => d.Derslikler = new List<Salon>
            {
                new() { SalonKodu = "T4-204", Kapasite = 60 }, new() { SalonKodu = "T1-101", Kapasite = 30 },
                new() { SalonKodu = "T4-LAB1", Kapasite = 25, SalonTipi = "UYGULAMALI" },
            }, (ctx, _, _) =>
            {
                var sayfa = ctx.Render<Derslikler>();
                var degerler = sayfa.FindAll(".ozet-degeri").Select(e => e.TextContent.Trim()).ToList();
                Dogrula(degerler.SequenceEqual(new[] { "3", "2", "1", "115" }), $"Özet: {string.Join(", ", degerler)}");
                Dogrula(sayfa.FindAll(".tablo-kabi tbody tr").Count == 3, $"{sayfa.FindAll(".tablo-kabi tbody tr").Count} tablo satırı.");
                Dogrula(sayfa.FindAll(".tablo-kabi tbody tr")[0].TextContent.Contains("T4-LAB1"), "Varsayılan sıra kapasiteye göre artan değil.");
                sayfa.FindAll(".mud-toggle-item").First(b => b.TextContent.Trim() == "Uygulamalı").Click();
                Dogrula(sayfa.FindAll(".tablo-kabi tbody tr").Count == 1 && sayfa.Find(".tablo-kabi tbody tr").TextContent.Contains("T4-LAB1"),
                    "Tür filtresi çalışmadı.");
            });
        });

        t.Test("Derslikler: dosya seç → özet diyaloğu → 'Listeyi değiştir' ile liste uygulanıyor", () =>
        {
            string yol = DerslikExceli();
            try
            {
                Baglamla(null, (ctx, durum, dosya) =>
                {
                    dosya.AcilacakYol = yol;
                    var diyaloglar = ctx.Render<MudDialogProvider>();
                    var sayfa = ctx.Render<Derslikler>();
                    sayfa.FindAll("button").First(b => b.TextContent.Contains("Derslik listesi yükle")).Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Derslik Listesi — Onay"), "Onay diyaloğu açılmadı."),
                        TimeSpan.FromSeconds(10));
                    Dogrula(diyaloglar.Markup.Contains("Eklenecek (2)") && diyaloglar.Markup.Contains("1 uygulamalı"), "Özet yanlış.");
                    diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Listeyi değiştir").Click();
                    sayfa.WaitForAssertion(() => Dogrula(durum.Derslikler.Count == 2, $"{durum.Derslikler.Count} derslik uygulandı."), TimeSpan.FromSeconds(10));
                    Dogrula(durum.SonDosyalar[SonDosyaTurleri.DerslikListesi] == yol, "Son dosya hatırlanmadı.");
                });
            }
            finally { File.Delete(yol); }
        });

        t.Test("Dersler: dosya seç → bölüm özeti onayı → liste yükleniyor, atlanan satırlar listeleniyor", () =>
        {
            string yol = SinavListesiExceli();
            try
            {
                Baglamla(null, (ctx, durum, dosya) =>
                {
                    dosya.AcilacakYol = yol;
                    var diyaloglar = ctx.Render<MudDialogProvider>();
                    var sayfa = ctx.Render<Dersler>();
                    sayfa.FindAll("button").First(b => b.TextContent.Trim() == "Sınav listesi yükle").Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Ders Listesi Yükleme Onayı"), "Onay diyaloğu açılmadı."),
                        TimeSpan.FromSeconds(10));
                    Dogrula(diyaloglar.Markup.Contains("3 şube") && diyaloglar.Markup.Contains("MAKİNE MÜHENDİSLİĞİ"), "Bölüm özeti yanlış.");
                    diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Listeyi yükle").Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Yükleme Notları"), "Atlanan satırlar gösterilmedi."),
                        TimeSpan.FromSeconds(10));
                    Dogrula(durum.Dersler.Count == 3 && durum.Kilitler.OtomatikDagit, "Liste uygulanmadı.");
                    Dogrula(diyaloglar.Markup.Contains("Ders kodu boş"), "Atlanan satır metni yok.");
                });
            }
            finally { File.Delete(yol); }
        });

        t.Test("Ders listesi: satırlar çiziliyor, arama süzüyor, hücreden kontenjan değişiyor, seçilen satır onayla siliniyor", () => Baglamla(DersliDurum, (ctx, durum, _) =>
        {
            var diyaloglar = ctx.Render<MudDialogProvider>();
            var sekme = ctx.Render<DersListesiSekmesi>();
            Dogrula(sekme.FindAll("tbody tr").Count(r => r.QuerySelector(".hucre-degeri") != null) == 3, "3 satır çizilmedi.");
            Dogrula(sekme.Find(".arac-notu").TextContent.Contains("3 / 3"), sekme.Find(".arac-notu").TextContent);

            sekme.Find(".arama-kutusu input").Input("fizik");
            sekme.WaitForAssertion(() => Dogrula(sekme.Find(".arac-notu").TextContent.Contains("2 / 3"), sekme.Find(".arac-notu").TextContent),
                TimeSpan.FromSeconds(5));

            var fizikA = durum.Dersler.First(d => d.Sube == "A" && d.DersAdi == "FİZİK I");
            // Arama kutusunun gecikmeli (debounce) yeniden çizimi tıklamayla yarışabilir: düzenleyici açılana kadar yeniden tıklanır
            sekme.WaitForAssertion(() =>
            {
                if (sekme.FindAll("input.hucre-kutusu").Count == 0)
                    sekme.FindAll("tbody tr").First(r => r.TextContent.Contains("FİZİK I") && r.TextContent.Contains("80"))
                         .QuerySelectorAll(".hucre-degeri").Last().Click();
                Dogrula(sekme.FindAll("input.hucre-kutusu").Count == 1, "Kontenjan düzenleyicisi açılmadı.");
            }, TimeSpan.FromSeconds(5));
            sekme.Find("input.hucre-kutusu").Input("85");
            sekme.Find("input.hucre-kutusu").KeyDown("Enter");
            Dogrula(fizikA.Kontenjan == 85, $"Kontenjan {fizikA.Kontenjan}.");

            sekme.FindAll("tbody tr").First(r => r.TextContent.Contains("85")).QuerySelector("input[type=checkbox]").Change(true);
            sekme.FindAll("button").First(b => b.TextContent.Contains("Seçilenleri sil")).Click();
            diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("silinecek"), "Silme onayı açılmadı."), TimeSpan.FromSeconds(5));
            diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Sil").Click();
            sekme.WaitForAssertion(() => Dogrula(!durum.Dersler.Contains(fizikA) && durum.Dersler.Count == 2, "Satır silinmedi."), TimeSpan.FromSeconds(5));
        }));

        t.Test("Uygulamalı / hata oranı / ortak ders sekmeleri çalışma durumunu değiştiriyor", () => Baglamla(DersliDurum, (ctx, durum, _) =>
        {
            var uyg = ctx.Render<UygulamaliSekmesi>();
            int adaySatiri = uyg.FindAll("tbody tr").Count(r => r.QuerySelector("td") != null);   // sanal kaydırmanın boşluk satırları hariç
            Dogrula(adaySatiri == 2, $"{adaySatiri} uygulamalı aday satırı.");
            uyg.FindAll("tbody tr").First(r => r.TextContent.Contains("FİZİK I")).QuerySelector("input").Change(true);
            Dogrula(durum.UygulamaliDersler.Contains("FİZİK I"), "Uygulamalı işareti kaydedilmedi.");

            var hata = ctx.Render<HataOraniSekmesi>();
            var bilSatiri = hata.FindAll("tbody tr").First(r => r.TextContent.Contains(Bil));
            bilSatiri.QuerySelector(".hucre-degeri").Click();
            hata.Find("input.hucre-kutusu").Input("10");
            hata.Find("input.hucre-kutusu").KeyDown("Enter");
            Dogrula(durum.BolumHataOranlari.TryGetValue(Bil, out var oran) && oran == 10, "Hata oranı kaydedilmedi.");
            Dogrula(hata.FindAll("tbody tr").First(r => r.TextContent.Contains(Bil)).TextContent.Contains("90 kişi"), "Gelecek kişi hesabı görünmüyor.");

            var ortak = ctx.Render<OrtakDerslerSekmesi>();
            Dogrula(ortak.FindAll("tbody input[type=checkbox]").All(c => c.HasAttribute("disabled")), "Otomatik modda kutular açık.");
            ortak.Find(".mod-satiri input").Change(true);
            Dogrula(durum.OrtakSecimYapildi && durum.ManuelOrtakDersAdlari.SetEquals(new[] { "FİZİK I" }), "Elle mod önerilerle açılmadı.");
            Dogrula(ortak.FindAll("tbody input[type=checkbox]").All(c => !c.HasAttribute("disabled")), "Elle modda kutular kapalı.");
        }));
    }

    /// <summary>İki bölümde okutulan FİZİK I (BİL'de iki şube) + tek bölümlü MUKAVEMET.</summary>
    private static void DersliDurum(CalismaDurumu d)
    {
        d.Dersler.AddRange(new[]
        {
            new Ders { DersKodu = "FIZ101", DersAdi = "FİZİK I", BolumAdi = Bil, Sube = "A", SinifSeviyesi = 1, Kontenjan = 80 },
            new Ders { DersKodu = "FIZ101", DersAdi = "FİZİK I", BolumAdi = "MAKİNE MÜHENDİSLİĞİ", Sube = "A", SinifSeviyesi = 1, Kontenjan = 60 },
            new Ders { DersKodu = "MAK201", DersAdi = "MUKAVEMET", BolumAdi = "MAKİNE MÜHENDİSLİĞİ", Sube = "A", SinifSeviyesi = 2, Kontenjan = 90 },
        });
    }

    /// <summary>Sahte dosya servisi: "aç" isteğine testin verdiği yolu döner.</summary>
    private sealed class TestDosyaServisi : IDosyaServisi
    {
        public string AcilacakYol { get; set; }
        public Task<string> AcilacakDosyaSecAsync(DosyaIstegi istek) => Task.FromResult(AcilacakYol);
        public Task<string> KaydedilecekDosyaSecAsync(DosyaIstegi istek) => Task.FromResult<string>(null);
        public void DosyayiAc(string yol) { }
        public void KlasordeGoster(string yol) { }
    }

    private static void Baglamla(Action<CalismaDurumu> hazirla, Action<BunitContext, CalismaDurumu, TestDosyaServisi> govde)
    {
        var ctx = new BunitContext();
        try
        {
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            var durum = new CalismaDurumu();
            hazirla?.Invoke(durum);
            var dosya = new TestDosyaServisi();
            ctx.Services.AddSingleton(durum);
            ctx.Services.AddSingleton<IDosyaServisi>(dosya);
            ctx.Services.ArayuzServisleriniEkle();
            ctx.Render<MudPopoverProvider>();
            govde(ctx, durum, dosya);
        }
        finally
        {
            ctx.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static string DerslikExceli()
    {
        string yol = Path.Combine(Path.GetTempPath(), $"derslik_sayfa_{Guid.NewGuid():N}.xlsx");
        using var p = new ExcelPackage();
        var ws = p.Workbook.Worksheets.Add("Derslikler");
        ws.Cells[1, 1].Value = "DERSLİK";
        ws.Cells[1, 2].Value = "SINAV DÜZEYİNDEKİ ÖĞR. SAYISI";
        ws.Cells[2, 1].Value = "T4-301"; ws.Cells[2, 2].Value = 60;
        ws.Cells[3, 1].Value = "TEKNİK RESİM SINIFLARI";
        ws.Cells[3, 1, 3, 2].Merge = true;
        ws.Cells[4, 1].Value = "T4-302"; ws.Cells[4, 2].Value = 40;
        p.SaveAs(new FileInfo(yol));
        return yol;
    }

    private static string SinavListesiExceli()
    {
        string yol = Path.Combine(Path.GetTempPath(), $"sinav_sayfa_{Guid.NewGuid():N}.xlsx");
        using var p = new ExcelPackage();
        var ws = p.Workbook.Worksheets.Add("Liste");
        string[] basliklar = { "Bölüm", "Ünvanı Adı Soyadı", "DERSİN KODU", "VERİLEN DERSİN ADI", "Dersin Şubesi", "Öğrenci Sayısı" };
        for (int c = 0; c < basliklar.Length; c++) ws.Cells[1, c + 1].Value = basliklar[c];
        object[][] satirlar =
        {
            new object[] { "Bilgisayar Mühendisliği", "Dr. A", "FIZ101", "Fizik I", "A", 80 },
            new object[] { "Makine Mühendisliği", "Dr. B", "FIZ101", "Fizik I", "A", 60 },
            new object[] { "Makine Mühendisliği", "Dr. C", "MAK201", "Mukavemet", "A", 90 },
            new object[] { "Makine Mühendisliği", "Dr. D", "", "Kodsuz", "A", 10 },
        };
        for (int r = 0; r < satirlar.Length; r++)
            for (int c = 0; c < satirlar[r].Length; c++)
                ws.Cells[r + 2, c + 1].Value = satirlar[r][c];
        p.SaveAs(new FileInfo(yol));
        return yol;
    }
}
