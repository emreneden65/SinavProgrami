using System.ComponentModel;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 2 — Gözetmen listesi uygulama içinden düzenlenir: Ekle, Seçili Sil (Delete tuşu),
/// tabloda Ad Soyad / Kod / Bölüm / Ağırlık düzeltme.
/// </summary>
static class GozetmenDuzenlemeTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 2 — Gözetmen ekle / sil / düzelt");

        t.Test("Ekle ve Seçili Sil butonları gözetmen bölümünde", () => FormIle(form =>
        {
            var yukle = Alan<Button>(form, "btnGozetmenYukle");
            var ekle = Alan<Button>(form, "btnGozetmenEkle");
            var sil = Alan<Button>(form, "btnGozetmenSil");
            Dogrula(ekle.Parent == yukle.Parent && sil.Parent == yukle.Parent, "Butonlar gözetmen bölümünde değil.");
            Dogrula(ekle.Top == yukle.Top && sil.Top == yukle.Top, "Butonlar 'Gözetmen Yükle' ile aynı sırada değil.");
            Dogrula(sil.Right <= Alan<DataGridView>(form, "dgvGozetmenler").Right + 1, "Butonlar panelden taşıyor.");
        }));

        t.Test("Bölüm açılır listesinde mevcut yazım + 6 standart bölüm var (tekrarsız)", () => FormIle(form =>
        {
            var kolon = (DataGridViewComboBoxColumn)Alan<DataGridView>(form, "dgvGozetmenler").Columns["Bolum"];
            var secenekler = kolon.Items.Cast<string>().ToList();
            Dogrula(secenekler.Count == 6, $"Seçenek sayısı {secenekler.Count}, beklenen 6: {string.Join(", ", secenekler)}");
            Dogrula(secenekler.Contains("Bilgisayar Mühendisliği") && secenekler.Contains("Mekatronik Mühendisliği"),
                "Beklenen bölümler listede yok.");
        }));

        t.Test("Yeni gözetmen eklenebiliyor", () => FormIle(form =>
        {
            Cagir(form, "GozetmenEkle", new Gozetmen { AdSoyad = "MEHMET YILMAZ", Bolum = "Makine Mühendisliği", Agirlik = 1.0 });
            var liste = Alan<List<Gozetmen>>(form, "_gozetmenListesi");
            var grid = Alan<DataGridView>(form, "dgvGozetmenler");
            Dogrula(liste.Count == 3 && SatirSayisi(grid) == 3, "Eklenen gözetmen listeye/tabloya girmedi.");
            Dogrula(Equals(grid.Rows[2].Cells["Bolum"].Value, "Makine Mühendisliği"), "Eklenen gözetmenin bölümü tabloda yok.");
        }));

        t.Test("Aynı ad ve aynı rakamlı kod ikinci kez eklenemiyor", () => FormIle(form =>
        {
            string Hata(string ad, string kod) => (string)Cagir(form, "GozetmenBilgisiHatasi", ad, kod, null);
            Dogrula(Hata("Ali Veli", "") != null, "Aynı ad (küçük harfle) kabul edildi.");
            Dogrula(Hata("ALI VELI", "") != null, "Aynı ad (Türkçe karaktersiz) kabul edildi.");
            Dogrula(Hata("YENİ KİŞİ", "b1") != null, "Kullanılan rakamlı kod kabul edildi.");
            Dogrula(Hata("", "") != null, "Boş ad kabul edildi.");
            Dogrula(Hata("YENİ KİŞİ", "") == null, "Geçerli yeni gözetmen reddedildi.");
        }));

        t.Test("Ad düzeltilince mevcut salon görevlerinde de düzeliyor", () => FormIle(form =>
        {
            HucreDuzenle(form, 0, "AdSoyad", "ALİ VELİOĞLU");
            Dogrula(Alan<List<Gozetmen>>(form, "_gozetmenListesi")[0].AdSoyad == "ALİ VELİOĞLU", "Ad listede değişmedi.");
            var salonlar = Alan<List<SinavAtama>>(form, "_sonSonuclar")[0].AtananSalonlar;
            Dogrula(salonlar[0].GozetmenAdSoyad == "ALİ VELİOĞLU", "Salon görevindeki ad güncellenmedi.");
            Dogrula(salonlar[1].GozetmenAdSoyad == "AYŞE KAYA", "Başka gözetmenin görevi değişti.");
        }));

        t.Test("Bölüm ve kod tablodan değiştirilebiliyor", () => FormIle(form =>
        {
            HucreDuzenle(form, 0, "Bolum", "Makine Mühendisliği");
            HucreDuzenle(form, 0, "Kod", "MA9");
            var g = Alan<List<Gozetmen>>(form, "_gozetmenListesi")[0];
            Dogrula(g.Bolum == "Makine Mühendisliği" && g.Kod == "MA9", "Bölüm/kod değişikliği modele işlenmedi.");
        }));

        t.Test("Sütuna göre sıralanınca da doğru kişi düzenleniyor", () => FormIle(form =>
        {
            var grid = Alan<DataGridView>(form, "dgvGozetmenler");
            grid.Sort(grid.Columns["AdSoyad"], ListSortDirection.Descending);   // AYŞE KAYA üste çıkar
            HucreDuzenle(form, 0, "Agirlik", "2.5");
            var liste = Alan<List<Gozetmen>>(form, "_gozetmenListesi");
            Dogrula(liste.First(x => x.AdSoyad == "AYŞE KAYA").Agirlik == 2.5, "Sıralı tabloda yanlış kişinin ağırlığı değişti.");
            Dogrula(liste.First(x => x.AdSoyad == "ALİ VELİ").Agirlik == 1.5, "Diğer gözetmenin ağırlığı bozuldu.");
        }));

        t.Test("Görevli gözetmen silinince görevi boşalıyor, diğerleri korunuyor", () => FormIle(form =>
        {
            var liste = Alan<List<Gozetmen>>(form, "_gozetmenListesi");
            Cagir(form, "GozetmenleriSil", new List<Gozetmen> { liste[0] }, false);
            Dogrula(liste.Count == 1 && liste[0].AdSoyad == "AYŞE KAYA", "Yanlış gözetmen silindi.");
            Dogrula(SatirSayisi(Alan<DataGridView>(form, "dgvGozetmenler")) == 1, "Tablo güncellenmedi.");
            var salonlar = Alan<List<SinavAtama>>(form, "_sonSonuclar")[0].AtananSalonlar;
            Dogrula(salonlar[0].GozetmenAdSoyad == "", "Silinen gözetmenin görevi boşaltılmadı.");
            Dogrula(salonlar[1].GozetmenAdSoyad == "AYŞE KAYA", "Diğer gözetmenin görevi silindi.");
        }));

        t.Test("Eklenen / silinen gözetmenler kapatıp açınca da korunuyor (Madde 1 ile)", () =>
        {
            FormIle(form =>
            {
                var liste = Alan<List<Gozetmen>>(form, "_gozetmenListesi");
                Cagir(form, "GozetmenleriSil", new List<Gozetmen> { liste[0] }, false);
                Cagir(form, "GozetmenEkle", new Gozetmen { AdSoyad = "MEHMET YILMAZ", Bolum = "Makine Mühendisliği", Agirlik = 1.0 });
            });
            using var yeniden = FormuAc();
            var adlar = Alan<List<Gozetmen>>(yeniden, "_gozetmenListesi").Select(g => g.AdSoyad).ToList();
            Dogrula(adlar.SequenceEqual(new[] { "AYŞE KAYA", "MEHMET YILMAZ" }),
                $"Yeniden açılışta liste farklı: {string.Join(", ", adlar)}");
            yeniden.Close();
        });
    }

    /// <summary>Örnek oturumla formu açar, gövdeyi çalıştırır ve formu kapatır (kapanışta oturum kaydedilir).</summary>
    private static void FormIle(Action<MainForm> govde)
    {
        OturumKaydi.Yaz(OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum()));
        using var form = FormuAc();
        govde(form);
        form.Close();
    }

    /// <summary>Kullanıcının hücreye yazıp çıkmasını taklit eder.</summary>
    private static void HucreDuzenle(MainForm form, int satir, string kolon, object deger)
    {
        var grid = Alan<DataGridView>(form, "dgvGozetmenler");
        int k = grid.Columns[kolon].Index;
        grid.Rows[satir].Cells[k].Value = deger;
        Cagir(form, "DgvGozetmenler_CellEndEdit", grid, new DataGridViewCellEventArgs(k, satir));
    }
}
