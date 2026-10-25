#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using SinavProgrami.Services;

namespace SinavProgrami
{
    /// <summary>
    /// YENİ ARAYÜZÜN PENCERESİ
    /// ─────────────────────────────────────────────────────────────
    /// Pencerenin tamamını BlazorWebView (Microsoft Edge WebView2) kaplar; arayüzün kendisi
    /// UI\ klasöründeki Razor bileşenleridir. Bu sınıf yalnızca pencere işlerini yapar:
    ///   • boyut / tam ekran durumunu oturumla saklar ve açılışta geri yükler,
    ///   • kapanışta yarım kalan düzenlemeyi işletip oturumu kaydeder,
    ///   • WebView2'yi masaüstü uygulamasına uygun ayarlar (tarayıcı kısayolları,
    ///     tarayıcı yakınlaştırması ve tarayıcının sağ tık menüsü kapalı).
    /// </summary>
    public class AnaPencere : Form
    {
        // 96 DPI'daki ölçüler — ekran ölçeğine göre büyütülür
        private const int VarsayilanGenislik = 1440, VarsayilanYukseklik = 900;
        private const int EnKucukGenislik = 1000, EnKucukYukseklik = 640;

        /// <summary>Sağ tık menüsünde bırakılan öğeler: yalnızca metin düzenleme.</summary>
        private static readonly HashSet<string> IzinliMenuOgeleri = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cut", "copy", "paste", "pasteAndMatchStyle", "selectAll", "undo", "redo",
#if DEBUG
            "inspectElement",
#endif
        };

        private readonly BlazorWebView _webView;
        private readonly CalismaDurumu _durum;
        private readonly OturumYoneticisi _oturum;
        private bool _kapaniyor, _kapanisHazir, _kaydetmedenKapat;

        /// <summary>Simge durumuna küçültülmeden önceki durum (küçültülüp kapatılan tam ekran pencere yine tam ekran açılsın).</summary>
        private FormWindowState _sonGorunurDurum = FormWindowState.Normal;

        /// <summary>
        /// Yalnızca Debug: uçtan uca otomasyon (SINAVPROGRAMI_OTOMASYON=1). Pencere ekran dışında,
        /// normal boyutta açılır; geliştirme betiği WebView2'ye bağlanıp ekran görüntüsü alırken
        /// kullanıcının ekranını kaplamaz. Bu modda pencere bilgisi oturuma yazılmaz.
        /// </summary>
        private readonly bool _otomasyon = OtomasyonModuMu();

        private static bool OtomasyonModuMu()
        {
#if DEBUG
            return Environment.GetEnvironmentVariable("SINAVPROGRAMI_OTOMASYON") == "1";
#else
            return false;
#endif
        }

        /// <summary>Açık pencere (dosya diyaloglarının sahibi, yeniden başlatma).</summary>
        public static AnaPencere Ornek { get; private set; }

        public AnaPencere(IServiceProvider servisler)
        {
            Ornek = this;
            _durum = servisler.GetRequiredService<CalismaDurumu>();
            _oturum = servisler.GetRequiredService<OturumYoneticisi>();
            bool koyu = servisler.GetRequiredService<AyarlarDeposu>().Ayarlar.KoyuTema;

            Text = VeriKlasoru.DenemeModu ? "Sınav Programı — Yeni Arayüz (deneme verisi)" : "Sınav Programı";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* simgesiz de açılır */ }
            AutoScaleMode = AutoScaleMode.None;   // ölçüler aşağıda ekran ölçeğine göre elle hesaplanır
            StartPosition = FormStartPosition.Manual;
            // WebView yüklenene kadar beyaz/siyah parlama olmasın: zemin temanın rengi
            BackColor = ColorTranslator.FromHtml(koyu ? "#181825" : "#EFF1F5");

