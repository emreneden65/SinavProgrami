using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A4 — salonlar (atama, salonlu Excel, matris), gözetmenler, final, temizle / sıfırla.
/// Matris ve gözetmen düzenlemeleri eski MainForm ile yan yana çalıştırılıp karşılaştırılır. Eski formun
/// salon atama / gözetmen atama / dışa aktarma işleyicileri diyalog açtığı ya da hoca eşlemesini gerçek
/// %AppData% dosyasına yazdığı için çağrılmaz; onların yerine aynı algoritma çağrıları sınanır.
/// </summary>
static class YeniArayuzSalonTestleri
{
    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A4 — salonlar, gözetmenler, final, temizleme");

        // ── Salonları ata ──
        t.Test("Salonları ata: algoritmayla aynı sonuç; kilitler eski arayüzdeki gibi (salonlu Excel açık, taslak geri yükleme kapalı)", () =>
        {
            var durum = SablonluYuklu();
            durum.Kilitler.GozetmenAta = true;
            durum.Kilitler.GozetmenliYukle = true;
            durum.Kilitler.FinalExport = true;
            var (salon, _, _) = Servisler(durum);
            Dogrula(salon.SalonAtamaEksigi() == null, salon.SalonAtamaEksigi());

            salon.OgretimElemanlariniDoldur();
            var dagitim = salon.SalonlariHesapla();
            var beklenen = new Algorithm.GunlukSalonDagitici().Dagit(durum.SablonSonuclari, durum.Derslikler, durum.UygulamaliDersler,
                durum.SablonHavuzlari, null);
            Dogrula(!dagitim.Bos && dagitim.GunSatirlari.Count == beklenen.Count, $"{dagitim.GunSatirlari.Count} / {beklenen.Count} satır.");
            Dogrula(durum.SonSonuclar == null, "Hesaplama çalışma durumunu değiştirdi.");

            salon.SalonDagitiminiUygula(dagitim);
            var k = durum.Kilitler;
            Dogrula(k.SalonExport && k.GozetmenAta && !k.GozetmenliYukle && !k.FinalExport, "Kilitler eski arayüzden farklı.");
            Dogrula(durum.SonSonuclar.Count > 0 && durum.GunSalonSatirlari == dagitim.GunSatirlari, "Sonuçlar uygulanmadı.");
            Dogrula(durum.Durum.Metin.Contains($"{dagitim.GunSayisi} gün") && durum.Durum.Metin.Contains("'Salonlu Excel çıkar'"), durum.Durum.Metin);
        });

        t.Test("Salon ataması ön koşulları: şablon yok / boş şablon / derslik yok", () =>
        {
            var durum = new CalismaDurumu();
            var (salon, _, _) = Servisler(durum);
            Dogrula(salon.SalonAtamaEksigi()?.Contains("şablonunu geri yükleyin") == true, salon.SalonAtamaEksigi());
            durum.SablonSonuclari = new List<SinavAtama>();
            Dogrula(salon.SalonAtamaEksigi()?.StartsWith("Şablon boş!") == true, salon.SalonAtamaEksigi());
            durum.SablonSonuclari.Add(new SinavAtama { DersAdi = "X", BolumAdi = "BİL", Gun = DateTime.Today, Saat = "09:00" });
            Dogrula(salon.SalonAtamaEksigi()?.StartsWith("Derslik listesi yüklenmemiş") == true, salon.SalonAtamaEksigi());
        });

        // ── Matris ──
        t.Test("Eski arayüzle eşdeğer: salon matrisi satır satır aynı (sıra, boş slot satırları, T4 önce sütunlar, hücre içerikleri)", () =>
        {
            string kayit = OturumKaydi.Serilestir(new OturumYoneticisi(SalonluYuklu(), new IsYurutucu()).Topla());
            var eski = EskiMatris(kayit, out var eskiSutunlar);
            var durum = YeniArayuzSablonTestleri.Yuklu(kayit);
            var (salon, _, _) = Servisler(durum);
            var matris = salon.Matris();
            var yeniSutunlar = matris.Salonlar.Select(s => s.SalonKodu).ToList();
            var yeni = matris.Satirlar.Select(s => SatirMetni(s, matris.Salonlar)).ToList();

            Dogrula(eskiSutunlar.SequenceEqual(yeniSutunlar), $"Sütunlar:\n      eski {string.Join(",", eskiSutunlar)}\n      yeni {string.Join(",", yeniSutunlar)}");
            Dogrula(eski.Count == yeni.Count && eski.Count > 10 && matris.Satirlar.Any(s => s.Bos), $"Satır sayısı: eski {eski.Count} / yeni {yeni.Count}");
            for (int i = 0; i < eski.Count; i++)
                Dogrula(eski[i] == yeni[i], $"Satır {i}:\n      eski {eski[i]}\n      yeni {yeni[i]}");
            Dogrula(SalonServisi.Suz(matris.Satirlar, null, 2).All(s => s.Sinif == 2 && !s.Bos), "Sınıf filtresi boş satırları gizlemedi.");
        });

