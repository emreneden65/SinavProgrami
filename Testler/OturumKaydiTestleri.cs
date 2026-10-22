using System.Diagnostics;
using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 1 — Uygulama kapatılıp açılınca kaldığı yerden devam eder
/// (oturum %AppData%\SinavProgrami\oturum.json'da saklanır).
/// </summary>
static class OturumKaydiTestleri
{
    private const string Bolum = "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ";

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 1 — Oturum kaydı (kaldığı yerden devam)");

        t.Test("Kayıt yazılıp okununca hiçbir veri kaybolmuyor", () =>
        {
            var durum = OrnekDurum();
            string ilk = OturumKaydi.Serilestir(durum);
            OturumKaydi.Yaz(ilk);
            var okunan = OturumKaydi.Oku(out string hata);
            Dogrula(hata == null && okunan != null, $"Okuma hatası: {hata}");
            Dogrula(OturumKaydi.Serilestir(okunan) == ilk, "Okunan oturum yazılanla aynı değil.");
        });

        t.Test("Okunan sözlükler büyük/küçük harf duyarsız kalıyor", () =>
        {
            OturumKaydi.Yaz(OturumKaydi.Serilestir(OrnekDurum()));
            var okunan = OturumKaydi.Oku(out _);
            Dogrula(okunan.HataOranlari.ContainsKey(Bolum.ToLowerInvariant()), "Hata oranı sözlüğü harf duyarlı açıldı.");
            Dogrula(okunan.GunSalonSatirlari[0].Subeler.ContainsKey("a"), "Şube sözlüğü harf duyarlı açıldı.");
        });

        t.Test("Tarihler saat diliminden bağımsız kaydediliyor (eski '+03:00' kayıtlar da okunuyor)", () =>
        {
            var durum = new OturumDurumu
            {
                Takvim = new List<TakvimGunu> { new() { Gun = DateTime.Today, Saatler = new List<string> { "09:00" } } }
            };
            string json = OturumKaydi.Serilestir(durum);
            Dogrula(!json.Contains("+0") && !json.Contains("Z\""), "Tarih saat dilimiyle yazıldı.");
            OturumKaydi.Yaz(json);
            Dogrula(OturumKaydi.Oku(out _).Takvim[0].Gun == DateTime.Today, "Tarih geri okunurken kaydı.");

            // Madde 1'in ilk sürümü tarihleri "+03:00" ekiyle yazıyordu — kullanıcıdaki kayıt
            OturumKaydi.Yaz("{\"Takvim\":[{\"Gun\":\"2026-10-22T00:00:00+03:00\",\"Saatler\":[\"09:00\"]}]}");
            Dogrula(OturumKaydi.Oku(out _).Takvim[0].Gun == new DateTime(2026, 10, 22), "Eski biçimli tarih yanlış okundu.");
        });

        t.Test("Kayıt yoksa boş döner, hata vermez", () =>
        {
            OturumKaydi.Sil();
            Dogrula(OturumKaydi.Oku(out string hata) == null && hata == null, "Kayıt yokken null/hatasız dönmedi.");
        });

        t.Test("Bozuk kayıt kenara yedeklenir, üzerine yazılıp kaybolmaz", () =>
        {
            Directory.CreateDirectory(OturumKaydi.Klasor);
            File.WriteAllText(OturumKaydi.DosyaYolu, "{bozuk");
            var okunan = OturumKaydi.Oku(out string hata);
            Dogrula(okunan == null && hata != null, "Bozuk kayıt hata olarak bildirilmedi.");
            Dogrula(File.Exists(OturumKaydi.BozukYedekYolu), "Bozuk kayıt yedeklenmedi.");
            Dogrula(File.ReadAllText(OturumKaydi.BozukYedekYolu) == "{bozuk", "Yedek içeriği farklı.");
        });

        t.Test("Büyük bir dönem 1 saniyenin altında kaydediliyor", () =>
        {
            var durum = BuyukDurum();
            var sw = Stopwatch.StartNew();
            OturumKaydi.Yaz(OturumKaydi.Serilestir(durum));
            sw.Stop();
            Dogrula(sw.ElapsedMilliseconds < 1000, $"Kayıt {sw.ElapsedMilliseconds} ms sürdü.");
        });

