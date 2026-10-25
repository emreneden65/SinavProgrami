#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Algorithm;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Durum çubuğu mesajının türü — renk ve simge buna göre seçilir.</summary>
    public enum DurumTuru
    {
        Bilgi,
        Basari,
        Uyari,
        Hata
    }

    /// <summary>Durum çubuğunda gösterilen tek mesaj (eski arayüzdeki lblDurum satırı).</summary>
    public sealed class DurumMesaji
    {
        public string Metin { get; init; }
        public DurumTuru Tur { get; init; }
        public DateTime Zaman { get; init; }
    }

    /// <summary>Pencerenin son durumu — oturum kaydında saklanır, açılışta geri yüklenir.</summary>
    public sealed class PencereBilgisi
    {
        public bool TamEkran { get; set; }

        /// <summary>Son normal (tam ekran olmayan) boyut; 0 = bilinmiyor.</summary>
        public int Genislik { get; set; }
        public int Yukseklik { get; set; }
    }

    /// <summary>
    /// ADIM KİLİTLERİ — eski arayüzde Enabled durumu oturumla saklanan 7 adım butonu.
    /// Oturum dosyasındaki anahtarlar eski buton adlarıdır (btnSalonAta …); böylece aynı
    /// oturum.json hem eski hem yeni arayüzde aynı anlamla okunur.
    /// </summary>
    public sealed class AdimKilitleri
    {
        /// <summary>Otomatik Şablon — sınav listesi yüklenince açılır.</summary>
        public bool OtomatikDagit { get; set; }

        /// <summary>Salonları Ata — şablon geri yüklenince açılır.</summary>
        public bool SalonAta { get; set; }

        /// <summary>Salonlu Excel Çıkart — salonlar atanınca açılır.</summary>
        public bool SalonExport { get; set; }

        /// <summary>Gözetmen Ata — salonlu Excel geri yüklenince açılır.</summary>
        public bool GozetmenAta { get; set; }

        /// <summary>Gözetmenli Taslak Çıkart — gözetmenler atanınca açılır.</summary>
        public bool MatrisExport { get; set; }

        /// <summary>Taslağı Geri Yükle — taslak çıkarılınca açılır.</summary>
        public bool GozetmenliYukle { get; set; }

        /// <summary>Final Programı Çıkart — düzenlenmiş taslak geri yüklenince açılır.</summary>
        public bool FinalExport { get; set; }

        /// <summary>Oturum dosyasındaki "Butonlar" sözlüğü — sıra eski arayüzdeki AdimButonlari ile aynıdır.</summary>
        public Dictionary<string, bool> SozlugeCevir() => new Dictionary<string, bool>
        {
            ["btnOtomatikDagit"] = OtomatikDagit,
            ["btnSalonAta"] = SalonAta,
            ["btnSalonExport"] = SalonExport,
            ["btnGozetmenAta"] = GozetmenAta,
            ["btnMatrisExport"] = MatrisExport,
            ["btnGozetmenliYukle"] = GozetmenliYukle,
            ["btnFinalExport"] = FinalExport
        };

        /// <summary>Kayıtlı sözlükten okur; kayıtta olmayan anahtarlar mevcut değerini korur (eski arayüzle aynı).</summary>
        public void SozluktenOku(Dictionary<string, bool> butonlar)
        {
            if (butonlar == null) return;
            if (butonlar.TryGetValue("btnOtomatikDagit", out bool v)) OtomatikDagit = v;
            if (butonlar.TryGetValue("btnSalonAta", out v)) SalonAta = v;
            if (butonlar.TryGetValue("btnSalonExport", out v)) SalonExport = v;
            if (butonlar.TryGetValue("btnGozetmenAta", out v)) GozetmenAta = v;
            if (butonlar.TryGetValue("btnMatrisExport", out v)) MatrisExport = v;
            if (butonlar.TryGetValue("btnGozetmenliYukle", out v)) GozetmenliYukle = v;
            if (butonlar.TryGetValue("btnFinalExport", out v)) FinalExport = v;
        }

        public void HepsiniKapat()
        {
            OtomatikDagit = SalonAta = SalonExport = GozetmenAta = MatrisExport = GozetmenliYukle = FinalExport = false;
        }
    }

    /// <summary>
    /// ÇALIŞMA DURUMU — yeni arayüzün tek veri kaynağı.
    /// ─────────────────────────────────────────────────────────────
    /// Eski arayüzde MainForm'un alanlarında duran bütün çalışma verisi (ders listesi,
    /// takvim, şablon, salon/gözetmen atamaları, ayarlar, adım kilitleri) burada tutulur.
    /// Servisler bu nesneyi değiştirir ve <see cref="Bildir"/> ile haber verir; sayfalar
    /// <see cref="Degisti"/> olayına abone olup yeniden çizilir.
    /// Tüm erişim arayüz iş parçacığında yapılır (uzun işlemler için bkz. IsYurutucu).
    /// </summary>
    public class CalismaDurumu
    {
        // ── Sınav listesi ve ayarları ──
        /// <summary>Yüklenen sınav listesi (her öğe bir şube) — eski _dersListesi.</summary>
        public List<Ders> Dersler { get; } = new List<Ders>();

        /// <summary>Kullanıcının yüklediği derslikler — salonların tek kaynağı (veritabanında tutulmaz).</summary>
        public List<Salon> Derslikler { get; set; } = new List<Salon>();

        /// <summary>Uygulamalı ders adları — yalnızca UYGULAMALI salonlara atanır.</summary>
        public HashSet<string> UygulamaliDersler { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Bölüm bazlı hata oranı (%) — sınava gelmeyecek kişi yüzdesi.</summary>
        public Dictionary<string, double> BolumHataOranlari { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Kullanıcı tanımlı şube havuzları — salon atamasında tek salonda birleştirilir.</summary>
        public List<DersHavuzu> SablonHavuzlari { get; } = new List<DersHavuzu>();

        /// <summary>Manuel ortak ders seçimi yapıldıysa otomatik tespit devre dışı kalır.</summary>
        public bool OrtakSecimYapildi { get; set; }
        public HashSet<string> ManuelOrtakKodlar { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ManuelOrtakDersAdlari { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Dönem ve takvim ──
        /// <summary>Sınav günü → o günün saatleri ("09:00" …).</summary>
        public Dictionary<DateTime, List<string>> TakvimSlotlari { get; } = new Dictionary<DateTime, List<string>>();
        public SinavDonemTipi SinavTipi { get; set; } = SinavDonemTipi.Vize;

        /// <summary>Öğretim yılının başladığı yıl (2026 → 2026/2027); null = seçilmedi.</summary>
        public int? OgretimYili { get; set; }

        /// <summary>Öğretim yılı kutusuna yazılan metin — geçersiz yazımın uyarısı için tutulur (kaydedilmez).</summary>
        public string OgretimYiliMetni { get; set; } = "";
        public Yariyil? Yariyil { get; set; }

        /// <summary>"Takvimi Doldur"un başlangıç tarihi.</summary>
        public DateTime BaslangicTarihi { get; set; } = DateTime.Today.AddDays(1);

        /// <summary>"Takvimi Doldur"un gün sayısı — sınav türü seçilince varsayılanı yazılır (kaydedilmez).</summary>
        public int GunSayisi { get; set; } = Models.SinavTipi.VarsayilanGunSayisi(SinavDonemTipi.Vize);

        // ── Şablon, salon ve gözetmen aşamaları ──
        /// <summary>Salonsuz takvim yerleşimi (null = şablon henüz yüklenmedi).</summary>
        public List<SinavAtama> SablonSonuclari { get; set; }

        /// <summary>Şablonun D (eski INT) sütunundaki serbest notlar.</summary>
        public Dictionary<string, string> SablonIntNotlari { get; } = new Dictionary<string, string>();

        /// <summary>'Salonları Ata' çıktısı — 'Salonlu Excel Çıkart' bunu kullanır (null = atama yok).</summary>
        public List<GunlukSalonDagitici.GunSatir> GunSalonSatirlari { get; set; }

        /// <summary>Salon (ve varsa gözetmen) atanmış sınavlar (null = henüz yok).</summary>
        public List<SinavAtama> SonSonuclar { get; set; }

        /// <summary>Gözetmen (araştırma görevlisi) listesi.</summary>
        public List<Gozetmen> GozetmenListesi { get; } = new List<Gozetmen>();

        public AdimKilitleri Kilitler { get; } = new AdimKilitleri();

        // ── Arayüz durumu (oturumla saklanır) ──
        /// <summary>Son üretilen / yüklenen Excel dosyaları: tür → tam yol (Excel'de aç, geri yükle için).</summary>
        public Dictionary<string, string> SonDosyalar { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Kapanışta açık olan sayfa — açılışta oraya dönülür.</summary>
        public string SonSayfa { get; set; }

        public PencereBilgisi Pencere { get; set; } = new PencereBilgisi();

        /// <summary>
        /// Eski arayüzün kendi alanları (seçili sonuç sekmesi, sonuç penceresi açık mı).
        /// Yeni arayüz kullanmaz; eski arayüzle aynı oturum dosyası paylaşıldığı için olduğu gibi geri yazılır.
        /// </summary>
        public int EskiArayuzSekmesi { get; set; }
        public bool EskiSonucPaneliAcik { get; set; }

        // ── Durum mesajı ──
        private const int DurumGecmisiSiniri = 50;
        private readonly List<DurumMesaji> _durumGecmisi = new List<DurumMesaji>();

        public DurumMesaji Durum { get; private set; } = new DurumMesaji
        {
            Metin = "Henüz program oluşturulmadı.",
            Tur = DurumTuru.Bilgi,
            Zaman = DateTime.Now
        };

        /// <summary>Son mesajlar, en yenisi başta.</summary>
        public IReadOnlyList<DurumMesaji> DurumGecmisi => _durumGecmisi;

        /// <summary>Durum çubuğuna mesaj yazar (eski lblDurum.Text + ForeColor).</summary>
        public void DurumYaz(string metin, DurumTuru tur)
        {
            Durum = new DurumMesaji { Metin = metin, Tur = tur, Zaman = DateTime.Now };
            _durumGecmisi.Insert(0, Durum);
            if (_durumGecmisi.Count > DurumGecmisiSiniri)
                _durumGecmisi.RemoveRange(DurumGecmisiSiniri, _durumGecmisi.Count - DurumGecmisiSiniri);
        }

        // ── Değişim bildirimi ──
        /// <summary>Veri değişti — sayfalar yeniden çizilsin.</summary>
        public event Action Degisti;

        public void Bildir() => Degisti?.Invoke();

        // ── Ortak yardımcılar ──
        public bool DersliklerYukluMu => Derslikler.Count > 0;

        /// <summary>
        /// Ders listesinde artık bulunmayan dersleri uygulamalı seçiminden düşer
        /// (eski UygulamaliDersGridiniGuncelle: ders listesi her değiştiğinde çalışır;
        /// arama filtresi değil TAM liste esas alınır).
        /// </summary>
        public void UygulamaliDersleriBuda()
        {
            var dersler = Dersler
                .Select(d => d.DersAdi)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            UygulamaliDersler.RemoveWhere(a => !dersler.Contains(a, StringComparer.OrdinalIgnoreCase));
        }
    }
}
