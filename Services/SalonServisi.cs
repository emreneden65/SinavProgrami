#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>"Salonları ata"nın arka planda hesaplanan sonucu (çalışma durumuna henüz yazılmamış).</summary>
    public sealed class SalonDagitimi
    {
        public List<GunlukSalonDagitici.GunSatir> GunSatirlari { get; init; }
        public List<SinavAtama> Sonuclar { get; init; }
        public List<string> Uyarilar { get; init; }

        public int GunSayisi => GunSatirlari.Select(s => s.Gun).Distinct().Count();
        public bool Bos => GunSatirlari.Count == 0;
    }

    /// <summary>Salonlu Excel okunduktan sonra, uygulamadan önce gösterilen özet.</summary>
    public sealed class SalonluExcelOnizlemesi
    {
        public GunSalonMatrisOkuyucu.Sonuc Okuma { get; init; }

        public int GunSayisi => Okuma.Atamalar.Select(a => a.Gun).Distinct().Count();
        public int SinavSayisi => Okuma.Atamalar.Count;
        public int SalonGorevi => Okuma.Atamalar.Sum(a => a.AtananSalonlar?.Count ?? 0);
        public List<string> Uyarilar => Okuma.Uyarilar;
        public bool Bos => Okuma.Atamalar.Count == 0;
    }

    /// <summary>Matrisin bir satırı: bir sınav (şube) ya da takvimde boş kalan gün + saat + bölüm.</summary>
    public sealed class MatrisSatiri
    {
        /// <summary>Boş slot satırında null.</summary>
        public SinavAtama Atama { get; init; }
        public string Bolum { get; init; }
        public int Sinif { get; init; }
        public string Tarih { get; init; }
        public string Saat { get; init; }
        public string Ders { get; init; }
        public int? Kontenjan { get; init; }

        /// <summary>Bölüm + sınıf grubu içindeki sıra (zebra deseni grup başında sıfırlanır).</summary>
        public int GrupSira { get; init; }

        public bool Bos => Atama == null;
    }

    /// <summary>Salon matrisi (eski dgvMatris'in modeli).</summary>
    public sealed class SalonMatrisi
    {
        /// <summary>Salon sütunları: Excel çıktılarıyla aynı sıra (önce T4 binası).</summary>
        public List<Salon> Salonlar { get; init; }
        public List<MatrisSatiri> Satirlar { get; init; }

        /// <summary>Filtre seçenekleri (sonuçlardaki bölümler ve sınıflar).</summary>
        public List<string> Bolumler { get; init; }
        public List<int> Siniflar { get; init; }
    }

    /// <summary>
    /// SALONLAR — şablondan salon dağıtımı, salonlu Excel gidiş-dönüşü, matris ve liste görünümü,
    /// matriste elle salon düzenleme (eski BtnSalonAta_Click, BtnSalonExport_Click,
    /// BtnGozetmenDosyaYukle_Click, MatrisGorunumuOlustur, MatrisFiltreUygula, DgvMatris_CellEndEdit).
    /// </summary>
    public class SalonServisi
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        /// <summary>Boş slot satırlarının bölümleri (eski matristeki sabit liste, yazımıyla).</summary>
        private static readonly string[] TumBolumler = { "BİL", "EEM", "INS", "MAK", "MEK", "MEM" };

        private readonly CalismaDurumu _d;
        private readonly HocaEslemesi _hoca;
        private readonly TakvimServisi _takvim;

        public SalonServisi(CalismaDurumu durum, HocaEslemesi hoca, TakvimServisi takvim)
        {
            _d = durum;
            _hoca = hoca;
            _takvim = takvim;
        }

        // ═══════════════════════════════════════════════════════════════
        // SALONLARI ATA
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Salon ataması için eksik ön koşulun mesajı; yoksa null.</summary>
        public string SalonAtamaEksigi()
        {
            if (_d.SablonSonuclari == null)
                return "Önce 4. adımda doldurulmuş dağıtım şablonunu geri yükleyin!";
            if (_d.SablonSonuclari.Count == 0)
                return "Şablon boş!\nSalon ataması için şablona en az bir ders yerleştirin.";
            if (!_d.DersliklerYukluMu)
                return "Derslik listesi yüklenmemiş!\n\nÖnce '2 · Derslikler' sayfasından okulunuzun derslik Excel'ini yükleyin.";
            return null;
        }

        /// <summary>
        /// Şablondaki öğretim elemanlarını sınav listesinden tamamlar (arayüz iş parçacığında; hoca
        /// eşlemesi dosyaya da yazılır). Şablon Excel'inde hoca sütunu yoktur.
        /// </summary>
        public void OgretimElemanlariniDoldur() => _hoca.Doldur(_d.SablonSonuclari, _d.Dersler);

        /// <summary>
        /// Günlük salon dağıtımını hesaplar: ortak dersler önce, ELM + MEK T4 önceliği, uygulamalı
        /// dersler ayrı salonlarda, kapasiteye göre. Çalışma durumuna yazmaz (arka planda çalışabilir).
        /// </summary>
        public SalonDagitimi SalonlariHesapla()
        {
            var salonlar = _d.Derslikler;
            var dagitici = new GunlukSalonDagitici();
            // Ortak dersler: elle seçim varsa YALNIZCA o liste; yoksa otomatik (aynı ad ≥ 2 bölüm)
            var gunSatirlari = dagitici.Dagit(_d.SablonSonuclari, salonlar, _d.UygulamaliDersler, _d.SablonHavuzlari,
                                              _d.OrtakSecimYapildi ? _d.ManuelOrtakDersAdlari.ToList() : null);
            return new SalonDagitimi
            {
                GunSatirlari = gunSatirlari,
                Sonuclar = gunSatirlari.Count == 0 ? null : GunlukSalonDagitici.SinavAtamalarinaCevir(gunSatirlari, salonlar),
                Uyarilar = dagitici.Uyarilar.ToList()
            };
        }

        /// <summary>Hesaplanan dağıtımı uygular: salonlu Excel açılır, önceki gözetmenli taslak geçersizleşir.</summary>
        public void SalonDagitiminiUygula(SalonDagitimi dagitim)
        {
            _d.GunSalonSatirlari = dagitim.GunSatirlari;
            _d.SonSonuclar = dagitim.Sonuclar;

            var k = _d.Kilitler;
            k.SalonExport = true;
            k.GozetmenliYukle = false;
            k.FinalExport = false;

            if (dagitim.Uyarilar.Count > 0)
                _d.DurumYaz($"⚠ {dagitim.GunSayisi} gün / {dagitim.GunSatirlari.Count} ders salona atandı, {dagitim.Uyarilar.Count} uyarı! " +
                            "'Salonlu Excel çıkar'a basın.", DurumTuru.Uyari);
            else
                _d.DurumYaz($"✅ Salon ataması tamam: {dagitim.GunSayisi} gün, {dagitim.GunSatirlari.Count} ders. " +
                            "Şimdi 'Salonlu Excel çıkar'a basın.", DurumTuru.Basari);
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        // SALONLU EXCEL ÇIKAR / YÜKLE
        // ═══════════════════════════════════════════════════════════════

        public string SalonluExcelEksigi()
        {
            if (_d.GunSalonSatirlari == null || _d.GunSalonSatirlari.Count == 0)
                return "Önce 'Salonları ata' ile salon dağıtımını yapın!";
            return _takvim.OgretimDonemiEksikMesaji();
        }

        /// <summary>Gün sayfalarında salon matrisi + "Sınav Haftası Dağıtım Şablonu" sayfası olan Excel'i yazar.</summary>
        public void SalonluExcelYaz(string yol)
            => GunSalonMatrisExportHelper.YazVeKaydet(_d.GunSalonSatirlari, _d.Derslikler, _d.SablonSonuclari, _d.TakvimSlotlari, yol);

        public void SalonluExcelYazildi(string yol)
        {
            _d.SonDosyalar[SonDosyaTurleri.SalonluExcel] = yol;
            _d.DurumYaz("✅ Salon atanmış dosya oluşturuldu. Gerekirse elle düzenleyip 'Salonlu Excel yükle' ile verin.", DurumTuru.Basari);
            _d.Bildir();
        }

        /// <summary>Salonlu Excel'i okur; çalışma durumuna dokunmaz (arka planda çalışabilir).</summary>
        public static SalonluExcelOnizlemesi SalonluExcelOnizle(string yol)
            => new SalonluExcelOnizlemesi { Okuma = GunSalonMatrisOkuyucu.Oku(yol) };

        /// <summary>
        /// Okunan (elle düzenlenmiş olabilecek) salon atamalarını geçerli sonuç yapar; gözetmen ataması
        /// açılır, önceki gözetmenli taslak geçersizleşir.
        /// </summary>
        public void SalonluExcelUygula(SalonluExcelOnizlemesi onizleme, string yol)
        {
            _d.SonSonuclar = onizleme.Okuma.Atamalar;
            // Bu dosyada hoca sütunu yok — öğretim elemanı kalıcı eşlemeden tamamlanır
            _hoca.Doldur(_d.SonSonuclar, _d.Dersler);

            var k = _d.Kilitler;
            k.GozetmenAta = true;
            k.GozetmenliYukle = false;
            k.FinalExport = false;

            _d.SonDosyalar[SonDosyaTurleri.SalonluExcel] = yol;
            _d.DurumYaz($"✅ {onizleme.GunSayisi} gün / {onizleme.SinavSayisi} ders (salon atanmış) yüklendi. " +
                        "6. adımda 'Gözetmenleri ata'ya basın.", DurumTuru.Basari);
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        // MATRİS VE LİSTE
        // ═══════════════════════════════════════════════════════════════

        private static readonly Dictionary<DayOfWeek, string> KisaGunler = new()
        {
            { DayOfWeek.Monday, "Pzt" }, { DayOfWeek.Tuesday, "Sal" }, { DayOfWeek.Wednesday, "Çar" },
            { DayOfWeek.Thursday, "Per" }, { DayOfWeek.Friday, "Cum" }, { DayOfWeek.Saturday, "Cmt" }, { DayOfWeek.Sunday, "Paz" }
        };

        /// <summary>
        /// Matris: her şube bir satır (bölüm, sınıf, gün, saat, ders, şube sırasıyla); takvimdeki her
        /// gün + saat + bölüm için sınavı olmayan boş satır da eklenir (Kural 6). Sonuç yoksa null.
        /// </summary>
        public SalonMatrisi Matris()
        {
            var sonuclar = _d.SonSonuclar;
            if (sonuclar == null || sonuclar.Count == 0 || _d.Derslikler.Count == 0) return null;

            var satirlar = new List<MatrisSatiri>();
            string onceki = "";
            int sira = 0;
            foreach (var a in sonuclar.OrderBy(s => s.BolumAdi).ThenBy(s => s.SinifSeviyesi).ThenBy(s => s.Gun)
                                      .ThenBy(s => s.Saat).ThenBy(s => s.DersAdi).ThenBy(s => s.Sube))
            {
                string grup = $"{a.BolumAdi}|{a.SinifSeviyesi}";
                if (grup != onceki && onceki != "") sira = 0;
                satirlar.Add(new MatrisSatiri
                {
                    Atama = a, Bolum = a.BolumAdi, Sinif = a.SinifSeviyesi, Tarih = $"{KisaGunler[a.Gun.DayOfWeek]} {a.Gun:dd.MM.yyyy}",
                    Saat = a.Saat, Ders = a.DersEtiketi, Kontenjan = a.Kontenjan, GrupSira = sira
                });
                onceki = grup;
                sira++;
            }

            var dolu = new HashSet<string>(sonuclar.Select(s => $"{s.BolumAdi?.Trim().ToUpperInvariant()}|{s.Gun.Date:yyyy-MM-dd}|{s.Saat}"));
            foreach (var gun in _d.TakvimSlotlari.OrderBy(k => k.Key))
                foreach (var saat in gun.Value.OrderBy(x => x))
                    foreach (var bolum in TumBolumler)
                    {
                        if (dolu.Contains($"{bolum}|{gun.Key.Date:yyyy-MM-dd}|{saat}")) continue;
                        satirlar.Add(new MatrisSatiri
                        {
                            Bolum = bolum, Sinif = 0, Tarih = $"{KisaGunler[gun.Key.DayOfWeek]} {gun.Key:dd.MM.yyyy}", Saat = saat
                        });
                    }

            return new SalonMatrisi
            {
                Salonlar = GunSalonMatrisExportHelper.T4OnceSirala(_d.Derslikler, s => s.SalonKodu).ToList(),
                Satirlar = satirlar,
                Bolumler = sonuclar.Select(s => s.BolumAdi).Distinct().OrderBy(b => b).ToList(),
                Siniflar = sonuclar.Select(s => s.SinifSeviyesi).Distinct().OrderBy(s => s).ToList()
            };
        }

        /// <summary>Bölüm / sınıf filtresi (null = tümü); boş slot satırları sınıf seçilince gizlenir.</summary>
        public static List<MatrisSatiri> Suz(IEnumerable<MatrisSatiri> satirlar, string bolum, int? sinif)
            => satirlar.Where(s => (bolum == null || s.Bolum == bolum) && (sinif == null || s.Sinif == sinif)).ToList();

        /// <summary>Satırdaki salon ataması (yoksa null).</summary>
        public static SalonAtama SalonAtamasi(SinavAtama atama, string salonKodu)
            => atama?.AtananSalonlar?.FirstOrDefault(s => s.SalonKodu == salonKodu);

        /// <summary>
        /// Matriste salon hücresi düzenlemesi: boş ya da 0 → atama silinir; sayı → öğrenci sayısı
        /// ("25 (A)" gibi şube etiketli yazımda baştaki sayı esas alınır). Hata varsa açıklamasını döner.
        /// </summary>
        public string MatrisHucresiniDegistir(SinavAtama atama, Salon salon, string ham)
        {
            if (atama == null || salon == null) return null;
            ham = ham?.Trim();
            var mevcut = SalonAtamasi(atama, salon.SalonKodu);

            if (string.IsNullOrEmpty(ham) || ham == "0")
            {
                if (mevcut != null) atama.AtananSalonlar.Remove(mevcut);
                DuzenlemeSonrasi(atama);
                return null;
            }

            var eslesme = Regex.Match(ham, @"^\s*(\d+)");
            if (!eslesme.Success || !int.TryParse(eslesme.Groups[1].Value, out int yeniDeger) || yeniDeger < 0)
                return "Lütfen pozitif bir tam sayı giriniz (silmek için hücreyi boşaltın veya 0 girin).";

            if (mevcut != null)
                mevcut.AtananOgrenciSayisi = yeniDeger;
            else
            {
                atama.AtananSalonlar ??= new List<SalonAtama>();
                atama.AtananSalonlar.Add(new SalonAtama
                {
                    SalonKodu = salon.SalonKodu,
                    SalonKapasitesi = salon.Kapasite,
                    AtananOgrenciSayisi = yeniDeger
                });
            }

            DuzenlemeSonrasi(atama);
            if (yeniDeger > salon.Kapasite)
            {
                _d.DurumYaz($"⚠ {salon.SalonKodu} kapasitesi ({salon.Kapasite}) aşıldı: {yeniDeger} kişi girildi.", DurumTuru.Hata);
                _d.Bildir();
            }
            return null;
        }

        /// <summary>Matriste silinen salon atamasını geri koyar (bildirimdeki "Geri al").</summary>
        public void SalonAtamasiniGeriKoy(SinavAtama atama, SalonAtama salonAtamasi)
        {
            if (atama == null || salonAtamasi == null) return;
            atama.AtananSalonlar ??= new List<SalonAtama>();
            if (atama.AtananSalonlar.Contains(salonAtamasi) || SalonAtamasi(atama, salonAtamasi.SalonKodu) != null) return;
            atama.AtananSalonlar.Add(salonAtamasi);
            _d.DurumYaz($"↩ '{atama.DersAdi} ({atama.Sube})' için {salonAtamasi.SalonKodu} ataması geri alındı.", DurumTuru.Bilgi);
            _d.Bildir();
        }

        private void DuzenlemeSonrasi(SinavAtama atama)
        {
            _d.DurumYaz($"✏ '{atama.DersAdi} ({atama.Sube})' salon dağılımı güncellendi — Excel çıktısı bu haliyle alınacak.", DurumTuru.Uyari);
            _d.Bildir();
        }

        /// <summary>Liste görünümündeki gün metni: "Pazartesi (02.11.2026)".</summary>
        public static string ListeGunu(DateTime gun) => $"{Tr.DateTimeFormat.GetDayName(gun.DayOfWeek)} ({gun:dd.MM.yyyy})";

        /// <summary>Liste araması: ders, bölüm, salon ya da gözetmen adında geçen (Türkçe, büyük/küçük harf duyarsız).</summary>
        public static List<SinavAtama> ListeyiSuz(IEnumerable<SinavAtama> sonuclar, string filtre)
        {
            filtre = filtre?.Trim();
            var liste = sonuclar ?? Enumerable.Empty<SinavAtama>();
            if (string.IsNullOrEmpty(filtre)) return liste.ToList();
            var ci = Tr.CompareInfo;
            bool Var(string s) => s != null && ci.IndexOf(s, filtre, CompareOptions.IgnoreCase) >= 0;
            return liste.Where(a => Var(a.DersAdi) || Var(a.BolumAdi) || Var(a.AtananSalonlarText)).ToList();
        }
    }
}