        t.Test("Eski arayüzle eşdeğer: matriste salon hücresi düzenleme (sayı, '25 (A)', 0 ile silme, boş bırakma, kapasite aşımı)", () =>
        {
            string kayit = OturumKaydi.Serilestir(new OturumYoneticisi(SalonluYuklu(), new IsYurutucu()).Topla());
            var durum0 = YeniArayuzSablonTestleri.Yuklu(kayit);
            var ilk = durum0.SonSonuclar.OrderBy(a => a.BolumAdi).ThenBy(a => a.DersAdi).First(a => a.AtananSalonlar.Count > 0);
            string dolu = ilk.AtananSalonlar[0].SalonKodu;
            string bos = durum0.Derslikler.Select(s => s.SalonKodu).First(k => ilk.AtananSalonlar.All(x => x.SalonKodu != k));
            var islemler = new (string Salon, string Deger)[] { (dolu, "17"), (bos, "25 (A)"), (bos, "999"), (dolu, "0"), (bos, "") };

            OturumKaydi.Yaz(kayit);
            var eskiler = new List<(string Sonuc, string Mesaj)>();
            using (var form = FormuAc())
            {
                var grid = Alan<DataGridView>(form, "dgvMatris");
                Cagir(form, "MatrisGorunumuOlustur", Alan<List<SinavAtama>>(form, "_sonSonuclar"), Alan<List<Salon>>(form, "_derslikler"));
                foreach (var (salonKodu, deger) in islemler)
                {
                    var satir = grid.Rows.Cast<DataGridViewRow>().First(r => Oz<SinavAtama>(r.Tag, "Atama") is { } a && Ayni(a, ilk));
                    int k = grid.Columns.Cast<DataGridViewColumn>().First(c => c.Tag is Salon s && s.SalonKodu == salonKodu).Index;
                    satir.Cells[k].Value = deger;
                    Cagir(form, "DgvMatris_CellEndEdit", grid, new DataGridViewCellEventArgs(k, satir.Index));
                    eskiler.Add((SalonMetni(Alan<List<SinavAtama>>(form, "_sonSonuclar")), Alan<Label>(form, "lblDurum").Text));
                }
                form.Close();
            }

            var durum = YeniArayuzSablonTestleri.Yuklu(kayit);
            var (salon, _, _) = Servisler(durum);
            var atama = durum.SonSonuclar.First(a => Ayni(a, ilk));
            for (int i = 0; i < islemler.Length; i++)
            {
                var (salonKodu, deger) = islemler[i];
                string hata = salon.MatrisHucresiniDegistir(atama, durum.Derslikler.First(s => s.SalonKodu == salonKodu), deger);
                Dogrula(hata == null, hata);
                Dogrula(SalonMetni(durum.SonSonuclar) == eskiler[i].Sonuc, $"İşlem {i + 1} ({salonKodu}='{deger}'): sonuç farklı.");
                Dogrula(durum.Durum.Metin == eskiler[i].Mesaj, $"İşlem {i + 1}: mesaj\n      eski {eskiler[i].Mesaj}\n      yeni {durum.Durum.Metin}");
            }
            Dogrula(eskiler.Select(e => e.Sonuc).Distinct().Count() >= 4 && eskiler.Any(e => e.Mesaj.Contains("kapasitesi")), "Eski arayüzde düzenlemeler etkisiz kaldı.");
            Dogrula(salon.MatrisHucresiniDegistir(atama, durum.Derslikler[0], "abc")?.StartsWith("Lütfen pozitif bir tam sayı") == true, "Metin kabul edildi.");

            var silinen = SalonServisi.SalonAtamasi(atama, atama.AtananSalonlar[0].SalonKodu);
            salon.MatrisHucresiniDegistir(atama, durum.Derslikler.First(s => s.SalonKodu == silinen.SalonKodu), "");
            salon.SalonAtamasiniGeriKoy(atama, silinen);
            Dogrula(atama.AtananSalonlar.Contains(silinen) && durum.Durum.Metin.StartsWith("↩"), "Geri al çalışmadı.");
        });

