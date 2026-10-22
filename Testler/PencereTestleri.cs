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

        t.Test("Sonuç paneli ana pencerede gizli; düğme ayrı pencerede açıyor, tekrar basınca kapatıyor", () => BosFormIle(form =>
        {
            var buton = Alan<Button>(form, "btnSagToggle");
            var sag = Alan<Panel>(form, "panelSag");
            Dogrula(!sag.Visible && buton.Enabled, "Açılışta panel görünür ya da düğme pasif.");

            buton.PerformClick(); Bekle();
            var pencere = Alan<Form>(form, "_sonucPenceresi");
            Dogrula(pencere != null && pencere.Visible, "Sonuç penceresi açılmadı.");
            Dogrula(sag.Parent == pencere && sag.Visible, "Sonuç paneli ayrı pencereye taşınmadı.");
            var sekmeler = Alan<TabControl>(form, "tabControlSonuc");
            Dogrula(sekmeler.Width > pencere.ClientSize.Width - 80 && sekmeler.Bottom > pencere.ClientSize.Height - 80,
                $"Sonuç tabloları pencereyi doldurmuyor ({sekmeler.Size}, pencere {pencere.ClientSize}).");

            buton.PerformClick(); Bekle();
            Dogrula(Alan<Form>(form, "_sonucPenceresi") == null && pencere.IsDisposed, "Sonuç penceresi kapanmadı.");
            Dogrula(sag.Parent == form && !sag.Visible, "Panel ana pencereye gizli olarak dönmedi.");
        }));

        t.Test("Sonuç penceresi normal boyutta da açılıyor; ana pencere tam ekranda paneli göstermiyor", () => BosFormIle(form =>
        {
            Alan<Button>(form, "btnSagToggle").PerformClick(); Bekle();
            Dogrula(Alan<Form>(form, "_sonucPenceresi") != null, "Normal boyutta sonuç penceresi açılmadı.");
            Alan<Button>(form, "btnSagToggle").PerformClick(); Bekle();

            form.WindowState = FormWindowState.Maximized; Bekle();
            var sol = Alan<Panel>(form, "panelSol");
            Dogrula(!Alan<Panel>(form, "panelSag").Visible, "Tam ekranda ana pencerede sonuç paneli açıldı.");
            Dogrula(sol.Width >= form.ClientSize.Width - 1 && sol.Height == form.ClientSize.Height,
                "Tam ekranda sol panel pencereyi doldurmuyor.");
        }));

        t.Test("Tam ekrandan çıkınca pencere eski boyutuna dönüyor", () => BosFormIle(form =>
        {
            var normalBoyut = form.Size;
            form.WindowState = FormWindowState.Maximized; Bekle();
            form.WindowState = FormWindowState.Normal; Bekle();
            Dogrula(form.Size == normalBoyut, $"Pencere eski boyutuna dönmedi: {form.Size} (beklenen {normalBoyut}).");
        }));

        t.Test("Sonuç penceresi açıkken kapatılırsa yeniden açılışta açık geliyor; ana pencereyle kapanıyor", () =>
        {
            Form ilkPencere = null;
            BosFormIle(form =>
            {
                Alan<Button>(form, "btnSagToggle").PerformClick(); Bekle();
                ilkPencere = Alan<Form>(form, "_sonucPenceresi");
            });
            Dogrula(ilkPencere != null && ilkPencere.IsDisposed, "Ana pencere kapanınca sonuç penceresi açık kaldı.");
            using var yeniden = FormuAc();
            Dogrula(Alan<Form>(yeniden, "_sonucPenceresi") != null && Alan<Panel>(yeniden, "panelSag").Visible,
                "Yeniden açılışta sonuç penceresi açılmadı.");
            yeniden.Close();
        });

        t.Test("Tam ekranda kapatılırsa yeniden tam ekran açılıyor (Madde 1 ile)", () =>
        {
            BosFormIle(form => { form.WindowState = FormWindowState.Maximized; Bekle(); });
            using var yeniden = FormuAc();
            Dogrula(yeniden.WindowState == FormWindowState.Maximized, "Yeniden açılışta tam ekran değil.");
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
