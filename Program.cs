#nullable disable
using OfficeOpenXml;
using SinavProgrami.DataAccess;

namespace SinavProgrami;

static class Program
{
    [STAThread]
    static void Main()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        ApplicationConfiguration.Initialize();

        // Veritabanı, exe'nin yanındaki tek dosyalık SinavDB.db'dir.
        // Yoksa burada oluşturulur — uygulamanın kopyalandığı her bilgisayarda
        // hiçbir kurulum gerektirmemesini bu adım sağlar.
        try
        {
            DatabaseHelper.VeritabaniniHazirla();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Veritabanı hazırlanamadı:\n\n{ex.Message}\n\n" +
                $"Beklenen konum:\n{DatabaseHelper.VeritabaniYolu}\n\n" +
                "Uygulamayı yazma izni olan bir klasöre taşımayı deneyin " +
                "(ör. Belgeler veya Masaüstü).",
                "Sınav Programı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm());
    }
}
