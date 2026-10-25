using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using SinavProgrami.UI;
using SinavProgrami.UI.Components.Salon;
using SinavProgrami.UI.Pages;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A4 — Salonlar, Gözetmenler, Final ve Ayarlar sayfaları pencere açılmadan (bUnit) çizilir;
/// dosya seçimi sahte servisle, onaylar gerçek diyalog düğmeleriyle yapılır. Matristeki fare / klavye
/// olayları gerçekte app.js'ten gelir; burada aynı .NET uçları doğrudan çağrılır.
/// </summary>
static class YeniArayuzA4SayfaTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A4 — Salonlar, Gözetmenler, Final, Ayarlar sayfaları");

        t.Test("Salonlar: şablon yokken atama kilitli; 'Salonları ata' → matris ve liste çiziliyor, salonlu Excel açılıyor", () =>
        {
            Baglamla(() => new CalismaDurumu(), (ctx, _, _) =>
            {
                var sayfa = ctx.Render<Salonlar>();
                Dogrula(Dugme(sayfa, "Salonları ata").HasAttribute("disabled") && sayfa.Markup.Contains("Önce 4. adımda şablonu geri yükleyin."),
                    "Atama kilitli değil.");
                Dogrula(Dugme(sayfa, "Salonlu Excel çıkar").HasAttribute("disabled") && sayfa.Markup.Contains("Henüz salon ataması yok"), "Boş durum yanlış.");
            });
            Baglamla(YeniArayuzSalonTestleri.SablonluYuklu, (ctx, durum, _) =>
            {
                durum.Kilitler.SalonAta = true;
                var diyaloglar = ctx.Render<MudDialogProvider>();
                var sayfa = ctx.Render<Salonlar>();
                Dugme(sayfa, "Salonları ata").Click();
                sayfa.WaitForAssertion(() => Dogrula(sayfa.FindAll("td[data-c]").Count > 0, "Matris çizilmedi."), TimeSpan.FromSeconds(15));
                if (diyaloglar.Markup.Contains("Salon Ataması Uyarıları"))
                    diyaloglar.FindAll("button").Last(b => b.TextContent.Trim() == "Kapat").Click();
                Dogrula(durum.Kilitler.SalonExport && !Dugme(sayfa, "Salonlu Excel çıkar").HasAttribute("disabled"), "Salonlu Excel açılmadı.");
                int satir = sayfa.FindAll(".matris-izgarasi tbody tr").Count(r => r.QuerySelector("td") != null);
                Dogrula(satir == ctx.Services.GetRequiredService<SalonServisi>().Matris().Satirlar.Count, $"{satir} matris satırı.");
                Dogrula(sayfa.FindAll("th.salon-basligi")[0].TextContent.Contains("T4-"), "T4 salonları önce gelmiyor.");

                Dugme(sayfa, "Salonları ata").Click();
                diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Salonları Yeniden Ata"), "Yeniden atama onayı sorulmadı."),
                    TimeSpan.FromSeconds(5));
                diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Vazgeç").Click();

                sayfa.FindAll(".mud-tab").First(x => x.TextContent.Contains("Liste")).Click();
                sayfa.WaitForAssertion(() => Dogrula(sayfa.FindAll(".tablo-kabi tbody tr").Count(r => r.QuerySelector("td") != null) == durum.SonSonuclar.Count,
                    "Liste satırları yanlış."), TimeSpan.FromSeconds(5));
            });
        });

        t.Test("Matris: rakamla düzenleme öğrenci sayısını yazıyor, Delete atamayı silip 'Geri al' sunuyor, boş slot satırı düzenlenmiyor", () =>
            Baglamla(YeniArayuzSalonTestleri.SalonluYuklu, (ctx, durum, _) =>
            {
                var sayfa = ctx.Render<Salonlar>();
                var matris = sayfa.FindComponent<SalonMatrisiBileseni>();
                var model = ctx.Services.GetRequiredService<SalonServisi>().Matris();
                int r = model.Satirlar.FindIndex(s => !s.Bos);
                var atama = model.Satirlar[r].Atama;
                int c = model.Salonlar.FindIndex(s => SalonServisi.SalonAtamasi(atama, s.SalonKodu) == null);

                sayfa.InvokeAsync(() => matris.Instance.Tus(r, c, "1", false, false)).GetAwaiter().GetResult();
                var kutu = sayfa.Find(".matris-izgarasi input");
                kutu.Input("12");
                kutu.KeyDown("Enter");
                Dogrula(SalonServisi.SalonAtamasi(atama, model.Salonlar[c].SalonKodu)?.AtananOgrenciSayisi == 12, "Öğrenci sayısı yazılmadı.");
                Dogrula(sayfa.Find($"#mh-{r}-{c}").ClassList.Contains("dolu"), "Hücre dolu görünmüyor.");

                sayfa.InvokeAsync(() => matris.Instance.Tus(r, c, "Delete", false, false)).GetAwaiter().GetResult();
                Dogrula(SalonServisi.SalonAtamasi(atama, model.Salonlar[c].SalonKodu) == null, "Delete atamayı silmedi.");
                var bildirim = ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.LastOrDefault();
                Dogrula(bildirim?.Message.Contains("ataması silindi") == true, bildirim?.Message);

                int bos = model.Satirlar.FindIndex(s => s.Bos);
                if (bos >= 0)
                {
                    sayfa.InvokeAsync(() => matris.Instance.CiftTikla(bos, 0)).GetAwaiter().GetResult();
                    Dogrula(sayfa.FindAll(".matris-izgarasi input").Count == 0, "Boş slot satırında düzenleyici açıldı.");
                }
            }));

        t.Test("Gözetmenler: tablo, ad ve bölüm düzeltme, ekleme diyaloğu, onaylı silme ve 'Gözetmenleri ata' (ağırlık özeti)", () =>
            Baglamla(YeniArayuzSalonTestleri.SalonluYuklu, (ctx, durum, _) =>
            {
                durum.Kilitler.GozetmenAta = true;
                var diyaloglar = ctx.Render<MudDialogProvider>();
                var sayfa = ctx.Render<Gozetmenler>();
                Dogrula(sayfa.FindAll(".tablo-kabi tbody tr").Count == durum.GozetmenListesi.Count, "Satır sayısı yanlış.");

                var ali = durum.GozetmenListesi.First(g => g.AdSoyad == "ALİ VELİ");
                var satir = sayfa.FindAll(".tablo-kabi tbody tr").First(r => r.TextContent.Contains("ALİ VELİ"));
                satir.QuerySelectorAll(".hucre-degeri")[1].Click();
                sayfa.Find("input.hucre-kutusu").Input("Ali  Veli Yıldız");
                sayfa.Find("input.hucre-kutusu").KeyDown("Enter");
                Dogrula(ali.AdSoyad == "Ali Veli Yıldız", $"Ad: {ali.AdSoyad}");
                sayfa.FindAll(".tablo-kabi tbody tr").First(r => r.TextContent.Contains("Ali Veli Yıldız"))
                     .QuerySelector("select").Change("Makine Mühendisliği");
                Dogrula(ali.Bolum == "Makine Mühendisliği", $"Bölüm: {ali.Bolum}");

                Dugme(sayfa, "Gözetmen ekle").Click();
                diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.FindAll("input").Count >= 2, "Ekleme diyaloğu açılmadı."), TimeSpan.FromSeconds(5));
                diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Ekle").Click();
                diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Ad Soyad boş bırakılamaz."), "Boş ad uyarısı yok."), TimeSpan.FromSeconds(5));
                diyaloglar.Find(".mud-dialog input").Change("Deniz Ak");
                diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Ekle").Click();
                diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Bölüm seçin."), "Bölüm uyarısı yok."), TimeSpan.FromSeconds(5));
                diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Vazgeç").Click();

                sayfa.FindAll(".tablo-kabi tbody tr").First(r => r.TextContent.Contains("CAN ŞAHİN")).QuerySelector("input[type=checkbox]").Change(true);
                Dugme(sayfa, "Seçilenleri sil").Click();
                diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("gözetmen listesinden silinecek"), "Silme onayı yok."), TimeSpan.FromSeconds(5));
                diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Sil").Click();
                sayfa.WaitForAssertion(() => Dogrula(durum.GozetmenListesi.All(g => g.AdSoyad != "CAN ŞAHİN"), "Silinmedi."), TimeSpan.FromSeconds(5));

                Dugme(sayfa, "Gözetmenleri ata").Click();
                diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("salon-gözetmen ataması"), "Atama sonucu gösterilmedi."), TimeSpan.FromSeconds(15));
                Dogrula(durum.Kilitler.MatrisExport && diyaloglar.Markup.Contains("Ortalama sınav"), "Kilit ya da ağırlık özeti yok.");
                diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Tamam").Click();
                sayfa.WaitForAssertion(() => Dogrula(sayfa.FindAll(".agirlik-ozeti tbody tr").Count > 0, "Sayfada ağırlık özeti yok."), TimeSpan.FromSeconds(5));
            }));

        t.Test("Final: kilit nedenleri; taslak çıkar → taslağı geri yükle → final programı (dosyalar ve kilitler)", () =>
        {
            string taslak = Path.Combine(Path.GetTempPath(), $"sayfa_taslak_{Guid.NewGuid():N}.xlsx");
            string final = Path.Combine(Path.GetTempPath(), $"sayfa_final_{Guid.NewGuid():N}.xlsx");
            try
            {
                Baglamla(() =>
                {
                    var d = YeniArayuzSalonTestleri.SalonluYuklu();
                    d.OgretimYili = 2026;
                    d.Yariyil = Yariyil.Guz;
                    return d;
                }, (ctx, durum, dosya) =>
                {
                    var diyaloglar = ctx.Render<MudDialogProvider>();
                    var sayfa = ctx.Render<Final>();
                    Dogrula(Dugme(sayfa, "Gözetmenli taslak çıkar").HasAttribute("disabled") && sayfa.Markup.Contains("Önce 6. adımda gözetmenleri atayın."),
                        "Taslak kilitli değil.");
                    Dogrula(Dugme(sayfa, "Final programını çıkar").HasAttribute("disabled"), "Final kilitli değil.");

                    var goz = ctx.Services.GetRequiredService<GozetmenServisi>();
                    sayfa.InvokeAsync(() => goz.AtamaTamamlandi(goz.GozetmenleriAta())).GetAwaiter().GetResult();

                    dosya.KaydedilecekYol = taslak;
                    Dugme(sayfa, "Gözetmenli taslak çıkar").Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Dışa Aktarma Başarılı"), "Taslak sonucu yok."), TimeSpan.FromSeconds(20));
                    diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Kapat").Click();
                    Dogrula(File.Exists(taslak) && durum.Kilitler.GozetmenliYukle, "Taslak yazılmadı.");

                    dosya.AcilacakYol = taslak;
                    sayfa.WaitForAssertion(() => Dogrula(!Dugme(sayfa, "Taslağı geri yükle").HasAttribute("disabled"), "Geri yükleme açılmadı."), TimeSpan.FromSeconds(5));
                    Dugme(sayfa, "Taslağı geri yükle").Click();
                    sayfa.WaitForAssertion(() => Dogrula(durum.Kilitler.FinalExport, "Taslak uygulanmadı."), TimeSpan.FromSeconds(20));

                    dosya.KaydedilecekYol = final;
                    sayfa.WaitForAssertion(() => Dogrula(!Dugme(sayfa, "Final programını çıkar").HasAttribute("disabled"), "Final açılmadı."), TimeSpan.FromSeconds(5));
                    Dugme(sayfa, "Final programını çıkar").Click();
                    diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Dışa Aktarma Başarılı"), "Final sonucu yok."), TimeSpan.FromSeconds(20));
                    diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Kapat").Click();
                    sayfa.WaitForAssertion(() => Dogrula(File.Exists(final) && sayfa.Markup.Contains(Path.GetFileName(final)), "Final dosyası kartta yok."),
                        TimeSpan.FromSeconds(5));
                });
            }
            finally
            {
                foreach (var y in new[] { taslak, final }) if (File.Exists(y)) File.Delete(y);
            }
        });

        t.Test("Ayarlar: tema seçimi kaydediliyor; Temizle onayla çalışma verisini siliyor, derslikler kalıyor", () =>
            Baglamla(YeniArayuzSalonTestleri.SalonluYuklu, (ctx, durum, _) =>
            {
                var depo = ctx.Services.GetRequiredService<AyarlarDeposu>();
                var diyaloglar = ctx.Render<MudDialogProvider>();
                var sayfa = ctx.Render<Ayarlar>();
                Dogrula(sayfa.Markup.Contains(OturumKaydi.Klasor), "Oturum klasörü gösterilmiyor.");
                sayfa.FindAll(".tema-secimi .mud-toggle-item").First(b => b.TextContent.Contains("Koyu")).Click();
                Dogrula(depo.Ayarlar.KoyuTema && File.ReadAllText(AyarlarDeposu.DosyaYolu).Contains("\"KoyuTema\": true"), "Tema kaydedilmedi.");
                depo.TemaAyarla(false);

                Dugme(sayfa, "Temizle").Click();
                diyaloglar.WaitForAssertion(() => Dogrula(diyaloglar.Markup.Contains("Tüm veriler temizlenecek"), "Onay sorulmadı."), TimeSpan.FromSeconds(5));
                diyaloglar.FindAll("button").First(b => b.TextContent.Trim() == "Temizle").Click();
                sayfa.WaitForAssertion(() => Dogrula(durum.Dersler.Count == 0 && durum.SonSonuclar == null && durum.Derslikler.Count > 0,
                    "Temizle beklendiği gibi çalışmadı."), TimeSpan.FromSeconds(5));
            }));
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCILAR
    // ═══════════════════════════════════════════════════════════════

    private static AngleSharp.Dom.IElement Dugme<T>(IRenderedComponent<T> sayfa, string metin) where T : Microsoft.AspNetCore.Components.IComponent
        => sayfa.FindAll("button").First(b => b.TextContent.Trim().Contains(metin));

    private sealed class TestDosyaServisi : IDosyaServisi
    {
        public string AcilacakYol { get; set; }
        public string KaydedilecekYol { get; set; }
        public Task<string> AcilacakDosyaSecAsync(DosyaIstegi istek) => Task.FromResult(AcilacakYol);
        public Task<string> KaydedilecekDosyaSecAsync(DosyaIstegi istek) => Task.FromResult(KaydedilecekYol);
        public void DosyayiAc(string yol) { }
        public void KlasordeGoster(string yol) { }
    }

    private static void Baglamla(Func<CalismaDurumu> olustur, Action<BunitContext, CalismaDurumu, TestDosyaServisi> govde)
    {
        var ctx = new BunitContext();
        try
        {
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            var durum = olustur();
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