        // ── Salonlu Excel ──
        t.Test("Salonlu Excel gidiş-dönüş: yazılan dosya okununca aynı sınavlar gelir; uygulanınca gözetmen ataması açılır", () =>
        {
            string yol = GeciciYol("salonlu");
            try
            {
                var durum = SalonluYuklu();
                durum.OgretimYili = 2026;
                durum.Yariyil = Yariyil.Guz;
                var (salon, _, _) = Servisler(durum);
                Dogrula(salon.SalonluExcelEksigi() == null, salon.SalonluExcelEksigi());
                salon.SalonluExcelYaz(yol);
                salon.SalonluExcelYazildi(yol);
                Dogrula(durum.SonDosyalar[SonDosyaTurleri.SalonluExcel] == yol && durum.Durum.Tur == DurumTuru.Basari, "Son dosya yazılmadı.");

                var onizleme = SalonServisi.SalonluExcelOnizle(yol);
                // Atama sonucu şubeleri birleşik satırlarda tutar ("A+B"); okuyucu her şubeyi ayrı satır okur (eski arayüzde de böyle)
                string Slotlar(IEnumerable<SinavAtama> l) => string.Join(";", l.Select(a => $"{a.BolumAdi}|{a.DersAdi}|{a.Gun:MMdd}|{a.Saat}").Distinct().OrderBy(x => x));
                Dogrula(!onizleme.Bos && Slotlar(onizleme.Okuma.Atamalar) == Slotlar(durum.SonSonuclar), "Okunan sınavlar yazılanlardan farklı.");
                int Ogrenci(IEnumerable<SinavAtama> l) => l.Sum(a => a.AtananSalonlar.Sum(x => x.AtananOgrenciSayisi));
                Dogrula(Ogrenci(onizleme.Okuma.Atamalar) == Ogrenci(durum.SonSonuclar) && onizleme.SalonGorevi > 0,
                    $"Salonlara yerleşen öğrenci: {Ogrenci(onizleme.Okuma.Atamalar)} / {Ogrenci(durum.SonSonuclar)}");

                durum.Kilitler.GozetmenliYukle = true;
                durum.Kilitler.FinalExport = true;
                salon.SalonluExcelUygula(onizleme, yol);
                var k = durum.Kilitler;
                Dogrula(k.GozetmenAta && !k.GozetmenliYukle && !k.FinalExport, "Kilitler yanlış.");
                Dogrula(durum.SonSonuclar == onizleme.Okuma.Atamalar && durum.Durum.Metin.Contains("'Gözetmenleri ata'"), durum.Durum.Metin);
            }
            finally { if (File.Exists(yol)) File.Delete(yol); }
        });

