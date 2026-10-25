#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Bir iş akışı adımının görünen durumu.</summary>
    public enum AdimDurumTuru
    {
        /// <summary>Adımın işi bitti (✓).</summary>
        Tamam,

        /// <summary>Sıradaki yapılacak adım (●).</summary>
        Siradaki,

        /// <summary>Henüz sırası gelmedi ama açılabilir.</summary>
        Bekliyor,

        /// <summary>Önceki bir adım bitmeden kullanılamaz (🔒).</summary>
        Kilitli
    }

    /// <summary>Sol menüdeki bir iş akışı adımı.</summary>
    public sealed class AdimTanimi
    {
        public int No { get; init; }
        public string Ad { get; init; }

        /// <summary>Sayfa adresi ("donem", "derslikler" …).</summary>
        public string Rota { get; init; }

        /// <summary>Sayfa başlığının altındaki kısa açıklama: bu adımda ne yapılır.</summary>
        public string Aciklama { get; init; }
    }

    public sealed class AdimDurumBilgisi
    {
        public AdimDurumTuru Tur { get; init; }

        /// <summary>Kartta / ipucunda görünen tek satırlık özet ("48 derslik · kapasite 2.430").</summary>
        public string Ozet { get; init; }

        /// <summary>Kilitliyse nedeni ("Önce gözetmen atamasını yapın").</summary>
        public string Neden { get; init; }
    }

    /// <summary>Genel Bakış'taki "eksikler" listesinin bir maddesi.</summary>
    public sealed class EksikMadde
    {
        public int AdimNo { get; init; }
        public string Metin { get; init; }
    }

    /// <summary>
    /// İŞ AKIŞI — 7 adımın tanımı ve çalışma durumundan hesaplanan durumları.
    /// Adımların sırası eski arayüzdeki gruplarla aynıdır (Derslikler → Sınav listesi ve
    /// şablon → Salon ataması → Gözetmen ataması → Kontrol ve final); öğretim dönemi ve
    /// takvim, şablondan önce gerektiği için en başa alındı.
    /// </summary>
    public static class IsAkisi
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        public static readonly IReadOnlyList<AdimTanimi> Adimlar = new[]
        {
            new AdimTanimi { No = 1, Ad = "Dönem ve Takvim", Rota = "donem",
                Aciklama = "Öğretim yılını, dönemi ve sınav türünü seçin; sınav günlerini ve saatlerini belirleyin." },
            new AdimTanimi { No = 2, Ad = "Derslikler", Rota = "derslikler",
                Aciklama = "Okulunuzun derslik listesini Excel'den yükleyin. Bir kez yüklemek yeterli; kayıtta saklanır." },
            new AdimTanimi { No = 3, Ad = "Dersler", Rota = "dersler",
                Aciklama = "Fakülteden gelen sınav listesini yükleyin; uygulamalı dersleri, hata oranlarını, ortak dersleri ve havuzları ayarlayın." },
            new AdimTanimi { No = 4, Ad = "Şablon", Rota = "sablon",
                Aciklama = "Dersleri günlere ve saatlere otomatik dağıtın, Excel'de kontrol edip düzeltin ve geri yükleyin." },
            new AdimTanimi { No = 5, Ad = "Salonlar", Rota = "salonlar",
                Aciklama = "Şablondaki sınavlara salonları atayın, Excel'de kontrol edip geri yükleyin." },
            new AdimTanimi { No = 6, Ad = "Gözetmenler", Rota = "gozetmenler",
                Aciklama = "Gözetmen listesini yükleyin ya da düzenleyin ve salonlara gözetmen atayın." },
            new AdimTanimi { No = 7, Ad = "Final", Rota = "final",
                Aciklama = "Gözetmenli taslağı kontrol edip geri yükleyin ve nihai sınav programını oluşturun." },
        };

        public static AdimTanimi Adim(int no) => Adimlar.FirstOrDefault(a => a.No == no);

        /// <summary>Adımın işi bitti mi? (durum yalnızca çalışma verisinden hesaplanır)</summary>
        public static bool TamamMi(CalismaDurumu d, int no) => no switch
        {
            1 => d.OgretimYili.HasValue && d.Yariyil.HasValue && d.TakvimSlotlari.Count > 0,
            2 => d.Derslikler.Count > 0,
            3 => d.Dersler.Count > 0,
            4 => d.SablonSonuclari != null,
            // Salonlu Excel geri yüklenince gözetmen ataması açılır
            5 => d.SonSonuclar != null && d.Kilitler.GozetmenAta,
            // Gözetmenler atanınca taslak çıkarma açılır
            6 => d.SonSonuclar != null && d.Kilitler.MatrisExport,
            7 => d.SonDosyalar.ContainsKey(SonDosyaTurleri.Final),
            _ => false
        };

        /// <summary>Kilitli adımın nedeni; kilitli değilse null.</summary>
        public static string KilitNedeni(CalismaDurumu d, int no) => no switch
        {
            7 when !d.Kilitler.MatrisExport => "Önce 6. adımda gözetmenleri atayın.",
            _ => null
        };

        /// <summary>Sıradaki adım: işi bitmemiş ilk adım; hepsi bittiyse 0.</summary>
        public static int SiradakiAdim(CalismaDurumu d)
            => Adimlar.Select(a => a.No).FirstOrDefault(no => !TamamMi(d, no));

        public static AdimDurumBilgisi Durum(CalismaDurumu d, int no)
        {
            string neden = KilitNedeni(d, no);
            AdimDurumTuru tur = TamamMi(d, no) ? AdimDurumTuru.Tamam
                : neden != null ? AdimDurumTuru.Kilitli
                : SiradakiAdim(d) == no ? AdimDurumTuru.Siradaki
                : AdimDurumTuru.Bekliyor;
            return new AdimDurumBilgisi { Tur = tur, Ozet = Ozet(d, no), Neden = neden };
        }

        /// <summary>Adım kartındaki tek satırlık özet.</summary>
        public static string Ozet(CalismaDurumu d, int no)
        {
            switch (no)
            {
                case 1:
                {
                    string donem = d.OgretimYili.HasValue && d.Yariyil.HasValue
                        ? $"{AkademikDonem.YilGorunen(d.OgretimYili.Value)} {AkademikDonem.YariyilAdi(d.Yariyil.Value)}"
                        : "Öğretim dönemi seçilmedi";
                    string gun = d.TakvimSlotlari.Count == 0 ? "sınav günü yok" : $"{d.TakvimSlotlari.Count} sınav günü";
                    return $"{donem} · {SinavTipi.Ad(d.SinavTipi)} · {gun}";
                }
                case 2:
                {
                    if (d.Derslikler.Count == 0) return "Derslik listesi yüklenmedi";
                    int uyg = d.Derslikler.Count(s => s.UygulamaliMi);
                    return $"{d.Derslikler.Count} derslik ({d.Derslikler.Count - uyg} normal, {uyg} uygulamalı) · " +
                           $"kapasite {Sayi(d.Derslikler.Sum(s => s.Kapasite))}";
                }
                case 3:
                {
                    if (d.Dersler.Count == 0) return "Sınav listesi yüklenmedi";
                    // Ders kodu şablondan yüklenen listede boş olabilir; dersler adlarından sayılır
                    int ders = d.Dersler.Select(x => (x.DersAdi ?? "").Trim()).Where(x => x.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    int bolum = d.Dersler.Select(x => x.BolumAdi).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    return $"{Sayi(d.Dersler.Count)} şube · {Sayi(ders)} ders · {bolum} bölüm";
                }
                case 4:
                    return d.SablonSonuclari == null
                        ? "Şablon henüz geri yüklenmedi"
                        : $"{Sayi(d.SablonSonuclari.Count)} şube yerleşimi";
                case 5:
                    if (d.SonSonuclar == null) return "Salon ataması yapılmadı";
                    return d.Kilitler.GozetmenAta
                        ? $"{Sayi(d.SonSonuclar.Count)} sınav salona atandı"
                        : $"{Sayi(d.SonSonuclar.Count)} sınav salona atandı · Excel kontrolü bekliyor";
                case 6:
                {
                    string liste = d.GozetmenListesi.Count == 0 ? "Gözetmen listesi yok" : $"{d.GozetmenListesi.Count} gözetmen";
                    return d.Kilitler.MatrisExport ? $"{liste} · atama yapıldı" : liste;
                }
                case 7:
                    return d.SonDosyalar.ContainsKey(SonDosyaTurleri.Final) ? "Final programı oluşturuldu"
                        : d.Kilitler.FinalExport ? "Taslak geri yüklendi · final bekliyor"
                        : d.Kilitler.GozetmenliYukle ? "Taslak çıkarıldı · geri yükleme bekliyor"
                        : "Bekliyor";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Şablon oluşturmak için gerekenlerden eksik olanlar (1–3. adımlar) ve sıradaki
        /// adımın kendi eksiği. Genel Bakış'ta "neler eksik" listesi olarak gösterilir.
        /// </summary>
        public static List<EksikMadde> Eksikler(CalismaDurumu d)
        {
            var liste = new List<EksikMadde>();
            if (d.OgretimYili == null) liste.Add(new EksikMadde { AdimNo = 1, Metin = "Öğretim yılı seçilmedi" });
            if (d.Yariyil == null) liste.Add(new EksikMadde { AdimNo = 1, Metin = "Dönem (Güz / Bahar) seçilmedi" });
            if (d.TakvimSlotlari.Count == 0) liste.Add(new EksikMadde { AdimNo = 1, Metin = "Takvimde sınav günü yok" });
            if (d.Derslikler.Count == 0) liste.Add(new EksikMadde { AdimNo = 2, Metin = "Derslik listesi yüklenmedi" });
            if (d.Dersler.Count == 0) liste.Add(new EksikMadde { AdimNo = 3, Metin = "Sınav listesi yüklenmedi" });

            int siradaki = SiradakiAdim(d);
            if (siradaki >= 4 && liste.Count == 0)
                liste.Add(new EksikMadde { AdimNo = siradaki, Metin = SiradakiIs(d, siradaki) });
            return liste;
        }

        /// <summary>Sıradaki adımda yapılacak somut iş (Genel Bakış'taki "Sıradaki adım" kartı).</summary>
        public static string SiradakiIs(CalismaDurumu d, int no) => no switch
        {
            1 => "Öğretim yılını, dönemi ve sınav günlerini belirleyin.",
            2 => "Derslik listesini Excel'den yükleyin.",
            3 => "Sınav listesini Excel'den yükleyin.",
            4 => d.Kilitler.OtomatikDagit
                ? "Otomatik şablonu oluşturun, Excel'de kontrol edip geri yükleyin."
                : "Şablonu Excel'den geri yükleyin.",
            5 => d.SonSonuclar == null ? "Salonları atayın."
                : d.Kilitler.SalonExport ? "Salonlu Excel'i çıkarıp kontrol edin ve geri yükleyin."
                : "Salonlu Excel'i geri yükleyin.",
            6 => d.GozetmenListesi.Count == 0 ? "Gözetmen listesini yükleyin ve gözetmenleri atayın." : "Gözetmenleri atayın.",
            7 => d.Kilitler.FinalExport ? "Final programını çıkarın."
                : d.Kilitler.GozetmenliYukle ? "Düzenlediğiniz gözetmenli taslağı geri yükleyin."
                : "Gözetmenli taslağı çıkarıp kontrol edin.",
            _ => "Tüm adımlar tamamlandı."
        };

        private static string Sayi(int n) => n.ToString("N0", Tr);
    }

    /// <summary><see cref="CalismaDurumu.SonDosyalar"/> anahtarları — son üretilen / yüklenen Excel türleri.</summary>
    public static class SonDosyaTurleri
    {
        public const string DerslikListesi = "DerslikListesi";
        public const string SinavListesi = "SinavListesi";
        public const string OtomatikSablon = "OtomatikSablon";
        public const string SalonluExcel = "SalonluExcel";
        public const string GozetmenListesi = "GozetmenListesi";
        public const string GozetmenliTaslak = "GozetmenliTaslak";
        public const string Final = "Final";
    }
}
