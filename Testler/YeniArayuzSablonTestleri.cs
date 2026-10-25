using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using SinavProgrami.Services;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// YENİ ARAYÜZ A3 — şablon: otomatik şablon, geri yükleme, ızgara modeli, ızgara düzenlemeleri, kural denetimi.
/// Izgara ve düzenlemeler eski MainForm (dgvSablon) ile yan yana çalıştırılıp hücre hücre karşılaştırılır.
/// Not: eski formun şablon yükleme işleyicisi hoca eşlemesini gerçek %AppData% dosyasına yazdığı için çağrılmaz.
/// </summary>
static class YeniArayuzSablonTestleri
{
    internal const string Bil = "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ";
    internal const string Mak = "MAKİNE MÜHENDİSLİĞİ BÖLÜMÜ";
    internal const string Eem = "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ BÖLÜMÜ";

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Yeni arayüz A3 — şablon");

        // ── Izgara modeli ──
        t.Test("Eski arayüzle eşdeğer: şablon ızgarası hücre hücre aynı (satırlar, kapalı saatler, ders, sınıf, A/B/C, D notu)", () =>
        {
            string kayit = OturumKaydi.Serilestir(SablonluDurum());
            var eski = EskiIzgara(kayit);
            var yeni = YeniIzgara(Yuklu(kayit));
            Dogrula(eski.Count == yeni.Count && eski.Count >= 12, $"Satır sayısı: eski {eski.Count} / yeni {yeni.Count}");
            Dogrula(eski.Any(x => x.Contains("kapalı")) && eski.Any(x => x.Contains("not1")) && eski.Any(x => x.Contains("FİZİK I | 1. Sınıf|1|60|50|40")),
                "Karşılaştırılan ızgara beklenen hücreleri içermiyor.");
            for (int i = 0; i < eski.Count; i++)
                Dogrula(eski[i] == yeni[i], $"Satır {i}:\n      eski {eski[i]}\n      yeni {yeni[i]}");
        });

        t.Test("Eski arayüzle eşdeğer: dersi olmayan boş şablonda sabit bölüm iskeleti", () =>
        {
            var d = SablonluDurum();
            d.Dersler.Clear();
            d.SablonSonuclari = new List<SinavAtama>();
            d.SablonIntNotlari.Clear();
            string kayit = OturumKaydi.Serilestir(d);
            var eski = EskiIzgara(kayit);
            var yeni = YeniIzgara(Yuklu(kayit));
            Dogrula(eski.Count > 0 && eski.SequenceEqual(yeni), $"Eski {eski.Count} / yeni {yeni.Count} satır; ilk fark: " +
                eski.Zip(yeni).FirstOrDefault(x => x.First != x.Second));
        });

        t.Test("Izgara: gün başlığı, saat blokları, aynı hücrede iki ders için ek satır, +1 şube taşması", () =>
        {
            var durum = Yuklu(OturumKaydi.Serilestir(SablonluDurum()));
            var izgara = new SablonServisi(durum, new DersServisi(durum, new HocaEslemesi()), new HocaEslemesi(), new TakvimServisi(durum)).Izgara();
            Dogrula(SablonServisi.GunBasligi(new DateTime(2026, 6, 15)) == "15 Haziran 2026 Pazartesi", SablonServisi.GunBasligi(new DateTime(2026, 6, 15)));
            Dogrula(izgara.Saatler.SequenceEqual(new[] { "09:00", "11:00", "13:00", "15:00" }), string.Join(",", izgara.Saatler));
            var bil09 = izgara.Satirlar.Where(s => s.Saat == "09:00" && s.BolumKisa == "BİL").ToList();
            Dogrula(bil09.Count == 2 && bil09[0].Hucreler[0].Ilk.DersAdi == "MATEMATİK I" && bil09[1].Hucreler[0].Ilk.DersAdi == "VERİ YAPILARI",
                "09:00 BİL satırları yanlış.");
            var ilkSatir = izgara.Satirlar.First(s => s.Saat == "09:00");
            Dogrula(ilkSatir.SaatinIlkSatiri && ilkSatir.SaatBlokSatirSayisi == izgara.Satirlar.Count(s => s.Saat == "09:00"), "Saat bloğu birleşimi yanlış.");
            var fizik = izgara.Satirlar.First(s => s.Saat == "11:00" && s.BolumKisa == "BİL").Hucreler[1];
            Dogrula(fizik.Grup.Count == 4, $"FİZİK I {fizik.Grup?.Count} şube.");
            Dogrula(!izgara.Satirlar.First(s => s.Saat == "13:00").Hucreler[1].Acik, "Takvimde olmayan saat açık görünüyor.");
        });