        // ── Gözetmenler ──
        t.Test("Eski arayüzle eşdeğer: gözetmen bölüm seçenekleri, ad / kod / bölüm / ağırlık düzeltmeleri (görevlerdeki ad da değişir)", () =>
        {
            var hazir = SalonluYuklu();
            var (_, goz0, _) = Servisler(hazir);
            goz0.GozetmenleriAta();
            string kayit = OturumKaydi.Serilestir(new OturumYoneticisi(hazir, new IsYurutucu()).Topla());
            string hedefAd = hazir.GozetmenListesi.First(g => g.AtananSinavSayisi > 0).AdSoyad;
            var islemler = new (string Kolon, GozetmenAlani Alan, string Deger)[]
            {
                ("AdSoyad", GozetmenAlani.AdSoyad, "  Ayşe   Yılmaz Kaya "),
                ("Kod", GozetmenAlani.Kod, "B9"),
                ("Bolum", GozetmenAlani.Bolum, "Makine Mühendisliği"),
                ("Agirlik", GozetmenAlani.Agirlik, "1,5"),
            };

            OturumKaydi.Yaz(kayit);
            List<string> eskiSecenekler;
            var eskiler = new List<(string Liste, string Gorevler, string Mesaj)>();
            using (var form = FormuAc())
            {
                eskiSecenekler = (List<string>)Cagir(form, "GozetmenBolumSecenekleri");
                var grid = Alan<DataGridView>(form, "dgvGozetmenler");
                foreach (var (kolon, _, deger) in islemler)
                {
                    var satir = grid.Rows.Cast<DataGridViewRow>().First(r => r.Tag is Gozetmen g && GozetmenServisi.GozetmenAdAnahtari(g.AdSoyad) is var a &&
                                (a == GozetmenServisi.GozetmenAdAnahtari(hedefAd) || a == GozetmenServisi.GozetmenAdAnahtari("Ayşe Yılmaz Kaya")));
                    int k = grid.Columns[kolon].Index;
                    satir.Cells[k].Value = deger;
                    Cagir(form, "DgvGozetmenler_CellEndEdit", grid, new DataGridViewCellEventArgs(k, satir.Index));
                    eskiler.Add((GozetmenMetni(Alan<List<Gozetmen>>(form, "_gozetmenListesi")), GorevMetni(Alan<List<SinavAtama>>(form, "_sonSonuclar")),
                                 Alan<Label>(form, "lblDurum").Text));
                }
                form.Close();
            }

            var durum = YeniArayuzSablonTestleri.Yuklu(kayit);
            var (_, goz, _) = Servisler(durum);
            Dogrula(goz.GozetmenBolumSecenekleri().SequenceEqual(eskiSecenekler), $"Seçenekler: {string.Join(" | ", goz.GozetmenBolumSecenekleri())}");
            var hedef = durum.GozetmenListesi.First(g => g.AdSoyad == hedefAd);
            for (int i = 0; i < islemler.Length; i++)
            {
                string hata = goz.AlaniDegistir(hedef, islemler[i].Alan, islemler[i].Deger);
                Dogrula(hata == null, hata);
                Dogrula(GozetmenMetni(durum.GozetmenListesi) == eskiler[i].Liste, $"{islemler[i].Kolon}: liste\n      eski {eskiler[i].Liste}\n      yeni {GozetmenMetni(durum.GozetmenListesi)}");
                Dogrula(GorevMetni(durum.SonSonuclar) == eskiler[i].Gorevler, $"{islemler[i].Kolon}: salon görevleri farklı.");
                Dogrula(durum.Durum.Metin == eskiler[i].Mesaj, $"{islemler[i].Kolon}: mesaj\n      eski {eskiler[i].Mesaj}\n      yeni {durum.Durum.Metin}");
            }
            Dogrula(eskiler[0].Mesaj.Contains("salon görevinde de güncellendi"), "Eski arayüzde ad değişikliği görevlere yansımadı.");
        });

        t.Test("Gözetmen düzeltme hataları: boş ad, aynı ad (Türkçe harf farkı), kullanılan rakamlı kod, geçersiz ağırlık", () =>
        {
            var durum = SalonluYuklu();
            var (_, goz, _) = Servisler(durum);
            var g0 = durum.GozetmenListesi[0];
            var g1 = durum.GozetmenListesi[1];
            string once = GozetmenMetni(durum.GozetmenListesi);
            Dogrula(goz.AlaniDegistir(g0, GozetmenAlani.AdSoyad, "   ") == "Ad Soyad boş bırakılamaz.", "Boş ad kabul edildi.");
            Dogrula(goz.AlaniDegistir(g0, GozetmenAlani.AdSoyad, g1.AdSoyad.ToLowerInvariant().Replace('İ', 'i'))?.Contains("zaten listede") == true, "Aynı ad kabul edildi.");
            g1.Kod = "B7";
            Dogrula(goz.AlaniDegistir(g0, GozetmenAlani.Kod, "b7") == "'b7' kodu başka bir gözetmende kullanılıyor.", "Kullanılan kod kabul edildi.");
            Dogrula(goz.AlaniDegistir(g0, GozetmenAlani.Kod, "SEKR") == null, "Rakamsız kod (benzersiz olması gerekmez) reddedildi.");
            foreach (var hatali in new[] { "0", "-1", "abc", "" })
                Dogrula(goz.AlaniDegistir(g0, GozetmenAlani.Agirlik, hatali)?.StartsWith("Ağırlık 0'dan büyük") == true, $"Ağırlık '{hatali}' kabul edildi.");
            Dogrula(goz.GozetmenEkle(" ", "Makine Mühendisliği", "", 1, out _) != null && goz.GozetmenEkle("Yeni Kişi", "", "", 1, out _) == "Bölüm seçin.",
                "Eksik bilgiyle gözetmen eklendi.");
        });

