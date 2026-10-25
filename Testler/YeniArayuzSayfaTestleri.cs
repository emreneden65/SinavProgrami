using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using SinavProgrami.UI;
using SinavProgrami.UI.Layout;
using SinavProgrami.UI.Pages;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A1 — sayfalar pencere açılmadan (bUnit ile) çizilir; kullanıcının yazması,
/// tıklaması, kutudan çıkması taklit edilip ekranda ve çalışma durumunda sonuç denetlenir.
/// </summary>
static class YeniArayuzSayfaTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A1 — sayfalar (pencere açmadan çizim)");

        t.Test("Genel Bakış: boş durumda sıradaki adım 'Dönem ve Takvim', 7 adım kartı, 5 eksik", () => Baglamla(_ => { }, (ctx, _) =>
        {
            var sayfa = ctx.Render<GenelBakis>();
            Dogrula(sayfa.FindAll(".adim-karti").Count == 7, $"{sayfa.FindAll(".adim-karti").Count} adım kartı.");
            Dogrula(sayfa.Find(".siradaki-adi").TextContent.Trim() == "Dönem ve Takvim", sayfa.Find(".siradaki-adi").TextContent);
            Dogrula(sayfa.FindAll(".eksik-maddesi").Count == 5, $"{sayfa.FindAll(".eksik-maddesi").Count} eksik.");
        }));

        t.Test("Genel Bakış: veri değişince sayfa kendiliğinden yenileniyor", () => Baglamla(_ => { }, (ctx, durum) =>
        {
            var sayfa = ctx.Render<GenelBakis>();
            durum.Derslikler = new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 } };
            durum.Bildir();
            sayfa.WaitForAssertion(() => Dogrula(sayfa.FindAll(".eksik-maddesi").Count == 4, "Derslik eksiği kalkmadı."));
            Dogrula(sayfa.Markup.Contains("1 derslik (1 normal, 0 uygulamalı) · kapasite 60"), "Derslik özeti görünmüyor.");
        }));

        t.Test("Dönem: geçersiz yıl kırmızı; geçerli yazım kutudan çıkınca 2031/2032'ye düzeliyor", () => Baglamla(Takvimli, (ctx, durum) =>
        {
            var sayfa = ctx.Render<DonemTakvim>();
            sayfa.Find(".yil-alani input").Input("2031/2035");
            Dogrula(durum.OgretimYili == null && sayfa.FindAll(".yil-alani .mud-input-error").Count > 0, "Geçersiz yıl işaretlenmedi.");
            Dogrula(sayfa.Find(".baslik-onizlemesi").ClassList.Contains("bos"), "Geçersiz yılda başlık önizlemesi var.");
            sayfa.Find(".yil-alani input").Input("2031-2032");
            Dogrula(durum.OgretimYili == 2031 && sayfa.FindAll(".yil-alani .mud-input-error").Count == 0, "Geçerli yıl kabul edilmedi.");
            sayfa.Find(".yil-alani input").Blur();
            Dogrula(durum.OgretimYiliMetni == "2031/2032", $"Kutu metni: {durum.OgretimYiliMetni}");
        }));

        t.Test("Dönem: öneri çipi yılı yazıyor; Bahar seçimi Excel başlığına yansıyor", () => Baglamla(Takvimli, (ctx, durum) =>
        {
            var sayfa = ctx.Render<DonemTakvim>();
            string yil = TakvimServisi.OgretimYiliSecenekleri()[1];
            Dogrula(sayfa.FindAll(".oneri-cipi").Count == 4, $"{sayfa.FindAll(".oneri-cipi").Count} öneri çipi.");
            sayfa.FindAll(".oneri-cipi").First(c => c.TextContent.Trim() == yil).Click();
            Dogrula(durum.OgretimYiliMetni == yil && durum.OgretimYili == AkademikDonem.YilCoz(yil), "Öneri çipi yılı yazmadı.");
            sayfa.FindAll(".mud-toggle-item").First(b => b.TextContent.Trim() == "Bahar").Click();
            Dogrula(durum.Yariyil == Yariyil.Bahar, "Bahar seçilmedi.");
            string baslik = sayfa.Find(".baslik-onizlemesi .onizleme-metni").TextContent.Trim();
            Dogrula(baslik == AkademikDonem.ProgramBasligi(durum.OgretimYili.Value, Yariyil.Bahar, SinavDonemTipi.Vize), $"Başlık: {baslik}");
        }));

        t.Test("Dönem: gün tablosu takvimle aynı; saat çipine tıklayınca saat açılıp kapanıyor", () => Baglamla(Takvimli, (ctx, durum) =>
        {
            var sayfa = ctx.Render<DonemTakvim>();
            Dogrula(sayfa.FindAll(".gun-tablosu tbody tr").Count == durum.TakvimSlotlari.Count, "Tablo satırı sayısı takvimle aynı değil.");
            var gun = durum.TakvimSlotlari.Keys.Min();
            AngleSharp.Dom.IElement Cip() => sayfa.FindAll(".gun-tablosu tbody tr")[0].QuerySelectorAll(".saat-cipi")
                .First(c => c.TextContent.Trim() == "10:00");
            Cip().Click();
            Dogrula(durum.TakvimSlotlari[gun].Contains("10:00") && Cip().ClassList.Contains("acik"), "10:00 eklenmedi.");
            Cip().Click();
            Dogrula(!durum.TakvimSlotlari[gun].Contains("10:00") && !Cip().ClassList.Contains("acik"), "10:00 kaldırılmadı.");
        }));

        t.Test("Dönem: takvimde boş güne tıklayınca sınav günü oluyor, tekrar tıklayınca çıkıyor", () => Baglamla(Takvimli, (ctx, durum) =>
        {
            var sayfa = ctx.Render<DonemTakvim>();
            int ilk = durum.TakvimSlotlari.Count;
            AngleSharp.Dom.IElement Gun() => sayfa.FindAll(".takvim-ayi")
                .First(a => a.QuerySelector(".takvim-ay-adi").TextContent.StartsWith("Ekim 2026"))
                .QuerySelectorAll(".takvim-gunu").First(g => g.TextContent.Trim() == "23");
            Gun().Click();
            Dogrula(durum.TakvimSlotlari.ContainsKey(new DateTime(2026, 10, 23)) && Gun().ClassList.Contains("sinav"), "23 Ekim eklenmedi.");
            Dogrula(sayfa.FindAll(".gun-tablosu tbody tr").Count == ilk + 1, "Tabloya satır eklenmedi.");
            Gun().Click();
            Dogrula(!durum.TakvimSlotlari.ContainsKey(new DateTime(2026, 10, 23)) && durum.TakvimSlotlari.Count == ilk, "23 Ekim çıkarılmadı.");
        }));

        t.Test("Adım menüsü: 7 adım; tamam / sıradaki / kilitli durumları çalışma durumuna göre", () => Baglamla(durum =>
        {
            Takvimli(durum);
            durum.OgretimYili = 2026;
            durum.Yariyil = Yariyil.Guz;
            durum.Derslikler = new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 } };
        }, (ctx, _) =>
        {
            var menu = ctx.Render<AdimMenusu>();
            var adimlar = menu.FindAll("a.adim");
            Dogrula(adimlar.Count == 7, $"{adimlar.Count} adım.");
            Dogrula(adimlar[0].ClassList.Contains("tamam") && adimlar[1].ClassList.Contains("tamam"), "1–2. adım tamam değil.");
            Dogrula(adimlar[2].ClassList.Contains("siradaki"), "3. adım sıradaki değil.");
            Dogrula(adimlar[6].ClassList.Contains("kilitli"), "Final kilitli değil.");
        }));

        t.Test("Üst çubuk: dönem seçilmemişse uyarı; tema düğmesi koyu temaya geçip tercihi kaydediyor", () => Baglamla(Takvimli, (ctx, _) =>
        {
            File.Delete(AyarlarDeposu.DosyaYolu);
            var cubuk = ctx.Render<UstCubuk>();
            Dogrula(cubuk.Find(".donem-ozeti").ClassList.Contains("eksik") &&
                    cubuk.Find(".donem-ozeti").TextContent.Contains("Öğretim dönemi seçilmedi"), "Dönem uyarısı yok.");
            cubuk.Find("#tema-dugmesi").Click();
            var ayarlar = ctx.Services.GetRequiredService<AyarlarDeposu>();
            Dogrula(ayarlar.Ayarlar.KoyuTema && File.ReadAllText(AyarlarDeposu.DosyaYolu).Contains("\"KoyuTema\": true"), "Koyu tema kaydedilmedi.");
            var yeniDepo = new AyarlarDeposu();
            yeniDepo.Yukle();
            Dogrula(yeniDepo.Ayarlar.KoyuTema, "Kayıtlı tema geri okunmadı.");
            File.Delete(AyarlarDeposu.DosyaYolu);
        }));
    }

    /// <summary>26.10.2026 Pazartesi'den itibaren 7 günlük vize takvimi (09/11/13/15).</summary>
    private static void Takvimli(CalismaDurumu durum)
    {
        foreach (var g in SinavTipi.GunleriUret(new DateTime(2026, 10, 26), SinavDonemTipi.Vize))
            durum.TakvimSlotlari[g] = new List<string> { "09:00", "11:00", "13:00", "15:00" };
        durum.BaslangicTarihi = new DateTime(2026, 10, 26);
    }

    /// <summary>Arayüz servisleriyle bir bUnit bağlamı kurar; çalışma durumunu testin hazırladığı nesne olarak verir.</summary>
    private static void Baglamla(Action<CalismaDurumu> hazirla, Action<BunitContext, CalismaDurumu> govde)
    {
        var ctx = new BunitContext();
        try
        {
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            var durum = new CalismaDurumu();
            hazirla(durum);
            ctx.Services.AddSingleton(durum);
            ctx.Services.ArayuzServisleriniEkle();
            ctx.Render<MudPopoverProvider>();   // açılır liste / ipucu bileşenleri için
            govde(ctx, durum);
        }
        finally
        {
            // MudBlazor servisleri yalnızca asenkron kapatılabilir
            ctx.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