        // ── Kural denetimi ──
        t.Test("Kural denetimi her zaman çalışıyor; ihlaller türüne göre ayrılıyor, hücrelere dağıtılıyor", () =>
        {
            var durum = Yuklu(OturumKaydi.Serilestir(SablonluDurum()));
            var servis = Servis(durum);
            var izgara = servis.Izgara();
            var turler = izgara.Ihlaller.Select(i => i.Tur).Distinct().OrderBy(x => x).ToList();
            var beklenen = new[] { IhlalTuru.TakvimDisi, IhlalTuru.BolumIciCakisma, IhlalTuru.OrtakDers, IhlalTuru.SinifSaat, IhlalTuru.KapasiteAsimi };
            Dogrula(beklenen.All(turler.Contains), $"Türler: {string.Join(", ", turler)}");
            Dogrula(izgara.Ihlaller.All(i => SablonServisi.TurBul(i.Mesaj) == i.Tur), "Tür eşlemesi tutarsız.");

            var vy = izgara.Satirlar.First(s => s.Saat == "09:00" && s.BolumKisa == "BİL" && s.GrupSira == 1).Hucreler[0];
            Dogrula(vy.Ihlaller.Any(i => i.Tur == IhlalTuru.SinifSaat) && vy.Ihlaller.Any(i => i.Tur == IhlalTuru.BolumIciCakisma),
                string.Join(" | ", vy.Ihlaller.Select(i => i.Mesaj)));
            Dogrula(vy.Ihlaller.Select(i => i.Mesaj).Distinct().Count() == vy.Ihlaller.Count, "Aynı mesaj hücrede tekrarlanıyor.");

            var takvimDisi = izgara.Ihlaller.First(i => i.Tur == IhlalTuru.TakvimDisi).Atama;
            Dogrula(!izgara.Satirlar.SelectMany(s => s.Hucreler).Any(h => h.Grup?.Contains(takvimDisi) == true), "Takvim dışı yerleşim ızgarada görünüyor.");
            Dogrula(SablonServisi.TurBul("Dinlenme molası ihlali: X") == IhlalTuru.DinlenmeMolasi, "Dinlenme molası türü bulunamadı.");
        });