        t.Test("Eski arayüzle eşdeğer: gözetmen ekleme ve görevli gözetmen silme (görevler boşalır, durum mesajı)", () =>
        {
            var hazir = SalonluYuklu();
            var (_, goz0, _) = Servisler(hazir);
            goz0.GozetmenleriAta();
            string kayit = OturumKaydi.Serilestir(new OturumYoneticisi(hazir, new IsYurutucu()).Topla());
            var silinecekAdlar = hazir.GozetmenListesi.Where(g => g.AtananSinavSayisi > 0).Take(2).Select(g => g.AdSoyad).ToList();

            OturumKaydi.Yaz(kayit);
            string eskiEkle, eskiSil, eskiListe, eskiGorev;
            using (var form = FormuAc())
            {
                Cagir(form, "GozetmenEkle", new Gozetmen { AdSoyad = "YENİ GÖZETMEN", Bolum = "İnşaat Mühendisliği", Kod = "", Agirlik = 0.5 });
                eskiEkle = Alan<Label>(form, "lblDurum").Text;
                var liste = Alan<List<Gozetmen>>(form, "_gozetmenListesi");
                Cagir(form, "GozetmenleriSil", liste.Where(g => silinecekAdlar.Contains(g.AdSoyad)).ToList(), false);
                eskiSil = Alan<Label>(form, "lblDurum").Text;
                eskiListe = GozetmenMetni(liste);
                eskiGorev = GorevMetni(Alan<List<SinavAtama>>(form, "_sonSonuclar"));
                form.Close();
            }

            var durum = YeniArayuzSablonTestleri.Yuklu(kayit);
            var (_, goz, _) = Servisler(durum);
            Dogrula(goz.GozetmenEkle("YENİ GÖZETMEN", "İnşaat Mühendisliği", "", 0.5, out _) == null && durum.Durum.Metin == eskiEkle,
                $"Ekleme mesajı: eski {eskiEkle} / yeni {durum.Durum.Metin}");
            var silinecek = durum.GozetmenListesi.Where(g => silinecekAdlar.Contains(g.AdSoyad)).ToList();
            string onay = goz.SilmeOnayMetni(silinecek);
            int gorev = goz.GozetmenleriSil(silinecek);
            Dogrula(gorev > 0 && onay.Contains($"{gorev} salon görevi var"), onay);
            Dogrula(durum.Durum.Metin == eskiSil, $"Silme mesajı: eski {eskiSil} / yeni {durum.Durum.Metin}");
            Dogrula(GozetmenMetni(durum.GozetmenListesi) == eskiListe && GorevMetni(durum.SonSonuclar) == eskiGorev, "Liste ya da görevler eski arayüzden farklı.");
        });

        t.Test("Gözetmen ataması: ortak ders listesi kuralı (elle seçim → kod öneki), kilitler, ağırlık özeti", () =>
        {
            var durum = SalonluYuklu();
            var (_, goz, _) = Servisler(durum);
            var kodDisi = goz.GozetmenOrtakDersleri();
            Dogrula(kodDisi.SequenceEqual(new[] { "MATEMATİK I", "FİZİK I" }), $"Kod öneki listesi: {string.Join(", ", kodDisi)}");
            durum.OrtakSecimYapildi = true;
            durum.ManuelOrtakDersAdlari.Add("VERİ YAPILARI");
            Dogrula(goz.GozetmenOrtakDersleri().SequenceEqual(new[] { "VERİ YAPILARI" }), "Elle seçim esas alınmadı.");
            durum.OrtakSecimYapildi = false;

            durum.Kilitler.GozetmenliYukle = true;
            durum.Kilitler.FinalExport = true;
            Dogrula(goz.AtamaEksigi() == null, goz.AtamaEksigi());
            var sonuc = goz.GozetmenleriAta();
            goz.AtamaTamamlandi(sonuc);
            int salonGorevi = durum.SonSonuclar.Sum(a => a.AtananSalonlar.Count);
            Dogrula(sonuc.ToplamAtama > 0 && sonuc.ToplamAtama <= salonGorevi, $"{sonuc.ToplamAtama} atama / {salonGorevi} görev.");
            Dogrula(durum.Kilitler.MatrisExport && !durum.Kilitler.GozetmenliYukle && !durum.Kilitler.FinalExport, "Kilitler yanlış.");
            Dogrula(sonuc.AgirlikOzeti.Select(x => x.Agirlik).SequenceEqual(new[] { 2.0, 1.0 }) &&
                    sonuc.AgirlikOzeti.Sum(x => x.Kisi) == durum.GozetmenListesi.Count, string.Join(" | ", sonuc.AgirlikOzeti));
            Dogrula(IsAkisi.TamamMi(durum, 6), "6. adım tamamlanmış görünmüyor.");
        });

