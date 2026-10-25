#nullable disable
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SinavProgrami.Services
{
    /// <summary>
    /// HATA GÜNLÜĞÜ — beklenmeyen hatalar %LocalAppData%\SinavProgrami\hata.log dosyasına yazılır;
    /// kullanıcıya Türkçe, kısa bir mesaj gösterilir. Destek isterken bu dosya gönderilebilir.
    /// </summary>
    public static class HataGunlugu
    {
        private const long EnFazlaBoyut = 1024 * 1024;   // 1 MB'ı geçince eski kayıt kenara alınır
        private static readonly object Kilit = new object();

        /// <summary>
        /// Uygulamanın bu bilgisayara özel (taşınmayan) klasörü: hata günlüğü, WebView2 önbelleği.
        /// (Testlerde geçici bir klasöre yönlendirilir.)
        /// </summary>
        public static string Klasor { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SinavProgrami");

        public static string DosyaYolu => Path.Combine(Klasor, "hata.log");

        public static void Yaz(string baglam, Exception ex)
        {
            try
            {
                lock (Kilit)
                {
                    Directory.CreateDirectory(Klasor);
                    var bilgi = new FileInfo(DosyaYolu);
                    if (bilgi.Exists && bilgi.Length > EnFazlaBoyut)
                        File.Move(DosyaYolu, Path.Combine(Klasor, "hata.eski.log"), overwrite: true);
                    File.AppendAllText(DosyaYolu,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {baglam}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
                }
            }
            catch
            {
                // Günlüğe yazılamıyorsa yapacak bir şey yok — asıl hatayı gölgelemesin
            }
        }

        /// <summary>Yakalanmamış hataları günlüğe yönlendirir (yeni arayüzde, pencere açılmadan önce çağrılır).</summary>
        public static void Kur()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                Yaz("Arayüz iş parçacığında yakalanmamış hata", e.Exception);
                MessageBox.Show(
                    "Beklenmeyen bir hata oluştu; işlem tamamlanamamış olabilir.\n\n" +
                    $"{e.Exception.Message}\n\nAyrıntılar şu dosyaya yazıldı:\n{DosyaYolu}",
                    "Sınav Programı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex) Yaz("Yakalanmamış hata (uygulama kapanıyor)", ex);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Yaz("Gözlenmeyen arka plan görevi hatası", e.Exception);
                e.SetObserved();
            };
        }
    }
}
