using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A1 — çekirdek: çalışma durumu, oturum kaydı, takvim / öğretim dönemi, iş akışı.
/// Yeni çekirdeğin eski arayüzle aynı oturum dosyasını aynı anlamla okuyup yazdığı, eski
/// MainForm ile yan yana çalıştırılarak denetlenir.
/// </summary>
static class YeniArayuzCekirdekTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A1 — çekirdek: oturum, takvim, iş akışı");

        // ── Oturum ──
        t.Test("Oturum gidiş-dönüşü: yüklenip yeniden toplanan oturum birebir aynı yazılıyor", () =>
        {
            string ilk = OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum());
            OturumKaydi.Yaz(ilk);
            var (_, oturum) = Yeni();
            Dogrula(oturum.GeriYukle() && oturum.GeriYuklendi, "Oturum geri yüklenmedi.");
            string ikinci = OturumKaydi.Serilestir(oturum.Topla());
            Dogrula(ikinci == ilk, Fark(ilk, ikinci));
        });

        t.Test("Eski arayüzle eşdeğer: aynı oturumdan aynı kayıt üretiliyor", () =>
        {
            // Örnek durum her çağrıda yeni havuz kimliği üretir — iki tarafa da aynı kayıt verilir
            string kayit = OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum());
            OturumKaydi.Yaz(kayit);
            string eski;
            using (var form = FormuAc())
            {
                eski = OturumKaydi.Serilestir(ArayuzAlanlariniSabitle((OturumDurumu)Cagir(form, "OturumDurumunuTopla")));
                form.Close();
            }

            // Eski pencere kapanırken kendi kaydını yazdı; yeni çekirdek aynı başlangıçtan yüklesin
            OturumKaydi.Yaz(kayit);
            var (_, oturum) = Yeni();
            oturum.GeriYukle();
            string yeni = OturumKaydi.Serilestir(ArayuzAlanlariniSabitle(oturum.Topla()));
            Dogrula(yeni == eski, Fark(eski, yeni));
        });

        t.Test("Eski arayüzle eşdeğer: kayıt yokken açılıştaki varsayılan takvim aynı", () =>
        {
            OturumKaydi.Sil();
            Dictionary<DateTime, List<string>> eski;
            using (var form = FormuAc())
            {
                eski = Alan<Dictionary<DateTime, List<string>>>(form, "_takvimSlotlari")
                    .ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
                form.Close();
            }
            OturumKaydi.Sil();
            var (durum, oturum) = Yeni();
            Dogrula(!oturum.GeriYukle(), "Kayıt yokken geri yükleme true döndü.");
            new TakvimServisi(durum).VarsayilanTakvimiKur();
            Dogrula(TakvimMetni(eski) == TakvimMetni(durum.TakvimSlotlari),
                $"Takvimler farklı.\n      eski: {TakvimMetni(eski)}\n      yeni: {TakvimMetni(durum.TakvimSlotlari)}");
        });

        t.Test("Kayıt yoksa uyarısız açılıyor; varsayılan vize takvimi 7 gün × 5 saat", () =>
        {
            OturumKaydi.Sil();
            var (durum, oturum) = Yeni();
            Dogrula(!oturum.GeriYukle() && oturum.AcilisUyarisi == null && !oturum.GeriYuklendi, "Kayıt yokken uyarı / geri yükleme var.");
            new TakvimServisi(durum).VarsayilanTakvimiKur();
            Dogrula(durum.TakvimSlotlari.Count == SinavTipi.VizeGunSayisi, $"{durum.TakvimSlotlari.Count} gün kuruldu.");
            Dogrula(durum.TakvimSlotlari.Values.All(s => s.SequenceEqual(TakvimServisi.VarsayilanSinavSaatleri)), "Varsayılan saatler yazılmadı.");
            Dogrula(durum.TakvimSlotlari.Keys.Min() == DateTime.Today.AddDays(1), "Takvim yarından başlamıyor.");
            Dogrula(durum.Durum.Metin == "🗓 Vize Programı — takvimde 7 sınav günü seçili.", $"Durum: {durum.Durum.Metin}");
        });

        t.Test("Bozuk kayıt: açılışta uyarı veriliyor, dosya kenara yedekleniyor, uygulama boş açılıyor", () =>
        {
            Directory.CreateDirectory(OturumKaydi.Klasor);
            File.WriteAllText(OturumKaydi.DosyaYolu, "{bozuk");
            var (durum, oturum) = Yeni();
            Dogrula(!oturum.GeriYukle(), "Bozuk kayıtta geri yükleme true döndü.");
            Dogrula(oturum.AcilisUyarisi != null && oturum.AcilisUyarisi.StartsWith("Önceki oturum dosyası okunamadı"),
                $"Açılış uyarısı: {oturum.AcilisUyarisi}");
            Dogrula(File.Exists(OturumKaydi.BozukYedekYolu) && durum.Dersler.Count == 0, "Yedeklenmedi ya da veri yüklendi.");
        });

        t.Test("Geri yüklemede ders listesinde olmayan uygulamalı dersler düşüyor (eski davranış)", () =>
        {
            var d = OturumKaydiTestleri.OrnekDurum();
            d.UygulamaliDersler.Add("LİSTEDE OLMAYAN DERS");
            OturumKaydi.Yaz(OturumKaydi.Serilestir(d));
            var (durum, oturum) = Yeni();
            oturum.GeriYukle();
            Dogrula(durum.UygulamaliDersler.SetEquals(new[] { "VERİ YAPILARI" }),
                $"Uygulamalı dersler: {string.Join(", ", durum.UygulamaliDersler)}");
        });

        t.Test("Değişmeyen oturum yeniden yazılmıyor; değişince yazılıyor", () =>
        {
            OturumKaydi.Yaz(OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum()));
            var (durum, oturum) = Yeni();
            oturum.GeriYukle();
            int yazma = 0;
            oturum.KayitYapildi += () => yazma++;
            Dogrula(oturum.Kaydet(out _) && yazma == 0, "Değişiklik yokken diske yazıldı.");
            durum.GozetmenListesi[0].Agirlik = 2.0;
            Dogrula(oturum.Kaydet(out _) && yazma == 1, "Değişiklik diske yazılmadı.");
            Dogrula(OturumKaydi.Oku(out _).Gozetmenler[0].Agirlik == 2.0, "Kayıttaki ağırlık değişmedi.");
        });

        t.Test("Adım kilitleri eski buton adlarıyla eski sırada yazılıyor; kayıtta olmayan anahtar değerini koruyor", () =>
        {
            var k = new AdimKilitleri { SalonAta = true, FinalExport = true };
            Dogrula(k.SozlugeCevir().Keys.SequenceEqual(new[]
                { "btnOtomatikDagit", "btnSalonAta", "btnSalonExport", "btnGozetmenAta", "btnMatrisExport", "btnGozetmenliYukle", "btnFinalExport" }),
                "Anahtar adları / sırası eski arayüzle aynı değil.");
            k.SozluktenOku(new Dictionary<string, bool> { ["btnSalonAta"] = false, ["btnGozetmenAta"] = true });
            Dogrula(!k.SalonAta && k.GozetmenAta && k.FinalExport, "Kısmi kayıt okunurken değerler bozuldu.");
        });

        // ── Takvim ve öğretim dönemi ──
        t.Test("Sınav türü seçilince gün kutusuna türün varsayılanı yazılıyor (7 / 10 / 5 / 1)", () =>
        {
            var (durum, _) = Yeni();
            var takvim = new TakvimServisi(durum);
            foreach (var (tip, gun) in new[] { (SinavDonemTipi.Final, 10), (SinavDonemTipi.Butunleme, 5), (SinavDonemTipi.TekDers, 1), (SinavDonemTipi.Vize, 7) })
            {
                takvim.SinavTipiSec(tip);
                Dogrula(durum.SinavTipi == tip && durum.GunSayisi == gun, $"{tip}: gün kutusu {durum.GunSayisi}.");
            }
        });

        t.Test("Takvimi Doldur: 10 günlük final iki hafta, hafta sonları boş; özet ve silinecek gün sayısı doğru", () =>
        {
            var (durum, _) = Yeni();
            var takvim = new TakvimServisi(durum);
            takvim.VarsayilanTakvimiKur();
            int onceki = durum.TakvimSlotlari.Count;
            takvim.SinavTipiSec(SinavDonemTipi.Final);
            takvim.BaslangicTarihiAyarla(new DateTime(2026, 10, 26));   // Pazartesi
            var o = takvim.DoldurmaOnizlemesi();
            Dogrula(o.Gunler.Count == 10 && o.Haftalar.Count == 2 && o.Haftalar.All(h => h.GunSayisi == 5),
                $"{o.Gunler.Count} gün, {o.Haftalar.Count} hafta.");
            Dogrula(o.Haftalar[0].ToString() == "1. Hafta: 26.10 – 30.10  (5 gün)", $"Hafta özeti: {o.Haftalar[0]}");
            Dogrula(o.Kural == "Pazartesi–Cuma — HAFTA SONLARI BOŞ" && o.SilinecekGunSayisi == onceki, "Kural / silinecek gün yanlış.");
            Dogrula(o.Gunler.All(g => g.DayOfWeek != DayOfWeek.Saturday && g.DayOfWeek != DayOfWeek.Sunday), "Final'de hafta sonu var.");

            takvim.TakvimiDoldur(o.Gunler);
            Dogrula(durum.TakvimSlotlari.Count == 10 &&
                    durum.TakvimSlotlari.Values.All(s => s.SequenceEqual(TakvimServisi.VarsayilanSinavSaatleri)), "Takvim kurulmadı.");
            Dogrula(durum.Durum.Metin == "🗓 Final Programı — takvimde 10 sınav günü seçili.", $"Durum: {durum.Durum.Metin}");
        });

        t.Test("Hafta sonu uyarısı yalnızca Final'de; elle eklenen güne varsayılan saatler yazılıyor", () =>
        {
            var (durum, _) = Yeni();
            var takvim = new TakvimServisi(durum);
            var cumartesi = new DateTime(2026, 10, 31);
            Dogrula(!takvim.HaftaSonuUyarisiGerekir(cumartesi), "Vize'de hafta sonu uyarısı çıktı.");
            takvim.SinavTipiSec(SinavDonemTipi.Final);
            Dogrula(takvim.HaftaSonuUyarisiGerekir(cumartesi) && !takvim.HaftaSonuUyarisiGerekir(cumartesi.AddDays(2)),
                "Final'de hafta sonu uyarısı doğru değil.");
            Dogrula(takvim.HaftaSonuUyariMetni(cumartesi).Contains("Final Programında hafta sonları normalde boş bırakılır"),
                "Uyarı metni eski arayüzdeki gibi değil.");
            takvim.SinavGunuYap(cumartesi);
            Dogrula(durum.TakvimSlotlari[cumartesi].SequenceEqual(TakvimServisi.VarsayilanSinavSaatleri), "Varsayılan saatler yazılmadı.");
        });

        t.Test("Saat aç / kapa sıralı; son saat kapanınca gün çıkıyor; geri koyma saatleri aynen getiriyor", () =>
        {
            var (durum, _) = Yeni();
            var takvim = new TakvimServisi(durum);
            var gun = new DateTime(2026, 10, 26);
            durum.TakvimSlotlari[gun] = new List<string> { "11:00" };
            takvim.SaatAyarla(gun, "09:00", true);
            Dogrula(durum.TakvimSlotlari[gun].SequenceEqual(new[] { "09:00", "11:00" }), "Saat sıralı eklenmedi.");
            Dogrula(!takvim.SaatAyarla(gun, "09:00", false), "Saat kalmışken gün çıktı.");
            Dogrula(takvim.SaatAyarla(gun, "11:00", false) && !durum.TakvimSlotlari.ContainsKey(gun), "Son saat kapanınca gün çıkmadı.");

            durum.TakvimSlotlari[gun] = new List<string> { "09:00", "15:00" };
            var saatler = takvim.SinavGununuKaldir(gun);
            Dogrula(!durum.TakvimSlotlari.ContainsKey(gun), "Gün çıkarılmadı.");
            takvim.GunuGeriKoy(gun, saatler);
            Dogrula(durum.TakvimSlotlari[gun].SequenceEqual(new[] { "09:00", "15:00" }), "Geri koyma saatleri değiştirdi.");
        });

        t.Test("Günün saat seçenekleri 09–17 + o güne özel saat (ör. 08:30), kronolojik", () =>
        {
            var (durum, _) = Yeni();
            var gun = new DateTime(2026, 10, 26);
            durum.TakvimSlotlari[gun] = new List<string> { "08:30", "13:00" };
            var secenekler = new TakvimServisi(durum).GununSaatSecenekleri(gun);
            Dogrula(secenekler.First() == "08:30" && secenekler.Count == 10 && secenekler.Last() == "17:00",
                $"Seçenekler: {string.Join(", ", secenekler)}");
        });

        t.Test("Öğretim yılı: yazım çözülüyor, kutudan çıkınca 2031/2032'ye düzeliyor, geçersiz yazım işaretleniyor", () =>
        {
            var (durum, _) = Yeni();
            var takvim = new TakvimServisi(durum);
            takvim.OgretimYiliMetniniAyarla("2031-2032");
            Dogrula(durum.OgretimYili == 2031 && !takvim.OgretimYiliGecersiz, "Geçerli yıl çözülmedi.");
            takvim.OgretimYiliMetniniDuzelt();
            Dogrula(durum.OgretimYiliMetni == "2031/2032", $"Düzeltilen metin: {durum.OgretimYiliMetni}");
            takvim.OgretimYiliMetniniAyarla("2031/2035");
            Dogrula(durum.OgretimYili == null && takvim.OgretimYiliGecersiz, "Geçersiz yıl kabul edildi.");
            takvim.OgretimYiliMetniniDuzelt();
            Dogrula(durum.OgretimYiliMetni == "2031/2035", "Geçersiz yazım kendiliğinden değiştirildi.");
            takvim.OgretimYiliMetniniAyarla("");
            Dogrula(!takvim.OgretimYiliGecersiz, "Boş kutu geçersiz sayıldı.");
        });

        t.Test("Öğretim dönemi eksik mesajı eski arayüzdeki gibi (boş / geçersiz yıl / dönem yok)", () =>
        {
            var (durum, _) = Yeni();
            var takvim = new TakvimServisi(durum);
            Dogrula(takvim.OgretimDonemiEksikMesaji().StartsWith("Önce öğretim yılı ve dönem (Güz / Bahar) seçilmeli."),
                takvim.OgretimDonemiEksikMesaji());
            takvim.OgretimYiliMetniniAyarla("31/32");
            Dogrula(takvim.OgretimDonemiEksikMesaji().StartsWith(
                "Önce geçerli bir öğretim yılı ('31/32' geçersiz — 2026/2027 biçiminde yazın) ve dönem (Güz / Bahar) seçilmeli."),
                takvim.OgretimDonemiEksikMesaji());
            takvim.OgretimYiliMetniniAyarla("2026/2027");
            takvim.YariyilSec(Yariyil.Bahar);
            Dogrula(takvim.OgretimDonemiEksikMesaji() == null, "Her şey seçiliyken mesaj var.");
        });

        t.Test("Excel başlığı seçimden oluşuyor; seçim eksikse önizleme yok", () =>
        {
            var (durum, _) = Yeni();
            var takvim = new TakvimServisi(durum);
            takvim.OgretimYiliMetniniAyarla("2026/2027");
            Dogrula(takvim.ProgramBasligiOnizlemesi == null, "Dönem seçilmeden önizleme var.");
            takvim.YariyilSec(Yariyil.Bahar);
            takvim.SinavTipiSec(SinavDonemTipi.Butunleme);
            Dogrula(takvim.ProgramBasligiOnizlemesi == "2026-2027 ÖĞRETİM YILI BAHAR DÖNEMİ BÜTÜNLEME SINAV PROGRAMI",
                takvim.ProgramBasligiOnizlemesi);
        });

        // ── İş akışı ──
        t.Test("İş akışı: boş durumda sıradaki adım 1; eksikler yıl, dönem, takvim, derslik, sınav listesi", () =>
        {
            var (durum, _) = Yeni();
            Dogrula(IsAkisi.SiradakiAdim(durum) == 1, $"Sıradaki adım {IsAkisi.SiradakiAdim(durum)}.");
            var eksik = IsAkisi.Eksikler(durum).Select(e => e.AdimNo).ToList();
            Dogrula(eksik.SequenceEqual(new[] { 1, 1, 1, 2, 3 }), $"Eksikler: {string.Join(", ", eksik)}");
            Dogrula(IsAkisi.Durum(durum, 7).Tur == AdimDurumTuru.Kilitli, "Final kilitli değil.");
        });

        t.Test("İş akışı: örnek oturumda adım durumları; gözetmen ataması yoksa Final kilitli", () =>
        {
            OturumKaydi.Yaz(OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum()));
            var (durum, oturum) = Yeni();
            oturum.GeriYukle();
            Dogrula(IsAkisi.SiradakiAdim(durum) == 1, "Öğretim dönemi seçilmemişken 1. adım sırada değil.");

            durum.OgretimYili = 2026;
            durum.Yariyil = Yariyil.Guz;
            Dogrula(Enumerable.Range(1, 6).All(no => IsAkisi.Durum(durum, no).Tur == AdimDurumTuru.Tamam),
                string.Join(", ", Enumerable.Range(1, 7).Select(no => $"{no}:{IsAkisi.Durum(durum, no).Tur}")));
            Dogrula(IsAkisi.Durum(durum, 7).Tur == AdimDurumTuru.Siradaki, "Final sıradaki değil.");

            durum.Kilitler.MatrisExport = false;
            var final = IsAkisi.Durum(durum, 7);
            Dogrula(final.Tur == AdimDurumTuru.Kilitli && final.Neden == "Önce 6. adımda gözetmenleri atayın.",
                $"Final: {final.Tur} / {final.Neden}");
        });

        t.Test("Ders özeti dersleri adıyla sayıyor (kodu boş şablon dersleri tek kod sayılmıyor)", () =>
        {
            var (durum, _) = Yeni();
            durum.Dersler.AddRange(new[]
            {
                new Ders { DersAdi = "FİZİK I", BolumAdi = "BİL", Sube = "A" },
                new Ders { DersAdi = "FİZİK I", BolumAdi = "BİL", Sube = "B" },
                new Ders { DersAdi = "MATEMATİK I", BolumAdi = "MAK", Sube = "A" },
            });
            Dogrula(IsAkisi.Ozet(durum, 3) == "3 şube · 2 ders · 2 bölüm", IsAkisi.Ozet(durum, 3));
        });
    }

    /// <summary>Boş bir yeni çekirdek: çalışma durumu + oturum yöneticisi.</summary>
    private static (CalismaDurumu Durum, OturumYoneticisi Oturum) Yeni()
    {
        var durum = new CalismaDurumu();
        return (durum, new OturumYoneticisi(durum, new IsYurutucu()));
    }

    /// <summary>Arayüze bağlı alanlar (pencere boyutu, sonuç sekmesi) karşılaştırma dışında tutulur.</summary>
    private static OturumDurumu ArayuzAlanlariniSabitle(OturumDurumu d)
    {
        d.PencereTamEkran = false;
        d.PencereGenislik = d.PencereYukseklik = 0;
        d.SeciliSekme = 0;
        d.SonucPaneliAcik = false;
        return d;
    }

    private static string TakvimMetni(Dictionary<DateTime, List<string>> takvim)
        => string.Join(" ", takvim.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key:dd.MM}[{string.Join(",", kv.Value)}]"));

    /// <summary>İki JSON'un ilk farklı olduğu yer (hata mesajı için).</summary>
    private static string Fark(string a, string b)
    {
        int i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
        string Parca(string s) => s.Substring(Math.Max(0, i - 60), Math.Min(140, s.Length - Math.Max(0, i - 60)));
        return $"Kayıtlar {i}. karakterde ayrılıyor.\n      beklenen: …{Parca(a)}…\n      oluşan:   …{Parca(b)}…";
    }
}
