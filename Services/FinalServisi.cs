#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Düzenlenmiş gözetmenli taslağın uygulanma sonucu.</summary>
    public sealed class TaslakYuklemeSonucu
    {
        public int Guncellenen { get; init; }
        public List<string> Uyarilar { get; init; }
    }

    /// <summary>
    /// FİNAL — gözetmenli taslak Excel'i, düzenlenmiş taslağı geri yükleme ve final programı
    /// (eski BtnMatrisExport_Click, BtnGozetmenliYukle_Click, BtnFinalExport_Click / MatrisDosyasiKaydet).
    /// </summary>
    public class FinalServisi
    {
        public const string TaslakOnEki = "SinavProgrami_GozetmenliTaslak";
        public const string FinalOnEki = "SinavProgrami_FINAL";

        private readonly CalismaDurumu _d;
        private readonly HocaEslemesi _hoca;
        private readonly TakvimServisi _takvim;

        public FinalServisi(CalismaDurumu durum, HocaEslemesi hoca, TakvimServisi takvim)
        {
            _d = durum;
            _hoca = hoca;
            _takvim = takvim;
        }

        /// <summary>Taslak / final Excel'i için eksik ön koşulun mesajı; yoksa null.</summary>
        public string MatrisExcelEksigi()
        {
            if (_d.SonSonuclar == null || _d.SonSonuclar.Count == 0)
                return "Dışa aktarılacak sonuç yok.\nÖnce şablonu geri yükleyip salonları atayın (4. ve 5. adımlar).";
            if (!_d.DersliklerYukluMu)
                return "Derslik listesi yüklenmemiş!\n\nÖnce '2 · Derslikler' sayfasından okulunuzun derslik Excel'ini yükleyin.";
            return _takvim.OgretimDonemiEksikMesaji();
        }

        public static string OnerilenAd(bool taslak) => $"{(taslak ? TaslakOnEki : FinalOnEki)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        /// <summary>Öğretim elemanlarını kalıcı eşlemeden tamamlar (arayüz iş parçacığında; eşleme dosyası da güncellenir).</summary>
        public void OgretimElemanlariniDoldur() => _hoca.Doldur(_d.SonSonuclar, _d.Dersler);

        /// <summary>
        /// Güncel sonuçlarla çok sayfalı matris Excel'ini yazar (gözetmen listesi Görev Çizelgesi için
        /// gönderilir). Çalışma durumuna yazmaz; arayüz kilitliyken arka planda çalışabilir.
        /// </summary>
        public void MatrisExceliYaz(string yol)
            => ExcelMatrisExportHelper.MatrisExcelOlustur(_d.SonSonuclar, _d.Derslikler, yol, _takvim.ProgramBasligi(), _d.GozetmenListesi);

        /// <summary>Excel yazıldıktan sonra: son dosya, durum ve (taslaksa) geri yükleme kilidi.</summary>
        public void MatrisExceliYazildi(string yol, bool taslak)
        {
            _d.SonDosyalar[taslak ? SonDosyaTurleri.GozetmenliTaslak : SonDosyaTurleri.Final] = yol;
            if (taslak) _d.Kilitler.GozetmenliYukle = true;
            _d.DurumYaz(taslak ? "✅ Gözetmenli taslak kaydedildi — düzenleyip 'Taslağı geri yükle'ye basın."
                               : "✅ Final sınav programı kaydedildi!", DurumTuru.Basari);
            _d.Bildir();
        }

        public string TaslakYuklemeEksigi()
            => _d.SonSonuclar == null || _d.SonSonuclar.Count == 0
                ? "Önce gözetmen atamasını yapıp gözetmenli taslağı çıkarın."
                : null;

        /// <summary>Düzenlenmiş taslağı okur; çalışma durumuna dokunmaz (arka planda çalışabilir).</summary>
        public static GozetmenliSablonOkuyucu.Sonuc TaslagiOku(string yol) => GozetmenliSablonOkuyucu.Oku(yol);

        /// <summary>
        /// Okunan taslağı sonuçlara uygular (öğrenci sayıları ve gözetmen adları); en az bir satır
        /// güncellendiyse final programı açılır.
        /// </summary>
        public TaslakYuklemeSonucu TaslagiUygula(GozetmenliSablonOkuyucu.Sonuc okuma, string yol)
        {
            int guncellenen = GozetmenliSablonOkuyucu.Uygula(_d.SonSonuclar, okuma);
            _d.Kilitler.FinalExport = guncellenen > 0;
            _d.SonDosyalar[SonDosyaTurleri.GozetmenliTaslak] = yol;

            _d.DurumYaz($"✅ {guncellenen} sınav satırı güncellendi" +
                        (okuma.Uyarilar.Count > 0 ? $", {okuma.Uyarilar.Count} uyarı" : "") +
                        (guncellenen > 0 ? " — 'Final programını çıkar'a basın." : ""),
                        okuma.Uyarilar.Count > 0 ? DurumTuru.Uyari : DurumTuru.Basari);
            _d.Bildir();
            return new TaslakYuklemeSonucu { Guncellenen = guncellenen, Uyarilar = okuma.Uyarilar };
        }
    }

    /// <summary>
    /// TEMİZLE / SIFIRLA (eski BtnTemizle_Click, BtnVeritabaniSifirla_Click). Düzeltme 3: "Temizle"
    /// adım kilitlerini ve salon atamasını da sıfırlar. Tema gibi tercihler (ayarlar.json) silinmez.
    /// </summary>
    public class TemizlemeServisi
    {
        private readonly CalismaDurumu _d;
        private readonly TakvimServisi _takvim;

        public TemizlemeServisi(CalismaDurumu durum, TakvimServisi takvim)
        {
            _d = durum;
            _takvim = takvim;
        }

        /// <summary>
        /// Ders listesi, şablon, salon ve gözetmen sonuçları, gözetmen listesi temizlenir; takvim
        /// varsayılana döner. Derslikler, öğretim dönemi, hata oranları, havuzlar ve ortak ders seçimi kalır.
        /// </summary>
        public void Temizle()
        {
            _d.Dersler.Clear();
            _d.SonSonuclar = null;
            _d.GozetmenListesi.Clear();
            _d.SablonSonuclari = null;
            _d.SablonIntNotlari.Clear();
            _d.GunSalonSatirlari = null;
            _d.Kilitler.HepsiniKapat();
            DosyaKayitlariniTemizle(derslikKalsin: true);
            _d.UygulamaliDersleriBuda();   // ders listesi boş: uygulamalı seçimi de boşalır (eski davranış)

            // Eskisi gibi: önce "henüz program yok", ardından varsayılan takvimin sınav türü durumu yazılır
            _d.DurumYaz("Henüz program oluşturulmadı.", DurumTuru.Bilgi);
            _takvim.VarsayilanTakvimiKur();
        }

        /// <summary>
        /// Uygulamayı ilk açılış durumuna döndürür: tüm çalışma verisi, derslikler ve ayarlar (öğretim
        /// dönemi, havuz, ortak ders, uygulamalı ders, hata oranı) temizlenir, oturum kaydı silinir.
        /// Veritabanındaki kayıtlı dersler ayrıca çağıran tarafta silinir.
        /// </summary>
        public void Sifirla()
        {
            Temizle();
            _d.SablonHavuzlari.Clear();
            _d.OrtakSecimYapildi = false;
            _d.ManuelOrtakKodlar.Clear();
            _d.ManuelOrtakDersAdlari.Clear();
            _d.UygulamaliDersler.Clear();
            _d.BolumHataOranlari.Clear();
            _d.Derslikler = new List<Salon>();
            _d.OgretimYili = null;
            _d.OgretimYiliMetni = null;
            _d.Yariyil = null;
            _d.SonDosyalar.Clear();
            OturumKaydi.Sil();

            _d.DurumYaz("✅ Uygulama sıfırlandı! Fabrika ayarlarına geri dönüldü.", DurumTuru.Basari);
            _d.Bildir();
        }

        private void DosyaKayitlariniTemizle(bool derslikKalsin)
        {
            foreach (var anahtar in _d.SonDosyalar.Keys.ToList())
                if (!(derslikKalsin && anahtar == SonDosyaTurleri.DerslikListesi))
                    _d.SonDosyalar.Remove(anahtar);
        }
    }
}
