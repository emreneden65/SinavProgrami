using OfficeOpenXml;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A2 — derslikler, sınav listesi, ders düzenleme, uygulamalı, hata oranı, ortak ders, havuz.
/// Kurallar eski MainForm ile yan yana çalıştırılarak karşılaştırılır (diyalog açmayan iç metotlarla).
/// Not: eski formun hoca eşlemesi gerçek %AppData% dosyasına yazdığı için o yol burada çağrılmaz.
/// </summary>
static class YeniArayuzDersTestleri
{
    private const string Bil = "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ";

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A2 — derslikler ve dersler");

        // ── Derslikler ──
        t.Test("Derslik önizlemesi: eklenen / silinen / değişen derslikler eski hesapla aynı", () =>
        {
            string yol = DerslikExceliYaz();
            try
            {
                var mevcut = new List<Salon>
                {
                    new() { SalonKodu = "A-101", Kapasite = 50 },
                    new() { SalonKodu = "Z-999", Kapasite = 40 },
                };
                var o = DerslikServisi.Onizle(yol, mevcut);
                Dogrula(o.Salonlar.Count == 3 && o.UygulamaliSayisi == 1 && o.ToplamKapasite == 140, $"{o.Salonlar.Count} derslik, kapasite {o.ToplamKapasite}.");
                Dogrula(o.Eklenen.SequenceEqual(new[] { "A-102", "LAB-1" }), $"Eklenen: {string.Join(", ", o.Eklenen)}");
                Dogrula(o.Silinen.SequenceEqual(new[] { "Z-999" }), $"Silinen: {string.Join(", ", o.Silinen)}");
                Dogrula(o.Degisen.SequenceEqual(new[] { "A-101: 50 → 62" }), $"Değişen: {string.Join(", ", o.Degisen)}");
            }
            finally { File.Delete(yol); }
        });

