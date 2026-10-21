using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 7 — Takvim bölümünde öğretim yılı (2026/2027) ve dönem (Güz / Bahar) seçimi.
/// İkisi de boş gelir, Excel çıktısından önce seçilmek zorundadır; final / gözetmenli
/// Excel başlıkları bu seçimden oluşur.
/// </summary>
static class OgretimDonemiTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 7 — Öğretim yılı ve dönem (Güz / Bahar)");

        t.Test("İlk açılışta öğretim yılı ve dönem boş (seçmek zorunlu)", () => BosFormIle(form =>
        {
            var cmb = Alan<ComboBox>(form, "cmbOgretimYili");
            Dogrula(cmb.SelectedIndex == -1 && cmb.Text == "", "Öğretim yılı dolu geldi.");
            Dogrula(!Alan<RadioButton>(form, "rbGuz").Checked && !Alan<RadioButton>(form, "rbBahar").Checked, "Dönem seçili geldi.");
            Dogrula(Alan<int?>(form, "_ogretimYili") == null && Alan<Yariyil?>(form, "_yariyil") == null, "Seçim alanları boş değil.");
        }));

        t.Test("Yıl seçenekleri 2026/2027 biçiminde ve bu öğretim yılını içeriyor", () =>
        {
            Dogrula(AkademikDonem.BuOgretimYili(new DateTime(2026, 10, 21)) == 2026, "Ekim 2026 → 2026/2027 olmalı.");
            Dogrula(AkademikDonem.BuOgretimYili(new DateTime(2027, 3, 15)) == 2026, "Mart 2027 → 2026/2027 olmalı (bahar).");
            Dogrula(AkademikDonem.BuOgretimYili(new DateTime(2027, 9, 1)) == 2027, "Eylül 2027 → 2027/2028 olmalı.");
            Dogrula(AkademikDonem.YilGorunen(2026) == "2026/2027", "Görünen biçim 2026/2027 değil.");
            BosFormIle(form =>
            {
                var secenekler = Alan<ComboBox>(form, "cmbOgretimYili").Items.Cast<object>().Select(o => o.ToString()).ToList();
                string buYil = AkademikDonem.YilGorunen(AkademikDonem.BuOgretimYili(DateTime.Today));
                Dogrula(secenekler.Contains(buYil), $"Seçeneklerde bu öğretim yılı yok: {string.Join(", ", secenekler)}");
            });
        });

        t.Test("Elle yazılan yıl çözülüyor: 2031/2032, 2031-2032, 2031 kabul; 2031/2035, abc, 31/32 ret", () =>
        {
            foreach (var gecerli in new[] { "2031/2032", "2031-2032", " 2031 / 2032 ", "2031" })
                Dogrula(AkademikDonem.YilCoz(gecerli) == 2031, $"'{gecerli}' kabul edilmedi.");
            foreach (var gecersiz in new[] { "2031/2035", "2031/2031", "abc", "31/32", "", "1999/2000", "2031/" })
                Dogrula(AkademikDonem.YilCoz(gecersiz) == null, $"'{gecersiz}' kabul edildi.");
        });

        t.Test("Listede olmayan yıl kutuya yazılabiliyor ve 2031/2032 biçimine düzeltiliyor", () => BosFormIle(form =>
        {
            var cmb = Alan<ComboBox>(form, "cmbOgretimYili");
            Dogrula(cmb.DropDownStyle == ComboBoxStyle.DropDown, "Kutu yazmaya kapalı.");
            cmb.Text = "2031-2032";
            Dogrula(Alan<int?>(form, "_ogretimYili") == 2031, "Yazılan yıl algılanmadı.");
            KutudanCik(cmb);
            Dogrula(cmb.Text == "2031/2032", $"Kutudan çıkınca biçim düzeltilmedi: '{cmb.Text}'.");
        }));

        t.Test("Geçersiz yazılan yıl seçilmemiş sayılıyor ve kutu uyarı rengi alıyor", () => BosFormIle(form =>
        {
            var cmb = Alan<ComboBox>(form, "cmbOgretimYili");
            var normalRenk = cmb.BackColor;
            cmb.Text = "2031/2035";
            Dogrula(Alan<int?>(form, "_ogretimYili") == null, "Geçersiz yıl kabul edildi.");
            Dogrula(cmb.BackColor != normalRenk, "Geçersiz yılda kutu rengi değişmedi.");
            cmb.Text = "2031/2032";
            Dogrula(Alan<int?>(form, "_ogretimYili") == 2031 && cmb.BackColor == normalRenk, "Düzeltilince kutu normale dönmedi.");
        }));

        t.Test("Elle yazılan (listede olmayan) yıl kapatıp açınca korunuyor", () =>
        {
            BosFormIle(form => Alan<ComboBox>(form, "cmbOgretimYili").Text = "2031/2032");
            using var yeniden = FormuAc();
            Dogrula(Alan<int?>(yeniden, "_ogretimYili") == 2031 && Alan<ComboBox>(yeniden, "cmbOgretimYili").Text == "2031/2032",
                "Elle yazılan yıl geri gelmedi.");
            yeniden.Close();
        });

        t.Test("Excel başlığı seçimden oluşuyor (2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE ...)", () =>
        {
            Dogrula(AkademikDonem.ProgramBasligi(2026, Yariyil.Guz, SinavDonemTipi.Vize) ==
                    "2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI", "Güz / vize başlığı yanlış.");
            Dogrula(AkademikDonem.ProgramBasligi(2027, Yariyil.Bahar, SinavDonemTipi.Final) ==
                    "2027-2028 ÖĞRETİM YILI BAHAR DÖNEMİ FİNAL SINAV PROGRAMI", "Bahar / final başlığı yanlış.");
        });

        t.Test("Uygulamada seçilen yıl, dönem ve sınav türü başlığa yansıyor", () => BosFormIle(form =>
        {
            Sec(form, 2026, bahar: true);
            Alan<ComboBox>(form, "cmbSinavTipi").SelectedIndex = 1;   // Final
            string baslik = (string)Cagir(form, "ProgramBasligi");
            Dogrula(baslik == "2026-2027 ÖĞRETİM YILI BAHAR DÖNEMİ FİNAL SINAV PROGRAMI", $"Başlık: {baslik}");
        }));

        t.Test("Seçim kapatıp açınca korunuyor (Madde 1 ile)", () =>
        {
            BosFormIle(form => Sec(form, 2026, bahar: false));
            using var yeniden = FormuAc();
            Dogrula(Alan<int?>(yeniden, "_ogretimYili") == 2026 && Alan<Yariyil?>(yeniden, "_yariyil") == Yariyil.Guz,
                "Seçim geri gelmedi.");
            Dogrula(Alan<ComboBox>(yeniden, "cmbOgretimYili").Text == "2026/2027" && Alan<RadioButton>(yeniden, "rbGuz").Checked,
                "Kontroller seçimi göstermiyor.");
            yeniden.Close();
        });

        t.Test("Final / gözetmenli Excel'inde sabit '2025-2026 BAHAR FİNAL' başlığı kalmadı, seçim yazılıyor", () =>
        {
            const string baslik = "2027-2028 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI";
            string yol = Path.Combine(Path.GetTempPath(), $"baslik_{Guid.NewGuid():N}.xlsx");
            try
            {
                var gun = new DateTime(2026, 10, 22);
                var sonuclar = new List<SinavAtama>
                {
                    new() { DersAdi = "FİZİK II", BolumAdi = "BİL", SinifSeviyesi = 1, Sube = "A", Kontenjan = 40, Gun = gun, Saat = "09:00",
                            AtananSalonlar = new() { new() { SalonKodu = "T4-204", SalonKapasitesi = 60, AtananOgrenciSayisi = 40, GozetmenAdSoyad = "ALİ VELİ" } } }
                };
                ExcelMatrisExportHelper.MatrisExcelOlustur(sonuclar, new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 } },
                    yol, baslik, new List<Gozetmen> { new() { AdSoyad = "ALİ VELİ", Bolum = "Bilgisayar Mühendisliği" } });

                using var paket = new ExcelPackage(new FileInfo(yol));
                var metinler = paket.Workbook.Worksheets.Where(w => w.Dimension != null)
                    .SelectMany(w => w.Cells[w.Dimension.Address].Select(c => (Sayfa: w.Name, Metin: c.Text ?? ""))).ToList();
                Dogrula(!metinler.Any(m => m.Metin.Contains("2025-2026")), "Sabit 2025-2026 başlığı hâlâ var.");
                Dogrula(!metinler.Any(m => m.Metin.Contains("YARIYIL İÇİ")), "Sabit 'YARIYIL İÇİ' başlığı hâlâ var.");
                Dogrula(metinler.Any(m => m.Metin == baslik), "Sınav Programı Formu başlığı seçimi göstermiyor.");
                // Madde 9: gözetmenli formun başlığı sade formla aynı ("(GÖZETMENLİ)" eki yok)
                Dogrula(metinler.Count(m => m.Metin == baslik && m.Sayfa.StartsWith("Sınav Programı Formu")) >= 2,
                    "Gözetmenli form başlığı seçimi göstermiyor.");
                Dogrula(metinler.Any(m => m.Sayfa == GozetmenliSablonOkuyucu.SayfaAdi && m.Metin.Contains(baslik)),
                    "Slot Bazlı Sınav Planı başlığı seçimi göstermiyor.");
            }
            finally { try { File.Delete(yol); } catch { } }
        });
    }

    private static void Sec(MainForm form, int yil, bool bahar)
    {
        var cmb = Alan<ComboBox>(form, "cmbOgretimYili");
        cmb.SelectedIndex = cmb.Items.Cast<object>().ToList().FindIndex(o => o.ToString() == AkademikDonem.YilGorunen(yil));
        Alan<RadioButton>(form, bahar ? "rbBahar" : "rbGuz").Checked = true;
    }

    /// <summary>Kullanıcının kutudan çıkmasını (Leave) taklit eder.</summary>
    private static void KutudanCik(Control kutu)
        => typeof(Control).GetMethod("OnLeave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                          .Invoke(kutu, new object[] { EventArgs.Empty });

    private static void BosFormIle(Action<MainForm> govde)
    {
        OturumKaydi.Sil();
        using var form = FormuAc();
        govde(form);
        form.Close();
    }
}