        // ── Final ──
        t.Test("Gözetmenli taslak → geri yükleme → final: dosyalar yazılıyor, değiştirilmemiş taslak tüm satırları güncelliyor, kilitler sırayla açılıyor", () =>
        {
            string taslak = GeciciYol("taslak"), final = GeciciYol("final");
            try
            {
                var durum = SalonluYuklu();
                durum.OgretimYili = 2026;
                durum.Yariyil = Yariyil.Guz;
                var (_, goz, fin) = Servisler(durum);
                goz.AtamaTamamlandi(goz.GozetmenleriAta());
                Dogrula(fin.MatrisExcelEksigi() == null, fin.MatrisExcelEksigi());
                Dogrula(FinalServisi.OnerilenAd(true).StartsWith("SinavProgrami_GozetmenliTaslak_") &&
                        FinalServisi.OnerilenAd(false).StartsWith("SinavProgrami_FINAL_"), "Önerilen adlar eski adlarla aynı değil.");

                fin.OgretimElemanlariniDoldur();
                fin.MatrisExceliYaz(taslak);
                fin.MatrisExceliYazildi(taslak, taslak: true);
                Dogrula(File.Exists(taslak) && durum.Kilitler.GozetmenliYukle && !durum.Kilitler.FinalExport, "Taslak sonrası kilitler yanlış.");

                var okuma = FinalServisi.TaslagiOku(taslak);
                var sonuc = fin.TaslagiUygula(okuma, taslak);
                Dogrula(sonuc.Guncellenen == durum.SonSonuclar.Count && durum.Kilitler.FinalExport, $"{sonuc.Guncellenen} / {durum.SonSonuclar.Count} satır güncellendi.");
                Dogrula(durum.Durum.Metin.StartsWith($"✅ {sonuc.Guncellenen} sınav satırı güncellendi"), durum.Durum.Metin);

                fin.MatrisExceliYaz(final);
                fin.MatrisExceliYazildi(final, taslak: false);
                Dogrula(File.Exists(final) && durum.SonDosyalar[SonDosyaTurleri.Final] == final && IsAkisi.TamamMi(durum, 7), "Final tamamlanmadı.");
                Dogrula(durum.Durum.Metin == "✅ Final sınav programı kaydedildi!", durum.Durum.Metin);
            }
            finally
            {
                foreach (var y in new[] { taslak, final }) if (File.Exists(y)) File.Delete(y);
            }
        });

        // ── Temizle / Sıfırla ──
        t.Test("Temizle (düzeltme 3): çalışma verisi, kilitler, salon ataması ve son dosyalar silinir; derslik / dönem / oranlar / havuz / ortak kalır", () =>
        {
            var durum = SalonluYuklu();
            durum.OgretimYili = 2026;
            durum.Yariyil = Yariyil.Bahar;
            durum.BolumHataOranlari["BİL"] = 10;
            durum.OrtakSecimYapildi = true;
            durum.SonDosyalar[SonDosyaTurleri.DerslikListesi] = "derslik.xlsx";
            durum.SonDosyalar[SonDosyaTurleri.Final] = "final.xlsx";
            int derslik = durum.Derslikler.Count;
            new TemizlemeServisi(durum, new TakvimServisi(durum)).Temizle();

            Dogrula(durum.Dersler.Count == 0 && durum.SablonSonuclari == null && durum.SonSonuclar == null && durum.GunSalonSatirlari == null &&
                    durum.GozetmenListesi.Count == 0 && durum.SablonIntNotlari.Count == 0 && durum.UygulamaliDersler.Count == 0, "Çalışma verisi kaldı.");
            Dogrula(!durum.Kilitler.SozlugeCevir().Values.Any(v => v), "Kilitler sıfırlanmadı.");
            Dogrula(durum.SonDosyalar.Keys.SequenceEqual(new[] { SonDosyaTurleri.DerslikListesi }), string.Join(",", durum.SonDosyalar.Keys));
            Dogrula(durum.Derslikler.Count == derslik && durum.OgretimYili == 2026 && durum.Yariyil == Yariyil.Bahar &&
                    durum.BolumHataOranlari.Count == 1 && durum.OrtakSecimYapildi, "Korunması gereken ayar silindi.");
            Dogrula(durum.TakvimSlotlari.Count > 0 && durum.TakvimSlotlari.Keys.Min() == DateTime.Today.AddDays(1), "Takvim varsayılana dönmedi.");
            Dogrula(durum.Durum.Metin.StartsWith("🗓"), durum.Durum.Metin);
            Dogrula(IsAkisi.SiradakiAdim(durum) == 3, $"Sıradaki adım {IsAkisi.SiradakiAdim(durum)}.");
        });