        t.Test("Eski arayüzle eşdeğer: derslik listesini uygulama (sıra, kilitler, durum mesajı)", () =>
        {
            var liste = new List<Salon>
            {
                new() { SalonKodu = "T4-204", Kapasite = 60 }, new() { SalonKodu = "T1-101", Kapasite = 30 },
                new() { SalonKodu = "T4-LAB", Kapasite = 45, SalonTipi = "UYGULAMALI" },
            };
            string kayit = OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum());

            OturumKaydi.Yaz(kayit);
            List<string> eskiSira; Dictionary<string, bool> eskiKilit; string eskiMesaj;
            using (var form = FormuAc())
            {
                Cagir(form, "DerslikListesiniUygula", liste.ToList());
                eskiSira = Alan<List<Salon>>(form, "_derslikler").Select(s => s.SalonKodu).ToList();
                eskiKilit = new AdimKilitleri().SozlugeCevir().Keys.ToDictionary(a => a, a => Alan<Button>(form, a).Enabled);
                eskiMesaj = Alan<Label>(form, "lblDurum").Text;
                form.Close();
            }

            var durum = Yuklu(kayit);
            new DerslikServisi(durum).Uygula(liste.ToList());
            Dogrula(durum.Derslikler.Select(s => s.SalonKodu).SequenceEqual(eskiSira), $"Sıra: {string.Join(", ", durum.Derslikler.Select(s => s.SalonKodu))}");
            Dogrula(durum.Kilitler.SozlugeCevir().SequenceEqual(eskiKilit), "Kilitler eski arayüzden farklı.");
            Dogrula(durum.Durum.Metin == eskiMesaj, $"Mesaj: {durum.Durum.Metin}\n      eski: {eskiMesaj}");
        });

        // ── Sınav listesi ──
        t.Test("Sınav listesi önizlemesi: şube / kod sayısı, bölüm özeti ve atlanan satırlar", () =>
        {
            string yol = SinavListesiExceliYaz();
            try
            {
                var o = DersServisi.Onizle(yol);
                Dogrula(o.SubeSayisi == 5 && o.KodSayisi == 3, $"{o.SubeSayisi} şube, {o.KodSayisi} kod.");
                Dogrula(o.Bolumler.Count == 2 && o.Bolumler[0].Bolum == "BİLGİSAYAR MÜHENDİSLİĞİ" && o.Bolumler[0].SubeSayisi == 4,
                    string.Join(" | ", o.Bolumler.Select(b => $"{b.Bolum}:{b.KodSayisi}/{b.SubeSayisi}")));
                Dogrula(o.Uyarilar.Count == 1 && o.Uyarilar[0].Contains("Ders kodu boş"), string.Join(" | ", o.Uyarilar));
            }
            finally { File.Delete(yol); }
        });

        t.Test("Sınav listesi uygulanınca: takvim KORUNUYOR, şablon / salon sonuçları ve kilitler sıfırlanıyor (onaylı düzeltmeler)", () =>
        {
            var durum = Yuklu(OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum()));
            int takvim = durum.TakvimSlotlari.Count;
            string yol = SinavListesiExceliYaz();
            try
            {
                new DersServisi(durum, new HocaEslemesi()).SinavListesiniUygula(DersServisi.Onizle(yol));
            }
            finally { File.Delete(yol); }

            Dogrula(durum.Dersler.Count == 5, $"{durum.Dersler.Count} ders yüklendi.");
            Dogrula(durum.TakvimSlotlari.Count == takvim && takvim > 0, "Takvim silindi (düzeltme 1).");
            Dogrula(durum.SablonSonuclari == null && durum.SonSonuclar == null && durum.SablonIntNotlari.Count == 0, "Şablon / sonuçlar sıfırlanmadı.");
            Dogrula(durum.GunSalonSatirlari == null && !durum.Kilitler.SalonExport, "Eski salon ataması / dışa aktarma kilidi kaldı (düzeltme 2).");
            var k = durum.Kilitler;
            Dogrula(k.OtomatikDagit && !k.SalonAta && !k.GozetmenAta && !k.MatrisExport && !k.GozetmenliYukle && !k.FinalExport, "Kilitler yanlış.");
            Dogrula(!durum.UygulamaliDersler.Contains("VERİ YAPILARI"), "Listede olmayan uygulamalı ders kaldı.");
            Dogrula(File.Exists(HocaEslemesi.DosyaYolu) && File.ReadAllText(HocaEslemesi.DosyaYolu).Contains("Dr. Fizik"),
                "Hoca eşlemesi oturum klasörüne kaydedilmedi.");
        });

        // ── Ders tablosu ──
        t.Test("Eski arayüzle eşdeğer: hücre düzenleme düzeltmeleri (büyük harf, kontenjan ≥0, boş şube = A)", () =>
        {
            string kayit = OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum());
            var denemeler = new (string Kolon, DersAlani Alan, string Deger)[]
            {
                ("DersAdi", DersAlani.DersAdi, "  veri yapıları ve algoritmalar "),
                ("Kontenjan", DersAlani.Kontenjan, "-5"),
                ("Sube", DersAlani.Sube, ""),
                ("Sube", DersAlani.Sube, " c "),
                ("BolumAdi", DersAlani.Bolum, " MAKİNE MÜHENDİSLİĞİ "),
            };
            foreach (var (kolon, alan, deger) in denemeler)
            {
                OturumKaydi.Yaz(kayit);
                string eski;
                using (var form = FormuAc())
                {
                    var grid = Alan<DataGridView>(form, "dgvDersler");
                    int k = grid.Columns[kolon].Index;
                    grid.Rows[0].Cells[k].Value = deger;
                    Cagir(form, "DgvDersler_CellEndEdit", grid, new DataGridViewCellEventArgs(k, 0));
                    eski = DersMetni(Alan<List<Ders>>(form, "_dersListesi")[0]);
                    form.Close();
                }
                var durum = Yuklu(kayit);
                string hata = new DersServisi(durum, new HocaEslemesi()).DersAlaniniDegistir(durum.Dersler[0], alan, deger);
                string yeni = DersMetni(durum.Dersler[0]);
                Dogrula(hata == null && yeni == eski, $"{kolon}='{deger}': eski {eski} / yeni {yeni} / hata {hata}");
            }
        });

        t.Test("Ders düzenleme: ad / bölüm boş bırakılamaz; değişmeyen değer hiçbir şeyi değiştirmez", () =>
        {
            var durum = Yuklu(OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum()));
            var servis = new DersServisi(durum, new HocaEslemesi());
            var ders = durum.Dersler[0];
            string once = DersMetni(ders);
            Dogrula(servis.DersAlaniniDegistir(ders, DersAlani.DersAdi, "  ") == "Ders Adı ve Bölüm boş bırakılamaz!", "Boş ad kabul edildi.");
            Dogrula(servis.DersAlaniniDegistir(ders, DersAlani.Bolum, "") != null, "Boş bölüm kabul edildi.");
            Dogrula(DersMetni(ders) == once, "Geçersiz düzenleme dersi değiştirdi.");
            string durumMesaji = durum.Durum.Metin;
            Dogrula(servis.DersAlaniniDegistir(ders, DersAlani.DersAdi, ders.DersAdi.ToLowerInvariant()) == null &&
                    durum.Durum.Metin == durumMesaji, "Aynı değer (küçük harfle) yeniden yazıldı.");
        });

        t.Test("Sınıf düzeltmesi: başka alan düzenlenince sınıf değişmez (0 kalır); sınıf hücresi 0–9 kabul eder", () =>
        {
            var durum = new CalismaDurumu();
            var ders = new Ders { DersAdi = "HAZIRLIK", BolumAdi = Bil, SinifSeviyesi = 0, Sube = "A", Kontenjan = 30, DersKodu = "BIL001" };
            durum.Dersler.Add(ders);
            var servis = new DersServisi(durum, new HocaEslemesi());
            servis.DersAlaniniDegistir(ders, DersAlani.Kontenjan, "35");
            servis.DersAlaniniDegistir(ders, DersAlani.Sube, "b");
            Dogrula(ders.Kontenjan == 35 && ders.Sube == "B" && ders.SinifSeviyesi == 0, $"Ders: {DersMetni(ders)}");

            Dogrula(servis.DersAlaniniDegistir(ders, DersAlani.Sinif, " 7 ") == null && ders.SinifSeviyesi == 7, $"Sınıf 7: {ders.SinifSeviyesi}");
            Dogrula(servis.DersAlaniniDegistir(ders, DersAlani.Sinif, "0") == null && ders.SinifSeviyesi == 0, $"Sınıf 0: {ders.SinifSeviyesi}");
            foreach (var hatali in new[] { "10", "-1", "abc", "", "2.5" })
                Dogrula(servis.DersAlaniniDegistir(ders, DersAlani.Sinif, hatali) == DersServisi.SinifHataMesaji && ders.SinifSeviyesi == 0,
                    $"'{hatali}' kabul edildi ya da sınıfı değiştirdi: {ders.SinifSeviyesi}");
        });

        t.Test("Ders ekleme ve silme: düzeltilmiş değerler, uygulamalı seçimi ve durum mesajı", () =>
        {
            var durum = Yuklu(OturumKaydi.Serilestir(OturumKaydiTestleri.OrnekDurum()));
            var servis = new DersServisi(durum, new HocaEslemesi());
            Dogrula(servis.DersEkle("", Bil, "1", "A", "10", out _) != null, "Adsız ders eklendi.");
            Dogrula(servis.DersEkle("yeni ders", Bil, "12", "A", "10", out _) == DersServisi.SinifHataMesaji, "Sınıf 12 kabul edildi.");
            Dogrula(servis.DersEkle("yeni ders", Bil, "9", "", "-3", out var eklenen) == null, "Ders eklenemedi.");
            Dogrula(eklenen.DersAdi == "YENİ DERS" || eklenen.DersAdi == "YENI DERS", $"Ad: {eklenen.DersAdi}");
            Dogrula(eklenen.SinifSeviyesi == 9 && eklenen.Sube == "A" && eklenen.Kontenjan == 0 && durum.Dersler.Contains(eklenen),
                $"Eklenen: {DersMetni(eklenen)}");

            var veriYapilari = durum.Dersler.Where(d => d.DersAdi == "VERİ YAPILARI").ToList();
            servis.DersleriSil(veriYapilari);
            Dogrula(!durum.Dersler.Any(d => d.DersAdi == "VERİ YAPILARI"), "Dersler silinmedi.");
            Dogrula(!durum.UygulamaliDersler.Contains("VERİ YAPILARI"), "Silinen dersin uygulamalı işareti kaldı.");
            Dogrula(durum.Durum.Metin == "🗑 2 şube listeden silindi.", durum.Durum.Metin);
        });

        t.Test("Arama Türkçe ve büyük/küçük harf duyarsız (ders adında ya da bölümde)", () =>
        {
            var dersler = new List<Ders>
            {
                new() { DersAdi = "FİZİK I", BolumAdi = Bil }, new() { DersAdi = "MATEMATİK I", BolumAdi = "MAKİNE MÜHENDİSLİĞİ" },
            };
            Dogrula(DersServisi.Filtrele(dersler, "fizik").Count == 1, "'fizik' FİZİK I'i bulmadı.");
            Dogrula(DersServisi.Filtrele(dersler, "makine").Single().DersAdi == "MATEMATİK I", "Bölüme göre arama çalışmadı.");
            Dogrula(DersServisi.Filtrele(dersler, "  ").Count == 2, "Boş arama filtreledi.");
        });

        // ── Uygulamalı ve hata oranı ──
        t.Test("Eski arayüzle eşdeğer: uygulamalı ders adayları ve hata oranı tablosu satırları", () =>
        {
            var d = OturumKaydiTestleri.OrnekDurum();
            d.HataOranlari["Makine Mühendisliği"] = 12.55;          // farklı yazım + iki ondalık (tablo 0.# gösterir)
            d.HataOranlari["MEKATRONİK MÜHENDİSLİĞİ BÖLÜMÜ"] = 7;
            string kayit = OturumKaydi.Serilestir(d);
            OturumKaydi.Yaz(kayit);
            List<string> eskiAdaylar, eskiSatirlar; Dictionary<string, double> eskiOranlar;
            using (var form = FormuAc())
            {
                eskiAdaylar = Alan<DataGridView>(form, "dgvOverbooking").Rows.Cast<DataGridViewRow>()
                    .Where(r => !r.IsNewRow).Select(r => r.Cells[0].Value?.ToString()).ToList();
                eskiSatirlar = Alan<DataGridView>(form, "dgvHataOrani").Rows.Cast<DataGridViewRow>()
                    .Where(r => !r.IsNewRow).Select(r => $"{r.Cells[0].Value}={r.Cells[1].Value}").ToList();
                eskiOranlar = (Dictionary<string, double>)Cagir(form, "HataOranlariniOku");
                form.Close();
            }

            var durum = Yuklu(kayit);
            var servis = new DersServisi(durum, new HocaEslemesi());
            Dogrula(servis.UygulamaliAdaylari().SequenceEqual(eskiAdaylar), $"Adaylar: {string.Join(", ", servis.UygulamaliAdaylari())}");
            var yeniSatirlar = servis.HataOraniSatirlari().Select(s => $"{s.Bolum}={s.Metin}").ToList();
            Dogrula(yeniSatirlar.SequenceEqual(eskiSatirlar), $"Satırlar:\n      yeni: {string.Join(" | ", yeniSatirlar)}\n      eski: {string.Join(" | ", eskiSatirlar)}");
            var yeniOranlar = servis.HataOranlariniOku();
            Dogrula(yeniOranlar.Count == eskiOranlar.Count && yeniOranlar.All(kv => eskiOranlar.TryGetValue(kv.Key, out var v) && v == kv.Value),
                $"Algoritmaya giden oranlar farklı: yeni {string.Join(", ", yeniOranlar.Select(kv => $"{kv.Key}={kv.Value}"))} / eski {string.Join(", ", eskiOranlar.Select(kv => $"{kv.Key}={kv.Value}"))}");
            Dogrula(yeniOranlar.Values.Contains(12.6), "12,55 tablodaki gibi 12,6'ya yuvarlanmadı.");
        });

        t.Test("Hata oranı girişi: 0–100 arası kabul ('12,5', '%20'); harf ve 100'ü aşan değer reddediliyor", () =>
        {
            var durum = new CalismaDurumu();
            var servis = new DersServisi(durum, new HocaEslemesi());
            Dogrula(servis.HataOraniAyarla(Bil, "12,5") == null && durum.BolumHataOranlari[Bil] == 12.5, "12,5 kabul edilmedi.");
            Dogrula(servis.HataOraniAyarla(Bil, "%20") == null && durum.BolumHataOranlari[Bil] == 20, "%20 kabul edilmedi.");
            Dogrula(servis.HataOraniAyarla(Bil, "abc") != null && servis.HataOraniAyarla(Bil, "120") != null, "Geçersiz değer kabul edildi.");
            Dogrula(durum.BolumHataOranlari[Bil] == 20, "Geçersiz giriş eski değeri bozdu.");
            Dogrula(servis.HataOraniSatirlari().Count == 6, $"Ders listesi boşken {servis.HataOraniSatirlari().Count} bölüm satırı (beklenen 6 sabit bölüm).");
        });

        // ── Ortak dersler ve havuzlar ──
        t.Test("Ortak dersler: öneri ≥2 bölüm; elle belirleme önerilerle başlıyor; kayıt eski pencerenin biçiminde", () =>
        {
            var durum = OrtakVeriliDurum();
            var ortak = new OrtakDersServisi(durum);
            var adaylar = ortak.Adaylar();
            Dogrula(adaylar[0].DersAdi == "MATEMATİK I" && adaylar[0].Onerilen && adaylar[0].Bolumler.Count == 2 && adaylar[0].ToplamKontenjan == 150,
                $"İlk aday: {adaylar[0].DersAdi} {adaylar[0].Bolumler.Count} bölüm {adaylar[0].ToplamKontenjan}");
            Dogrula(!durum.OrtakSecimYapildi && ortak.SeciliMi(adaylar[0]) && !ortak.SeciliMi(adaylar.Single(a => a.DersKodu == "BIL214")),
                "Otomatik modda öneri işaretleri yanlış.");

            ortak.ElleBelirle(true);
            Dogrula(durum.OrtakSecimYapildi && durum.ManuelOrtakKodlar.SetEquals(new[] { "MAT101" }) &&
                    durum.ManuelOrtakDersAdlari.SetEquals(new[] { "MATEMATİK I" }), "Elle belirleme önerilerle başlamadı.");
            ortak.Ayarla(adaylar.Single(a => a.DersKodu == "BIL214"), true);
            Dogrula(durum.ManuelOrtakKodlar.SetEquals(new[] { "MAT101", "BIL214" }), "İşaretleme kaydedilmedi.");
            ortak.SecimiTemizle();
            Dogrula(durum.OrtakSecimYapildi && durum.ManuelOrtakDersAdlari.Count == 0, "Temizle elle modu kapattı ya da listeyi boşaltmadı.");
            ortak.ElleBelirle(false);
            Dogrula(!durum.OrtakSecimYapildi && durum.ManuelOrtakKodlar.Count == 0, "Otomatik moda dönülemedi.");
        });

        t.Test("Havuz: yalnızca ≥2 bölüm/şubeli dersler; en az 2 şube gerekiyor; üyeler ve bölümler doğru", () =>
        {
            var durum = OrtakVeriliDurum();
            var ortak = new OrtakDersServisi(durum);
            Dogrula(ortak.HavuzDersleri().SequenceEqual(new[] { "MATEMATİK I", "VERİ YAPILARI" }), string.Join(", ", ortak.HavuzDersleri()));
            var subeler = ortak.HavuzSubeleri("MATEMATİK I");
            Dogrula(subeler.Select(s => s.Bolum).SequenceEqual(new[] { Bil, "MAKİNE MÜHENDİSLİĞİ" }), "Şubeler bölüme göre sıralı değil.");
            Dogrula(ortak.HavuzOlustur("MATEMATİK I", subeler.Take(1).ToList()) != null && durum.SablonHavuzlari.Count == 0, "Tek şubeyle havuz kuruldu.");
            Dogrula(ortak.HavuzOlustur("MATEMATİK I", subeler) == null && durum.SablonHavuzlari.Count == 1, "Havuz kurulmadı.");
            var havuz = durum.SablonHavuzlari[0];
            Dogrula(havuz.Uyeler.Count == 2 && havuz.Bolumler.Count == 2 && havuz.Uyeler[0].SinifSeviyesi == 1, "Havuz üyeleri yanlış.");
            Dogrula(OrtakDersServisi.HavuzMetni(havuz) == $"MATEMATİK I → {Bil}(A) + MAKİNE MÜHENDİSLİĞİ(A)", OrtakDersServisi.HavuzMetni(havuz));
            ortak.HavuzSil(havuz);
            Dogrula(durum.SablonHavuzlari.Count == 0, "Havuz silinmedi.");
        });

        t.Test("Ders listesi yokken ortak / havuz seçimleri yüklü şablondan yapılıyor (kodsuz)", () =>
        {
            var durum = new CalismaDurumu
            {
                SablonSonuclari = new List<SinavAtama>
                {
                    new() { DersAdi = "FİZİK I", BolumAdi = Bil, Sube = "A", SinifSeviyesi = 1, Kontenjan = 40 },
                    new() { DersAdi = "FİZİK I", BolumAdi = "MAKİNE MÜHENDİSLİĞİ", Sube = "A", SinifSeviyesi = 1, Kontenjan = 30 },
                }
            };
            var ortak = new OrtakDersServisi(durum);
            Dogrula(ortak.Kaynak().Count == 2 && ortak.Kaynak().All(k => k.DersKodu == ""), "Şablon kaynak alınmadı.");
            Dogrula(ortak.Adaylar().Single().Onerilen && ortak.HavuzDersleri().Single() == "FİZİK I", "Şablondan aday / havuz dersi çıkmadı.");
        });

        // ── Hoca eşlemesi ──
        t.Test("Hoca eşlemesi: oturum klasörüne kaydediliyor; şube → bölüm → ders sırasıyla dolduruluyor", () =>
        {
            File.Delete(HocaEslemesi.DosyaYolu);
            var hoca = new HocaEslemesi();
            hoca.GuncelleVeKaydet(new[]
            {
                new Ders { DersAdi = "VERİ YAPILARI", BolumAdi = Bil, Sube = "A", OgretimElemani = "Dr. A" },
                new Ders { DersAdi = "VERİ YAPILARI", BolumAdi = Bil, Sube = "B", OgretimElemani = "Dr. B" },
            });
            var yeniden = new HocaEslemesi();
            yeniden.Yukle();
            Dogrula(yeniden.KayitSayisi == hoca.KayitSayisi && hoca.KayitSayisi == 4, $"{yeniden.KayitSayisi} kayıt okundu.");

            var atamalar = new List<SinavAtama>
            {
                new() { DersAdi = "VERİ YAPILARI", BolumAdi = "BİL", Sube = "B" },          // şube eşleşmesi
                new() { DersAdi = "VERİ YAPILARI", BolumAdi = "BİL", Sube = "C" },          // bölüm eşleşmesi (ilk hoca)
                new() { DersAdi = "VERİ YAPILARI", BolumAdi = "MAKİNE", Sube = "A" },       // yalnız ders (bölüm tutmuyor)
                new() { DersAdi = "VERİ YAPILARI", BolumAdi = "BİL", Sube = "A", OgretimElemani = "Elle Yazılan" },
            };
            yeniden.Doldur(atamalar, Array.Empty<Ders>());
            Dogrula(atamalar.Select(a => a.OgretimElemani).SequenceEqual(new[] { "Dr. B", "Dr. A", "Dr. A", "Elle Yazılan" }),
                string.Join(", ", atamalar.Select(a => a.OgretimElemani)));
        });
    }

    /// <summary>Kaydı yükleyip yeni çekirdeğin çalışma durumunu döner.</summary>
    private static CalismaDurumu Yuklu(string kayit)
    {
        OturumKaydi.Yaz(kayit);
        var durum = new CalismaDurumu();
        new OturumYoneticisi(durum, new IsYurutucu()).GeriYukle();
        return durum;
    }

    private static string DersMetni(Ders d) => $"[{d.DersAdi}|{d.BolumAdi}|{d.SinifSeviyesi}|{d.Sube}|{d.Kontenjan}]";

    /// <summary>İki bölümde okutulan MAT101 + tek bölümlü BIL214 (iki şube).</summary>
    private static CalismaDurumu OrtakVeriliDurum()
    {
        var durum = new CalismaDurumu();
        durum.Dersler.AddRange(new[]
        {
            new Ders { DersKodu = "MAT101", DersAdi = "MATEMATİK I", BolumAdi = Bil, Sube = "A", SinifSeviyesi = 1, Kontenjan = 90 },
            new Ders { DersKodu = "MAT101", DersAdi = "MATEMATİK I", BolumAdi = "MAKİNE MÜHENDİSLİĞİ", Sube = "A", SinifSeviyesi = 1, Kontenjan = 60 },
            new Ders { DersKodu = "BIL214", DersAdi = "VERİ YAPILARI", BolumAdi = Bil, Sube = "A", SinifSeviyesi = 2, Kontenjan = 80 },
            new Ders { DersKodu = "BIL214", DersAdi = "VERİ YAPILARI", BolumAdi = Bil, Sube = "B", SinifSeviyesi = 2, Kontenjan = 70 },
        });
        return durum;
    }

    /// <summary>Okuyucunun beklediği düzende küçük bir derslik Excel'i (2 normal + 1 laboratuvar).</summary>
    private static string DerslikExceliYaz()
    {
        string yol = Path.Combine(Path.GetTempPath(), $"derslik_a2_{Guid.NewGuid():N}.xlsx");
        using var p = new ExcelPackage();
        var ws = p.Workbook.Worksheets.Add("Derslikler");
        ws.Cells[1, 1].Value = "DERSLİK";
        ws.Cells[1, 2].Value = "SINAV DÜZEYİNDEKİ ÖĞR. SAYISI";
        ws.Cells[2, 1].Value = "A-101"; ws.Cells[2, 2].Value = "62(31)";
        ws.Cells[3, 1].Value = "A-102"; ws.Cells[3, 2].Value = 48;
        ws.Cells[4, 1].Value = "BİLGİSAYAR LABARATUVARLARI";
        ws.Cells[4, 1, 4, 3].Merge = true;
        ws.Cells[5, 1].Value = "LAB-1"; ws.Cells[5, 2].Value = 30;
        p.SaveAs(new FileInfo(yol));
        return yol;
    }

    /// <summary>İki bölüm, üç ders kodu, beş şube + bir kodsuz (atlanan) satır.</summary>
    private static string SinavListesiExceliYaz()
    {
        string yol = Path.Combine(Path.GetTempPath(), $"sinav_a2_{Guid.NewGuid():N}.xlsx");
        using var p = new ExcelPackage();
        var ws = p.Workbook.Worksheets.Add("Liste");
        string[] basliklar = { "Bölüm", "Ünvanı Adı Soyadı", "Öğretim Türü", "DERSİN KODU", "VERİLEN DERSİN ADI", "Dersin Şubesi", "Öğrenci Sayısı" };
        for (int c = 0; c < basliklar.Length; c++) ws.Cells[1, c + 1].Value = basliklar[c];
        object[][] satirlar =
        {
            new object[] { "Bilgisayar Mühendisliği", "Dr. Fizik", "Normal", "FIZ101", "Fizik I", "A", 80 },
            new object[] { "Bilgisayar Mühendisliği", "Dr. Fizik", "İkinci", "FIZ101", "Fizik I", "B", 60 },
            new object[] { "Bilgisayar Mühendisliği", "Dr. Veri", "Normal", "BIL214", "Veri Yapıları ve Algoritmalar", "A", 75 },
            new object[] { "Bilgisayar Mühendisliği", "Dr. Veri", "Normal", "BIL214", "Veri Yapıları ve Algoritmalar", "B", 70 },
            new object[] { "Makine Mühendisliği", "Dr. Mukavemet", "Normal", "MAK201", "Mukavemet", "A", 90 },
            new object[] { "Makine Mühendisliği", "Dr. Kodsuz", "Normal", "", "Kodsuz Ders", "A", 20 },
        };
        for (int r = 0; r < satirlar.Length; r++)
            for (int c = 0; c < satirlar[r].Length; c++)
                ws.Cells[r + 2, c + 1].Value = satirlar[r][c];
        p.SaveAs(new FileInfo(yol));
        return yol;
    }
}