        // ── Düzenlemeler (eski arayüzle yan yana) ──
        t.Test("Eski arayüzle eşdeğer: ders taşıma / ekleme / değiştirme / çıkarma / yazarak eşleme / bulunamayan metin", () =>
        {
            string kayit = OturumKaydi.Serilestir(SablonluDurum());
            var islemler = new (string Saat, string Bolum, int Grup, int Gun, string Secim)[]
            {
                ("11:00", "BİL", 0, 2, "VERİ YAPILARI | 2. Sınıf"),   // başka slottan taşı
                ("09:00", "EEM", 0, 0, "DEVRE TEORİSİ | 2. Sınıf"),   // takvim dışındaki yerleşimi taşı
                ("13:00", "BİL", 0, 2, "algoritma"),                    // birebir ad (Türkçe büyük/küçük harf duyarsız)
                ("09:00", "MAK", 0, 1, "MUK"),                          // "ile başlayan" tek eşleşme → yeni ekleme
                ("13:00", "BİL", 0, 2, "ALGOR"),                        // iki derse uyuyor → bulunamadı
                ("09:00", "MAK", 0, 1, "MUKAVEMET | 2. Sınıf"),         // aynı ders aynı hücrede → işlem yok
                ("11:00", "BİL", 0, 1, "MATEMATİK I | 1. Sınıf"),      // dolu hücreye başka ders: eskisi çıkar, bu taşınır
                ("13:00", "MAK", 0, 2, ""),                              // boş seçim → çıkar
                ("15:00", "BİL", 0, 2, "YOK BÖYLE DERS"),
            };

            OturumKaydi.Yaz(kayit);
            var eskiSonuclar = new List<(string Sablon, string Mesaj)>();
            using (var form = FormuAc())
            {
                var grid = Alan<DataGridView>(form, "dgvSablon");
                foreach (var (saat, bolum, grup, gun, secim) in islemler)
                {
                    Cagir(form, "SablonDersSecimiUygula", EskiSatir(grid, saat, bolum, grup), gun, secim);
                    for (int i = 0; i < 5; i++) Application.DoEvents();   // SablonYenile (BeginInvoke)
                    eskiSonuclar.Add((SablonMetni(Alan<List<SinavAtama>>(form, "_sablonSonuclari")), Alan<Label>(form, "lblDurum").Text));
                }
                form.Close();
            }

            Dogrula(eskiSonuclar.Select(x => x.Sablon).Distinct().Count() == 5 && eskiSonuclar.Select(x => x.Mesaj).Distinct().Count() >= 6,
                "Eski arayüzde işlemler şablonu beklendiği kadar değiştirmedi.");
            var durum = Yuklu(kayit);
            var servis = Servis(durum);
            for (int i = 0; i < islemler.Length; i++)
            {
                var (saat, bolum, grup, gun, secim) = islemler[i];
                var izgara = servis.Izgara();
                var satir = izgara.Satirlar.First(s => s.Saat == saat && s.BolumKisa == bolum && s.GrupSira == grup);
                servis.DersSec(satir, gun, izgara.Gunler[gun], secim);
                string yeni = SablonMetni(durum.SablonSonuclari);
                Dogrula(yeni == eskiSonuclar[i].Sablon, $"İşlem {i + 1} ('{secim}'): şablon farklı.\n      eski {eskiSonuclar[i].Sablon}\n      yeni {yeni}");
                Dogrula(durum.Durum.Metin == eskiSonuclar[i].Mesaj, $"İşlem {i + 1} ('{secim}'): mesaj\n      eski {eskiSonuclar[i].Mesaj}\n      yeni {durum.Durum.Metin}");
            }
        });

