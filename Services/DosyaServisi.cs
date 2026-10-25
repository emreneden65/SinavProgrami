#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SinavProgrami.Services
{
    /// <summary>Bir aç / kaydet diyaloğu isteği.</summary>
    public sealed class DosyaIstegi
    {
        /// <summary>İsteğin türü (ör. "SinavListesi") — son klasörü hatırlamak ve otomasyonda dosya seçmek için.</summary>
        public string Anahtar { get; init; }
        public string Baslik { get; init; }
        public string Filtre { get; init; } = "Excel Dosyası (*.xlsx)|*.xlsx";

        /// <summary>Kaydetmede önerilen dosya adı.</summary>
        public string OnerilenAd { get; init; }

        /// <summary>Diyaloğun açılacağı dosya / klasör (ör. son üretilen dosya).</summary>
        public string BaslangicYolu { get; init; }
    }

    /// <summary>
    /// Dosya işleri: Windows'un aç / kaydet diyalogları, dosyayı varsayılan programla (Excel)
    /// açma, klasörde gösterme. Arayüz bileşenleri pencereyi tanımaz; testlerde sahtesi kullanılır.
    /// </summary>
    public interface IDosyaServisi
    {
        /// <summary>Açılacak dosyayı seçtirir; vazgeçilirse null.</summary>
        Task<string> AcilacakDosyaSecAsync(DosyaIstegi istek);

        /// <summary>Kaydedilecek dosya yolunu seçtirir; vazgeçilirse null.</summary>
        Task<string> KaydedilecekDosyaSecAsync(DosyaIstegi istek);

        /// <summary>Dosyayı varsayılan programla (Excel) açar.</summary>
        void DosyayiAc(string yol);

        /// <summary>Dosyayı Windows Gezgini'nde seçili olarak gösterir.</summary>
        void KlasordeGoster(string yol);
    }

    /// <summary>Pencere olmayan ortam (testler) için: diyalog hep "vazgeçildi" döner.</summary>
    public sealed class BosDosyaServisi : IDosyaServisi
    {
        public Task<string> AcilacakDosyaSecAsync(DosyaIstegi istek) => Task.FromResult<string>(null);
        public Task<string> KaydedilecekDosyaSecAsync(DosyaIstegi istek) => Task.FromResult<string>(null);
        public void DosyayiAc(string yol) { }
        public void KlasordeGoster(string yol) { }
    }

    /// <summary>
    /// Windows diyalogları. Diyalog, WebView2 olayının içinde değil olay bittikten sonra
    /// (BeginInvoke ile) açılır — iç içe ileti döngüsü WebView2'yi kilitlemesin.
    /// </summary>
    public sealed class WinFormsDosyaServisi : IDosyaServisi
    {
        public Task<string> AcilacakDosyaSecAsync(DosyaIstegi istek)
            => DiyalogAc(() => new OpenFileDialog { CheckFileExists = true }, istek);

        public Task<string> KaydedilecekDosyaSecAsync(DosyaIstegi istek)
            => DiyalogAc(() => new SaveFileDialog { OverwritePrompt = true, AddExtension = true, DefaultExt = "xlsx" }, istek);

        private static Task<string> DiyalogAc(Func<FileDialog> olustur, DosyaIstegi istek)
        {
            var sonuc = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pencere = AnaPencere.Ornek;
            if (pencere == null || pencere.IsDisposed)
            {
                sonuc.SetResult(null);
                return sonuc.Task;
            }

            pencere.BeginInvoke(new Action(() =>
            {
                try
                {
                    using var dlg = olustur();
                    dlg.Title = istek.Baslik;
                    dlg.Filter = istek.Filtre;
                    dlg.FilterIndex = 1;
                    if (!string.IsNullOrEmpty(istek.OnerilenAd)) dlg.FileName = istek.OnerilenAd;
                    KonumuAyarla(dlg, istek.BaslangicYolu);
                    sonuc.SetResult(dlg.ShowDialog(pencere) == DialogResult.OK ? dlg.FileName : null);
                }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz("Dosya diyaloğu açılamadı", ex);
                    sonuc.SetException(ex);
                }
            }));
            return sonuc.Task;
        }

        /// <summary>Diyalog, verilen dosyanın (ya da klasörün) bulunduğu yerde açılır.</summary>
        private static void KonumuAyarla(FileDialog dlg, string yol)
        {
            if (string.IsNullOrWhiteSpace(yol)) return;
            if (Directory.Exists(yol))
            {
                dlg.InitialDirectory = yol;
                return;
            }
            string klasor = Path.GetDirectoryName(yol);
            if (!string.IsNullOrEmpty(klasor) && Directory.Exists(klasor))
            {
                dlg.InitialDirectory = klasor;
                if (dlg is OpenFileDialog && File.Exists(yol)) dlg.FileName = Path.GetFileName(yol);
            }
        }

        public void DosyayiAc(string yol)
        {
            try { Process.Start(new ProcessStartInfo { FileName = yol, UseShellExecute = true }); }
            catch (Exception ex) { HataGunlugu.Yaz($"Dosya açılamadı: {yol}", ex); throw; }
        }

        public void KlasordeGoster(string yol)
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{yol}\"") { UseShellExecute = true }); }
            catch (Exception ex) { HataGunlugu.Yaz($"Klasör açılamadı: {yol}", ex); }
        }
    }

#if DEBUG
    /// <summary>
    /// Yalnızca Debug — uçtan uca otomasyon (SINAVPROGRAMI_OTOMASYON_DOSYALARI=klasör).
    /// Diyalog açılmaz: "aç" isteği klasördeki &lt;Anahtar&gt;.xlsx dosyasını, "kaydet" isteği
    /// klasörün "cikti" alt klasörüne önerilen adı döner. Release pakette yoktur.
    /// </summary>
    public sealed class OtomasyonDosyaServisi : IDosyaServisi
    {
        private readonly string _klasor;

        public OtomasyonDosyaServisi(string klasor) => _klasor = klasor;

        public Task<string> AcilacakDosyaSecAsync(DosyaIstegi istek)
        {
            string yol = Path.Combine(_klasor, istek.Anahtar + ".xlsx");
            return Task.FromResult(File.Exists(yol) ? yol : null);
        }

        public Task<string> KaydedilecekDosyaSecAsync(DosyaIstegi istek)
        {
            string cikti = Path.Combine(_klasor, "cikti");
            Directory.CreateDirectory(cikti);
            return Task.FromResult(Path.Combine(cikti, istek.OnerilenAd ?? istek.Anahtar + ".xlsx"));
        }

        public void DosyayiAc(string yol) { }
        public void KlasordeGoster(string yol) { }
    }
#endif
}
