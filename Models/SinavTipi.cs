#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SinavProgrami.Models
{
    /// <summary>
    /// SINAV DÖNEMİ TİPİ — Vize (1 hafta) / Final (2 hafta).
    /// ─────────────────────────────────────────────────────────────
    /// Sistem tarihsel olarak tek haftalık (7 günlük) vize düzenine göre kurulmuştu.
    /// Bu sınıf, dağıtım süresini TEK BİR PARAMETRE üzerinden yönetilebilir hale getirir:
    /// takvim doldurma, şablon Excel bant sayısı, algoritmanın yayılma kuralı ve
    /// çıktılardaki hafta ayrımı hep buradaki değerleri baz alır.
    /// </summary>
    public enum SinavDonemTipi
    {
        /// <summary>Vize Programı — 1 hafta, 7 sınav günü (Pazartesi–Pazar).</summary>
        Vize = 0,

        /// <summary>Final Programı — 2 hafta, 12 sınav günü.</summary>
        Final = 1
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
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Vize'de bir haftaya sığan sınav günü (Pzt–Paz = 7 gün).</summary>
        public const int VizeGunSayisi = 7;

        /// <summary>Final'de bir haftaya sığan sınav günü (Pzt–Cum).</summary>
        public const int FinalHaftalikGun = 5;

        /// <summary>Final kaç hafta sürer.</summary>
        public const int FinalHaftaSayisi = 2;

        /// <summary>Vize: 7 gün. Final: 2 × 5 = 10 gün (hafta sonları boş).</summary>
        public static int VarsayilanGunSayisi(SinavDonemTipi tip)
            => tip == SinavDonemTipi.Final ? FinalHaftalikGun * FinalHaftaSayisi : VizeGunSayisi;

        /// <summary>
        /// Bu dönemde CUMARTESİ sınav günü sayılır mı?
        /// Vize'de evet, Final'de hayır (hafta sonu boş).
        /// </summary>
        public static bool CumartesiSinavGunuMu(SinavDonemTipi tip)
            => tip != SinavDonemTipi.Final;

        /// <summary>
        /// Bu dönemde PAZAR sınav günü sayılır mı?
        /// Vize'de evet (7 güne ulaşmak için), Final'de hayır (hafta sonu boş).
        /// </summary>
        public static bool PazarSinavGunuMu(SinavDonemTipi tip)
            => tip != SinavDonemTipi.Final;

        /// <summary>Dönemin kapsadığı hafta sayısı (Vize 1, Final 2).</summary>
        public static int HaftaSayisi(SinavDonemTipi tip)
            => tip == SinavDonemTipi.Final ? FinalHaftaSayisi : 1;

        /// <summary>Arayüzde gösterilen ad.</summary>
        public static string Ad(SinavDonemTipi tip)
            => tip == SinavDonemTipi.Final ? "Final Programı (2 Hafta)" : "Vize Programı (1 Hafta)";

        /// <summary>Kısa ad — çıktı başlıklarında kullanılır.</summary>
        public static string KisaAd(SinavDonemTipi tip)
            => tip == SinavDonemTipi.Final ? "FİNAL" : "VİZE";

        /// <summary>Arayüz açılır listesini doldurmak için tüm seçenekler.</summary>
        public static IEnumerable<SinavDonemTipi> Tumu()
        {
            yield return SinavDonemTipi.Vize;
            yield return SinavDonemTipi.Final;
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
            => GunleriUret(baslangic, VarsayilanGunSayisi(tip),
                           CumartesiSinavGunuMu(tip), PazarSinavGunuMu(tip));

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

        /// <summary>
        /// Takvime bakarak dönem tipini tahmin eder (Excel'den okunan takvimler için).
        /// Gün sayısı Vize'yi (7) aşıyorsa → Final.
        /// NOT: Vize artık 7 gün olduğu için hafta ortasında başlayan bir vize takvimi
        /// iki takvim haftasına taşabilir; bu yüzden yalnızca GÜN SAYISI'na bakılır.
        /// </summary>
        public static SinavDonemTipi TahminEt(IEnumerable<DateTime> gunler)
        {
            if (gunler == null) return SinavDonemTipi.Vize;
            var liste = gunler.Select(g => g.Date).Distinct().ToList();
            if (liste.Count == 0) return SinavDonemTipi.Vize;

            return liste.Count > VizeGunSayisi
                ? SinavDonemTipi.Final
                : SinavDonemTipi.Vize;
        }
    }
}