        t.Test("Uygulamayı sıfırla: ayarlarla birlikte her şey silinir, oturum dosyası kalkar", () =>
        {
            var durum = SalonluYuklu();
            OturumKaydi.Yaz(OturumKaydi.Serilestir(new OturumYoneticisi(durum, new IsYurutucu()).Topla()));
            durum.OgretimYili = 2026;
            durum.Yariyil = Yariyil.Guz;
            durum.OrtakSecimYapildi = true;
            durum.ManuelOrtakDersAdlari.Add("X");
            durum.UygulamaliDersler.Add("VERİ YAPILARI");
            durum.BolumHataOranlari["BİL"] = 10;
            new TemizlemeServisi(durum, new TakvimServisi(durum)).Sifirla();
            Dogrula(durum.Derslikler.Count == 0 && durum.OgretimYili == null && durum.Yariyil == null && !durum.OrtakSecimYapildi &&
                    durum.ManuelOrtakDersAdlari.Count == 0 && durum.UygulamaliDersler.Count == 0 && durum.BolumHataOranlari.Count == 0 &&
                    durum.SablonHavuzlari.Count == 0 && durum.SonDosyalar.Count == 0, "Ayarlar sıfırlanmadı.");
            Dogrula(!File.Exists(OturumKaydi.DosyaYolu), "Oturum dosyası silinmedi.");
            Dogrula(durum.Durum.Metin == "✅ Uygulama sıfırlandı! Fabrika ayarlarına geri dönüldü.", durum.Durum.Metin);
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCILAR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>A3'ün şablonlu oturumu + T1 / T4 derslikleri, uygulamalı lab ve iki ağırlıkta gözetmenler.</summary>
    internal static CalismaDurumu SablonluYuklu()
    {
        var d = YeniArayuzSablonTestleri.SablonluDurum();
        d.Derslikler = new List<Salon>
        {
            new() { SalonKodu = "T1-101", Kapasite = 40 }, new() { SalonKodu = "T1-102", Kapasite = 30 },
            new() { SalonKodu = "T4-201", Kapasite = 60 }, new() { SalonKodu = "T4-202", Kapasite = 45 },
            new() { SalonKodu = "T4-150", Kapasite = 25, SalonTipi = "UYGULAMALI" }, new() { SalonKodu = "T1-301", Kapasite = 70 },
        };
        d.Gozetmenler = new List<Gozetmen>
        {
            new() { Kod = "B1", AdSoyad = "ALİ VELİ", Bolum = "Bilgisayar Mühendisliği", Agirlik = 1.0 },
            new() { Kod = "B2", AdSoyad = "AYŞE KAYA", Bolum = "Bilgisayar Mühendisliği", Agirlik = 2.0 },
            new() { Kod = "M1", AdSoyad = "MEHMET ÖZ", Bolum = "Makine Mühendisliği", Agirlik = 1.0 },
            new() { Kod = "E1", AdSoyad = "ZEYNEP ÇELİK", Bolum = "Elektrik Elektronik Mühendisliği", Agirlik = 1.0 },
            new() { Kod = "", AdSoyad = "CAN ŞAHİN", Bolum = "Makine Mühendisliği", Agirlik = 2.0 },
        };
        // Takvim dışı yerleşimi (EEM, 13:00) takvime alıp salon dağıtımına katalım
        d.Takvim[1].Saatler.Add("13:00");
        return YeniArayuzSablonTestleri.Yuklu(OturumKaydi.Serilestir(d));
    }

    /// <summary>Şablonlu oturumda salonlar atanmış hali.</summary>
    internal static CalismaDurumu SalonluYuklu()
    {
        var durum = SablonluYuklu();
        var (salon, _, _) = Servisler(durum);
        salon.OgretimElemanlariniDoldur();
        salon.SalonDagitiminiUygula(salon.SalonlariHesapla());
        return durum;
    }

    private static (SalonServisi Salon, GozetmenServisi Gozetmen, FinalServisi Final) Servisler(CalismaDurumu durum)
    {
        var hoca = new HocaEslemesi();
        var takvim = new TakvimServisi(durum);
        return (new SalonServisi(durum, hoca, takvim), new GozetmenServisi(durum), new FinalServisi(durum, hoca, takvim));
    }

    private static string GeciciYol(string ad) => Path.Combine(Path.GetTempPath(), $"a4_{ad}_{Guid.NewGuid():N}.xlsx");

    private static T Oz<T>(object nesne, string ad) => nesne == null ? default : (T)nesne.GetType().GetProperty(ad).GetValue(nesne);

    private static bool Ayni(SinavAtama a, SinavAtama b)
        => a.BolumAdi == b.BolumAdi && a.DersAdi == b.DersAdi && a.Sube == b.Sube && a.Gun == b.Gun && a.Saat == b.Saat;

    /// <summary>Eski dgvMatris'i satır satır metne çevirir; salon sütunlarının sırasını da döner.</summary>
    private static List<string> EskiMatris(string kayit, out List<string> salonlar)
    {
        OturumKaydi.Yaz(kayit);
        using var form = FormuAc();
        Cagir(form, "MatrisGorunumuOlustur", Alan<List<SinavAtama>>(form, "_sonSonuclar"), Alan<List<Salon>>(form, "_derslikler"));
        var grid = Alan<DataGridView>(form, "dgvMatris");
        var salonKolonlari = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Tag is Salon).ToList();
        salonlar = salonKolonlari.Select(c => ((Salon)c.Tag).SalonKodu).ToList();
        var satirlar = grid.Rows.Cast<DataGridViewRow>().Select(r =>
        {
            string V(string kolon) => r.Cells[kolon].Value?.ToString() ?? "";
            return $"{V("Bolum")}|{V("Sinif")}|{V("Tarih")}|{V("Saat")}|{V("Ders")}|{V("Kontenjan")} || " +
                   string.Join(",", salonKolonlari.Select(c => r.Cells[c.Index].Value?.ToString() ?? ""));
        }).ToList();
        form.Close();
        return satirlar;
    }

    private static string SatirMetni(MatrisSatiri s, List<Salon> salonlar)
        => $"{s.Bolum}|{(s.Bos ? "" : s.Sinif.ToString())}|{s.Tarih}|{s.Saat}|{s.Ders ?? ""}|{s.Kontenjan?.ToString() ?? ""} || " +
           string.Join(",", salonlar.Select(salon =>
           {
               var sa = SalonServisi.SalonAtamasi(s.Atama, salon.SalonKodu);
               return sa == null ? "" : string.IsNullOrWhiteSpace(sa.Sube) ? sa.AtananOgrenciSayisi.ToString() : sa.Sube;
           }));

    private static string SalonMetni(IEnumerable<SinavAtama> sonuclar)
        => string.Join(" ; ", sonuclar.Select(a => $"{a.BolumAdi}|{a.DersAdi}|{a.Sube}|{a.Gun:MMdd}|{a.Saat}:" +
                                                   string.Join(",", a.AtananSalonlar.Select(s => $"{s.SalonKodu}={s.AtananOgrenciSayisi}/{s.SalonKapasitesi}/{s.Sube}"))));

    private static string GorevMetni(IEnumerable<SinavAtama> sonuclar)
        => string.Join(";", sonuclar.SelectMany(a => a.AtananSalonlar.Select(s => s.GozetmenAdSoyad ?? "")));

    private static string GozetmenMetni(IEnumerable<Gozetmen> liste)
        => string.Join(" ; ", liste.Select(g => $"{g.Kod}|{g.AdSoyad}|{g.Bolum}|{g.Agirlik:0.0#}|{g.AtananSinavSayisi}"));
}
