using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using SinavProgrami.UI;
using SinavProgrami.UI.Components.Sablon;
using SinavProgrami.UI.Pages;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A3 — Şablon sayfası pencere açılmadan (bUnit) çizilir. Izgaradaki fare / klavye olayları
/// gerçekte app.js'ten gelir; burada aynı .NET uçları (Tikla, CiftTikla, Tus) doğrudan çağrılır.
/// </summary>
static class YeniArayuzSablonSayfaTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A3 — Şablon sayfası");

        t.Test("Şablon yokken: boş durum; sınav listesi yüklenmeden otomatik şablon kilitli, geri yükleme açık", () => Baglamla(null, (ctx, _, _) =>
        {
            var sayfa = ctx.Render<Sablon>();
            Dogrula(sayfa.Markup.Contains("Henüz şablon yüklenmedi"), "Boş durum gösterilmedi.");
            var otomatik = sayfa.FindAll("button").First(b => b.TextContent.Contains("Otomatik şablon oluştur"));
            Dogrula(otomatik.HasAttribute("disabled") && sayfa.Markup.Contains("Önce 3. adımda sınav listesini yükleyin."), "Otomatik şablon kilitli değil.");
            Dogrula(!sayfa.FindAll("button").First(b => b.TextContent.Contains("Şablonu geri yükle")).HasAttribute("disabled"), "Geri yükleme kapalı.");
        }));

        t.Test("Şablon varken: gün bantları, satırlar, kapalı saatler, ihlalli hücreler ve türe göre ihlal düğmeleri", () => Baglamla(SablonluHazirla, (ctx, durum, _) =>
        {
            var sayfa = ctx.Render<Sablon>();
            Dogrula(sayfa.FindAll("th.gun-bandi").Count == 3, $"{sayfa.FindAll("th.gun-bandi").Count} gün bandı.");
            Dogrula(sayfa.Find("th.gun-bandi").TextContent.Contains("2 Kasım 2026 Pazartesi"), sayfa.Find("th.gun-bandi").TextContent);
            int satir = sayfa.FindAll(".sablon-izgarasi tbody tr").Count;
            Dogrula(satir == Izgara(ctx).Satirlar.Count && satir >= 12, $"{satir} satır.");
            Dogrula(sayfa.FindAll("td.kapali").Count > 0 && sayfa.FindAll("td.ihlal").Count > 0, "Kapalı / ihlalli hücre yok.");
            var turler = sayfa.FindAll("button.ihlal-turu").Select(b => b.TextContent).ToList();
            Dogrula(turler.Any(x => x.Contains("Ortak ders bütünlüğü")) && turler.Any(x => x.Contains("Sınıf-saat kuralı")), string.Join(" | ", turler));
            Dogrula(sayfa.Markup.Contains("Salonlar bu şablondan atandı") == (durum.SonSonuclar != null), "Salon ataması notu yanlış.");
        }));

        t.Test("İhlal türü gizlenince hücrelerden kalkıyor, tercih ayarlar.json'a kaydediliyor; tekrar tıklayınca geri geliyor", () => Baglamla(SablonluHazirla, (ctx, _, _) =>
        {
            var ayarlar = ctx.Services.GetRequiredService<AyarlarDeposu>();
            var sayfa = ctx.Render<Sablon>();
            int once = sayfa.FindAll("td.ihlal").Count;
            foreach (var ad in new[] { "Ortak ders bütünlüğü", "Sınıf-saat kuralı", "Bölüm içi çakışma", "Slot kapasite aşımı" })
                sayfa.FindAll("button.ihlal-turu").First(b => b.TextContent.Contains(ad)).Click();
            Dogrula(sayfa.FindAll("td.ihlal").Count == 0 && once > 0, $"Gizlenen türler hâlâ işaretli ({sayfa.FindAll("td.ihlal").Count}/{once}).");
            Dogrula(ayarlar.IhlalTuruGizliMi(IhlalTuru.OrtakDers) && File.ReadAllText(AyarlarDeposu.DosyaYolu).Contains("OrtakDers"), "Tercih kaydedilmedi.");
            Dogrula(sayfa.Find(".ihlal-paneli .kart-notu").TextContent.Contains("ihlal gizli"), sayfa.Find(".ihlal-paneli .kart-notu").TextContent);

            sayfa.FindAll("button.ihlal-turu").First(b => b.TextContent.Contains("Sınıf-saat kuralı")).Click();
            Dogrula(sayfa.FindAll("td.ihlal").Count > 0 && !ayarlar.IhlalTuruGizliMi(IhlalTuru.SinifSaat), "Tür yeniden gösterilmedi.");
            foreach (var tur in Enum.GetValues<IhlalTuru>()) ayarlar.IhlalTuruGizle(tur, false);
        }));

        t.Test("Ders hücresi: çift tıkla → yazarak süz → Enter ile ders taşınıyor; Delete çıkarıyor ve bildirim gösteriyor", () => Baglamla(SablonluHazirla, (ctx, durum, _) =>
        {
            var sayfa = ctx.Render<Sablon>();
            var izgara = sayfa.FindComponent<SablonIzgarasiBileseni>();
            var (r, c) = DersHucresi(ctx, "09:00", "BİL", 0, gun: 2);   // boş hücre
            sayfa.InvokeAsync(() => izgara.Instance.CiftTikla(r, c)).GetAwaiter().GetResult();
            var kutu = sayfa.Find(".ders-secici input");
            Dogrula(sayfa.FindAll(".ders-secici-listesi li").Any(li => li.TextContent.Contains("FİZİK I") && li.TextContent.Contains("taşınır")),
                "Seçici listesinde dersin şu anki yeri görünmüyor.");
            kutu.Input("fizik");
            Dogrula(sayfa.FindAll(".ders-secici-listesi li").Count == 1, $"{sayfa.FindAll(".ders-secici-listesi li").Count} öğe süzüldü.");
            kutu.KeyDown("Enter");
            var fizik = durum.SablonSonuclari.Where(a => a.DersAdi == "FİZİK I").ToList();
            Dogrula(fizik.Count == 4 && fizik.All(a => a.Gun == new DateTime(2026, 11, 4) && a.Saat == "09:00"), "FİZİK I taşınmadı.");
            Dogrula(sayfa.FindAll(".ders-secici").Count == 0 && durum.Durum.Metin.StartsWith("📌 'FİZİK I'"), durum.Durum.Metin);

            (r, c) = DersHucresi(ctx, "09:00", "BİL", 0, gun: 2);
            sayfa.InvokeAsync(() => izgara.Instance.Tikla(r, c, false, false)).GetAwaiter().GetResult();
            sayfa.InvokeAsync(() => izgara.Instance.Tus(r, c, "Delete", false, false)).GetAwaiter().GetResult();
            Dogrula(!durum.SablonSonuclari.Any(a => a.DersAdi == "FİZİK I"), "Delete dersi çıkarmadı.");
            var bildirim = ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.LastOrDefault();
            Dogrula(bildirim?.Message == "'FİZİK I' şablondan çıkarıldı.", bildirim?.Message);
        }));

        t.Test("Kontenjan hücresi: rakama basınca düzenleyici açılıyor, Enter kaydediyor; şubesi olmayan hücre ve harf düzenlemeyi açmıyor", () => Baglamla(SablonluHazirla, (ctx, durum, _) =>
        {
            var sayfa = ctx.Render<Sablon>();
            var izgara = sayfa.FindComponent<SablonIzgarasiBileseni>();
            var (r, c) = DersHucresi(ctx, "09:00", "BİL", 1, gun: 0);   // VERİ YAPILARI A/B
            sayfa.InvokeAsync(() => izgara.Instance.Tus(r, c + 2, "x", false, false)).GetAwaiter().GetResult();   // B sütunu, harf
            Dogrula(sayfa.FindAll(".sablon-izgarasi input").Count == 0, "Harfle kontenjan düzenleyicisi açıldı.");
            sayfa.InvokeAsync(() => izgara.Instance.Tus(r, c + 2, "6", false, false)).GetAwaiter().GetResult();
            var kutu = sayfa.Find(".sablon-izgarasi input");
            Dogrula(kutu.GetAttribute("value") == "6", $"Kutu '{kutu.GetAttribute("value")}' ile açıldı.");
            kutu.Input("65");
            kutu.KeyDown("Enter");
            Dogrula(durum.SablonSonuclari.First(a => a.DersAdi == "VERİ YAPILARI" && a.Sube == "B").Kontenjan == 65 &&
                    durum.Dersler.First(d => d.DersAdi == "VERİ YAPILARI" && d.Sube == "B").Kontenjan == 65, "Kontenjan kaydedilmedi.");

            sayfa.InvokeAsync(() => izgara.Instance.Tus(r, c + 3, "Enter", false, false)).GetAwaiter().GetResult();   // C sütunu: şube yok
            Dogrula(sayfa.FindAll(".sablon-izgarasi input").Count == 0, "Şubesi olmayan hücrede düzenleyici açıldı.");
            Dogrula(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Any(s => s.Message.StartsWith("Bu hücrede düzenlenecek bir şube yok")),
                "Bilgi bildirimi yok.");
        }));

        t.Test("Ok tuşları etkin hücreyi taşıyor, Shift ile dikdörtgen seçim; Ctrl + tekerlek yakınlaştırıyor (6–16)", () => Baglamla(SablonluHazirla, (ctx, _, _) =>
        {
            var ayarlar = ctx.Services.GetRequiredService<AyarlarDeposu>();
            var sayfa = ctx.Render<Sablon>();
            var izgara = sayfa.FindComponent<SablonIzgarasiBileseni>();
            sayfa.InvokeAsync(() => izgara.Instance.Tikla(0, 2, false, false)).GetAwaiter().GetResult();
            sayfa.InvokeAsync(() => izgara.Instance.Tus(0, 2, "ArrowDown", false, false)).GetAwaiter().GetResult();
            sayfa.InvokeAsync(() => izgara.Instance.Tus(1, 2, "ArrowRight", false, true)).GetAwaiter().GetResult();
            Dogrula(sayfa.Find("td.aktif").Id == "sh-1-3" && sayfa.FindAll("td.secili").Count == 2, $"Etkin {sayfa.Find("td.aktif").Id}, {sayfa.FindAll("td.secili").Count} seçili.");
            sayfa.InvokeAsync(() => izgara.Instance.Tus(1, 3, "ArrowUp", false, true)).GetAwaiter().GetResult();
            Dogrula(sayfa.FindAll("td.secili").Count == 4, $"{sayfa.FindAll("td.secili").Count} seçili (2×2 bekleniyordu).");

            for (int i = 0; i < 12; i++) sayfa.InvokeAsync(() => izgara.Instance.Tekerlek(1)).GetAwaiter().GetResult();
            Dogrula(ayarlar.Ayarlar.SablonYaziBoyutu == 16 && sayfa.Find(".yakinlastirma-orani").TextContent.Contains("%178"), sayfa.Find(".yakinlastirma-orani").TextContent);
            sayfa.Find(".yakinlastirma-orani").Click();
            Dogrula(ayarlar.Ayarlar.SablonYaziBoyutu == 9, "Varsayılana dönmedi.");
        }));

        t.Test("Otomatik şablon → Excel → Şablonu geri yükle akışı diyaloglarla baştan sona çalışıyor", () =>
        {
            string yol = Path.Combine(Path.GetTempPath(), $"sayfa_otomatik_{Guid.NewGuid():N}.xlsx");
            try
            {
                Baglamla(d =>
                {
                    SablonluHazirla(d);
                    d.SablonSonuclari = null;
                    d.SablonIntNotlari.Clear();
                    d.OgretimYili = 2026;
                    d.Yariyil = Yariyil.Guz;
                }, (ctx, durum, dosya) =>
                {
                    dosya.KaydedilecekYol = yol;
                    dosya.AcilacakYol = yol;
                    var diyaloglar = ctx.Render<MudDialogProvider>();
                    var sayfa = ctx.Render<Sablon>();

                    sayfa.FindAll("button").First(b => b.TextContent.Contains("Otomatik şablon oluştur")).Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Otomatik Şablon Hazır"), "Sonuç diyaloğu açılmadı."),
                        TimeSpan.FromSeconds(30));
                    Dogrula(File.Exists(yol) && durum.SablonSonuclari == null, "Excel yazılmadı ya da şablon belleğe alındı.");
                    diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Kapat").Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(!diyaloglar.Markup.Contains("Otomatik Şablon Hazır"), "Diyalog kapanmadı."), TimeSpan.FromSeconds(5));
                    if (diyaloglar.Markup.Contains("Otomatik Dağıtım Uyarıları"))
                        diyaloglar.FindAll("button").Last(b => b.TextContent.Contains("Kapat") || b.TextContent.Contains("Tamam")).Click();
                    sayfa.WaitForAssertion(() => Dogrula(sayfa.Markup.Contains(Path.GetFileName(yol)), "Son dosya kartta görünmüyor."), TimeSpan.FromSeconds(5));

                    sayfa.FindAll("button").First(b => b.TextContent.Contains("Şablonu geri yükle")).Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Şablon Yükleme Onayı"), "Onay diyaloğu açılmadı."),
                        TimeSpan.FromSeconds(30));
                    diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Şablonu yükle").Click();
                    sayfa.WaitForAssertion(() => Dogrula(durum.SablonSonuclari?.Count > 0 && sayfa.FindAll(".sablon-izgarasi tbody tr").Count > 0,
                        "Şablon yüklenip ızgara çizilmedi."), TimeSpan.FromSeconds(10));
                    Dogrula(durum.Kilitler.SalonAta && IsAkisi.TamamMi(durum, 4), "Salonları Ata açılmadı.");
                });
            }
            finally { if (File.Exists(yol)) File.Delete(yol); }
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCILAR
    // ═══════════════════════════════════════════════════════════════

    private static void SablonluHazirla(CalismaDurumu d)
    {
        // Çekirdek testlerindeki şablonlu oturum; oturum yöneticisinin geri yükleme yolundan geçirilir
        var yuklu = YeniArayuzSablonTestleri.Yuklu(OturumKaydi.Serilestir(YeniArayuzSablonTestleri.SablonluDurum()));
        d.Dersler.AddRange(yuklu.Dersler);
        d.Derslikler = yuklu.Derslikler;
        foreach (var k in yuklu.TakvimSlotlari) d.TakvimSlotlari[k.Key] = k.Value;
        d.SablonSonuclari = yuklu.SablonSonuclari;
        foreach (var k in yuklu.SablonIntNotlari) d.SablonIntNotlari[k.Key] = k.Value;
        d.Kilitler.OtomatikDagit = true;
        d.Kilitler.SalonAta = true;
    }

    private static SablonIzgarasi Izgara(BunitContext ctx) => ctx.Services.GetRequiredService<SablonServisi>().Izgara();

    /// <summary>Izgaradaki "Ders adı" hücresinin (satır, sütun) konumu.</summary>
    private static (int R, int C) DersHucresi(BunitContext ctx, string saat, string bolum, int grup, int gun)
    {
        var satir = Izgara(ctx).Satirlar.First(s => s.Saat == saat && s.BolumKisa == bolum && s.GrupSira == grup);
        return (satir.Sira, gun * 7 + 2);
    }

    private sealed class TestDosyaServisi : IDosyaServisi
    {
        public string AcilacakYol { get; set; }
        public string KaydedilecekYol { get; set; }
        public Task<string> AcilacakDosyaSecAsync(DosyaIstegi istek) => Task.FromResult(AcilacakYol);
        public Task<string> KaydedilecekDosyaSecAsync(DosyaIstegi istek) => Task.FromResult(KaydedilecekYol);
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
}
