using System.Reflection;
using System.Text;
using OfficeOpenXml;
using SinavProgrami.DataAccess;

namespace SinavProgrami.Testler;

/// <summary>
/// Tüm revizyon testlerini sırayla çalıştırır. Hepsi geçerse 0, biri bile
/// başarısızsa 1 ile çıkar.
/// </summary>
static class Program
{
    [STAThread]
    static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        DatabaseHelper.VeritabaniniHazirla();   // test klasöründe ayrı SinavDB.db

        // Testler bilgisayardaki GERÇEK oturum kaydına dokunmasın
        OturumKaydi.Klasor = Path.Combine(Path.GetTempPath(), "SinavProgramiTest_" + Guid.NewGuid().ToString("N"));

        var t = new TestKosucu();
        try
        {
            SablonTakvimiTestleri.Calistir(t);   // Madde 0 — şablonda tüm günler tek bantta
            OturumKaydiTestleri.Calistir(t);     // Madde 1 — kapatıp açınca kaldığı yerden devam
            GozetmenDuzenlemeTestleri.Calistir(t); // Madde 2 — gözetmen ekle / sil / düzelt
            DerslikTestleri.Calistir(t);         // Madde 3 — derslikler veritabanında değil, kullanıcı yüklüyor
            GozetmenAgirlikTestleri.Calistir(t); // Madde 4 — ağırlık görev sayısına oranlı yansır
            PencereTestleri.Calistir(t);         // Madde 5 — pencere tam ekran / boyutlandırma
            SalonSirasiTestleri.Calistir(t);     // Madde 6 — taslak / final Excel'inde T4 bloğu önce
        }
        finally
        {
            try { Directory.Delete(OturumKaydi.Klasor, recursive: true); } catch { }
        }
        return t.Ozet();
    }
}

/// <summary>Bağımlılıksız küçük test çalıştırıcı.</summary>
class TestKosucu
{
    private int _basarili, _basarisiz;

    public void Baslik(string baslik) => Console.WriteLine($"\n── {baslik} ──");

    public void Test(string ad, Action govde)
    {
        try
        {
            govde();
            _basarili++;
            Console.WriteLine($"  ✔ {ad}");
        }
        catch (Exception ex)
        {
            _basarisiz++;
            Console.WriteLine($"  ✘ {ad}\n      {ex.Message}");
        }
    }

    public static void Dogrula(bool kosul, string mesaj)
    {
        if (!kosul) throw new Exception(mesaj);
    }

    public int Ozet()
    {
        Console.WriteLine($"\n{_basarili} test geçti, {_basarisiz} test başarısız.");
        return _basarisiz == 0 ? 0 : 1;
    }

    /// <summary>Formun private alanını okur (arayüz testleri için).</summary>
    public static T Alan<T>(object nesne, string ad)
    {
        var alan = nesne.GetType().GetField(ad, BindingFlags.NonPublic | BindingFlags.Instance)
                   ?? throw new Exception($"'{ad}' alanı bulunamadı.");
        return (T)alan.GetValue(nesne);
    }

    /// <summary>Formun private metodunu çağırır (onay penceresi açmayan iç işlemler için).</summary>
    public static object Cagir(object nesne, string ad, params object[] parametreler)
    {
        var metot = nesne.GetType().GetMethod(ad, BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new Exception($"'{ad}' metodu bulunamadı.");
        try { return metot.Invoke(nesne, parametreler); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }

    /// <summary>Uygulama penceresini açar ve açılış (Shown) olayının işlenmesini bekler.</summary>
    public static MainForm FormuAc()
    {
        var form = new MainForm();
        form.Show();
        for (int i = 0; i < 20; i++) Application.DoEvents();
        return form;
    }

    /// <summary>Grid'in veri satırı sayısı (en alttaki boş "yeni satır" hariç).</summary>
    public static int SatirSayisi(DataGridView grid) => grid.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);
}
