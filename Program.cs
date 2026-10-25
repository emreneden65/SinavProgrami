#nullable disable
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Services;
using SinavProgrami.UI;

namespace SinavProgrami;

static class Program
{
    /// <summary>Eski WinForms arayüzünü açan komut satırı anahtarı (geçişte yedek olarak duruyor).</summary>
    public const string EskiArayuzAnahtari = "--eski-arayuz";

    /// <summary>
    /// Yeni arayüzü gerçek veri yerine deneme kopyasıyla açar (%AppData%\SinavProgrami\Deneme\):
    /// gerçek oturum dosyasına dokunmadan denemek için.
    /// </summary>
    public const string DenemeAnahtari = "--deneme";

    /// <summary>Geçiş döneminden kalan anahtar; yeni arayüz artık varsayılan olduğu için etkisizdir.</summary>
    public const string YeniArayuzAnahtari = "--yeni-arayuz";

    /// <summary>Uygulama kendini yeniden başlatırken eklenir: önceki pencerenin kapanması beklenir.</summary>
    public const string YenidenBaslatAnahtari = "--yeniden-baslat";

    private const string WebView2IndirmeAdresi = "https://developer.microsoft.com/microsoft-edge/webview2/";

    [STAThread]
    static void Main(string[] args)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        if (Anahtar(args, EskiArayuzAnahtari))
            EskiArayuzuCalistir();
        else
            YeniArayuzuCalistir(args);
    }

    private static bool Anahtar(string[] args, string anahtar) => args.Contains(anahtar, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tek örnek kilidi: aynı veri klasörüne iki pencere birden yazmasın. Eski ve yeni arayüz gerçek
    /// veride aynı kilidi kullanır; deneme kopyasının kilidi ayrıdır.
    /// </summary>
    private static Mutex TekOrnekKilidi(bool deneme) => new(false, @"Local\SinavProgrami_" + (deneme ? "Deneme" : "Gercek"));

    private static void ZatenAcikUyarisi()
        => MessageBox.Show("Sınav Programı zaten açık.\n\nAçık olan pencereyi kullanın (görev çubuğuna bakın).",
            "Sınav Programı", MessageBoxButtons.OK, MessageBoxIcon.Information);

    /// <summary>Eski WinForms arayüzü (--eski-arayuz) — yedek olarak, eskisi gibi açılır.</summary>
    private static void EskiArayuzuCalistir()
    {
        ApplicationConfiguration.Initialize();
        using var kilit = TekOrnekKilidi(deneme: false);
        if (!KilidiAl(kilit, TimeSpan.Zero))
        {
            ZatenAcikUyarisi();
            return;
        }
        try
        {
            if (!VeritabaniniHazirla()) return;
            Application.Run(new MainForm());
        }
        finally
        {
            kilit.ReleaseMutex();
        }
    }

    /// <summary>Yeni arayüz: WinForms penceresinde Blazor (WebView2).</summary>
    private static void YeniArayuzuCalistir(string[] args)
    {
        // WebView2 her monitörün kendi ölçeğinde keskin çizilsin
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        HataGunlugu.Kur();

        // Gerçek veri klasörü; --deneme ile gerçek dosyaların kopyalarıyla çalışılır
        VeriKlasoru.Kur(deneme: Anahtar(args, DenemeAnahtari));

        using var kilit = TekOrnekKilidi(VeriKlasoru.DenemeModu);
        if (!KilidiAl(kilit, Anahtar(args, YenidenBaslatAnahtari) ? TimeSpan.FromSeconds(15) : TimeSpan.Zero))
        {
            ZatenAcikUyarisi();
            return;
        }

        try
        {
            if (!WebView2KuruluMu()) return;
            if (!VeritabaniniHazirla()) return;

            // Servis kabı uygulama boyunca yaşar; kapanışta elle atılmaz (MudBlazor servisleri yalnızca
            // asenkron kapatılabilir, süreç zaten sona eriyor)
            var servisler = ServisleriKur();
            servisler.GetRequiredService<AyarlarDeposu>().Yukle();
            servisler.GetRequiredService<HocaEslemesi>().Yukle();

            // Önceki oturum varsa kaldığı yerden devam; yoksa varsayılan takvim
            var oturum = servisler.GetRequiredService<OturumYoneticisi>();
            if (!oturum.GeriYukle())
                servisler.GetRequiredService<TakvimServisi>().VarsayilanTakvimiKur();

            using var pencere = new AnaPencere(servisler);
            oturum.OtomatikKaydiBaslat();
            Application.Run(pencere);
        }
        finally
        {
            kilit.ReleaseMutex();
        }
    }

    private static ServiceProvider ServisleriKur()
    {
        var servisler = new ServiceCollection();
        servisler.AddWindowsFormsBlazorWebView();
#if DEBUG
        servisler.AddBlazorWebViewDeveloperTools();
#endif
        // Gerçek pencere / dosya servisleri önce kaydedilir; arayüz kaydı yalnızca eksik olanları ekler
        servisler.AddSingleton<IPencereServisi, WinFormsPencereServisi>();
        servisler.AddSingleton<IDosyaServisi>(DosyaServisiSec());
        servisler.ArayuzServisleriniEkle();
        return servisler.BuildServiceProvider();
    }

    /// <summary>
    /// Windows aç / kaydet diyalogları. Yalnızca Debug derlemesinde, uçtan uca otomasyon için
    /// (SINAVPROGRAMI_OTOMASYON_DOSYALARI=klasör) diyalog açmayan sahte servis kullanılır.
    /// </summary>
    private static IDosyaServisi DosyaServisiSec()
    {
#if DEBUG
        string klasor = Environment.GetEnvironmentVariable("SINAVPROGRAMI_OTOMASYON_DOSYALARI");
        if (!string.IsNullOrWhiteSpace(klasor)) return new OtomasyonDosyaServisi(klasor);
#endif
        return new WinFormsDosyaServisi();
    }

    /// <summary>Kilidi alır; önceki örnek kilidi bırakmadan kapandıysa kilit yine alınmış sayılır.</summary>
    private static bool KilidiAl(Mutex kilit, TimeSpan bekleme)
    {
        try { return kilit.WaitOne(bekleme); }
        catch (AbandonedMutexException) { return true; }
    }

    /// <summary>
    /// Veritabanı, exe'nin yanındaki tek dosyalık SinavDB.db'dir. Yoksa burada oluşturulur —
    /// uygulamanın kopyalandığı her bilgisayarda hiçbir kurulum gerektirmemesini bu adım sağlar.
    /// </summary>
    private static bool VeritabaniniHazirla()
    {
        try
        {
            DatabaseHelper.VeritabaniniHazirla();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Veritabanı hazırlanamadı:\n\n{ex.Message}\n\n" +
                $"Beklenen konum:\n{DatabaseHelper.VeritabaniYolu}\n\n" +
                "Uygulamayı yazma izni olan bir klasöre taşımayı deneyin " +
                "(ör. Belgeler veya Masaüstü).",
                "Sınav Programı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>
    /// Yeni arayüz Microsoft Edge WebView2 ile çizilir. Windows 11'de hazır gelir, Windows 10'da
    /// Edge ile birlikte kurulur; yoksa indirme sayfası önerilir.
    /// </summary>
    private static bool WebView2KuruluMu()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            var cevap = MessageBox.Show(
                "Bu bilgisayarda Microsoft Edge WebView2 bulunamadı. Uygulamanın arayüzü için gereklidir.\n\n" +
                "Microsoft'un sayfasından \"Evergreen Bootstrapper\"ı indirip kurun, ardından programı yeniden açın.\n\n" +
                "İndirme sayfası şimdi açılsın mı?",
                "WebView2 Gerekli", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (cevap == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo(WebView2IndirmeAdresi) { UseShellExecute = true }); }
                catch { /* tarayıcı açılamazsa adres mesajdan okunabilir */ }
            }
            return false;
        }
    }
}
