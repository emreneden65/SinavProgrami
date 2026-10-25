#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Doldurulmuş şablon Excel'i okunduktan sonra, uygulamadan önce gösterilen özet.</summary>
    public sealed class SablonOnizlemesi
    {
        public DagitimSablonuOkuyucu.Sonuc Okuma { get; init; }

        /// <summary>Bölüm başına farklı ders sayısı (bölüm adına göre sıralı).</summary>
        public List<(string Bolum, int DersSayisi)> Bolumler { get; init; }

        public int GunSayisi => Okuma.Takvim.Count;
        public int SubeSayisi => Okuma.Sablon.Count;
        public List<string> Uyarilar => Okuma.Uyarilar;
        public bool Bos => Okuma.Sablon.Count == 0;
    }

    /// <summary>Otomatik şablon Excel'i oluşturulduktan sonra kullanıcıya gösterilen sonuç.</summary>
    public sealed class OtomatikSablonSonucu
    {
        public string Yol { get; init; }

        /// <summary>Yerleştirilen ders slotu sayısı (bölüm + ders + gün + saat).</summary>
        public int Yerlesen { get; init; }

        public int NormalKapasite { get; init; }

        /// <summary>
        /// Ders kodu boş ya da rakamsız olduğu için algoritmanın dağıtıma almadığı şube sayısı
        /// (ör. ders listesi bir şablondan geri yüklendiyse kodlar yoktur).
        /// </summary>
        public int KodsuzSube { get; init; }

        /// <summary>Algoritma ve Excel yazıcısının uyarıları (ortak ders bölünmesi, sığmayan dersler vb.).</summary>
        public List<string> Uyarilar { get; init; }
    }

    /// <summary>Şablon doğrulayıcısının ihlal türleri (gruplama ve gizleme için).</summary>
    public enum IhlalTuru
    {
        TakvimDisi,
        BolumIciCakisma,
        OrtakDers,
        SinifSaat,
        DinlenmeMolasi,
        KapasiteAsimi
    }

    public sealed class SablonIhlali
    {
        public IhlalTuru Tur { get; init; }
        public SinavAtama Atama { get; init; }
        public string Mesaj { get; init; }
    }

    /// <summary>Şablon ızgarasında bir gün bloğunun bir satırdaki hücreleri (Bölüm · Sınıf · Ders · A · B · C · D).</summary>
    public sealed class SablonHucresi
    {
        /// <summary>Bu gün için bu saat takvimde seçili mi? Değilse hücreler kapalıdır.</summary>
        public bool Acik { get; init; }

        /// <summary>Hücredeki ders grubunun atamaları (şube sıralı); boşsa null.</summary>
        public List<SinavAtama> Grup { get; init; }

        /// <summary>D sütununun serbest notu (eski "INT").</summary>
        public string Not { get; init; }

        /// <summary>Gruptaki atamaların ihlalleri (aynı mesaj bir kez).</summary>
        public List<SablonIhlali> Ihlaller { get; init; } = new();

        public SinavAtama Ilk => Grup?[0];
    }

    /// <summary>Şablon ızgarasının bir satırı: saat bloğu × bölüm (+ aynı hücrede birden çok ders varsa ek satır).</summary>
    public sealed class SablonSatiri
    {
        public int Sira { get; init; }
        public string Saat { get; init; }
        public string BolumKisa { get; init; }
        public string BolumTam { get; init; }

        /// <summary>Aynı saat+bölümde birden çok ders varsa bu satırın grup sırası (0 tabanlı).</summary>
        public int GrupSira { get; init; }

        /// <summary>Saat bloğunun ilk satırı mı (saat hücresi bu satırda, blok boyunca birleşik)?</summary>
        public bool SaatinIlkSatiri { get; init; }

        /// <summary>Saat bloğunun satır sayısı (yalnızca ilk satırda anlamlı).</summary>
        public int SaatBlokSatirSayisi { get; set; }

        public int SaatBlokSira { get; init; }

        /// <summary>Gün indeksi → hücre.</summary>
        public SablonHucresi[] Hucreler { get; init; }
    }

    /// <summary>Şablon ızgarasının tamamı (eski dgvSablon'un modeli).</summary>
    public sealed class SablonIzgarasi
    {
        public List<DateTime> Gunler { get; init; }
        public List<string> Saatler { get; init; }
        public List<(string Kisa, string Tam)> Bolumler { get; init; }
        public List<SablonSatiri> Satirlar { get; init; }
        public List<SablonIhlali> Ihlaller { get; init; }
    }

    /// <summary>
    /// ŞABLON — otomatik şablon (Excel), doldurulmuş şablonu geri yükleme ve şablon ızgarası
    /// üzerinde düzenleme (eski BtnOtomatikDagit_Click, BtnSablonYukle_Click, SablonGorunumuOlustur,
    /// SablonDersSecimiUygula, DgvSablon_CellEndEdit, DgvSablon_KeyDown, SablonDogrulaVeBoya).
    /// Kural ihlalleri ENGELLEMEZ; yalnızca gösterilir.
    /// </summary>
    public class SablonServisi
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
        private readonly CalismaDurumu _d;
        private readonly DersServisi _dersler;
        private readonly HocaEslemesi _hoca;
        private readonly TakvimServisi _takvim;

        public SablonServisi(CalismaDurumu durum, DersServisi dersler, HocaEslemesi hoca, TakvimServisi takvim)
        {
            _d = durum;
            _dersler = dersler;
            _hoca = hoca;
            _takvim = takvim;
        }

        // ═══════════════════════════════════════════════════════════════
        // OTOMATİK ŞABLON
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Otomatik şablon için eksik olan ilk ön koşulun mesajı (eski uyarı sırası: ders listesi,
        /// takvim, derslikler, öğretim dönemi); hepsi tamamsa null.
        /// </summary>
        public string OtomatikSablonEksigi()
        {
            if (_d.Dersler.Count == 0)
                return "Önce '3 · Dersler' sayfasından sınav listesini yükleyin!";
            if (_d.TakvimSlotlari.Count == 0)
                return "Takvimde hiç sınav günü seçili değil!\n'1 · Dönem ve Takvim' sayfasındaki takvimden en az bir gün seçin.";
            if (!_d.DersliklerYukluMu)
                return "Derslik listesi yüklenmemiş!\n\nÖnce '2 · Derslikler' sayfasından okulunuzun derslik Excel'ini yükleyin.";
            return _takvim.OgretimDonemiEksikMesaji();
        }

        /// <summary>
        /// Dersleri kurallara göre dağıtır ve "Sınav Haftası Dağıtım Şablonu" Excel'ini yazar. Çalışma
        /// durumu değişmez (şablon Excel'den geri yüklenince belleğe alınır). Arka planda çalışabilir;
        /// girdiler çağrı anındaki listelerin kopyalarıdır.
        /// </summary>
        public OtomatikSablonSonucu OtomatikSablonOlustur(string yol)
        {
            var dersler = _d.Dersler.ToList();
            var takvim = _d.TakvimSlotlari.ToDictionary(k => k.Key, k => k.Value.ToList());
            var derslikler = _d.Derslikler.ToList();
            int normalKapasite = derslikler.Where(s => !s.UygulamaliMi).Sum(s => s.Kapasite);

            var algo = new SablonDagitimAlgoritmasi();
            // Ortak dersler: kullanıcı elle seçim yaptıysa YALNIZCA o liste; yoksa otomatik tespit.
            // Hata oranı: şablon efektif kontenjanla kurulur, salon dağıtımı da bu şablonu esas alır.
            var dagitim = algo.Dagit(dersler, takvim, normalKapasite,
                                     _d.OrtakSecimYapildi ? _d.ManuelOrtakKodlar.ToList() : null,
                                     _dersler.HataOranlariniOku());

            CekilmisDerslerExportHelper.Olustur(dersler, takvim, yol, out var uyarilar, dagitim, derslikler);

            var tumUyarilar = new List<string>();
            if (algo.Uyarilar != null) tumUyarilar.AddRange(algo.Uyarilar);
            if (uyarilar != null) tumUyarilar.AddRange(uyarilar);

            return new OtomatikSablonSonucu
            {
                Yol = yol,
                Yerlesen = dagitim.Select(a => (a.BolumAdi, a.DersAdi, a.Gun, a.Saat)).Distinct().Count(),
                NormalKapasite = normalKapasite,
                KodsuzSube = dersler.Count(d => string.IsNullOrWhiteSpace(d.DersKodu) || !d.DersKodu.Any(char.IsDigit)),
                Uyarilar = tumUyarilar
            };
        }

        /// <summary>Oluşturulan otomatik şablonu son dosya olarak kaydeder ve durumu yazar (arayüz iş parçacığında).</summary>
        public void OtomatikSablonKaydedildi(OtomatikSablonSonucu sonuc)
        {
            _d.SonDosyalar[SonDosyaTurleri.OtomatikSablon] = sonuc.Yol;
            _d.DurumYaz("✅ Otomatik şablon oluşturuldu. Excel'de kontrol edin; gerekirse düzenleyip 'Şablonu geri yükle' ile yükleyin.",
                        DurumTuru.Basari);
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        // ŞABLONU GERİ YÜKLE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Doldurulmuş şablonu okur; çalışma durumuna dokunmaz (arka planda çalışabilir).</summary>
        public static SablonOnizlemesi Onizle(string yol)
        {
            var okuma = DagitimSablonuOkuyucu.Oku(yol);
            return new SablonOnizlemesi
            {
                Okuma = okuma,
                Bolumler = okuma.Sablon
                    .GroupBy(a => a.BolumAdi)
                    .Select(g => (Bolum: g.Key, DersSayisi: g.Select(x => x.DersAdi).Distinct().Count()))
                    .OrderBy(x => x.Bolum)
                    .ToList()
            };
        }

        /// <summary>
        /// Onaylanan şablonu yükler: ders listesi ve takvim şablondan gelir, D notları temizlenir.
        /// Düzeltme 2: önceki salon ataması ve "Salonlu Excel" kilidi de sıfırlanır.
        /// </summary>
        public void SablonuUygula(SablonOnizlemesi onizleme, string yol)
        {
            var okuma = onizleme.Okuma;

            _d.Dersler.Clear();
            _d.Dersler.AddRange(okuma.Dersler);
            _d.UygulamaliDersleriBuda();

            // Şablondan gelen derslerde hoca bilgisi varsa eşlemeye ekle / kaydet
            _hoca.GuncelleVeKaydet(_d.Dersler);

            _d.TakvimSlotlari.Clear();
            foreach (var kvp in okuma.Takvim)
                _d.TakvimSlotlari[kvp.Key] = new List<string>(kvp.Value);

            _d.SablonSonuclari = okuma.Sablon;
            _d.SablonIntNotlari.Clear();
            _d.GunSalonSatirlari = null;
            _d.SonSonuclar = null;

            // Adım kilitleri: Salonları Ata açılır, sonrakiler kapanır (Otomatik Şablon olduğu gibi kalır)
            var k = _d.Kilitler;
            k.SalonAta = true;
            k.SalonExport = false;
            k.GozetmenAta = false;
            k.MatrisExport = false;
            k.GozetmenliYukle = false;
            k.FinalExport = false;

            _d.SonDosyalar[SonDosyaTurleri.OtomatikSablon] = yol;

            // Sınav türü gün sayısından tahmin edilmez (10 günlük vize Final sayılmasın); seçim korunur
            _takvim.SinavTipiDurumunuYaz();
            _d.DurumYaz($"✅ {onizleme.GunSayisi} gün / {_d.SablonSonuclari.Count} şube yüklendi. " +
                        "Gerekirse hata oranlarını kontrol edip 5. adımda 'Salonları ata'yı çalıştırın.", DurumTuru.Basari);
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        // ŞABLON IZGARASI
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Ders seçim öğesi biçimi: "DERS ADI | 3. Sınıf".</summary>
        public static string DersItemYap(string dersAdi, int sinif) => $"{dersAdi} | {sinif}. Sınıf";

        /// <summary>DersItemYap biçimini (dersAdi, sinif) olarak geri çözer.</summary>
        public static bool DersItemCoz(string item, out string dersAdi, out int sinif)
        {
            dersAdi = null; sinif = 0;
            if (string.IsNullOrWhiteSpace(item)) return false;
            int idx = item.LastIndexOf(" | ");
            if (idx <= 0) return false;
            dersAdi = item.Substring(0, idx).Trim();
            string son = item.Substring(idx + 3).Replace(". Sınıf", "").Trim();
            return int.TryParse(son, out sinif) && dersAdi.Length > 0;
        }

        /// <summary>D notları için satır + gün anahtarı (oturum kaydındaki biçim).</summary>
        public static string IntNotAnahtari(int gunIdx, SablonSatiri satir)
            => $"{gunIdx}|{satir.Saat}|{satir.BolumKisa}|{satir.GrupSira}";

        /// <summary>Gün bandı başlığı: "15 Haziran 2026 Pazartesi".</summary>
        public static string GunBasligi(DateTime gun) => gun.ToString("d MMMM yyyy dddd", Tr);

        /// <summary>
        /// Izgarayı çalışma durumundan kurar ve şablonu doğrular. Şablon yoksa null.
        /// Eksenler takvim ve ders listesinden gelir; bölümler kısa adlarına göre sıralanır.
        /// </summary>
        public SablonIzgarasi Izgara()
        {
            var sablon = _d.SablonSonuclari;
            if (sablon == null) return null;

            var gunler = _d.TakvimSlotlari.Keys.OrderBy(d => d).ToList();
            var saatler = _d.TakvimSlotlari.Values.SelectMany(s => s).Distinct().OrderBy(s => s).ToList();

            var bolumAdlari = _d.Dersler.Select(d => d.BolumAdi)
                .Concat(sablon.Select(a => a.BolumAdi))
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => b.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (bolumAdlari.Count == 0) // hiç ders yoksa sabit bölüm listesi iskeleti
                bolumAdlari = BolumAdlari.StandartBolumler.ToList();

            var bolumler = bolumAdlari
                .Select(b => (Kisa: BolumAdlari.BolumKisalt(b), Tam: b))
                .OrderBy(x => x.Kisa)
                .ToList();

            var ihlaller = Dogrula();
            var ihlalMap = ihlaller.GroupBy(i => i.Atama).ToDictionary(g => g.Key, g => g.ToList());

            // (günIdx, saat, bölüm) → ders grupları (ders + sınıf; şubeler birlikte)
            var hucreGruplari = new Dictionary<string, List<List<SinavAtama>>>();
            for (int g = 0; g < gunler.Count; g++)
            {
                var gun = gunler[g].Date;
                foreach (var atamaGrubu in sablon
                    .Where(a => a.Gun.Date == gun)
                    .GroupBy(a => new { a.Saat, Bolum = (a.BolumAdi ?? "").ToUpperInvariant().Trim() }))
                {
                    hucreGruplari[$"{g}|{atamaGrubu.Key.Saat}|{atamaGrubu.Key.Bolum}"] = atamaGrubu
                        .GroupBy(a => new { Ders = (a.DersAdi ?? "").ToUpperInvariant().Trim(), a.SinifSeviyesi })
                        .OrderBy(x => x.Key.SinifSeviyesi).ThenBy(x => x.Key.Ders)
                        .Select(x => x.OrderBy(a => a.Sube).ToList())
                        .ToList();
                }
            }

            var satirlar = new List<SablonSatiri>();
            int saatBlokIdx = 0;
            foreach (var saat in saatler)
            {
                SablonSatiri blokIlki = null;
                foreach (var (kisa, tam) in bolumler)
                {
                    string bolumUpper = tam.ToUpperInvariant().Trim();

                    // Bu (saat, bölüm) için gün başına grup sayısının en büyüğü kadar satır gerekir
                    int satirSayisi = 1;
                    for (int g = 0; g < gunler.Count; g++)
                        if (hucreGruplari.TryGetValue($"{g}|{saat}|{bolumUpper}", out var gr) && gr.Count > satirSayisi)
                            satirSayisi = gr.Count;

                    for (int k = 0; k < satirSayisi; k++)
                    {
                        var hucreler = new SablonHucresi[gunler.Count];
                        var satir = new SablonSatiri
                        {
                            Sira = satirlar.Count, Saat = saat, BolumKisa = kisa, BolumTam = tam, GrupSira = k,
                            SaatinIlkSatiri = blokIlki == null, SaatBlokSira = saatBlokIdx, Hucreler = hucreler
                        };
                        blokIlki ??= satir;
                        blokIlki.SaatBlokSatirSayisi++;

                        for (int g = 0; g < gunler.Count; g++)
                        {
                            if (!(_d.TakvimSlotlari.TryGetValue(gunler[g], out var gunSaatleri) && gunSaatleri.Contains(saat)))
                            {
                                hucreler[g] = new SablonHucresi { Acik = false };
                                continue;
                            }

                            List<SinavAtama> grup = null;
                            if (hucreGruplari.TryGetValue($"{g}|{saat}|{bolumUpper}", out var gruplar) && k < gruplar.Count)
                                grup = gruplar[k];

                            _d.SablonIntNotlari.TryGetValue(IntNotAnahtari(g, satir), out string not);
                            hucreler[g] = new SablonHucresi
                            {
                                Acik = true,
                                Grup = grup,
                                Not = not,
                                Ihlaller = grup == null ? new List<SablonIhlali>() : grup
                                    .Where(ihlalMap.ContainsKey)
                                    .SelectMany(a => ihlalMap[a])
                                    .GroupBy(i => i.Mesaj).Select(x => x.First())
                                    .ToList()
                            };
                        }
                        satirlar.Add(satir);
                    }
                }
                saatBlokIdx++;
            }

            return new SablonIzgarasi { Gunler = gunler, Saatler = saatler, Bolumler = bolumler, Satirlar = satirlar, Ihlaller = ihlaller };
        }

        /// <summary>
        /// Şablonu güncel takvim, derslik, uygulamalı ders ve hata oranı bilgileriyle doğrular.
        /// (Yeni arayüzde her zaman çalışır; eski arayüzde yalnızca derslik yeniden yüklenince kuruluyordu.)
        /// </summary>
        public List<SablonIhlali> Dogrula()
        {
            if (_d.SablonSonuclari == null) return new List<SablonIhlali>();
            var dogrulayici = new SablonDogrulayici(_d.TakvimSlotlari, _d.Derslikler, _d.UygulamaliDersler,
                                                    _dersler.HataOranlariniOku(), 1);
            return dogrulayici.Dogrula(_d.SablonSonuclari)
                .Select(i => new SablonIhlali { Tur = TurBul(i.Mesaj), Atama = i.Atama, Mesaj = i.Mesaj })
                .ToList();
        }

        /// <summary>Doğrulayıcı mesajından ihlal türü (mesajların başı doğrulayıcıda sabittir).</summary>
        public static IhlalTuru TurBul(string mesaj)
        {
            mesaj ??= "";
            if (mesaj.StartsWith("BÖLÜM İÇİ ÇAKIŞMA", StringComparison.Ordinal)) return IhlalTuru.BolumIciCakisma;
            if (mesaj.StartsWith("Ortak ders bütünlüğü", StringComparison.Ordinal)) return IhlalTuru.OrtakDers;
            if (mesaj.StartsWith("Sınıf-saat kuralı", StringComparison.Ordinal)) return IhlalTuru.SinifSaat;
            if (mesaj.StartsWith("Dinlenme molası", StringComparison.Ordinal)) return IhlalTuru.DinlenmeMolasi;
            if (mesaj.StartsWith("Slot kapasite aşımı", StringComparison.Ordinal)) return IhlalTuru.KapasiteAsimi;
            return IhlalTuru.TakvimDisi;
        }

        public static string TurAdi(IhlalTuru tur) => tur switch
        {
            IhlalTuru.BolumIciCakisma => "Bölüm içi çakışma",
            IhlalTuru.OrtakDers => "Ortak ders bütünlüğü",
            IhlalTuru.SinifSaat => "Sınıf-saat kuralı",
            IhlalTuru.DinlenmeMolasi => "Dinlenme molası",
            IhlalTuru.KapasiteAsimi => "Slot kapasite aşımı",
            _ => "Takvimde olmayan slot"
        };

        public static string TurAciklamasi(IhlalTuru tur) => tur switch
        {
            IhlalTuru.BolumIciCakisma => "Bir bölümün aynı saatte birden fazla farklı sınavı var (17:00 hariç).",
            IhlalTuru.OrtakDers => "Aynı adlı ders farklı bölümlerde farklı saatlere dağılmış. Denetim dersleri adlarına göre " +
                                   "eşler; dağıtım ise ortak dersi ders koduna göre belirler, bu yüzden bölüme özel aynı adlı " +
                                   "dersler de burada görünebilir.",
            IhlalTuru.SinifSaat => "Sınıfın tercih edilen saati: 1. sınıf 11:00, 2. sınıf 15:00, 3. sınıf 09:00, 4. sınıf 13:00 (17:00 hariç).",
            IhlalTuru.DinlenmeMolasi => "Aynı bölüm ve sınıfın aynı gündeki iki sınavı arasında en az bir slot boşluk yok.",
            IhlalTuru.KapasiteAsimi => "Slottaki öğrenci sayısı (hata oranı düşülmüş) derslik kapasitesini aşıyor.",
            _ => "Yerleşimin gün/saati takvimde seçili değil; bu yerleşim ızgarada görünmez."
        };

        /// <summary>Bir satırın bölümünde seçilebilecek dersler (kontenjanı olan ders + sınıf çiftleri, sınıf ve ada göre).</summary>
        public List<(string DersAdi, int Sinif)> DersSecenekleri(string bolumTam)
            => _d.Dersler
                .Where(d => d.Kontenjan > 0 && string.Equals(d.BolumAdi?.Trim(), bolumTam, StringComparison.OrdinalIgnoreCase))
                .Select(d => (d.DersAdi, d.SinifSeviyesi))
                .Distinct()
                .OrderBy(x => x.SinifSeviyesi).ThenBy(x => x.DersAdi)
                .ToList();

        /// <summary>Dersin şablondaki şu anki slotları (bölüm + ders + sınıf); seçim listesinde "taşınacak" bilgisi için.</summary>
        public List<(DateTime Gun, string Saat)> DersinSlotlari(string bolumTam, string dersAdi, int sinif)
            => (_d.SablonSonuclari ?? new List<SinavAtama>())
                .Where(a => string.Equals(a.BolumAdi, bolumTam, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(a.DersAdi, dersAdi, StringComparison.OrdinalIgnoreCase)
                         && a.SinifSeviyesi == sinif)
                .Select(a => (a.Gun.Date, a.Saat))
                .Distinct()
                .OrderBy(x => x.Date).ThenBy(x => x.Saat)
                .ToList();

        /// <summary>
        /// Serbest yazılan metni bölümün ders listesindeki bir derse eşler: önce birebir ad, sonra
        /// "ile başlayan" TEK eşleşme (Türkçe, büyük/küçük harf duyarsız).
        /// </summary>
        public bool YazilanMetniDerseEsle(string metin, string bolumTam, out string dersAdi, out int sinif)
        {
            dersAdi = null; sinif = 0;
            metin = metin?.Trim();
            if (string.IsNullOrEmpty(metin)) return false;

            var trCompare = Tr.CompareInfo;
            var adaylar = _d.Dersler
                .Where(d => d.Kontenjan > 0 && string.Equals(d.BolumAdi, bolumTam, StringComparison.OrdinalIgnoreCase))
                .Select(d => new { d.DersAdi, d.SinifSeviyesi })
                .Distinct()
                .ToList();

            var tam = adaylar.Where(a => trCompare.Compare(a.DersAdi, metin, CompareOptions.IgnoreCase) == 0).ToList();
            if (tam.Count == 1) { dersAdi = tam[0].DersAdi; sinif = tam[0].SinifSeviyesi; return true; }

            var baslayan = adaylar.Where(a => trCompare.IsPrefix(a.DersAdi, metin, CompareOptions.IgnoreCase)).ToList();
            if (baslayan.Count == 1) { dersAdi = baslayan[0].DersAdi; sinif = baslayan[0].SinifSeviyesi; return true; }

            return false;
        }

        /// <summary>
        /// Ders hücresi seçimini şablona uygular:
        /// • Boş seçim → hücredeki ders şablondan çıkarılır.
        /// • Ders şablonun başka slotundaysa → buraya TAŞINIR (gün / saat değişimi).
        /// • Değilse → ders listesindeki tüm şubeleriyle bu slota EKLENİR.
        /// Eşleşmeyen metinde açıklama döner (şablon değişmez), aksi halde null.
        /// </summary>
        public string DersSec(SablonSatiri satir, int gunIdx, DateTime gun, string secim)
        {
            if (satir == null || _d.SablonSonuclari == null) return null;
            var eskiGrup = satir.Hucreler.ElementAtOrDefault(gunIdx)?.Grup;

            if (string.IsNullOrWhiteSpace(secim))
            {
                if (eskiGrup == null || eskiGrup.Count == 0) return null;
                string cikarilan = eskiGrup[0].DersAdi;
                _d.SablonSonuclari.RemoveAll(a => eskiGrup.Contains(a));
                _d.DurumYaz($"🗑 '{cikarilan}' şablondan çıkarıldı.", DurumTuru.Uyari);
                _d.Bildir();
                return null;
            }

            // Önce standart öğe biçimini çöz; olmuyorsa serbest yazılan metni derse eşle
            if (!DersItemCoz(secim, out string dersAdi, out int sinif) &&
                !YazilanMetniDerseEsle(secim, satir.BolumTam, out dersAdi, out sinif))
            {
                string mesaj = $"⚠ '{secim}' bu bölümün ders listesinde bulunamadı — listeden seçin veya tam adı yazın.";
                _d.DurumYaz(mesaj, DurumTuru.Uyari);
                _d.Bildir();
                return mesaj.Substring(2);
            }

            // Aynı ders zaten bu hücredeyse işlem yok
            if (eskiGrup != null && eskiGrup.Count > 0 &&
                eskiGrup[0].DersAdi.Equals(dersAdi, StringComparison.OrdinalIgnoreCase) &&
                eskiGrup[0].SinifSeviyesi == sinif)
                return null;

            // Hücrede başka ders varsa önce onu çıkar
            if (eskiGrup != null && eskiGrup.Count > 0)
                _d.SablonSonuclari.RemoveAll(a => eskiGrup.Contains(a));

            var mevcutAtamalar = _d.SablonSonuclari
                .Where(a => a.BolumAdi.Equals(satir.BolumTam, StringComparison.OrdinalIgnoreCase)
                         && a.DersAdi.Equals(dersAdi, StringComparison.OrdinalIgnoreCase)
                         && a.SinifSeviyesi == sinif)
                .ToList();

            if (mevcutAtamalar.Count > 0)
            {
                foreach (var a in mevcutAtamalar) { a.Gun = gun; a.Saat = satir.Saat; }
                _d.DurumYaz($"📌 '{dersAdi}' → {gun:dd.MM.yyyy} {satir.Saat} slotuna taşındı ({mevcutAtamalar.Count} şube).", DurumTuru.Uyari);
            }
            else
            {
                // Yeni ekleme: dersin tüm şubeleri bu slota yerleştirilir
                var subeler = _d.Dersler
                    .Where(d => d.Kontenjan > 0
                             && d.BolumAdi.Equals(satir.BolumTam, StringComparison.OrdinalIgnoreCase)
                             && d.DersAdi.Equals(dersAdi, StringComparison.OrdinalIgnoreCase)
                             && d.SinifSeviyesi == sinif)
                    .ToList();

                foreach (var sube in subeler)
                {
                    _d.SablonSonuclari.Add(new SinavAtama
                    {
                        DersAdi = sube.DersAdi,
                        BolumAdi = sube.BolumAdi,
                        SinifSeviyesi = sube.SinifSeviyesi,
                        Kontenjan = sube.Kontenjan,
                        Sube = sube.Sube,
                        OgretimElemani = sube.OgretimElemani,
                        Gun = gun,
                        Saat = satir.Saat,
                        AtananSalonlar = new List<SalonAtama>()
                    });
                }
                _d.DurumYaz($"➕ '{dersAdi}' şablona eklendi ({subeler.Count} şube — {gun:dd.MM.yyyy} {satir.Saat}).", DurumTuru.Basari);
            }
            _d.Bildir();
            return null;
        }

        /// <summary>
        /// A / B / C hücresi: şube kontenjanını değiştirir; ders listesindeki ilgili şubeye de yansır.
        /// Hata varsa açıklamasını döner (değer uygulanmaz), yoksa null.
        /// </summary>
        public string KontenjanDegistir(SablonSatiri satir, int gunIdx, int subeSira, string ham)
        {
            if (satir == null || _d.SablonSonuclari == null) return null;
            ham = ham?.Trim();
            var grup = satir.Hucreler.ElementAtOrDefault(gunIdx)?.Grup;
            if (grup == null || subeSira < 0 || subeSira >= grup.Count)
                return string.IsNullOrEmpty(ham) ? null
                    : "Bu hücrede düzenlenecek bir şube yok. Önce 'Ders adı' sütunundan ders yerleştirin.";

            var atama = grup[subeSira];
            if (!int.TryParse(ham, out int yeniKontenjan) || yeniKontenjan <= 0)
                return "Kontenjan pozitif bir tam sayı olmalıdır!";
            if (atama.Kontenjan == yeniKontenjan) return null;

            atama.Kontenjan = yeniKontenjan;

            var ders = _d.Dersler.FirstOrDefault(d =>
                d.BolumAdi.Equals(atama.BolumAdi, StringComparison.OrdinalIgnoreCase) &&
                d.DersAdi.Equals(atama.DersAdi, StringComparison.OrdinalIgnoreCase) &&
                d.SinifSeviyesi == atama.SinifSeviyesi &&
                d.Sube.Equals(atama.Sube, StringComparison.OrdinalIgnoreCase));
            if (ders != null) ders.Kontenjan = yeniKontenjan;

            _d.DurumYaz($"✏ '{atama.DersAdi}' (Şube {atama.Sube}) kontenjanı {yeniKontenjan} olarak güncellendi.", DurumTuru.Uyari);
            _d.Bildir();
            return null;
        }

        /// <summary>D hücresi: serbest not (şablon modeline dokunmaz; boş bırakılırsa silinir).</summary>
        public void NotDegistir(SablonSatiri satir, int gunIdx, string ham)
        {
            if (satir == null) return;
            ham = ham?.Trim();
            string anahtar = IntNotAnahtari(gunIdx, satir);
            if (string.IsNullOrEmpty(ham)) _d.SablonIntNotlari.Remove(anahtar);
            else _d.SablonIntNotlari[anahtar] = ham;
            _d.Bildir();
        }

        /// <summary>Delete tuşu: seçili ders hücrelerindeki dersleri şablondan çıkarır; çıkarılan yerleşimleri döner (geri almak için).</summary>
        public List<SinavAtama> HucreleriBosalt(IEnumerable<(SablonSatiri Satir, int GunIdx)> hucreler)
        {
            var cikarilacaklar = new List<SinavAtama>();
            if (_d.SablonSonuclari == null) return cikarilacaklar;
            foreach (var (satir, gunIdx) in hucreler)
            {
                var grup = satir?.Hucreler.ElementAtOrDefault(gunIdx)?.Grup;
                if (grup != null) cikarilacaklar.AddRange(grup);
            }
            if (cikarilacaklar.Count == 0) return cikarilacaklar;

            _d.SablonSonuclari.RemoveAll(a => cikarilacaklar.Contains(a));
            _d.DurumYaz($"🗑 {cikarilacaklar.Count} şube yerleşimi şablondan çıkarıldı (Delete).", DurumTuru.Uyari);
            _d.Bildir();
            return cikarilacaklar;
        }

        /// <summary>Delete ile çıkarılan yerleşimleri geri koyar (bu arada şablon değiştiyse yalnızca eksik olanlar eklenir).</summary>
        public void GeriEkle(IReadOnlyCollection<SinavAtama> atamalar)
        {
            if (_d.SablonSonuclari == null || atamalar == null) return;
            var eklenecek = atamalar.Where(a => !_d.SablonSonuclari.Contains(a)).ToList();
            if (eklenecek.Count == 0) return;
            _d.SablonSonuclari.AddRange(eklenecek);
            _d.DurumYaz($"↩ {eklenecek.Count} şube yerleşimi geri alındı.", DurumTuru.Bilgi);
            _d.Bildir();
        }
    }
}