        t.Test("Uygulama açılınca önceki oturumu ekrana geri yüklüyor", () =>
        {
            OturumKaydi.Yaz(OturumKaydi.Serilestir(OrnekDurum()));
            using var form = FormuAc();

            Dogrula(Alan<List<Ders>>(form, "_dersListesi").Count == 3, "Ders listesi geri gelmedi.");
            Dogrula(SatirSayisi(Alan<DataGridView>(form, "dgvDersler")) == 3, "Ders grid'i dolmadı.");
            Dogrula(Alan<Dictionary<DateTime, List<string>>>(form, "_takvimSlotlari").Count == 10, "Takvim geri gelmedi.");
            Dogrula(Alan<ComboBox>(form, "cmbSinavTipi").SelectedIndex == 1, "Final seçimi geri gelmedi.");
            Dogrula(Alan<HashSet<string>>(form, "_uygulamaliDersler").Contains("VERİ YAPILARI"), "Uygulamalı işareti geri gelmedi.");

            var hataGrid = Alan<DataGridView>(form, "dgvHataOrani");
            bool oranVar = hataGrid.Rows.Cast<DataGridViewRow>()
                .Any(r => Equals(r.Cells[0].Value, Bolum) && Equals(r.Cells[1].Value, 12.5.ToString("0.#")));
            Dogrula(oranVar, "Hata oranı grid'de görünmüyor.");

            var gozetmenGrid = Alan<DataGridView>(form, "dgvGozetmenler");
            Dogrula(SatirSayisi(gozetmenGrid) == 2 && Equals(gozetmenGrid.Rows[0].Cells["Agirlik"].Value, 1.5.ToString("0.0#")),
                "Gözetmen listesi/ağırlığı geri gelmedi.");

            Dogrula(Alan<List<SinavAtama>>(form, "_sablonSonuclari").Count == 1, "Şablon yerleşimi geri gelmedi.");
            Dogrula(SatirSayisi(Alan<DataGridView>(form, "dgvSablon")) > 0, "Şablon grid'i kurulmadı.");
            var son = Alan<List<SinavAtama>>(form, "_sonSonuclar");
            Dogrula(son.Count == 1 && son[0].AtananSalonlar[0].GozetmenAdSoyad == "ALİ VELİ", "Salon/gözetmen ataması geri gelmedi.");
            Dogrula(SatirSayisi(Alan<DataGridView>(form, "dgvMatris")) > 0, "Matris grid'i kurulmadı.");
            Dogrula(Alan<List<GunlukSalonDagitici.GunSatir>>(form, "_gunSalonSatirlari").Count == 1, "Salon dağıtım satırları geri gelmedi.");

            Dogrula(Alan<Button>(form, "btnSalonAta").Enabled && Alan<Button>(form, "btnMatrisExport").Enabled,
                "Açık adım butonları açık gelmedi.");
            Dogrula(!Alan<Button>(form, "btnFinalExport").Enabled && !Alan<Button>(form, "btnOtomatikDagit").Enabled,
                "Kapalı adım butonları kapalı gelmedi.");
            Dogrula(Alan<TabControl>(form, "tabControlSonuc").SelectedIndex == 1, "Seçili sekme geri gelmedi.");
            Dogrula(Alan<Panel>(form, "panelSag").Visible, "Sonuç paneli açık gelmedi.");
            Dogrula(Alan<Label>(form, "lblDurum").Text.StartsWith("↩"), "Geri yükleme bilgisi gösterilmedi.");
            form.Close();
        });

