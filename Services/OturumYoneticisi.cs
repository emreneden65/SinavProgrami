#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>
    /// OTURUM YÖNETİCİSİ — uygulama kapatılıp açılınca kaldığı yerden devam eder.
    /// ─────────────────────────────────────────────────────────────
    /// Eski arayüzdeki OturumuGeriYukle / OturumDurumunuTopla / OturumuKaydet mantığının
    /// arayüzden bağımsız hali. Dosya biçimi değişmedi (<see cref="OturumKaydi"/>): aynı
    /// oturum.json eski ve yeni arayüzde açılır. Çalışma durumu 15 sn'de bir (değiştiyse)
    /// ve kapanışta yazılır.
    /// </summary>
    public class OturumYoneticisi : IDisposable
    {
        public const int KayitAraligiMs = 15000;

        private readonly CalismaDurumu _durum;
        private readonly IsYurutucu _isYurutucu;
        private System.Windows.Forms.Timer _zamanlayici;
        private string _sonKaydedilen;
        private string _sonOtomatikKayitHatasi;

        public OturumYoneticisi(CalismaDurumu durum, IsYurutucu isYurutucu)
        {
            _durum = durum;
            _isYurutucu = isYurutucu;
        }

        /// <summary>Toplamadan hemen önce çağrılır — pencere kendi boyut bilgisini buradan günceller.</summary>
        public Action ToplamadanOnce { get; set; }

        /// <summary>Açılışta önceki oturum ekrana geri yüklendi mi?</summary>
        public bool GeriYuklendi { get; private set; }

        /// <summary>Açılışta gösterilecek uyarı (kayıt okunamadıysa); yoksa null.</summary>
        public string AcilisUyarisi { get; private set; }

        /// <summary>Diske en son yazılma zamanı (durum çubuğunda gösterilir).</summary>
        public DateTime? SonKayitZamani { get; private set; }

        /// <summary>Bir kayıt diske yazıldığında (durum çubuğu saati tazelensin).</summary>
        public event Action KayitYapildi;

        /// <summary>
        /// Kayıtlı oturumu çalışma durumuna geri yükler; kayıt yoksa (veya okunamadıysa) false döner
        /// ve çağıran varsayılan takvimi kurar. Eski arayüzle aynı: kayıt okunup uygulanırken hata
        /// çıkarsa dosya kenara yedeklenir ve true döner (kısmen yüklenen veri korunur).
        /// </summary>
        public bool GeriYukle()
        {
            var d = OturumKaydi.Oku(out string okumaHatasi);
            if (d == null)
            {
                if (okumaHatasi != null)
                    AcilisUyarisi = "Önceki oturum dosyası okunamadı, uygulama boş açılıyor.\n\n" +
                                    $"{okumaHatasi}\n\nDosya şuraya yedeklendi:\n{OturumKaydi.BozukYedekYolu}";
                return false;
            }

            try
            {
                DurumaUygula(d);
                _sonKaydedilen = OturumKaydi.Serilestir(Topla());
                SonKayitZamani = OturumKaydi.SonKayitZamani;
                GeriYuklendi = true;

                var zaman = OturumKaydi.SonKayitZamani;
                _durum.DurumYaz("↩ Önceki oturum geri yüklendi" +
                                (zaman.HasValue ? $" (son kayıt: {zaman:dd.MM.yyyy HH:mm})" : "") +
                                " — kaldığınız yerden devam edebilirsiniz.", DurumTuru.Basari);
            }
            catch (Exception ex)
            {
                try { File.Copy(OturumKaydi.DosyaYolu, OturumKaydi.BozukYedekYolu, overwrite: true); } catch { }
                HataGunlugu.Yaz("Oturum geri yükleme", ex);
                AcilisUyarisi = "Önceki oturum tam olarak geri yüklenemedi.\n\n" +
                                $"{ex.Message}\n\nKayıt dosyası şuraya yedeklendi:\n{OturumKaydi.BozukYedekYolu}";
            }
            _durum.Bildir();
            return true;
        }

        /// <summary>Kayıttaki veriyi çalışma durumuna yazar (eski OturumuGeriYukle'nin "bellek" ve "ekran" kısmı).</summary>
        private void DurumaUygula(OturumDurumu d)
        {
            _durum.Derslikler = (d.Derslikler ?? new List<Salon>()).ToList();
            _durum.Dersler.Clear();
            _durum.Dersler.AddRange(d.Dersler ?? new List<Ders>());

            _durum.TakvimSlotlari.Clear();
            foreach (var t in d.Takvim ?? new List<TakvimGunu>())
                _durum.TakvimSlotlari[t.Gun.Date] = t.Saatler ?? new List<string>();
            _durum.SinavTipi = d.SinavTipi;

            _durum.SablonHavuzlari.Clear();
            _durum.SablonHavuzlari.AddRange(d.Havuzlar ?? new List<DersHavuzu>());
            _durum.OrtakSecimYapildi = d.OrtakSecimYapildi;
            _durum.ManuelOrtakKodlar.Clear();
            _durum.ManuelOrtakKodlar.UnionWith(d.ManuelOrtakKodlar ?? new List<string>());
            _durum.ManuelOrtakDersAdlari.Clear();
            _durum.ManuelOrtakDersAdlari.UnionWith(d.ManuelOrtakDersAdlari ?? new List<string>());
            _durum.UygulamaliDersler.Clear();
            _durum.UygulamaliDersler.UnionWith(d.UygulamaliDersler ?? new List<string>());
            _durum.BolumHataOranlari.Clear();
            foreach (var kv in d.HataOranlari ?? new Dictionary<string, double>())
                _durum.BolumHataOranlari[kv.Key] = kv.Value;

            _durum.SablonSonuclari = d.SablonSonuclari;
            _durum.SablonIntNotlari.Clear();
            foreach (var kv in d.SablonIntNotlari ?? new Dictionary<string, string>())
                _durum.SablonIntNotlari[kv.Key] = kv.Value;
            _durum.GunSalonSatirlari = d.GunSalonSatirlari;
            _durum.SonSonuclar = d.SonSonuclar;

            _durum.GozetmenListesi.Clear();
            _durum.GozetmenListesi.AddRange(d.Gozetmenler ?? new List<Gozetmen>());

            // ── Eski arayüzde kontrollerde duran değerler ──
            // Sınav türü seçilince gün kutusuna o türün varsayılanı yazılırdı
            _durum.GunSayisi = SinavTipi.VarsayilanGunSayisi(d.SinavTipi);
            _durum.OgretimYili = d.OgretimYili;
            _durum.OgretimYiliMetni = d.OgretimYili.HasValue ? AkademikDonem.YilGorunen(d.OgretimYili.Value) : "";
            _durum.Yariyil = d.Yariyil;
            if (d.BaslangicTarihi.HasValue)
                _durum.BaslangicTarihi = d.BaslangicTarihi.Value;

            _durum.Kilitler.SozluktenOku(d.Butonlar);
            _durum.EskiArayuzSekmesi = d.SeciliSekme;
            _durum.EskiSonucPaneliAcik = d.SonucPaneliAcik;
            _durum.Pencere = new PencereBilgisi
            {
                TamEkran = d.PencereTamEkran,
                Genislik = d.PencereGenislik,
                Yukseklik = d.PencereYukseklik
            };

            _durum.SonSayfa = d.SonSayfa;
            _durum.SonDosyalar.Clear();
            foreach (var kv in d.SonDosyalar ?? new Dictionary<string, string>())
                _durum.SonDosyalar[kv.Key] = kv.Value;

            // Eski arayüzde uygulamalı ders listesi yenilenirken listede olmayanlar düşüyordu
            _durum.UygulamaliDersleriBuda();
        }

        /// <summary>Çalışma durumunun kaydedilecek anlık görüntüsü (eski OturumDurumunuTopla).</summary>
        public OturumDurumu Topla()
        {
            ToplamadanOnce?.Invoke();
            var p = _durum.Pencere ?? new PencereBilgisi();
            return new OturumDurumu
            {
                Derslikler = _durum.Derslikler.ToList(),
                Dersler = _durum.Dersler.ToList(),
                Takvim = _durum.TakvimSlotlari.OrderBy(kv => kv.Key)
                    .Select(kv => new TakvimGunu { Gun = kv.Key, Saatler = kv.Value.ToList() })
                    .ToList(),
                SinavTipi = _durum.SinavTipi,
                OgretimYili = _durum.OgretimYili,
                Yariyil = _durum.Yariyil,
                BaslangicTarihi = _durum.BaslangicTarihi.Date,
                Havuzlar = _durum.SablonHavuzlari.ToList(),
                OrtakSecimYapildi = _durum.OrtakSecimYapildi,
                ManuelOrtakKodlar = _durum.ManuelOrtakKodlar.ToList(),
                ManuelOrtakDersAdlari = _durum.ManuelOrtakDersAdlari.ToList(),
                UygulamaliDersler = _durum.UygulamaliDersler.ToList(),
                HataOranlari = new Dictionary<string, double>(_durum.BolumHataOranlari),
                SablonSonuclari = _durum.SablonSonuclari,
                SablonIntNotlari = new Dictionary<string, string>(_durum.SablonIntNotlari),
                GunSalonSatirlari = _durum.GunSalonSatirlari,
                SonSonuclar = _durum.SonSonuclar,
                Gozetmenler = _durum.GozetmenListesi.ToList(),
                Butonlar = _durum.Kilitler.SozlugeCevir(),
                SeciliSekme = _durum.EskiArayuzSekmesi,
                SonucPaneliAcik = _durum.EskiSonucPaneliAcik,
                PencereTamEkran = p.TamEkran,
                PencereGenislik = p.Genislik,
                PencereYukseklik = p.Yukseklik,
                SonSayfa = _durum.SonSayfa,
                SonDosyalar = new Dictionary<string, string>(_durum.SonDosyalar)
            };
        }

        /// <summary>Durum son kayıttan farklıysa diske yazar; başarısızsa hata mesajını döner.</summary>
        public bool Kaydet(out string hata)
        {
            hata = null;
            try
            {
                string json = OturumKaydi.Serilestir(Topla());
                if (json == _sonKaydedilen) return true;
                OturumKaydi.Yaz(json);
                _sonKaydedilen = json;
                SonKayitZamani = DateTime.Now;
                KayitYapildi?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz("Oturum kaydı", ex);
                hata = ex.Message;
                return false;
            }
        }

        /// <summary>15 sn'de bir otomatik kayıt (arayüz iş parçacığında; uzun işlem sürerken bekler).</summary>
        public void OtomatikKaydiBaslat()
        {
            _zamanlayici?.Dispose();
            _zamanlayici = new System.Windows.Forms.Timer { Interval = KayitAraligiMs };
            _zamanlayici.Tick += (s, e) =>
            {
                if (_isYurutucu.Mesgul) return;
                if (Kaydet(out string hata))
                {
                    _sonOtomatikKayitHatasi = null;
                }
                else if (hata != _sonOtomatikKayitHatasi)
                {
                    // Aynı hata her 15 sn'de bir yazılmasın
                    _sonOtomatikKayitHatasi = hata;
                    _durum.DurumYaz($"⚠ Otomatik kayıt yapılamadı: {hata}", DurumTuru.Uyari);
                    _durum.Bildir();
                }
            };
            _zamanlayici.Start();
        }

        public void OtomatikKaydiDurdur() => _zamanlayici?.Stop();

        public void Dispose() => _zamanlayici?.Dispose();
    }
}
