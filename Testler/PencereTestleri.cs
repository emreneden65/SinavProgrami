using SinavProgrami.DataAccess;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 5 — Uygulama penceresi büyütülebilir / tam ekran yapılabilir; tam ekran
/// durumu kapatıp açınca korunur.
/// </summary>
static class PencereTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 5 — Pencere tam ekran / boyutlandırma");

        t.Test("Pencere boyutlandırılabilir ve tam ekran düğmesi açık", () => BosFormIle(form =>
        {
            Dogrula(form.FormBorderStyle == FormBorderStyle.Sizable, "Pencere kenarından boyutlandırılamıyor.");
            Dogrula(form.MaximizeBox, "Tam ekran (büyüt) düğmesi kapalı.");
        }));

        t.Test("Tam ekranda sonuç paneli açılıyor ve tablolar ekranı dolduruyor", () => BosFormIle(form =>
        {
            form.WindowState = FormWindowState.Maximized;
            Bekle();
            var sol = Alan<Panel>(form, "panelSol");
            var sag = Alan<Panel>(form, "panelSag");
            var sekmeler = Alan<TabControl>(form, "tabControlSonuc");
            Dogrula(sag.Visible, "Tam ekranda sonuç paneli açılmadı (sağ taraf boş).");
            Dogrula(sol.Height == form.ClientSize.Height, "Sol panel pencere boyunca uzamadı.");
            Dogrula(sag.Left >= sol.Right - 1 && sag.Right >= form.ClientSize.Width - 1, "Sonuç paneli kalan alanı doldurmuyor.");
            Dogrula(sekmeler.Width > sag.Width - 80 && sekmeler.Bottom > sag.Height - 80,
                $"Sonuç tabloları büyümedi ({sekmeler.Size}, panel {sag.Size}).");
        }));

        t.Test("Tam ekrandan çıkınca otomatik açılan panel kapanıyor, pencere eski boyutuna dönüyor", () => BosFormIle(form =>
        {
            var normalBoyut = form.Size;
            form.WindowState = FormWindowState.Maximized; Bekle();
            form.WindowState = FormWindowState.Normal; Bekle();
            Dogrula(!Alan<Panel>(form, "panelSag").Visible, "Tam ekranın açtığı panel normale dönünce kapanmadı (sağda şerit kalır).");
            Dogrula(form.Size == normalBoyut, $"Pencere eski boyutuna dönmedi: {form.Size} (beklenen {normalBoyut}).");
        }));

        t.Test("Tam ekranda panel elle açıldıysa normale dönünce pencere paneli gösterecek kadar genişliyor", () => BosFormIle(form =>
        {
            form.WindowState = FormWindowState.Maximized; Bekle();
            var buton = Alan<Button>(form, "btnSagToggle");
            buton.PerformClick(); buton.PerformClick();   // kullanıcı kapatıp yeniden açtı
            form.WindowState = FormWindowState.Normal; Bekle();
            int ekran = Screen.FromControl(form).WorkingArea.Width - (form.Width - form.ClientSize.Width);
            Dogrula(Alan<Panel>(form, "panelSag").Visible, "Kullanıcının açtığı panel kapandı.");
            Dogrula(form.ClientSize.Width >= Math.Min(1300, ekran), $"Pencere genişlemedi: {form.ClientSize.Width}.");
        }));

        t.Test("Tam ekranda kapatılırsa yeniden tam ekran açılıyor (Madde 1 ile)", () =>
        {
            BosFormIle(form => { form.WindowState = FormWindowState.Maximized; Bekle(); });
            using var yeniden = FormuAc();
            Dogrula(yeniden.WindowState == FormWindowState.Maximized, "Yeniden açılışta tam ekran değil.");
            Dogrula(Alan<Panel>(yeniden, "panelSag").Visible, "Yeniden açılışta sonuç paneli kapalı.");
            yeniden.Close();
        });

        t.Test("Normal boyutta kapatılırsa son boyutuyla açılıyor", () =>
        {
            var boyut = new Size(1000, 700);
            BosFormIle(form => { form.Size = boyut; Bekle(); });
            using var yeniden = FormuAc();
            Dogrula(yeniden.WindowState == FormWindowState.Normal && yeniden.Size == boyut,
                $"Yeniden açılış boyutu {yeniden.Size}, beklenen {boyut}.");
            yeniden.Close();
        });
    }

    /// <summary>Kayıtsız (ilk açılış) formla çalışır; kapanışta oturum kaydedilir.</summary>
    private static void BosFormIle(Action<MainForm> govde)
    {
        OturumKaydi.Sil();
        using var form = FormuAc();
        govde(form);
        form.Close();
    }

    private static void Bekle()
    {
        for (int i = 0; i < 10; i++) Application.DoEvents();
    }
}