        t.Test("Eski arayüzle eşdeğer: A/B/C kontenjanı (ders listesine yansır), D notu ve Delete ile çıkarma", () =>
        {
            string kayit = OturumKaydi.Serilestir(SablonluDurum());
            string eskiSablon, eskiMesaj, eskiDersler, eskiNotlar, eskiSilinmis, eskiSilMesaj;
            List<(string Saat, string Bolum, int Grup, int Gun)> silinecek;

            OturumKaydi.Yaz(kayit);
            using (var form = FormuAc())
            {
                var grid = Alan<DataGridView>(form, "dgvSablon");
                HucreDuzenle(form, grid, EskiSatir(grid, "11:00", "BİL", 0), "g1_B", "55");
                HucreDuzenle(form, grid, EskiSatir(grid, "09:00", "BİL", 1), "g0_D", "  ikinci not ");
                HucreDuzenle(form, grid, EskiSatir(grid, "09:00", "BİL", 0), "g0_D", "");
                eskiSablon = SablonMetni(Alan<List<SinavAtama>>(form, "_sablonSonuclari"));
                eskiMesaj = Alan<Label>(form, "lblDurum").Text;
                eskiDersler = string.Join(";", Alan<List<Ders>>(form, "_dersListesi").Select(d => $"{d.DersAdi}/{d.Sube}={d.Kontenjan}"));
                eskiNotlar = NotMetni(Alan<Dictionary<string, string>>(form, "_sablonIntNotlari"));

                grid = Alan<DataGridView>(form, "dgvSablon");
                grid.ClearSelection();
                EskiSatir(grid, "11:00", "BİL", 0).Cells["g1_Ders"].Selected = true;
                EskiSatir(grid, "09:00", "BİL", 1).Cells["g0_Ders"].Selected = true;
                silinecek = grid.SelectedCells.Cast<DataGridViewCell>()
                    .Where(c => grid.Columns[c.ColumnIndex].Name.EndsWith("_Ders"))
                    .Select(c =>
                    {
                        var bilgi = grid.Rows[c.RowIndex].Tag;
                        string ad = grid.Columns[c.ColumnIndex].Name;
                        return (Oz<string>(bilgi, "Saat"), Oz<string>(bilgi, "BolumKisa"), Oz<int>(bilgi, "GrupSira"), int.Parse(ad.Substring(1, ad.IndexOf('_') - 1)));
                    }).ToList();
                Cagir(form, "DgvSablon_KeyDown", grid, new KeyEventArgs(Keys.Delete));
                for (int i = 0; i < 5; i++) Application.DoEvents();
                eskiSilinmis = SablonMetni(Alan<List<SinavAtama>>(form, "_sablonSonuclari"));
                eskiSilMesaj = Alan<Label>(form, "lblDurum").Text;
                form.Close();
            }

            var durum = Yuklu(kayit);
            var servis = Servis(durum);
            Dogrula(servis.KontenjanDegistir(YeniSatir(servis, "11:00", "BİL", 0), 1, 1, "55") == null, "Kontenjan değiştirilemedi.");
            servis.NotDegistir(YeniSatir(servis, "09:00", "BİL", 1), 0, "  ikinci not ");
            servis.NotDegistir(YeniSatir(servis, "09:00", "BİL", 0), 0, "");
            Dogrula(eskiMesaj.Contains("kontenjanı 55") && eskiDersler.Contains("FİZİK I/B=55") && eskiNotlar.Contains("ikinci not") && !eskiNotlar.Contains("not1"),
                $"Eski arayüzde düzenleme beklenen etkiyi yapmadı: {eskiMesaj} / {eskiNotlar}");
            Dogrula(SablonMetni(durum.SablonSonuclari) == eskiSablon, $"Şablon farklı:\n      eski {eskiSablon}\n      yeni {SablonMetni(durum.SablonSonuclari)}");
            Dogrula(durum.Durum.Metin == eskiMesaj, $"Mesaj: eski {eskiMesaj} / yeni {durum.Durum.Metin}");
            string yeniDersler = string.Join(";", durum.Dersler.Select(d => $"{d.DersAdi}/{d.Sube}={d.Kontenjan}"));
            Dogrula(yeniDersler == eskiDersler, $"Ders listesi:\n      eski {eskiDersler}\n      yeni {yeniDersler}");
            Dogrula(NotMetni(durum.SablonIntNotlari) == eskiNotlar, $"Notlar: eski {eskiNotlar} / yeni {NotMetni(durum.SablonIntNotlari)}");

            var izgara = servis.Izgara();
            var cikan = servis.HucreleriBosalt(silinecek.Select(h =>
                (izgara.Satirlar.First(s => s.Saat == h.Saat && s.BolumKisa == h.Bolum && s.GrupSira == h.Grup), h.Gun)).ToList());
            Dogrula(silinecek.Count >= 2 && SablonMetni(durum.SablonSonuclari) == eskiSilinmis, $"Delete sonrası şablon farklı ({silinecek.Count} hücre).");
            Dogrula(durum.Durum.Metin == eskiSilMesaj, $"Delete mesajı: eski {eskiSilMesaj} / yeni {durum.Durum.Metin}");

            servis.GeriEkle(cikan);
            Dogrula(SablonMetni(durum.SablonSonuclari) == eskiSablon, "Geri al, çıkarılan yerleşimleri geri koymadı.");
            Dogrula(durum.Durum.Metin == $"↩ {cikan.Count} şube yerleşimi geri alındı.", durum.Durum.Metin);
        });

        t.Test("Kontenjan düzenleme hataları: sayı olmayan / sıfır / şubesi olmayan hücre; aynı değer bir şey değiştirmez", () =>
        {
            var durum = Yuklu(OturumKaydi.Serilestir(SablonluDurum()));
            var servis = Servis(durum);
            var satir = YeniSatir(servis, "09:00", "BİL", 0);   // MATEMATİK I: tek şube
            string once = SablonMetni(durum.SablonSonuclari), mesaj = durum.Durum.Metin;
            Dogrula(servis.KontenjanDegistir(satir, 0, 0, "abc") == "Kontenjan pozitif bir tam sayı olmalıdır!", "Metin kabul edildi.");
            Dogrula(servis.KontenjanDegistir(satir, 0, 0, "0") != null, "Sıfır kabul edildi.");
            Dogrula(servis.KontenjanDegistir(satir, 0, 1, "40")?.StartsWith("Bu hücrede düzenlenecek bir şube yok") == true, "Olmayan şube düzenlendi.");
            Dogrula(servis.KontenjanDegistir(satir, 0, 1, "") == null, "Boş olmayan şube hücresi hata verdi.");
            Dogrula(servis.KontenjanDegistir(satir, 0, 0, "120") == null && durum.Durum.Metin == mesaj, "Aynı değer durum yazdı.");
            Dogrula(SablonMetni(durum.SablonSonuclari) == once, "Geçersiz düzenleme şablonu değiştirdi.");
        });

