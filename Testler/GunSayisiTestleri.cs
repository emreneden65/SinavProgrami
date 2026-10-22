using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 10 — Vize / Final gün sayısında kısıt yok: 7 / 10 yalnızca varsayılan,
/// vize 10-15 gün de sürebilir.
/// </summary>
static class GunSayisiTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 10 — Sınav günü sayısı serbest");

        t.Test("15 günlük vize: hafta sonları dahil art arda 15 gün", () =>
        {
            var gunler = SinavTipi.GunleriUret(new DateTime(2026, 10, 26), SinavDonemTipi.Vize, 15);
            Dogrula(gunler.Count == 15, $"{gunler.Count} gün üretildi.");
            Dogrula(gunler.Last() == new DateTime(2026, 11, 9), $"Son gün {gunler.Last():dd.MM.yyyy}.");
        });

        t.Test("15 günlük final: hafta sonları boş, 15 iş günü", () =>
        {
            var gunler = SinavTipi.GunleriUret(new DateTime(2026, 10, 26), SinavDonemTipi.Final, 15);
            Dogrula(gunler.Count == 15, $"{gunler.Count} gün üretildi.");
            Dogrula(gunler.All(g => g.DayOfWeek != DayOfWeek.Saturday && g.DayOfWeek != DayOfWeek.Sunday),
                "Final takviminde hafta sonu günü var.");
        });

        t.Test("Gün kutusu tipe göre varsayılanı öneriyor, 15 gün girilebiliyor", () =>
        {
            OturumKaydi.Sil();
            using var form = FormuAc();
            var tip = Alan<ComboBox>(form, "cmbSinavTipi");
            var gun = Alan<NumericUpDown>(form, "numGunSayisi");
            tip.SelectedIndex = 1;
            Dogrula(gun.Value == 10, $"Final seçilince gün kutusu {gun.Value}.");
            tip.SelectedIndex = 0;
            Dogrula(gun.Value == 7, $"Vize seçilince gün kutusu {gun.Value}.");
            gun.Value = 15;
            Dogrula(gun.Value == 15 && gun.Maximum >= 15, "15 gün girilemedi.");
            Dogrula(!Alan<Label>(form, "lblDurum").Text.Contains("hedef"), "Durum yazısında hâlâ gün hedefi var.");
            form.Close();
        });
    }
}