        t.Test("Uygulama kapanırken son değişiklikler kaydediliyor", () =>
        {
            OturumKaydi.Yaz(OturumKaydi.Serilestir(OrnekDurum()));
            using (var form = FormuAc())
            {
                Alan<List<Gozetmen>>(form, "_gozetmenListesi")[0].Agirlik = 2.0;
                Alan<Dictionary<string, double>>(form, "_bolumHataOranlari")[Bolum] = 30;
                form.Close();
            }
            var okunan = OturumKaydi.Oku(out _);
            Dogrula(okunan.Gozetmenler[0].Agirlik == 2.0, "Kapanışta ağırlık değişikliği kaydedilmedi.");
            Dogrula(okunan.HataOranlari[Bolum] == 30, "Kapanışta hata oranı değişikliği kaydedilmedi.");
            Dogrula(okunan.Dersler.Count == 3 && okunan.SonSonuclar.Count == 1, "Kapanışta diğer veriler kayboldu.");
        });

        t.Test("Kayıt yoksa uygulama eskisi gibi boş açılıyor ve kapanınca kayıt oluşuyor", () =>
        {
            OturumKaydi.Sil();
            using (var form = FormuAc())
            {
                Dogrula(Alan<List<Ders>>(form, "_dersListesi").Count == 0, "Boş açılışta ders listesi dolu.");
                Dogrula(Alan<Dictionary<DateTime, List<string>>>(form, "_takvimSlotlari").Count == SinavTipi.VizeGunSayisi,
                    "Boş açılışta varsayılan vize takvimi kurulmadı.");
                Dogrula(!Alan<Label>(form, "lblDurum").Text.StartsWith("↩"), "Kayıt yokken 'geri yüklendi' yazıyor.");
                form.Close();
            }
            Dogrula(File.Exists(OturumKaydi.DosyaYolu), "Kapanışta oturum dosyası oluşmadı.");
        });
    }

    /// <summary>Örnek dönem: 3 şube, 10 günlük final, 2 gözetmen (ikisi de salon görevli).</summary>
    internal static OturumDurumu OrnekDurum()
    {
        var gunler = SinavTipi.GunleriUret(new DateTime(2026, 10, 26), SinavDonemTipi.Final);
        return new OturumDurumu
        {
            // Madde 3'ten beri salonların tek kaynağı yüklenen derslik listesi
            Derslikler = new List<Salon>
            {
                new() { SalonKodu = "T4-204", Kapasite = 60, SalonTipi = "NORMAL" },
                new() { SalonKodu = "T4-205", Kapasite = 48, SalonTipi = "NORMAL" }
            },
            Dersler = new List<Ders>
            {
                new() { BolumAdi = Bolum, DersKodu = "BIL214", DersAdi = "VERİ YAPILARI", Sube = "A", Kontenjan = 80, SinifSeviyesi = 2, OgretimElemani = "Dr. X" },
                new() { BolumAdi = Bolum, DersKodu = "BIL214", DersAdi = "VERİ YAPILARI", Sube = "B", Kontenjan = 70, SinifSeviyesi = 2 },
                new() { BolumAdi = Bolum, DersKodu = "MAT101", DersAdi = "MATEMATİK I", Sube = "A", Kontenjan = 120, SinifSeviyesi = 1 },
            },
            Takvim = gunler.Select(g => new TakvimGunu { Gun = g, Saatler = new List<string> { "09:00", "11:00", "13:00" } }).ToList(),
            SinavTipi = SinavDonemTipi.Final,
            BaslangicTarihi = gunler[0],
            Havuzlar = new List<DersHavuzu>
            {
                new() { DersAdi = "MATEMATİK I", Bolumler = new List<string> { "BİL", "MAK" },
                        Uyeler = new List<HavuzUyesi> { new() { Bolum = "BİL", Sube = "A", SinifSeviyesi = 1 } } }
            },
            OrtakSecimYapildi = true,
            ManuelOrtakKodlar = new List<string> { "MAT101" },
            ManuelOrtakDersAdlari = new List<string> { "MATEMATİK I" },
            UygulamaliDersler = new List<string> { "VERİ YAPILARI" },
            HataOranlari = new Dictionary<string, double> { [Bolum] = 12.5 },
            SablonSonuclari = new List<SinavAtama>
            {
                new() { DersAdi = "VERİ YAPILARI", BolumAdi = Bolum, SinifSeviyesi = 2, Sube = "A", Kontenjan = 80, Gun = gunler[0], Saat = "09:00" }
            },
            SablonIntNotlari = new Dictionary<string, string> { ["0|09:00|BİL|0"] = "not" },
            GunSalonSatirlari = new List<GunlukSalonDagitici.GunSatir>
            {
                new()
                {
                    Gun = gunler[0], Saat = "09:00", Bolum = "BİL", Sinif = 2, DersAdi = "VERİ YAPILARI", Toplam = 80,
                    Subeler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["A"] = 80 },
                    SalonDagilimi = new Dictionary<string, int> { ["T4-204"] = 60, ["T4-205"] = 20 },
                    SalonSubeDagilimi = new Dictionary<string, Dictionary<string, int>>
                    {
                        ["T4-204"] = new() { ["A"] = 60 }, ["T4-205"] = new() { ["A"] = 20 }
                    }
                }
            },
            SonSonuclar = new List<SinavAtama>
            {
                new()
                {
                    DersAdi = "VERİ YAPILARI", BolumAdi = "BİL", SinifSeviyesi = 2, Sube = "A", Kontenjan = 80, Gun = gunler[0], Saat = "09:00",
                    AtananSalonlar = new List<SalonAtama>
                    {
                        new() { SalonKodu = "T4-204", SalonKapasitesi = 60, AtananOgrenciSayisi = 60, Sube = "A", GozetmenAdSoyad = "ALİ VELİ" },
                        new() { SalonKodu = "T4-205", SalonKapasitesi = 48, AtananOgrenciSayisi = 20, Sube = "A", GozetmenAdSoyad = "AYŞE KAYA" }
                    }
                }
            },
            Gozetmenler = new List<Gozetmen>
            {
                new() { Kod = "B1", AdSoyad = "ALİ VELİ", Bolum = "Bilgisayar Mühendisliği", Agirlik = 1.5, AtananSinavSayisi = 1 },
                new() { Kod = "B2", AdSoyad = "AYŞE KAYA", Bolum = "Bilgisayar Mühendisliği", Agirlik = 1.0, AtananSinavSayisi = 1 }
            },
            Butonlar = new Dictionary<string, bool>
            {
                ["btnOtomatikDagit"] = false, ["btnSalonAta"] = true,
                ["btnSalonExport"] = true, ["btnGozetmenAta"] = true, ["btnMatrisExport"] = true,
                ["btnGozetmenliYukle"] = false, ["btnFinalExport"] = false
            },
            SeciliSekme = 1,
            SonucPaneliAcik = true,
            PencereTamEkran = true   // sonuç paneli yalnızca tam ekranda açılır
        };
    }

    /// <summary>Gerçekçi üst sınır: ~2000 şube, ~2000 salon atanmış sınav.</summary>
    private static OturumDurumu BuyukDurum()
    {
        var d = OrnekDurum();
        var gun = d.Takvim[0].Gun;
        d.Dersler = Enumerable.Range(0, 2000).Select(i => new Ders
        {
            BolumAdi = Bolum, DersKodu = $"BIL{i:000}", DersAdi = $"DERS {i}", Sube = "A", Kontenjan = 60, SinifSeviyesi = 1 + i % 4
        }).ToList();
        d.SablonSonuclari = d.Dersler.Select(x => new SinavAtama
        {
            DersAdi = x.DersAdi, BolumAdi = x.BolumAdi, SinifSeviyesi = x.SinifSeviyesi, Sube = "A", Kontenjan = 60, Gun = gun, Saat = "09:00"
        }).ToList();
        d.SonSonuclar = d.SablonSonuclari.Select(a => new SinavAtama
        {
            DersAdi = a.DersAdi, BolumAdi = "BİL", SinifSeviyesi = a.SinifSeviyesi, Sube = "A", Kontenjan = 60, Gun = gun, Saat = "09:00",
            AtananSalonlar = Enumerable.Range(0, 3).Select(k => new SalonAtama
            {
                SalonKodu = $"T4-20{k}", SalonKapasitesi = 60, AtananOgrenciSayisi = 20, Sube = "A", GozetmenAdSoyad = "ALİ VELİ"
            }).ToList()
        }).ToList();
        return d;
    }
}