        t.Test("Ders seçenekleri: yalnızca bölümün kontenjanı olan dersleri, sınıf ve ada göre; dersin şu anki yeri", () =>
        {
            var durum = Yuklu(OturumKaydi.Serilestir(SablonluDurum()));
            var servis = Servis(durum);
            var mak = servis.DersSecenekleri(Mak);
            Dogrula(mak.SequenceEqual(new[] { ("MATEMATİK I", 1), ("MUKAVEMET", 2) }), string.Join(", ", mak));
            var yer = servis.DersinSlotlari(Bil, "FİZİK I", 1);
            Dogrula(yer.Count == 1 && yer[0].Saat == "11:00", $"{yer.Count} slot.");
            Dogrula(SablonServisi.DersItemCoz(SablonServisi.DersItemYap("VERİ YAPILARI", 2), out var ad, out var sn) && ad == "VERİ YAPILARI" && sn == 2,
                "Öğe biçimi çözülemedi.");
        });

        // ── Otomatik şablon ve geri yükleme ──
        t.Test("Otomatik şablon ön koşulları eski sırayla: ders listesi → takvim → derslikler → öğretim dönemi", () =>
        {
            var durum = new CalismaDurumu();
            var servis = Servis(durum);
            Dogrula(servis.OtomatikSablonEksigi()?.Contains("sınav listesini") == true, servis.OtomatikSablonEksigi());
            durum.Dersler.Add(new Ders { DersAdi = "X", BolumAdi = Bil, Sube = "A", Kontenjan = 10, SinifSeviyesi = 1 });
            Dogrula(servis.OtomatikSablonEksigi()?.StartsWith("Takvimde hiç sınav günü") == true, servis.OtomatikSablonEksigi());
            durum.TakvimSlotlari[new DateTime(2026, 11, 2)] = new List<string> { "09:00" };
            Dogrula(servis.OtomatikSablonEksigi()?.StartsWith("Derslik listesi yüklenmemiş") == true, servis.OtomatikSablonEksigi());
            durum.Derslikler.Add(new Salon { SalonKodu = "T4-101", Kapasite = 40 });
            Dogrula(servis.OtomatikSablonEksigi()?.Contains("öğretim yılı") == true, servis.OtomatikSablonEksigi());
            durum.OgretimYili = 2026;
            durum.Yariyil = Yariyil.Guz;
            Dogrula(servis.OtomatikSablonEksigi() == null, servis.OtomatikSablonEksigi());
        });