            _webView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = @"wwwroot\index.html",
                Services = servisler,
                StartPath = BaslangicSayfasi(_durum.SonSayfa)
            };
            _webView.WebView.DefaultBackgroundColor = BackColor;
            _webView.RootComponents.Add<UI.App>("#app");
            // WebView2 önbelleği exe'nin yanına değil kullanıcı klasörüne (exe klasörü salt okunur olabilir)
            _webView.BlazorWebViewInitializing += (s, e) => e.UserDataFolder = Path.Combine(HataGunlugu.Klasor, "WebView2");
            _webView.BlazorWebViewInitialized += (s, e) => WebViewiAyarla(e.WebView.CoreWebView2);
            Controls.Add(_webView);

            _oturum.ToplamadanOnce = () =>
            {
                if (!_otomasyon) _durum.Pencere = PencereBilgisiAl();
            };
            PencereyiYerlestir();
            _sonGorunurDurum = WindowState;
            Resize += (s, e) =>
            {
                if (WindowState != FormWindowState.Minimized) _sonGorunurDurum = WindowState;
            };

            if (_otomasyon)
            {
                WindowState = FormWindowState.Normal;
                Size = new Size(1366, 768);
                Location = new Point(-20000, -20000);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  PENCERE BOYUTU — son normal boyut + tam ekran oturumla saklanır
        // ══════════════════════════════════════════════════════════════

        private int Olcekle(int deger, int dpi = 0)
            => (int)Math.Round(deger * (dpi > 0 ? dpi : DeviceDpi) / 96.0);

        private void PencereyiYerlestir()
        {
            MinimumSize = new Size(Olcekle(EnKucukGenislik), Olcekle(EnKucukYukseklik));
            var alan = Screen.FromPoint(Cursor.Position).WorkingArea;
            var p = _durum.Pencere ?? new PencereBilgisi();

            // Kayıttaki boyut yeni pencerenin en küçük boyutundan küçükse (ör. eski arayüzden kalma) kullanılmaz
            bool kayitliBoyut = p.Genislik >= MinimumSize.Width && p.Yukseklik >= MinimumSize.Height;
            Size = kayitliBoyut
                ? new Size(Math.Min(p.Genislik, alan.Width), Math.Min(p.Yukseklik, alan.Height))
                : new Size(Math.Min(Olcekle(VarsayilanGenislik), alan.Width), Math.Min(Olcekle(VarsayilanYukseklik), alan.Height));
            Location = new Point(alan.Left + (alan.Width - Width) / 2, alan.Top + (alan.Height - Height) / 2);

            // Tam ekranda kapatıldıysa ya da kayıtta hiç pencere bilgisi yoksa (ilk açılış) tam ekran açılır
            bool ilkAcilis = p.Genislik == 0 && p.Yukseklik == 0;
            if (p.TamEkran || ilkAcilis)
                WindowState = FormWindowState.Maximized;
        }

        /// <summary>
        /// Tam ekran / simge durumundayken normal boyut RestoreBounds'tadır. Simge durumundaki
        /// pencere için küçültülmeden önceki durum (tam ekran mı) esas alınır.
        /// </summary>
        private PencereBilgisi PencereBilgisiAl()
        {
            var sinir = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var durum = WindowState == FormWindowState.Minimized ? _sonGorunurDurum : WindowState;
            return new PencereBilgisi
            {
                TamEkran = durum == FormWindowState.Maximized,
                Genislik = sinir.Width,
                Yukseklik = sinir.Height
            };
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            MinimumSize = new Size(Olcekle(EnKucukGenislik, e.DeviceDpiNew), Olcekle(EnKucukYukseklik, e.DeviceDpiNew));
        }

        /// <summary>Kayıttaki son sayfa geçerliyse oradan açılır.</summary>
        private static string BaslangicSayfasi(string sonSayfa)
        {
            var gecerli = IsAkisi.Adimlar.Select(a => a.Rota).Append("ayarlar");
            return gecerli.Contains(sonSayfa, StringComparer.OrdinalIgnoreCase) ? "/" + sonSayfa : "/";
        }

        // ══════════════════════════════════════════════════════════════
        //  WEBVIEW2 AYARLARI
        // ══════════════════════════════════════════════════════════════

        private static void WebViewiAyarla(CoreWebView2 web)
        {
            var ayar = web.Settings;
#if DEBUG
            ayar.AreDevToolsEnabled = true;   // F12 ile geliştirici araçları
#else
            ayar.AreDevToolsEnabled = false;
            // F5 (sayfayı yeniden yükler), Ctrl+F, Ctrl+P, Ctrl+R gibi tarayıcı kısayolları kapalı
            ayar.AreBrowserAcceleratorKeysEnabled = false;
#endif
            ayar.IsZoomControlEnabled = false;   // Ctrl+tekerlek sayfayı değil tabloları büyütsün
            ayar.IsPinchZoomEnabled = false;
            ayar.IsStatusBarEnabled = false;
            ayar.IsGeneralAutofillEnabled = false;
            ayar.IsPasswordAutosaveEnabled = false;
            ayar.IsSwipeNavigationEnabled = false;

            // Sağ tık: "Geri / Yeniden yükle / Yazdır / Farklı kaydet" gibi tarayıcı öğeleri çıkarılır,
            // metin kutularındaki Kes / Kopyala / Yapıştır kalır
            web.ContextMenuRequested += (s, e) =>
            {
                for (int i = e.MenuItems.Count - 1; i >= 0; i--)
                    if (!IzinliMenuOgeleri.Contains(e.MenuItems[i].Name))
                        e.MenuItems.RemoveAt(i);
                if (e.MenuItems.Count == 0) e.Handled = true;
            };

            // Bağlantılar uygulamanın içinde değil varsayılan tarayıcıda açılır
            web.NewWindowRequested += (s, e) =>
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var adres) &&
                    (adres.Scheme == Uri.UriSchemeHttps || adres.Scheme == Uri.UriSchemeHttp))
                {
                    try { Process.Start(new ProcessStartInfo(adres.AbsoluteUri) { UseShellExecute = true }); }
                    catch (Exception ex) { HataGunlugu.Yaz("Bağlantı açılamadı", ex); }
                }
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  KAPANIŞ — yarım kalan hücre düzenlemesi işlenir, sonra oturum kaydedilir
        // ══════════════════════════════════════════════════════════════

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            if (_kapanisHazir)
            {
                base.OnFormClosing(e);
                return;
            }

            // Windows kapanırken bekletmeden kaydet
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                _oturum.OtomatikKaydiDurdur();
                if (!_kaydetmedenKapat) _oturum.Kaydet(out _);
                base.OnFormClosing(e);
                return;
            }

            e.Cancel = true;
            if (_kapaniyor) return;   // kayıt sürerken tekrar kapat'a basıldı
            _kapaniyor = true;

            _oturum.OtomatikKaydiDurdur();
            if (!_kaydetmedenKapat)
            {
                await OdagiBirakAsync();
                if (!_oturum.Kaydet(out string hata))
                    MessageBox.Show(this,
                        $"Çalışma durumu kaydedilemedi; bir sonraki açılışta kaldığınız yerden devam edilemeyebilir.\n\n{hata}",
                        "Oturum Kaydı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _kapanisHazir = true;
            Close();
        }

        /// <summary>
        /// Odaktaki kutudan çıkılır: yazılıp henüz onaylanmamış değer (change olayı) çalışma
        /// durumuna işlensin (eski arayüzde kapanışta grid.EndEdit() yapılıyordu).
        /// </summary>
        private async Task OdagiBirakAsync()
        {
            try
            {
                if (_webView.WebView.CoreWebView2 == null) return;
                await _webView.WebView.ExecuteScriptAsync(
                    "document.activeElement && document.activeElement.blur && document.activeElement.blur();");
                await Task.Delay(200);   // change olayının .NET tarafına ulaşması için
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz("Kapanışta odak bırakılamadı", ex);
            }
        }

        /// <summary>
        /// Deneme kopyalarını gerçek verinin güncel haliyle yeniler ve uygulamayı yeniden başlatır.
        /// Bu pencere kapanırken kaydetmez (yoksa yeni kopyanın üzerine yazardı).
        /// </summary>
        public void DenemeVerisiyleYenidenBaslat()
        {
            // WebView olayının içinden değil, olay bittikten sonra çalışsın
            BeginInvoke(new Action(() =>
            {
                try
                {
                    _kaydetmedenKapat = true;
                    VeriKlasoru.DenemeVerisiniYenile();
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath,
                        $"{Program.DenemeAnahtari} {Program.YenidenBaslatAnahtari}") { UseShellExecute = false });
                    Close();
                }
                catch (Exception ex)
                {
                    _kaydetmedenKapat = false;
                    HataGunlugu.Yaz("Deneme verisi yenilenemedi", ex);
                    MessageBox.Show(this, $"Deneme verisi yenilenemedi:\n\n{ex.Message}", "Sınav Programı",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (Ornek == this) Ornek = null;
            base.OnFormClosed(e);
        }
    }
}
