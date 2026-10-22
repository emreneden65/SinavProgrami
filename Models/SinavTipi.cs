#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SinavProgrami.Models
{
    /// <summary>
    /// SINAV DÖNEMİ TİPİ — Vize / Final / Bütünleme / Tek Ders.
    /// ─────────────────────────────────────────────────────────────
    /// Sistem tarihsel olarak tek haftalık (7 günlük) vize düzenine göre kurulmuştu.
    /// Bu sınıf, dağıtım süresini TEK BİR PARAMETRE üzerinden yönetilebilir hale getirir:
    /// takvim doldurma, şablon Excel bant sayısı, algoritmanın yayılma kuralı ve
    /// çıktılardaki hafta ayrımı hep buradaki değerleri baz alır.
    /// </summary>
    public enum SinavDonemTipi
    {
        /// <summary>Vize Programı — varsayılan 7 sınav günü (hafta sonu dahil), gün sayısı serbest.</summary>
        Vize = 0,

        /// <summary>Final Programı — varsayılan 10 sınav günü (Pzt–Cum), gün sayısı serbest.</summary>
        Final = 1,

        /// <summary>Bütünleme Programı — varsayılan 5 sınav günü, hafta sonu dahil; hiçbir gün kısıtı yok.</summary>
        Butunleme = 2,

        /// <summary>Tek Ders Sınavları — varsayılan 1 sınav günü; hiçbir gün kısıtı yok.</summary>
        TekDers = 3
    }

    /// <summary>
    /// Sınav dönemi tipine bağlı ayarlar ve takvim yardımcıları.
    /// </summary>
    public static class SinavTipi
    {
        // ═══════════════════════════════════════════════════════════════
        // DÖNEM BAŞINA GÜN SAYISI VE HAFTA SONU KURALI
        // ───────────────────────────────────────────────────────────────
        //  VİZE  → 1 hafta / 7 gün. TÜM HAFTA sınav günüdür: Pazartesi–Pazar.
        //          (Eski standart 6 gündü — Pazar boş bırakılıyordu. Artık Pazar da
        //           sınav günü sayılır ve varsayılan takvim 7 gün üretir.)
        //  FİNAL → 2 hafta / 10 gün. HAFTA SONU (Cumartesi + Pazar) TAMAMEN BOŞ
        //          bırakılır; her hafta Pazartesi–Cuma 5 iş günüdür.
        //
        //  Örnek Final takvimi (kullanıcı örneği ile birebir):
        //      1. Hafta: 20–24 Temmuz (Pzt–Cum)
        //      2. Hafta: 27–31 Temmuz (Pzt–Cum)      → 25-26 Temmuz hafta sonu boş
        //
        //  7 / 10 yalnızca VARSAYILAN gün sayısıdır — kısıt değildir. Vize 10-15 gün
        //  sürebilir; kullanıcı "Takvimi Doldur" yanındaki kutudan gün sayısını seçer,
        //  takvimden elle gün de ekleyip çıkarabilir. Sınav türü gün sayısından tahmin
        //  edilmez (10 günlük bir vize Final sayılmaz).
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Vize'de bir haftaya sığan sınav günü (Pzt–Paz = 7 gün).</summary>
        public const int VizeGunSayisi = 7;

        /// <summary>Final'de bir haftaya sığan sınav günü (Pzt–Cum).</summary>
        public const int FinalHaftalikGun = 5;

        /// <summary>Final kaç hafta sürer.</summary>
        public const int FinalHaftaSayisi = 2;

        /// <summary>Vize: 7 gün. Final: 2 × 5 = 10 gün (hafta sonları boş).</summary>
        /// <summary>Bütünleme'nin varsayılan gün sayısı (art arda 5 gün, hafta sonu dahil).</summary>
        public const int ButunlemeGunSayisi = 5;

        /// <summary>Tek ders sınavları genelde tek günde yapılır.</summary>
        public const int TekDersGunSayisi = 1;

        public static int VarsayilanGunSayisi(SinavDonemTipi tip) => tip switch
        {
            SinavDonemTipi.Final => FinalHaftalikGun * FinalHaftaSayisi,
            SinavDonemTipi.Butunleme => ButunlemeGunSayisi,
            SinavDonemTipi.TekDers => TekDersGunSayisi,
            _ => VizeGunSayisi
        };

        /// <summary>
        /// Bu dönemde CUMARTESİ sınav günü sayılır mı?
        /// Vize, Bütünleme ve Tek Ders'te evet (hafta içi / sonu fark etmez); Final'de hayır (hafta sonu boş).
        /// </summary>
        public static bool CumartesiSinavGunuMu(SinavDonemTipi tip)
            => tip != SinavDonemTipi.Final;

        /// <summary>
        /// Bu dönemde PAZAR sınav günü sayılır mı?
        /// Vize, Bütünleme ve Tek Ders'te evet; Final'de hayır (hafta sonu boş).
        /// </summary>
        public static bool PazarSinavGunuMu(SinavDonemTipi tip)
            => tip != SinavDonemTipi.Final;

        /// <summary>Dönemin kapsadığı hafta sayısı (Vize 1, Final 2).</summary>
        public static int HaftaSayisi(SinavDonemTipi tip)
            => tip == SinavDonemTipi.Final ? FinalHaftaSayisi : 1;

        /// <summary>Arayüzde gösterilen ad. Süre yazılmaz — gün sayısı serbesttir.</summary>
        public static string Ad(SinavDonemTipi tip) => tip switch
        {
            SinavDonemTipi.Final => "Final Programı",
            SinavDonemTipi.Butunleme => "Bütünleme Programı",
            SinavDonemTipi.TekDers => "Tek Ders Sınavları",
            _ => "Vize Programı"
        };

        /// <summary>Takvim doldururken seçilebilecek en fazla sınav günü.</summary>
        public const int EnFazlaGunSayisi = 60;

        /// <summary>Kısa ad — çıktı başlıklarında kullanılır.</summary>
        public static string KisaAd(SinavDonemTipi tip) => tip switch
        {
            SinavDonemTipi.Final => "FİNAL",
            SinavDonemTipi.Butunleme => "BÜTÜNLEME",
            SinavDonemTipi.TekDers => "TEK DERS",
            _ => "VİZE"
        };

        /// <summary>
        /// Arayüz açılır listesini doldurmak için tüm seçenekler.
        /// Sıra enum değeriyle aynıdır — liste indeksi doğrudan tipe çevrilir.
        /// </summary>
        public static IEnumerable<SinavDonemTipi> Tumu()
        {
            yield return SinavDonemTipi.Vize;
            yield return SinavDonemTipi.Final;
            yield return SinavDonemTipi.Butunleme;
            yield return SinavDonemTipi.TekDers;
        }

        /// <summary>
        /// Başlangıç tarihinden itibaren <paramref name="gunSayisi"/> adet SINAV GÜNÜ üretir.
        /// <paramref name="cumartesiDahil"/> / <paramref name="pazarDahil"/> false ise
        /// ilgili hafta sonu günü atlanır (Final'de ikisi de false → yalnızca Pzt–Cum).
        /// </summary>
        public static List<DateTime> GunleriUret(DateTime baslangic, int gunSayisi,
                                                 bool cumartesiDahil, bool pazarDahil = false)
        {
            var gunler = new List<DateTime>();
            if (gunSayisi <= 0) return gunler;

            bool SinavGunuMu(DateTime x)
            {
                if (x.DayOfWeek == DayOfWeek.Sunday) return pazarDahil;
                if (x.DayOfWeek == DayOfWeek.Saturday) return cumartesiDahil;
                return true;
            }

            // Başlangıç tarihi sınav günü değilse (hafta sonu) ilk uygun güne kaydır
            DateTime d = baslangic.Date;
            int kaydirma = 0;
            while (!SinavGunuMu(d) && kaydirma++ < 7) d = d.AddDays(1);

            int guvenlik = 0;
            while (gunler.Count < gunSayisi && guvenlik++ < 400)
            {
                if (SinavGunuMu(d)) gunler.Add(d);
                d = d.AddDays(1);
            }
            return gunler;
        }

        /// <summary>
        /// Tip için varsayılan gün listesini üretir.
        ///  • Vize  → 7 gün, Pazartesi–Pazar (tüm hafta sınav günü)
        ///  • Final → 10 gün, 2 × (Pazartesi–Cuma) — HAFTA SONLARI BOŞ
        /// </summary>
        public static List<DateTime> GunleriUret(DateTime baslangic, SinavDonemTipi tip)
            => GunleriUret(baslangic, tip, VarsayilanGunSayisi(tip));

        /// <summary>
        /// Tipin hafta sonu kuralıyla İSTENEN SAYIDA gün üretir (vize/final süresi serbesttir:
        /// ör. 15 günlük vize → hafta sonları dahil 15 gün; 15 günlük final → 15 iş günü).
        /// </summary>
        public static List<DateTime> GunleriUret(DateTime baslangic, SinavDonemTipi tip, int gunSayisi)
            => GunleriUret(baslangic, gunSayisi, CumartesiSinavGunuMu(tip), PazarSinavGunuMu(tip));

        /// <summary>
        /// Bir tarihin içinde bulunduğu takvim haftasının PAZARTESİ gününü döndürür.
        /// Hafta ayrımı bu değere göre yapılır (Vize 7, Final 5 günlük olduğundan
        /// sabit blok sayısıyla bölmek doğru sonuç vermez).
        /// </summary>
        private static DateTime HaftaBasi(DateTime gun)
        {
            int fark = ((int)gun.DayOfWeek + 6) % 7;   // Pazartesi = 0, Pazar = 6
            return gun.Date.AddDays(-fark);
        }

        /// <summary>
        /// Bir günün, sınav takviminde kaçıncı HAFTAYA düştüğünü döndürür (1 tabanlı).
        /// Ayrım gerçek takvim haftasına göredir: Pazartesi–Pazar aynı haftadır.
        /// Örn. Final takvimi 20–24 ve 27–31 Temmuz ise → 20-24 → 1, 27-31 → 2.
        /// </summary>
        public static int HaftaNo(DateTime gun, IEnumerable<DateTime> siraliGunler)
        {
            if (siraliGunler == null) return 1;

            var haftaBaslari = siraliGunler
                .Select(g => HaftaBasi(g.Date))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            int idx = haftaBaslari.IndexOf(HaftaBasi(gun.Date));
            return idx < 0 ? 1 : idx + 1;
        }

        /// <summary>
        /// Takvimin kaç FARKLI haftaya yayıldığını hesaplar (en az 1).
        /// Vize (Pzt–Paz) tek haftaya sığar → 1. Final (2 × Pzt–Cum) → 2.
        /// </summary>
        public static int HaftaSayisiHesapla(IEnumerable<DateTime> gunler)
        {
            if (gunler == null) return 1;
            int n = gunler.Select(g => HaftaBasi(g.Date)).Distinct().Count();
            return Math.Max(1, n);
        }
    }
}