        t.Test("Otomatik şablon Excel'e yazılıyor (çalışma durumu değişmiyor) ve geri yüklenince takvim, kilitler, sıfırlamalar doğru", () =>
        {
            string yol = Path.Combine(Path.GetTempPath(), $"otomatik_sablon_{Guid.NewGuid():N}.xlsx");
            try
            {
                var durum = Yuklu(OturumKaydi.Serilestir(SablonluDurum()));
                var servis = Servis(durum);
                string once = OturumKaydi.Serilestir(new OturumYoneticisi(durum, new IsYurutucu()).Topla());

                var sonuc = servis.OtomatikSablonOlustur(yol);
                Dogrula(File.Exists(yol) && sonuc.Yerlesen > 0 && sonuc.NormalKapasite == 108, $"Yerleşen {sonuc.Yerlesen}, kapasite {sonuc.NormalKapasite}.");
                Dogrula(OturumKaydi.Serilestir(new OturumYoneticisi(durum, new IsYurutucu()).Topla()) == once, "Otomatik şablon çalışma durumunu değiştirdi.");
                servis.OtomatikSablonKaydedildi(sonuc);
                Dogrula(durum.SonDosyalar[SonDosyaTurleri.OtomatikSablon] == yol && durum.Durum.Tur == DurumTuru.Basari, "Son dosya / durum yazılmadı.");

                var onizleme = SablonServisi.Onizle(yol);
                Dogrula(!onizleme.Bos && onizleme.GunSayisi == durum.TakvimSlotlari.Count, $"{onizleme.SubeSayisi} yerleşim, {onizleme.GunSayisi} gün okundu.");
                Dogrula(onizleme.Bolumler.Select(b => b.Bolum).SequenceEqual(onizleme.Bolumler.Select(b => b.Bolum).OrderBy(b => b)), "Bölüm özeti sıralı değil.");

                durum.Kilitler.SalonExport = true;
                durum.Kilitler.OtomatikDagit = true;
                durum.Kilitler.FinalExport = true;
                durum.TakvimSlotlari[new DateTime(2027, 1, 4)] = new List<string> { "09:00" };   // şablonda olmayan gün
                servis.SablonuUygula(onizleme, yol);

                var k = durum.Kilitler;
                Dogrula(k.OtomatikDagit && k.SalonAta && !k.SalonExport && !k.GozetmenAta && !k.MatrisExport && !k.GozetmenliYukle && !k.FinalExport,
                    "Kilitler yanlış.");
                Dogrula(durum.GunSalonSatirlari == null && durum.SonSonuclar == null && durum.SablonIntNotlari.Count == 0,
                    "Salon ataması / sonuçlar / notlar sıfırlanmadı (düzeltme 2).");
                Dogrula(!durum.TakvimSlotlari.ContainsKey(new DateTime(2027, 1, 4)) && durum.TakvimSlotlari.Count == onizleme.GunSayisi,
                    "Takvim şablondakiyle değiştirilmedi.");
                Dogrula(durum.SablonSonuclari.Count == onizleme.SubeSayisi && durum.Dersler.Count == onizleme.Okuma.Dersler.Count, "Şablon / ders listesi yüklenmedi.");
                Dogrula(durum.Durum.Metin.StartsWith($"✅ {onizleme.GunSayisi} gün / {onizleme.SubeSayisi} şube yüklendi."), durum.Durum.Metin);
                Dogrula(IsAkisi.TamamMi(durum, 4) && servis.Izgara().Satirlar.Count > 0, "4. adım tamamlanmış görünmüyor.");
            }
            finally { if (File.Exists(yol)) File.Delete(yol); }
        });

