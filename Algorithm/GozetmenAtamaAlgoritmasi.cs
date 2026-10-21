#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Algorithm
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════════════
    ///  GÖZETMEN ATAMA ALGORİTMASI
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    /// Tüm atamalar (Gün + Saat) bazlı SLOT'lar halinde yürütülür.
    /// Bir slot içindeki işlem hiyerarşisi:
    ///     önce BÖLÜM KODLU dersler (BIL101...), ardından ORTAK dersler.
    ///
    /// ── AĞIRLIK ──────────────────────────────────────────────────────────
    ///   Her gözetmenin hedef görev sayısı: Toplam Görev × Ağırlık / Toplam Ağırlık.
    ///   Örn. ağırlığı 1,0 olan 14 sınava giriyorsa 0,5 olan 7 sınava girer.
    ///   "Yük" her yerde ağırlıklıdır: Skor = Sınav Sayısı / Ağırlık.
    ///   Ağırlıklar eşitse bu, herkese eşit görev demektir.
    ///
    /// ── FAZ 1: KESİN KURALLAR (asla esnetilmez) ──────────────────────────
    ///   K1) TEK SALON, TEK GÖZETMEN
    ///       Aynı slotta aynı fiziksel salon birden fazla satırda görünüyorsa
    ///       salona YALNIZCA 1 gözetmen atanır; adı diğer satırlara kopyalanır
    ///       ve yük yalnızca 1 artar.
    ///   K2) ÇAKIŞMA KORUMASI
    ///       Bir gözetmen aynı slotta yalnızca 1 salonda bulunabilir. Görev
    ///       alan kişi o slotun aday havuzundan anında çıkarılır.
    ///
    /// ── FAZ 1-A: BÖLÜM KODLU DERSLER ─────────────────────────────────────
    ///   A1) Kesin öncelik: dersin KENDİ BÖLÜMÜNDEN müsait gözetmenler.
    ///   A2) Destek çağrısı: kendi bölümünden müsait kimse yoksa, o anki
    ///       AĞIRLIKLI ORTALAMA YÜKÜ EN DÜŞÜK bölümden destek çağrılır.
    ///   A3) Nihai seçim: En Düşük Skor → En Düşük Sınav Sayısı.
    ///
    /// ── FAZ 1-B: ORTAK DERSLER ───────────────────────────────────────────
    ///   B1) MUTLAK YÜK ÖNCELİĞİ: en düşük SKOR'a (ağırlıklı yük) sahip hocalar grubu.
    ///   B2) Sınıf-bölüm eşleşmesi: bu grup İÇİNDE dersin bölümünden biri
    ///       varsa o seçilir.
    ///   B3) ADALET KORUMASI: grupta o bölümden kimse yoksa bölüm eşleşmesi
    ///       ARANMAZ. Sırf kendi bölümü diye yüksek yüklü hoca ASLA seçilmez.
    ///   B4) T4 tercihi (esnek): sınav T4 binasındaysa, hâlâ eşit durumdaki
    ///       adaylar arasında Elektrik / Mekatronik hocası öne alınır.
    ///   B5) Eşitlik tie-breaker: bünyesinde en fazla akademisyen barındıran
    ///       bölümün hocası tercih edilir.
    ///   B6) Her atamadan sonra yükler güncellenir; sonraki ders yeni
    ///       sayılarla işlenir.
    ///
    /// ── FAZ 2: YÜK DENGELEME (SWAP) ──────────────────────────────────────
    ///   Tüm slotlar bittikten sonra çalışır. YALNIZCA ortak ders atamaları
    ///   yer değiştirebilir; bölüm kodlu derslere dokunulmaz.
    ///   Sapma = Sınav Sayısı − Hedef (ağırlığa göre). Bir görev, verenin sapması
    ///   alanınkinden 1'den FAZLA büyükse devredilir (devir dengeyi iyileştirir).
    ///   Tetikleyici: max(sapma) − min(sapma) > 1
    ///   (ağırlıklar eşitse bu, eski "max − min sınav sayısı >= 2" kuralının aynısıdır)
    ///     S1) Bölüm öncelikli aktarım  — en yüklü hocadan, fakirin bölümüne
    ///         ait bir ortak ders varsa o devredilir.
    ///     S2) Havuz içi çapraz kontrol — farkı >=2 olan DİĞER yüklü hocalar
    ///         taranır, bölüm uyuşan ilk ortak ders devredilir.
    ///     S3) Mutlak eşitleme          — bölüm şartı göz ardı edilir; herhangi
    ///         bir ortak ders devredilir.
    ///     S4) Bölüm kodlu ders devri   — YALNIZCA aynı bölümden bir hocaya.
    ///         Dersin kendi bölümünden gözetmen alma kuralı korunur.
    ///     S5) Zincirleme aktarım       — doğrudan devir çakışma/uyumsuzluk
    ///         nedeniyle mümkün değilse araya köprü hoca(lar) konur:
    ///             Fakir ← M1 ← ... ← Zengin   (köprülerin yükü değişmez)
    ///     S6) SON ÇARE (SonCareCaprazDevir) — hâlâ fark >1 ise bölüm kodlu bir
    ///         ders FARKLI bölümden bir hocaya devredilir. Bu, Faz 1-A2'deki
    ///         "destek çağrısı" ile aynı mantıktır ve yalnızca başka hiçbir
    ///         yol kalmadığında devreye girer.
    ///   Sapma farkı en fazla 1 olana kadar tekrar eder.
    ///
    ///   MUTLAK SINIR: K2 (çakışma koruması) Faz 2'de de esnetilmez. Fakir,
    ///   devredilecek görevin slotunda başka bir salonda görevliyse aktarım
    ///   yapılamaz. Fark yine de kapanamazsa nedenini söyleyen UYARI üretilir.
    /// ═══════════════════════════════════════════════════════════════════════
    /// </summary>
    public class GozetmenAtamaAlgoritmasi
    {
        /// <summary>Faz 2 hedefi: hocaların hedeften sapmaları arasındaki en büyük fark.</summary>
        private const int HedefMaksimumFark = 1;

        /// <summary>Ağırlıklı yükler kesirli olduğundan karşılaştırmalardaki yuvarlama payı.</summary>
        private const double Eps = 1e-9;

        /// <summary>Faz 2: gözetmen → ağırlığına göre olması gereken görev sayısı (kesirli olabilir).</summary>
        private Dictionary<Gozetmen, double> _hedefler;

        /// <summary>
        /// S5 zincirleme aktarımda izin verilen en fazla köprü sayısı.
        /// 4 pratikte fazlasıyla yeterli; daha büyük değer yalnızca süreyi uzatır.
        /// </summary>
        private const int MaksZincirDerinligi = 4;

        /// <summary>
        /// S6 — SON ÇARE: fark hâlâ >1 ise bölüm kodlu bir dersin FARKLI bölümden
        /// bir hocaya devredilmesine izin verilir mi?
        ///
        /// true  (varsayılan): yük eşitliği önceliklidir. Bölüm kodlu dersler
        ///        yalnızca başka hiçbir yol kalmadığında bölüm dışına çıkar —
        ///        Faz 1-A2'deki "destek çağrısı" ile aynı mantık.
        /// false : "bölüm dersine yalnızca kendi bölümünden gözetmen" kuralı
        ///        mutlaktır; kadro dengesizliğinden doğan fark kapatılamayabilir.
        ///
        /// Kaç kez devreye girdiği <see cref="CaprazDestekDevri"/> ile izlenir.
        /// </summary>
        public bool SonCareCaprazDevir { get; set; } = true;

        /// <summary>Ortak ders adları (normalize) — Faz 1-B ve Faz 2 dengeleme havuzu.</summary>
        private HashSet<string> _ortakDersNormSet;

        /// <summary>Bölüm kısaltması → o bölümdeki akademisyen sayısı (B5 tie-breaker).</summary>
        private Dictionary<string, int> _bolumMevcudu = new Dictionary<string, int>();

        /// <summary>Atama sırasında oluşan uyarılar.</summary>
        public List<string> Uyarilar { get; } = new List<string>();

        /// <summary>Toplam yapılan salon-gözetmen ataması sayısı.</summary>
        public int ToplamAtama { get; private set; }

        /// <summary>Faz 2'de gerçekleştirilen toplam görev aktarımı sayısı.</summary>
        public int ToplamTakas { get; private set; }

        /// <summary>Bölüm kodlu derste kendi bölümü dışından yapılan destek ataması sayısı.</summary>
        public int DestekAtamaSayisi { get; private set; }

        /// <summary>
        /// S6 (son çare) kapsamında bölüm dışına devredilen bölüm kodlu ders sayısı.
        /// 0 ise "bölüm dersine kendi bölümünden gözetmen" kuralı hiç esnetilmemiştir.
        /// </summary>
        public int CaprazDestekDevri { get; private set; }

        // ═══════════════════════════════════════════════════════════════════
        //  GİRİŞ NOKTASI
        // ═══════════════════════════════════════════════════════════════════

        /// <param name="sinavAtamalari">Aşama 1-2 çıktısı: gün/saat/salon atanmış sınavlar.</param>
        /// <param name="gozetmenler">Arayüzdeki güncel gözetmen listesi.</param>
        /// <param name="ortakDersAdlari">Ortak ders adları (manuel seçim).
        /// null → aynı ders adının ≥2 bölümde geçmesiyle otomatik tespit.</param>
        public void GozetmenleriAta(List<SinavAtama> sinavAtamalari, List<Gozetmen> gozetmenler,
                                    IEnumerable<string> ortakDersAdlari = null)
        {
            Uyarilar.Clear();
            ToplamAtama = 0;
            ToplamTakas = 0;
            DestekAtamaSayisi = 0;
            CaprazDestekDevri = 0;

            if (sinavAtamalari == null || sinavAtamalari.Count == 0)
                throw new ArgumentException("Gözetmen ataması için önce sınav programı oluşturulmalıdır.");
            if (gozetmenler == null || gozetmenler.Count == 0)
                throw new ArgumentException("Gözetmen listesi boş — önce Excel'den gözetmen yükleyin.");

            OrtakDersSetiniKur(sinavAtamalari, ortakDersAdlari);
            BolumMevcutlariniKur(gozetmenler);

            // Önceki atamaları sıfırla (algoritma tekrar çalıştırılabilir olsun)
            foreach (var g in gozetmenler)
                g.AtananSinavSayisi = 0;
            foreach (var sinav in sinavAtamalari)
                foreach (var salon in sinav.AtananSalonlar ?? new List<SalonAtama>())
                    salon.GozetmenAdSoyad = "";

            Faz1_IlkDagitim(sinavAtamalari, gozetmenler);
            Faz2_YukDengeleme(sinavAtamalari, gozetmenler);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  FAZ 1 — İLK DAĞITIM (SLOT BAZLI)
        // ═══════════════════════════════════════════════════════════════════

        private void Faz1_IlkDagitim(List<SinavAtama> sinavAtamalari, List<Gozetmen> gozetmenler)
        {
            var slotGruplari = sinavAtamalari
                .GroupBy(s => new { s.Gun, s.Saat })
                .OrderBy(g => g.Key.Gun)
                .ThenBy(g => g.Key.Saat)
                .ToList();

            foreach (var slot in slotGruplari)
            {
                // K2 — ÇAKIŞMA KORUMASI: bu slotta görev alanlar havuzdan düşer
                var mesgul = new HashSet<Gozetmen>();

                // K1 — TEK SALON, TEK GÖZETMEN: salon kodu → atanan gözetmen
                var salonGozetmeni = new Dictionary<string, Gozetmen>(StringComparer.OrdinalIgnoreCase);

                // İŞLEM HİYERARŞİSİ: önce bölüm kodlu dersler, sonra ortak dersler
                var slotSinavlari = slot
                    .OrderBy(s => OrtakDersMi(s.DersAdi) ? 1 : 0)
                    .ThenBy(s => s.BolumAdi)
                    .ThenBy(s => s.DersAdi)
                    .ThenBy(s => s.Sube)
                    .ToList();

                foreach (var sinav in slotSinavlari)
                {
                    if (sinav.AtananSalonlar == null) continue;

                    foreach (var salon in sinav.AtananSalonlar)
                    {
                        // ── K1: salon bu slotta zaten atanmışsa aynı kişi yazılır ──
                        // Yeni atama YOK, sayaç ARTMAZ (yük yalnızca 1 kez sayılır).
                        if (!string.IsNullOrWhiteSpace(salon.SalonKodu) &&
                            salonGozetmeni.TryGetValue(salon.SalonKodu, out var mevcut))
                        {
                            salon.GozetmenAdSoyad = mevcut.AdSoyad;
                            continue;
                        }

                        // ── K2: bu slotta müsait olanlar ──
                        var musaitler = gozetmenler.Where(g => !mesgul.Contains(g)).ToList();

                        bool ortak = OrtakDersMi(sinav.DersAdi);
                        Gozetmen secilen = ortak
                            ? OrtakDersIcinSec(musaitler, sinav.BolumAdi, salon.SalonKodu)
                            : BolumDersiIcinSec(musaitler, sinav.BolumAdi, gozetmenler);

                        if (secilen == null)
                        {
                            Uyarilar.Add(
                                $"⚠ {sinav.Gun:dd.MM.yyyy} {sinav.Saat} — '{sinav.DersAdi} ({sinav.Sube})' / {salon.SalonKodu}: " +
                                "Bu slotta müsait gözetmen kalmadı, salon gözetmensiz bırakıldı.");
                            continue;
                        }

                        salon.GozetmenAdSoyad = secilen.AdSoyad;
                        secilen.AtananSinavSayisi++;   // B6 — yük anında güncellenir
                        mesgul.Add(secilen);
                        ToplamAtama++;

                        if (!ortak && !BolumEslesiyorMu(secilen.Bolum, sinav.BolumAdi))
                            DestekAtamaSayisi++;

                        if (!string.IsNullOrWhiteSpace(salon.SalonKodu))
                            salonGozetmeni[salon.SalonKodu] = secilen;
                    }
                }
            }
        }

        // ───────────────────────────────────────────────────────────────────
        //  FAZ 1-A — BÖLÜM KODLU DERS SEÇİMİ
        // ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// A1) Önce dersin kendi bölümünden müsait gözetmenler.
        /// A2) Kendi bölümünden kimse yoksa, ORTALAMA YÜKÜ EN DÜŞÜK bölümden destek.
        /// A3) Havuz içinde: En Düşük Skor → En Düşük Sınav Sayısı → alfabetik.
        /// </summary>
        private Gozetmen BolumDersiIcinSec(List<Gozetmen> musaitler, string sinavBolumu,
                                           List<Gozetmen> tumGozetmenler)
        {
            if (musaitler.Count == 0) return null;

            // A1 — kesin öncelik: dersin kendi bölümü
            var havuz = musaitler.Where(g => BolumEslesiyorMu(g.Bolum, sinavBolumu)).ToList();

            // A2 — destek çağrısı: ortalama yükü en düşük bölüm
            if (havuz.Count == 0)
            {
                var enHafifBolum = musaitler
                    .GroupBy(g => BolumKisaltmasi(BolumNormalizeEt(g.Bolum)))
                    .OrderBy(gr => BolumOrtalamaYuk(gr.Key, tumGozetmenler))
                    .ThenBy(gr => gr.Key, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                havuz = enHafifBolum != null ? enHafifBolum.ToList() : musaitler;
            }

            // A3 — nihai seçim
            return havuz
                .OrderBy(g => g.Skor)                 // ağırlıklı yük
                .ThenBy(g => g.AtananSinavSayisi)
                .ThenBy(g => g.AdSoyad, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>
        /// Bir bölümün o anki ağırlıklı ortalama yükü (bölümün TÜM kadrosu üzerinden):
        /// toplam sınav / toplam ağırlık.
        /// </summary>
        private static double BolumOrtalamaYuk(string bolumKisa, List<Gozetmen> tumGozetmenler)
        {
            var uyeler = tumGozetmenler
                .Where(g => BolumKisaltmasi(BolumNormalizeEt(g.Bolum)) == bolumKisa)
                .ToList();
            return uyeler.Count == 0 ? double.MaxValue
                                     : uyeler.Sum(g => g.AtananSinavSayisi) / uyeler.Sum(EtkinAgirlik);
        }

        /// <summary>Gözetmenin geçerli ağırlığı (0 veya negatifse 1 — <see cref="Gozetmen.Skor"/> ile aynı kural).</summary>
        private static double EtkinAgirlik(Gozetmen g) => g.Agirlik <= 0 ? 1.0 : g.Agirlik;

        // ───────────────────────────────────────────────────────────────────
        //  FAZ 1-B — ORTAK DERS SEÇİMİ
        // ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// B1) MUTLAK YÜK ÖNCELİĞİ — en düşük ağırlıklı yüke (skor) sahip hocalar grubu.
        ///     Bu grubun DIŞINA hiçbir koşulda çıkılmaz (B3 adalet koruması).
        /// B2) Grup içinde dersin bölümünden biri varsa o tercih edilir.
        /// B3) Grupta o bölümden kimse yoksa bölüm eşleşmesi aranmaz.
        /// B4) T4 tercihi — sınav T4'teyse Elektrik/Mekatronik hocası öne alınır.
        /// B5) Tie-breaker — en kalabalık bölümün hocası.
        /// </summary>
        private Gozetmen OrtakDersIcinSec(List<Gozetmen> musaitler, string sinavBolumu, string salonKodu)
        {
            if (musaitler.Count == 0) return null;

            // ── B1: MUTLAK YÜK ÖNCELİĞİ (ağırlıklı yük: sınav sayısı / ağırlık) ──
            double enAzSkor = musaitler.Min(g => g.Skor);
            var dusukYukluGrup = musaitler.Where(g => g.Skor <= enAzSkor + Eps).ToList();

            // ── B2 / B3: bölüm eşleşmesi YALNIZCA bu grup içinde aranır ──
            var bolumdenOlanlar = dusukYukluGrup
                .Where(g => BolumEslesiyorMu(g.Bolum, sinavBolumu))
                .ToList();

            // Grupta dersin bölümünden kimse yoksa ADALET KORUMASI devreye girer:
            // grubun dışına çıkılmaz, bölüm şartı düşürülür.
            var adaylar = bolumdenOlanlar.Count > 0 ? bolumdenOlanlar : dusukYukluGrup;

            bool t4Salonu = T4SalonuMu(salonKodu);

            return adaylar
                // B4 — T4 tercihi (esnek): yük eşitliğini ve bölüm eşleşmesini bozmaz,
                //      yalnızca hâlâ eşit durumdaki adaylar arasında sıra değiştirir.
                .OrderByDescending(g => t4Salonu && ElmMekGozetmeniMi(g.Bolum))
                // B5 — en kalabalık bölümün hocası
                .ThenByDescending(g => BolumMevcudu(g.Bolum))
                .ThenBy(g => g.AdSoyad, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  FAZ 2 — YÜK DENGELEME (SWAP)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Devredilebilen en küçük görev birimi.
        /// ÖNEMLİ: Aynı slotta aynı salonu paylaşan BİRDEN FAZLA ders satırı olabilir
        /// (K1). Bunlar fiziksel olarak tek görevdir ve yük yalnızca 1 sayılmıştır.
        /// Bu yüzden aktarım satır satır değil, "slot + salon" birimi üzerinden yapılır;
        /// birime bağlı TÜM satırlar birlikte el değiştirir.
        /// </summary>
        private sealed class GorevBirimi
        {
            public string SlotAnahtari;
            public string SalonKodu;
            public List<SalonAtama> Satirlar = new List<SalonAtama>();

            /// <summary>Ortak ders birimi mi?</summary>
            public bool Ortak;

            /// <summary>Dersin bölümü — bölüm uyumu aranan aşamalarda kullanılır.</summary>
            public string DersBolumu;
        }

        /// <summary>Bir görevin kime, hangi koşulla devredilebileceğini belirleyen mod.</summary>
        private enum AktarimModu
        {
            /// <summary>S1+S2 — ortak ders, alıcının KENDİ BÖLÜMÜNE ait.</summary>
            OrtakBolumUyumlu,

            /// <summary>S3 — ortak ders, bölüm şartı yok (mutlak eşitleme).</summary>
            OrtakSerbest,

            /// <summary>S4 — bölüm kodlu ders, YALNIZCA aynı bölümden hocaya.</summary>
            BolumKodluAyniBolum,

            /// <summary>S6 — bölüm kodlu ders, farklı bölümden hocaya (son çare / destek).</summary>
            BolumKodluDestek,

            /// <summary>S5 zinciri — S1..S4 ile aynı kısıtlar.</summary>
            ZincirKati,

            /// <summary>S6 zinciri — tüm kısıtlar serbest (yalnızca son çare turunda).</summary>
            ZincirGevsek
        }

        // Faz 2 çalışma haritaları
        private Dictionary<string, List<GorevBirimi>> _gorevler;
        private Dictionary<string, HashSet<string>> _doluSlotlar;

        private void Faz2_YukDengeleme(List<SinavAtama> sinavAtamalari, List<Gozetmen> gozetmenler)
        {
            GorevHaritasiniKur(sinavAtamalari);

            // Ağırlığa göre hedef görev sayıları — devirler toplamı değiştirmez, bir kez hesaplanır
            int toplamGorev = gozetmenler.Sum(g => g.AtananSinavSayisi);
            double toplamAgirlik = gozetmenler.Sum(EtkinAgirlik);
            _hedefler = gozetmenler.ToDictionary(g => g, g => toplamGorev * EtkinAgirlik(g) / toplamAgirlik);

            int adimSiniri = gozetmenler.Count * sinavAtamalari.Count + 2000;

            // ── TUR 1: Spec'in asıl aşamaları (S1 → S2 → S3 → S4) + katı zincir (S5) ──
            DengelemeDongusu(gozetmenler, adimSiniri,
                new[] { AktarimModu.OrtakBolumUyumlu,
                        AktarimModu.OrtakSerbest,
                        AktarimModu.BolumKodluAyniBolum },
                AktarimModu.ZincirKati);

            // ── TUR 2: SON ÇARE (S6) — yalnızca hâlâ fark >1 kaldıysa ──
            if (SonCareCaprazDevir)
                DengelemeDongusu(gozetmenler, adimSiniri,
                    new[] { AktarimModu.BolumKodluDestek },
                    AktarimModu.ZincirGevsek);

            DengesizlikUyarisiUret(gozetmenler);
        }

        /// <summary>Sonuçlardan "slot + salon" görev birimlerini ve doluluk haritasını çıkarır.</summary>
        private void GorevHaritasiniKur(List<SinavAtama> sinavAtamalari)
        {
            _gorevler = new Dictionary<string, List<GorevBirimi>>(StringComparer.OrdinalIgnoreCase);
            _doluSlotlar = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var birimHaritasi = new Dictionary<string, GorevBirimi>(StringComparer.OrdinalIgnoreCase);

            foreach (var sinav in sinavAtamalari)
            {
                if (sinav.AtananSalonlar == null) continue;
                string slotAnahtari = SlotAnahtariUret(sinav.Gun, sinav.Saat);
                bool ortakDers = OrtakDersMi(sinav.DersAdi);

                foreach (var satir in sinav.AtananSalonlar)
                {
                    if (string.IsNullOrWhiteSpace(satir.GozetmenAdSoyad)) continue;

                    string birimAnahtari = string.IsNullOrWhiteSpace(satir.SalonKodu)
                        ? $"{slotAnahtari}|#{Guid.NewGuid():N}"
                        : $"{slotAnahtari}|{satir.SalonKodu}";

                    if (!birimHaritasi.TryGetValue(birimAnahtari, out var birim))
                    {
                        birim = new GorevBirimi
                        {
                            SlotAnahtari = slotAnahtari,
                            SalonKodu = satir.SalonKodu,
                            DersBolumu = sinav.BolumAdi,
                            Ortak = ortakDers
                        };
                        birimHaritasi[birimAnahtari] = birim;
                        GorevListesi(satir.GozetmenAdSoyad).Add(birim);
                        SlotKumesi(satir.GozetmenAdSoyad).Add(slotAnahtari);
                    }

                    birim.Satirlar.Add(satir);
                    // Aynı salonu paylaşan satırlardan HERHANGİ biri ortak dersse birim ortaktır
                    birim.Ortak |= ortakDers;
                }
            }
        }

        /// <summary>
        /// Ana dengeleme döngüsü. Fark 1'e inene veya yapılabilecek aktarım kalmayana
        /// kadar; her turda önce DOĞRUDAN aktarım modları, olmazsa ZİNCİRLEME aktarım
        /// denenir.
        /// </summary>
        private void DengelemeDongusu(List<Gozetmen> gozetmenler, int adimSiniri,
                                      AktarimModu[] dogrudanModlar, AktarimModu zincirModu)
        {
            while (adimSiniri-- > 0)
            {
                double enAz = gozetmenler.Min(Sapma);
                double enCok = gozetmenler.Max(Sapma);
                if (enCok - enAz <= HedefMaksimumFark + Eps) return;   // hedefe ulaşıldı

                var fakirler = gozetmenler
                    .Where(g => enCok - Sapma(g) > HedefMaksimumFark + Eps)
                    .OrderBy(Sapma)
                    .ThenBy(g => g.AdSoyad, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                bool aktarimYapildi = false;

                foreach (var fakir in fakirler)
                {
                    var zenginler = gozetmenler
                        .Where(g => !ReferenceEquals(g, fakir) && DevirFaydali(g, fakir))
                        .OrderByDescending(Sapma)
                        .ThenBy(g => g.AdSoyad, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (zenginler.Count == 0) continue;

                    // Doğrudan aktarım — modlar sırayla denenir
                    foreach (var mod in dogrudanModlar)
                    {
                        if (DogrudanAktarimDene(fakir, zenginler, mod)) { aktarimYapildi = true; break; }
                    }
                    if (aktarimYapildi) break;

                    // Doğrudan olmadıysa ZİNCİRLEME (çok adımlı) aktarım
                    if (ZincirlemeAktarimDene(fakir, gozetmenler, zincirModu))
                    {
                        aktarimYapildi = true;
                        break;
                    }
                }

                if (!aktarimYapildi) return;   // yapılabilecek aktarım kalmadı
            }
        }

        /// <summary>Gözetmenin ağırlığına göre hedeften sapması: + fazla görev, − eksik görev.</summary>
        private double Sapma(Gozetmen g) => g.AtananSinavSayisi - _hedefler[g];

        /// <summary>
        /// Verenden alana tek görev devri dengeyi iyileştirir mi? Sapma farkı 1'den büyükse
        /// devirden sonra ikisi de hedefine yaklaşır (ağırlıklar eşitse: sayı farkı >= 2).
        /// </summary>
        private bool DevirFaydali(Gozetmen veren, Gozetmen alan)
            => Sapma(veren) - Sapma(alan) > HedefMaksimumFark + Eps;

        /// <summary>Bir görev, verilen moda göre alıcıya devredilebilir mi?</summary>
        private bool UygunMu(Gozetmen alici, GorevBirimi birim, AktarimModu mod)
        {
            switch (mod)
            {
                case AktarimModu.OrtakBolumUyumlu:
                    return birim.Ortak && BolumEslesiyorMu(alici.Bolum, birim.DersBolumu);
                case AktarimModu.OrtakSerbest:
                    return birim.Ortak;
                case AktarimModu.BolumKodluAyniBolum:
                    return !birim.Ortak && BolumEslesiyorMu(alici.Bolum, birim.DersBolumu);
                case AktarimModu.BolumKodluDestek:
                    return !birim.Ortak;
                case AktarimModu.ZincirKati:
                    return birim.Ortak || BolumEslesiyorMu(alici.Bolum, birim.DersBolumu);
                case AktarimModu.ZincirGevsek:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// S1–S4 / S6 — DOĞRUDAN AKTARIM.
        /// Zenginleri en yüklüden başlayarak tarar; fakire devredilebilen ilk görevi aktarır.
        /// </summary>
        private bool DogrudanAktarimDene(Gozetmen fakir, List<Gozetmen> zenginler, AktarimModu mod)
        {
            var fakirSlotlari = SlotKumesi(fakir.AdSoyad);

            foreach (var zengin in zenginler)
            {
                // Aradaki fark önceki aktarımlarla kapanmış olabilir
                if (!DevirFaydali(zengin, fakir)) continue;
                if (!_gorevler.TryGetValue(zengin.AdSoyad, out var zenginGorevleri)) continue;

                foreach (var birim in zenginGorevleri.ToList())
                {
                    if (!UygunMu(fakir, birim, mod)) continue;

                    // K2 — ÇAKIŞMA KORUMASI (Faz 2'de de mutlak)
                    if (fakirSlotlari.Contains(birim.SlotAnahtari)) continue;

                    GoreviAktar(fakir, zengin, birim, mod);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// S5 — ZİNCİRLEME (ÇOK ADIMLI) AKTARIM.
        /// Zengin görevi doğrudan fakire veremiyorsa (çakışma veya bölüm uyumsuzluğu),
        /// araya bir veya birkaç "köprü" hoca konur:
        ///
        ///     Fakir ← M1 ← M2 ← ... ← Zengin
        ///
        /// Her köprü hoca bir görev alır ve bir görev verir; yükü DEĞİŞMEZ.
        /// Net etki: Zengin −1, Fakir +1. Aradaki tüm kurallar her adımda korunur.
        ///
        /// Yol BFS ile bulunur (en kısa zincir tercih edilir).
        /// </summary>
        private bool ZincirlemeAktarimDene(Gozetmen fakir, List<Gozetmen> gozetmenler, AktarimModu zincirModu)
        {
            // ad → (bu kişiden görevi ALACAK kişi, alınacak görev)
            var onceki = new Dictionary<string, ValueTuple<Gozetmen, GorevBirimi>?>(StringComparer.OrdinalIgnoreCase)
            {
                [fakir.AdSoyad] = null
            };

            var kuyruk = new Queue<ValueTuple<Gozetmen, int>>();
            kuyruk.Enqueue((fakir, 0));
            Gozetmen hedef = null;

            while (kuyruk.Count > 0 && hedef == null)
            {
                var (alici, derinlik) = kuyruk.Dequeue();
                if (derinlik >= MaksZincirDerinligi) continue;

                var aliciSlotlari = SlotKumesi(alici.AdSoyad);

                foreach (var veren in gozetmenler)
                {
                    if (ReferenceEquals(veren, alici)) continue;
                    if (onceki.ContainsKey(veren.AdSoyad)) continue;
                    if (!_gorevler.TryGetValue(veren.AdSoyad, out var verenGorevleri)) continue;

                    foreach (var birim in verenGorevleri)
                    {
                        if (!UygunMu(alici, birim, zincirModu)) continue;
                        if (aliciSlotlari.Contains(birim.SlotAnahtari)) continue;

                        onceki[veren.AdSoyad] = (alici, birim);

                        if (DevirFaydali(veren, fakir))
                            hedef = veren;                       // zincirin ucu bulundu
                        else
                            kuyruk.Enqueue((veren, derinlik + 1));
                        break;
                    }
                    if (hedef != null) break;
                }
            }

            if (hedef == null) return false;

            // Zinciri hedeften geriye sararak adımları çıkar
            var adimlar = new List<ValueTuple<Gozetmen, Gozetmen, GorevBirimi>>();
            var dugum = hedef;
            while (onceki[dugum.AdSoyad] != null)
            {
                var (alici, birim) = onceki[dugum.AdSoyad].Value;
                adimlar.Add((alici, dugum, birim));
                dugum = alici;
            }
            adimlar.Reverse();   // Fakir tarafından başlanır

            // Fakir tarafından uygulanır: her köprü önce verir, sonra alır →
            // slot'u boşalttığı için çakışma koruması hep sağlanır.
            foreach (var (alici, veren, birim) in adimlar)
                GoreviAktar(alici, veren, birim, zincirModu);

            return true;
        }

        /// <summary>
        /// Bir görev birimini verenden alıcıya taşır; sayaçları, görev listelerini
        /// ve doluluk haritasını günceller.
        /// K1 gereği birime bağlı TÜM satırlar birlikte el değiştirir.
        /// </summary>
        private void GoreviAktar(Gozetmen alici, Gozetmen veren, GorevBirimi birim, AktarimModu mod)
        {
            foreach (var satir in birim.Satirlar)
                satir.GozetmenAdSoyad = alici.AdSoyad;

            alici.AtananSinavSayisi++;
            veren.AtananSinavSayisi--;

            var verenGorevleri = GorevListesi(veren.AdSoyad);
            verenGorevleri.Remove(birim);
            GorevListesi(alici.AdSoyad).Add(birim);

            SlotKumesi(alici.AdSoyad).Add(birim.SlotAnahtari);
            if (!verenGorevleri.Any(b => b.SlotAnahtari == birim.SlotAnahtari))
                SlotKumesi(veren.AdSoyad).Remove(birim.SlotAnahtari);

            ToplamTakas++;

            // Bölüm kodlu bir ders farklı bölümden bir hocaya geçtiyse bu bir
            // "destek" atamasıdır (Faz 1-A2 ile aynı mantık) — istatistikte sayılır.
            if (!birim.Ortak && !BolumEslesiyorMu(alici.Bolum, birim.DersBolumu))
            {
                DestekAtamaSayisi++;
                CaprazDestekDevri++;
            }
        }

        private List<GorevBirimi> GorevListesi(string ad)
        {
            if (!_gorevler.TryGetValue(ad, out var liste))
                _gorevler[ad] = liste = new List<GorevBirimi>();
            return liste;
        }

        private HashSet<string> SlotKumesi(string ad)
        {
            if (!_doluSlotlar.TryGetValue(ad, out var kume))
                _doluSlotlar[ad] = kume = new HashSet<string>();
            return kume;
        }

        /// <summary>
        /// Faz 2 bittiğinde hedeften sapma farkı hâlâ >1 kaldıysa nedenini teşhis eden uyarı üretir.
        /// </summary>
        private void DengesizlikUyarisiUret(List<Gozetmen> gozetmenler)
        {
            double enAz = gozetmenler.Min(Sapma);
            double enCok = gozetmenler.Max(Sapma);
            if (enCok - enAz <= HedefMaksimumFark + Eps) return;

            var fakirler = gozetmenler.Where(g => enCok - Sapma(g) > HedefMaksimumFark + Eps).ToList();
            var zenginler = gozetmenler.Where(g => Sapma(g) >= enCok - Eps).ToList();

            int cakismaEngeli = 0, devredilebilirVarMi = 0;
            foreach (var fakir in fakirler)
            {
                var fakirSlotlari = SlotKumesi(fakir.AdSoyad);
                foreach (var zengin in zenginler)
                {
                    if (!_gorevler.TryGetValue(zengin.AdSoyad, out var liste)) continue;
                    foreach (var birim in liste)
                    {
                        devredilebilirVarMi++;
                        if (fakirSlotlari.Contains(birim.SlotAnahtari)) cakismaEngeli++;
                    }
                }
            }

            string sebep = devredilebilirVarMi > 0 && cakismaEngeli == devredilebilirVarMi
                ? "ÇAKIŞMA KORUMASI engelledi — düşük yüklü gözetmen(ler), devredilebilecek " +
                  "tüm görevlerin slotlarında zaten başka bir salonda görevli."
                : !SonCareCaprazDevir
                    ? "Fark, bölüm kodlu derslerin bölüm dışına çıkamamasından kaynaklanıyor. " +
                      "SonCareCaprazDevir açılırsa kapatılabilir."
                    : "Devredilebilecek uygun görev bulunamadı (çakışma koruması ve kural kısıtları).";

            string Kisi(Gozetmen g) => $"{g.AdSoyad} ({g.AtananSinavSayisi} sınav, hedef {_hedefler[g]:0.#})";
            var az = gozetmenler.Where(g => Sapma(g) <= enAz + Eps).Select(Kisi).Take(5);
            var cok = zenginler.Select(Kisi).Take(5);

            Uyarilar.Add(
                $"⚠ YÜK DENGESİ: Ağırlığa göre hedefinin en üstündeki ile en altındaki gözetmen " +
                $"arasında {enCok - enAz:0.#} sınavlık fark kaldı. {sebep}\n" +
                $"   Hedefin üstünde: {string.Join(", ", cok)}\n" +
                $"   Hedefin altında: {string.Join(", ", az)}");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Gün + Saat ikilisini tek bir karşılaştırılabilir slot anahtarına çevirir.</summary>
        private static string SlotAnahtariUret(DateTime gun, string saat)
            => $"{gun:yyyy-MM-dd}|{saat?.Trim()}";

        /// <summary>Ortak ders setini kurar (manuel liste ya da otomatik tespit).</summary>
        private void OrtakDersSetiniKur(List<SinavAtama> sinavAtamalari, IEnumerable<string> ortakDersAdlari)
        {
            if (ortakDersAdlari != null)
            {
                _ortakDersNormSet = new HashSet<string>(ortakDersAdlari.Select(DersNormalizeEt));
                return;
            }

            // Otomatik: aynı ders adı ≥2 farklı bölümde geçiyorsa ortaktır
            var dersBolumleri = new Dictionary<string, HashSet<string>>();
            foreach (var s in sinavAtamalari)
            {
                string ad = DersNormalizeEt(s.DersAdi);
                if (!dersBolumleri.TryGetValue(ad, out var set))
                    dersBolumleri[ad] = set = new HashSet<string>();
                set.Add(BolumNormalizeEt(s.BolumAdi));
            }
            _ortakDersNormSet = new HashSet<string>(
                dersBolumleri.Where(kv => kv.Value.Count >= 2).Select(kv => kv.Key));
        }

        /// <summary>B5 tie-breaker için bölüm kadro sayılarını hesaplar.</summary>
        private void BolumMevcutlariniKur(List<Gozetmen> gozetmenler)
        {
            _bolumMevcudu = gozetmenler
                .GroupBy(g => BolumKisaltmasi(BolumNormalizeEt(g.Bolum)))
                .ToDictionary(gr => gr.Key, gr => gr.Count(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gözetmenin bölümündeki toplam akademisyen sayısı (B5).</summary>
        private int BolumMevcudu(string bolum)
            => _bolumMevcudu.TryGetValue(BolumKisaltmasi(BolumNormalizeEt(bolum)), out int n) ? n : 0;

        /// <summary>Ders, ortak (dengelemeye açık) ders mi?</summary>
        private bool OrtakDersMi(string dersAdi)
            => _ortakDersNormSet != null && _ortakDersNormSet.Contains(DersNormalizeEt(dersAdi));

        /// <summary>Salon T4 binasında mı? ("T4-207" → true)</summary>
        private static bool T4SalonuMu(string salonKodu)
            => (salonKodu ?? "").Trim().ToUpperInvariant().StartsWith("T4");

        /// <summary>Gözetmen Elektrik-Elektronik veya Mekatronik bölümünden mi? (B4)</summary>
        private static bool ElmMekGozetmeniMi(string gozetmenBolumu)
        {
            string k = BolumKisaltmasi(BolumNormalizeEt(gozetmenBolumu));
            return k == "EEM" || k == "MEK";
        }

        /// <summary>Ders adını normalize eder (yalnızca harf/rakam, büyük harf).</summary>
        private static string DersNormalizeEt(string ad)
        {
            if (string.IsNullOrEmpty(ad)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in ad.ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// Bölüm adlarını normalize ederek karşılaştırır.
        /// "Bilgisayar Mühendisliği" (gözetmen Excel'i) ile "BİL" / "BİLGİSAYAR
        /// MÜHENDİSLİĞİ BÖLÜMÜ" (ders listesi) eşleşir.
        /// </summary>
        private static bool BolumEslesiyorMu(string gozetmenBolumu, string sinavBolumu)
        {
            string g = BolumNormalizeEt(gozetmenBolumu);
            string s = BolumNormalizeEt(sinavBolumu);
            if (g.Length == 0 || s.Length == 0) return false;
            if (g == s) return true;

            // Salon dağıtımı sonrası sınavın bölümü KISALTMA gelir ("BİL"), gözetmen
            // listesinde tam ad vardır → karşılaştırma kısaltma düzeyine indirgenir.
            string gk = BolumKisaltmasi(g), sk = BolumKisaltmasi(s);
            return gk != "?" && gk == sk;
        }

        /// <summary>Normalize edilmiş bölüm adını standart kısaltmaya indirger.</summary>
        private static string BolumKisaltmasi(string norm)
        {
            if (string.IsNullOrEmpty(norm)) return "?";
            if (norm.Contains("bilg") || norm == "bil") return "BIL";
            if (norm.Contains("elektr") || norm == "eem" || norm == "elm") return "EEM";
            if (norm.Contains("insa") || norm == "ins") return "INS";
            if (norm.Contains("mekatron") || norm == "mek") return "MEK";
            if (norm.Contains("metalurj") || norm.Contains("malzeme") || norm == "mem") return "MEM";
            if (norm.Contains("makin") || norm == "mak") return "MAK";
            return "?";
        }

        /// <summary>
        /// Türkçe karakterleri sadeleştirir, yalnızca harfleri tutar (küçük harf),
        /// sondaki "bolumu"/"bolum" ekini atar.
        /// </summary>
        private static string BolumNormalizeEt(string bolum)
        {
            if (string.IsNullOrWhiteSpace(bolum)) return "";

            var sb = new System.Text.StringBuilder();
            foreach (char ch in bolum)
            {
                char c = ch switch
                {
                    'ı' or 'I' or 'İ' => 'i',
                    'ş' or 'Ş' => 's',
                    'ğ' or 'Ğ' => 'g',
                    'ç' or 'Ç' => 'c',
                    'ö' or 'Ö' => 'o',
                    'ü' or 'Ü' => 'u',
                    _ => ch
                };
                if (char.IsLetter(c))
                    sb.Append(char.ToLowerInvariant(c));
            }

            string sonuc = sb.ToString();
            if (sonuc.EndsWith("bolumu")) sonuc = sonuc.Substring(0, sonuc.Length - 6);
            else if (sonuc.EndsWith("bolum")) sonuc = sonuc.Substring(0, sonuc.Length - 5);
            return sonuc;
        }
    }
}
