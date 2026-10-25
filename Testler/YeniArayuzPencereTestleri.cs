using Microsoft.Extensions.DependencyInjection;
using SinavProgrami.DataAccess;
using SinavProgrami.Services;
using SinavProgrami.UI;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A1 — pencere: boyut ve tam ekran durumu oturumla saklanır, açılışta geri gelir
/// (eski Madde 5'in yeni penceredeki karşılığı).
/// </summary>
static class YeniArayuzPencereTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A1 — pencere boyutu ve tam ekran");

        t.Test("İlk açılışta (kayıtta pencere bilgisi yok) tam ekran", () => PencereIle(new PencereBilgisi(), (p, _) =>
            Dogrula(p.WindowState == FormWindowState.Maximized, $"Durum: {p.WindowState}")));

        t.Test("Tam ekranda kapatıldıysa tam ekran açılıyor", () => PencereIle(new PencereBilgisi { TamEkran = true, Genislik = 1200, Yukseklik = 800 }, (p, _) =>
            Dogrula(p.WindowState == FormWindowState.Maximized, $"Durum: {p.WindowState}")));

        t.Test("Normal boyutta kapatıldıysa o boyutla açılıyor; pencere boyutlandırılabilir", () => PencereIle(new PencereBilgisi { Genislik = 1200, Yukseklik = 800 }, (p, _) =>
        {
            Dogrula(p.WindowState == FormWindowState.Normal && p.Size == new Size(1200, 800), $"{p.WindowState} {p.Size}");
            Dogrula(p.FormBorderStyle == FormBorderStyle.Sizable && p.MaximizeBox, "Pencere boyutlandırılamıyor.");
        }));

        t.Test("Kayıttaki boyut en küçük boyuttan küçükse (eski arayüzden kalma) kullanılmıyor", () => PencereIle(new PencereBilgisi { Genislik = 852, Yukseklik = 632 }, (p, _) =>
            Dogrula(p.Width >= p.MinimumSize.Width && p.Height >= p.MinimumSize.Height && p.WindowState == FormWindowState.Normal,
                $"{p.WindowState} {p.Size}, en küçük {p.MinimumSize}")));

        t.Test("Toplanan oturuma pencerenin o anki boyutu ve tam ekran durumu yazılıyor", () => PencereIle(new PencereBilgisi { Genislik = 1200, Yukseklik = 800 }, (p, oturum) =>
        {
            p.Size = new Size(1300, 820);
            var d = oturum.Topla();
            Dogrula(!d.PencereTamEkran && d.PencereGenislik == 1300 && d.PencereYukseklik == 820,
                $"{d.PencereTamEkran} {d.PencereGenislik}x{d.PencereYukseklik}");
        }));
    }

    /// <summary>Yeni pencereyi (gösterilmeden) verilen kayıtlı pencere bilgisiyle kurar.</summary>
    private static void PencereIle(PencereBilgisi bilgi, Action<AnaPencere, OturumYoneticisi> govde)
    {
        var servisler = new ServiceCollection();
        servisler.AddWindowsFormsBlazorWebView();
        var durum = new CalismaDurumu { Pencere = bilgi };
        servisler.AddSingleton(durum);
        servisler.ArayuzServisleriniEkle();
        var saglayici = servisler.BuildServiceProvider();
        try
        {
            using var pencere = new AnaPencere(saglayici);
            govde(pencere, saglayici.GetRequiredService<OturumYoneticisi>());
        }
        finally
        {
            saglayici.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