        t.Test("Ders kodu olmayan şubeler (şablondan yüklenmiş liste) otomatik dağıtıma alınmıyor ve sonuçta sayılıyor", () =>
        {
            string yol = Path.Combine(Path.GetTempPath(), $"kodsuz_{Guid.NewGuid():N}.xlsx");
            try
            {
                var durum = Yuklu(OturumKaydi.Serilestir(SablonluDurum()));
                foreach (var ders in durum.Dersler) ders.DersKodu = "";
                var sonuc = Servis(durum).OtomatikSablonOlustur(yol);
                Dogrula(sonuc.Yerlesen == 0 && sonuc.KodsuzSube == durum.Dersler.Count, $"Yerleşen {sonuc.Yerlesen}, kodsuz {sonuc.KodsuzSube}.");

                durum = Yuklu(OturumKaydi.Serilestir(SablonluDurum()));
                durum.Dersler[0].DersKodu = "SEC";   // rakamsız kod da dağıtıma alınmaz
                sonuc = Servis(durum).OtomatikSablonOlustur(yol);
                Dogrula(sonuc.Yerlesen > 0 && sonuc.KodsuzSube == 1, $"Yerleşen {sonuc.Yerlesen}, kodsuz {sonuc.KodsuzSube}.");
            }
            finally { if (File.Exists(yol)) File.Delete(yol); }
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCILAR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Üç bölüm, üç gün (gün başına farklı saatler) ve bilerek kural ihlalleri içeren şablon:
    /// aynı slotta iki BİL dersi, 09:00'da 2. sınıf dersi, iki bölümde farklı saatlere bölünmüş MATEMATİK I,
    /// takvimde olmayan bir yerleşim, 4 şubeli FİZİK I ve kapasiteyi aşan slot.
    /// </summary>
    internal static OturumDurumu SablonluDurum()
    {
        var g0 = new DateTime(2026, 11, 2); var g1 = g0.AddDays(1); var g2 = g0.AddDays(2);
        Ders D(string bolum, string kod, string ad, int sinif, string sube, int kont)
            => new() { BolumAdi = bolum, DersKodu = kod, DersAdi = ad, SinifSeviyesi = sinif, Sube = sube, Kontenjan = kont };
        SinavAtama A(Ders d, DateTime gun, string saat)
            => new() { DersAdi = d.DersAdi, BolumAdi = d.BolumAdi, SinifSeviyesi = d.SinifSeviyesi, Sube = d.Sube, Kontenjan = d.Kontenjan, Gun = gun, Saat = saat };

        var dersler = new List<Ders>
        {
            D(Bil, "BIL214", "VERİ YAPILARI", 2, "A", 80), D(Bil, "BIL214", "VERİ YAPILARI", 2, "B", 70),
            D(Bil, "MAT101", "MATEMATİK I", 1, "A", 120),
            D(Bil, "FIZ101", "FİZİK I", 1, "A", 60), D(Bil, "FIZ101", "FİZİK I", 1, "B", 50),
            D(Bil, "FIZ101", "FİZİK I", 1, "C", 40), D(Bil, "FIZ101", "FİZİK I", 1, "D", 30),
            D(Bil, "BIL301", "ALGORİTMA", 3, "A", 50), D(Bil, "BIL302", "ALGORİTMA ANALİZİ", 3, "A", 40),
            D(Mak, "MAT101", "MATEMATİK I", 1, "A", 90), D(Mak, "MAK201", "MUKAVEMET", 2, "A", 70),
            D(Mak, "MAK999", "SIFIR KONTENJAN", 2, "A", 0),
            D(Eem, "EEM201", "DEVRE TEORİSİ", 2, "A", 60),
        };
        Ders Bul(string bolum, string ad, string sube = "A") => dersler.First(d => d.BolumAdi == bolum && d.DersAdi == ad && d.Sube == sube);

        var sablon = new List<SinavAtama>
        {
            A(Bul(Bil, "VERİ YAPILARI"), g0, "09:00"), A(Bul(Bil, "VERİ YAPILARI", "B"), g0, "09:00"),
            A(Bul(Bil, "MATEMATİK I"), g0, "09:00"),
            A(Bul(Mak, "MATEMATİK I"), g0, "11:00"),
            A(Bul(Bil, "FİZİK I"), g1, "11:00"), A(Bul(Bil, "FİZİK I", "B"), g1, "11:00"),
            A(Bul(Bil, "FİZİK I", "C"), g1, "11:00"), A(Bul(Bil, "FİZİK I", "D"), g1, "11:00"),
            A(Bul(Mak, "MUKAVEMET"), g2, "13:00"),
            A(Bul(Eem, "DEVRE TEORİSİ"), g1, "13:00"),   // g1'de 13:00 yok → takvim dışı
        };

        return new OturumDurumu
        {
            Derslikler = new List<Salon> { new() { SalonKodu = "T4-204", Kapasite = 60 }, new() { SalonKodu = "T4-205", Kapasite = 48 } },
            Dersler = dersler,
            Takvim = new List<TakvimGunu>
            {
                new() { Gun = g0, Saatler = new List<string> { "09:00", "11:00", "13:00" } },
                new() { Gun = g1, Saatler = new List<string> { "09:00", "11:00" } },
                new() { Gun = g2, Saatler = new List<string> { "09:00", "11:00", "13:00", "15:00" } },
            },
            SinavTipi = SinavDonemTipi.Vize,
            BaslangicTarihi = g0,
            SablonSonuclari = sablon,
            SablonIntNotlari = new Dictionary<string, string> { ["0|09:00|BİL|0"] = "not1", ["2|13:00|MAK|0"] = "not2" },
            Butonlar = new Dictionary<string, bool>
            {
                ["btnOtomatikDagit"] = true, ["btnSalonAta"] = true, ["btnSalonExport"] = false, ["btnGozetmenAta"] = false,
                ["btnMatrisExport"] = false, ["btnGozetmenliYukle"] = false, ["btnFinalExport"] = false
            },
        };
    }

    internal static CalismaDurumu Yuklu(string kayit)
    {
        OturumKaydi.Yaz(kayit);
        var durum = new CalismaDurumu();
        new OturumYoneticisi(durum, new IsYurutucu()).GeriYukle();
        return durum;
    }

    private static SablonServisi Servis(CalismaDurumu durum)
    {
        var hoca = new HocaEslemesi();
        return new SablonServisi(durum, new DersServisi(durum, hoca), hoca, new TakvimServisi(durum));
    }

    private static SablonSatiri YeniSatir(SablonServisi servis, string saat, string bolum, int grup)
        => servis.Izgara().Satirlar.First(s => s.Saat == saat && s.BolumKisa == bolum && s.GrupSira == grup);

    /// <summary>Eski dgvSablon'u satır satır metne çevirir: "saat|bölüm|grup || gün: ders|sınıf|A|B|C|D|açık".</summary>
    private static List<string> EskiIzgara(string kayit)
    {
        OturumKaydi.Yaz(kayit);
        using var form = FormuAc();
        var grid = Alan<DataGridView>(form, "dgvSablon");
        int gunSayisi = grid.Columns.Cast<DataGridViewColumn>().Count(c => c.Name.EndsWith("_Ders"));
        var satirlar = new List<string>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            var bilgi = row.Tag;
            var parcalar = new List<string>();
            for (int g = 0; g < gunSayisi; g++)
            {
                string V(string ek) => row.Cells[$"g{g}{ek}"].Value?.ToString() ?? "";
                bool acik = !row.Cells[$"g{g}_Ders"].ReadOnly;
                parcalar.Add(acik ? $"{V("_Bolum")}:{V("_Ders")}|{V("_Sinif")}|{V("_A")}|{V("_B")}|{V("_C")}|{V("_D")}" : "kapalı");
            }
            satirlar.Add($"{Oz<string>(bilgi, "Saat")}|{Oz<string>(bilgi, "BolumKisa")}|{Oz<int>(bilgi, "GrupSira")}|{(row.Cells["Saat"].Value?.ToString() ?? "")} || {string.Join(" ; ", parcalar)}");
        }
        form.Close();
        return satirlar;
    }

    private static List<string> YeniIzgara(CalismaDurumu durum)
    {
        var izgara = Servis(durum).Izgara();
        return izgara.Satirlar.Select(s =>
        {
            var parcalar = s.Hucreler.Select(h =>
            {
                if (!h.Acik) return "kapalı";
                string ders = h.Ilk == null ? "" : SablonServisi.DersItemYap(h.Ilk.DersAdi, h.Ilk.SinifSeviyesi);
                string K(int i) => h.Grup != null && i < h.Grup.Count ? h.Grup[i].Kontenjan.ToString() : "";
                return $"{s.BolumKisa}:{ders}|{h.Ilk?.SinifSeviyesi.ToString() ?? ""}|{K(0)}|{K(1)}|{K(2)}|{h.Not ?? ""}";
            });
            return $"{s.Saat}|{s.BolumKisa}|{s.GrupSira}|{(s.SaatinIlkSatiri ? s.Saat : "")} || {string.Join(" ; ", parcalar)}";
        }).ToList();
    }

    private static DataGridViewRow EskiSatir(DataGridView grid, string saat, string bolum, int grup)
        => grid.Rows.Cast<DataGridViewRow>().First(r =>
            Oz<string>(r.Tag, "Saat") == saat && Oz<string>(r.Tag, "BolumKisa") == bolum && Oz<int>(r.Tag, "GrupSira") == grup);

    /// <summary>Eski formun iç sınıfındaki (SablonSatirBilgi) özelliği okur.</summary>
    private static T Oz<T>(object nesne, string ad) => (T)nesne.GetType().GetProperty(ad).GetValue(nesne);

    /// <summary>Eski tablodaki hücre düzenlemesi: değeri yaz, CellEndEdit'i çağır, yeniden çizimi bekle.</summary>
    private static void HucreDuzenle(MainForm form, DataGridView grid, DataGridViewRow satir, string kolon, string deger)
    {
        int k = grid.Columns[kolon].Index;
        satir.Cells[k].Value = deger;
        Cagir(form, "DgvSablon_CellEndEdit", grid, new DataGridViewCellEventArgs(k, satir.Index));
        for (int i = 0; i < 5; i++) Application.DoEvents();
    }

    private static string SablonMetni(IEnumerable<SinavAtama> sablon)
        => string.Join(" ; ", (sablon ?? Enumerable.Empty<SinavAtama>())
            .Select(a => $"{a.BolumAdi}|{a.DersAdi}|{a.SinifSeviyesi}|{a.Sube}|{a.Kontenjan}|{a.Gun:yyyy-MM-dd}|{a.Saat}")
            .OrderBy(x => x, StringComparer.Ordinal));

    private static string NotMetni(Dictionary<string, string> notlar)
        => string.Join(" ; ", notlar.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}={k.Value}"));
}
