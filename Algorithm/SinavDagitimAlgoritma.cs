#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Algorithm
{
    /// <summary>
    /// Sınav dağıtım algoritması — V8 "İki Aşamalı: Önce Takvim, Sonra Toptan Salon Dağıtımı".
    /// ─────────────────────────────────────────────
    /// AŞAMA 1: Tüm dersleri gün+saat slotlarına yerleştir (salon düşünmeden).
    /// AŞAMA 2: Her slot için TÜM salonlar müsait olarak başla, slottaki tüm derslere toptan dağıt.
    /// ─────────────────────────────────────────────
    /// Kurallar:
    ///   • Dinamik Ortak Ders Keşfi — farklı bölümlerde aynı adla geçen dersler otomatik bulunur.
    ///   • Ortak dersler aynı gün+saat slotuna çivilenir.
    ///   • Bölüm+Sınıf çakışma koruması — aynı bölüm+sınıf aynı slotta olamaz.
    ///   • Dinlenme molası — aynı bölümün sınavları arasında minimum boşluk.
    ///   • Dengeli gün dağıtımı — round-robin.
    ///   • Blok sabitleme — her şube tek blokta kalır (fallback: diğer bloklar).
    ///   • Dengeli salon bölme — 36+36 vs 65+7.
    ///   • Minimum artık eşiği (15) — küçük artıklar için yeni salon açılmaz.
    ///   • Overbooking — bölüm bazlı kapasite artırımı.
    /// </summary>
    public class SinavDagitimAlgoritma
    {
        private readonly List<Salon> _salonlar;
        private readonly List<Ders> _dersler;
        private readonly Dictionary<DateTime, List<string>> _takvimSlotlari;
        private readonly Dictionary<string, double> _bolumOverbooking;
        // Kullanıcı tanımlı ders havuzları — havuzdaki bölümlerin ortak dersi salon atamasında birleştirilir
        private readonly List<DersHavuzu> _dersHavuzlari;

        public int MinDinlenmeMolasi { get; set; } = 1;
        public List<string> Uyarilar { get; private set; } = new List<string>();

        // Slot → Bölüm|Sınıf setleri (çakışma kontrolü — geriye dönük)
        private readonly Dictionary<string, HashSet<string>> _slotBolumSinifMap = new();
        // KURAL 1 — Slot → (Bölüm → o slotta o bölüme atanmış NORMALİZE ders adları)
        // Bir bölümün bir slotta yalnızca TEK ders adı bulunabilir. Aynı dersin
        // farklı şubeleri/ortak ders paylaşımı çakışma sayılmaz; farklı ders adı çakışmadır.
        private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _slotBolumDersMap = new();
        // Slot → atanmış ders adları
        private readonly Dictionary<string, HashSet<string>> _slotDersMap = new();
        // Bölüm → atandığı slot indekslerinin listesi (spread kontrolü)
        private readonly Dictionary<string, List<int>> _bolumAtananSlotlar = new();
        // Birden fazla bölümde geçen ders adları (DİNAMİK KEŞİF)
        private readonly HashSet<string> _ortakDersler = new();
        // Uygulamalı dersler (normalize adlar) — sadece UYGULAMALI tipli salonlara atanır
        private readonly HashSet<string> _uygulamaliDersler = new();
        // Slot başına atanmış öğrenci yükü (salon tipi bazında kapasite ön-kontrolü)
        private readonly Dictionary<string, int> _slotNormalOgrenciYuku = new();
        private readonly Dictionary<string, int> _slotUygulamaliOgrenciYuku = new();
        private int _normalToplamKapasite;
        private int _uygulamaliToplamKapasite;
        // Tüm slotların sıralı dizini
        private readonly List<string> _tumSlotlar = new();
        // Gün başına atanan sınav sayısı
        private readonly Dictionary<string, int> _gunSinavSayisi = new();
        private readonly List<DateTime> _gunler = new();
        private readonly Dictionary<string, int> _gunSlotSayisi = new();
        private readonly Dictionary<string, int> _gunBaslangicIdx = new();

        public SinavDagitimAlgoritma(List<Salon> salonlar, List<Ders> dersler,
                                      Dictionary<DateTime, List<string>> takvimSlotlari,
                                      Dictionary<string, double> bolumOverbooking = null,
                                      List<DersHavuzu> dersHavuzlari = null)
        {
            _salonlar = salonlar ?? throw new ArgumentNullException(nameof(salonlar));
            _dersler = dersler ?? throw new ArgumentNullException(nameof(dersler));
            _takvimSlotlari = takvimSlotlari ?? throw new ArgumentNullException(nameof(takvimSlotlari));
            _bolumOverbooking = bolumOverbooking ?? new Dictionary<string, double>();
            _dersHavuzlari = dersHavuzlari ?? new List<DersHavuzu>();

            if (_salonlar.Count == 0)
                throw new ArgumentException("Salonlar listesi boş. Veritabanında salon kaydı olduğundan emin olun.");
            if (_dersler.Count == 0)
                throw new ArgumentException("Ders listesi boş. En az bir ders eklemelisiniz.");
            if (_takvimSlotlari.Count == 0)
                throw new ArgumentException("Takvim boş. En az bir sınav günü ve saati seçmelisiniz.");

            _gunler = _takvimSlotlari.Keys.OrderBy(d => d).ToList();
            int currentIdx = 0;
            foreach (var gun in _gunler)
            {
                string gunKey = gun.ToString("yyyy-MM-dd");
                var saatler = _takvimSlotlari[gun].OrderBy(s => s).ToList();
                _gunBaslangicIdx[gunKey] = currentIdx;
                _gunSlotSayisi[gunKey] = saatler.Count;
                _gunSinavSayisi[gunKey] = 0;
                foreach (var saat in saatler)
                    _tumSlotlar.Add($"{gunKey}|{saat}");
                currentIdx += saatler.Count;
            }

            // Salon tipi bazında toplam kapasiteler (Aşama 1 kapasite ön-kontrolü için)
            _uygulamaliToplamKapasite = _salonlar.Where(s => s.UygulamaliMi).Sum(s => s.Kapasite);
            _normalToplamKapasite = _salonlar.Where(s => !s.UygulamaliMi).Sum(s => s.Kapasite);

            OrtakDersleriTespitEt();
        }

        /// <summary>
        /// Uygulamalı ders adlarını ayarlar. Bu dersler SADECE 'UYGULAMALI' tipli
        /// salonlara (lab, teknik resim sınıfı) atanır; diğer dersler bu salonları kullanamaz.
        /// </summary>
        public void UygulamaliDersleriAyarla(IEnumerable<string> dersAdlari)
        {
            _uygulamaliDersler.Clear();
            if (dersAdlari == null) return;
            foreach (var ad in dersAdlari)
                if (!string.IsNullOrWhiteSpace(ad))
                    _uygulamaliDersler.Add(NormalizeDersAdi(ad));
        }

        private bool DersUygulamaliMi(string dersAdi)
            => _uygulamaliDersler.Contains(NormalizeDersAdi(dersAdi));

        /// <summary>
        /// Slot'a eklenmek istenen öğrenci yükü, o salon tipinin toplam
        /// kapasitesini aşıyor mu? (Aşama 1 kapasite ön-kontrolü)
        /// </summary>
        private bool SlotKapasiteYetersiz(string slotKey, bool uygulamali, int ogrenciSayisi)
        {
            // KURAL 3: 17:00 taşırma slotu LİMİTSİZDİR — kapasite kontrolü yapılmaz
            if (TasmaSlotuMu(slotKey)) return false;

            var yukMap = uygulamali ? _slotUygulamaliOgrenciYuku : _slotNormalOgrenciYuku;
            int kapasite = uygulamali ? _uygulamaliToplamKapasite : _normalToplamKapasite;
            int mevcut = yukMap.TryGetValue(slotKey, out int v) ? v : 0;
            return mevcut + ogrenciSayisi > kapasite;
        }

        /// <summary>Geriye dönük uyumluluk — eski constructor.</summary>
        public SinavDagitimAlgoritma(List<Salon> salonlar, List<Ders> dersler,
                                      DateTime baslangicTarihi, int gunSayisi, List<string> saatler)
            : this(salonlar, dersler, EskiParametreleriDonustur(baslangicTarihi, gunSayisi, saatler))
        { }

        private static Dictionary<DateTime, List<string>> EskiParametreleriDonustur(
            DateTime baslangicTarihi, int gunSayisi, List<string> saatler)
        {
            var takvim = new Dictionary<DateTime, List<string>>();
            var tarih = baslangicTarihi;
            int eklenen = 0;
            while (eklenen < gunSayisi)
            {
                if (tarih.DayOfWeek != DayOfWeek.Saturday && tarih.DayOfWeek != DayOfWeek.Sunday)
                {
                    takvim[tarih] = new List<string>(saatler);
                    eklenen++;
                }
                tarih = tarih.AddDays(1);
            }
            return takvim;
        }

        // ═══════════════════════════════════════════════════
        // DİNAMİK ORTAK DERS KEŞİF MOTORU
        // ═══════════════════════════════════════════════════

        private void OrtakDersleriTespitEt()
        {
            _ortakDersler.Clear();
            var dersGruplari = _dersler
                .GroupBy(d => NormalizeDersAdi(d.DersAdi))
                .Where(g => g.Select(d => d.BolumAdi.ToUpperInvariant().Trim()).Distinct().Count() > 1);

            foreach (var grup in dersGruplari)
                foreach (var d in grup)
                    _ortakDersler.Add(d.DersAdi.ToUpperInvariant().Trim());
        }

        private static string NormalizeDersAdi(string dersAdi)
        {
            if (string.IsNullOrWhiteSpace(dersAdi)) return "";
            var map = new Dictionary<char, char>
            {
                {'ı','i'},{'I','i'},{'İ','i'},{'i','i'},
                {'ş','s'},{'Ş','s'},{'ğ','g'},{'Ğ','g'},
                {'ç','c'},{'Ç','c'},{'ö','o'},{'Ö','o'},
                {'ü','u'},{'Ü','u'},{'â','a'},{'Â','a'},
                {'î','i'},{'Î','i'},{'û','u'},{'Û','u'}
            };
            var sb = new System.Text.StringBuilder();
            foreach (char c in dersAdi)
                sb.Append(map.TryGetValue(c, out char n) ? n : c);
            return sb.ToString().ToLowerInvariant().Replace(" ", "").Trim();
        }

        // ═══════════════════════════════════════════════════
        // HATA ORANI (bölüm bazlı sınava gelmeyecek kişi %)
        // ═══════════════════════════════════════════════════

        /// <summary>Bölümün hata oranını (%) döndürür; tanımlı değilse 0.</summary>
        private double BolumHataOrani(string bolum)
        {
            if (string.IsNullOrWhiteSpace(bolum)) return 0;
            string b = bolum.ToUpperInvariant().Trim();
            foreach (var kvp in _bolumOverbooking)
            {
                if (kvp.Key.ToUpperInvariant().Trim() == b)
                    return kvp.Value;
            }
            return 0;
        }

        /// <summary>
        /// Efektif öğrenci sayısı: kontenjandan sınava gelmeyecek % düşülür.
        /// Örn: 120 kontenjan, %20 hata oranı → 96 kişi olarak dağıtılır.
        /// </summary>
        private int EfektifOgrenci(string bolum, int kontenjan)
        {
            double yuzde = BolumHataOrani(bolum);
            if (yuzde <= 0) return kontenjan;
            return Math.Max(1, (int)Math.Ceiling(kontenjan * (1 - yuzde / 100.0)));
        }

        // ═══════════════════════════════════════════════════
        // GRUPLAMA
        // ═══════════════════════════════════════════════════

        private List<SubeGrubu> SubeGruplariOlustur()
        {
            var gruplar = new List<SubeGrubu>();
            var dersGruplari = _dersler
                .GroupBy(d => new { Bolum = d.BolumAdi.ToUpperInvariant().Trim(), DersAdi = d.DersAdi.ToUpperInvariant().Trim(), d.SinifSeviyesi });

            foreach (var grup in dersGruplari)
            {
                var subeler = grup.ToList();
                gruplar.Add(new SubeGrubu
                {
                    DersAdi = subeler.First().DersAdi,
                    Bolum = subeler.First().BolumAdi,
                    SinifSeviyesi = subeler.First().SinifSeviyesi,
                    Subeler = subeler,
                    // Hata oranı düşülmüş efektif toplam (slot kapasite kontrolleri de buna göre yapılır)
                    ToplamOgrenci = subeler.Sum(s => EfektifOgrenci(s.BolumAdi, s.Kontenjan))
                });
            }
            return gruplar;
        }

        private List<OrtakDersPaketi> OrtakDersPaketleriOlustur(List<SubeGrubu> tumGruplar)
        {
            var paketler = new List<OrtakDersPaketi>();
            var ortakGruplar = tumGruplar
                .Where(g => _ortakDersler.Contains(g.DersAdi.ToUpperInvariant().Trim()))
                .GroupBy(g => NormalizeDersAdi(g.DersAdi));

            foreach (var ortakGrup in ortakGruplar)
            {
                var altGruplar = ortakGrup.ToList();
                paketler.Add(new OrtakDersPaketi
                {
                    NormalizeDersAdi = ortakGrup.Key,
                    OrijinalDersAdi = altGruplar.First().DersAdi,
                    BolumGruplari = altGruplar,
                    ToplamOgrenci = altGruplar.Sum(g => g.ToplamOgrenci)
                });
            }
            return paketler.OrderByDescending(p => p.ToplamOgrenci).ToList();
        }

        // ═══════════════════════════════════════════════════
        // ANA DAĞITIM — V8 İKİ AŞAMALI
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// İki aşamalı dağıtım (GERİYE DÖNÜK UYUMLU SARMALAYICI):
        /// AŞAMA 1: SablonOlustur() — tüm dersler gün+saat slotlarına yerleştirilir (salonsuz).
        /// AŞAMA 2: SalonlariDagit() — şablondaki her slot için toptan salon dağıtımı yapılır.
        /// Eski tek tuş davranışı aynen korunur; yeni akışta bu iki metod ayrı ayrı çağrılır.
        /// </summary>
        public List<SinavAtama> ProgramiOlustur()
        {
            var sablon = SablonOlustur();
            return SalonlariDagit(sablon);
        }

        /// <summary>
        /// AŞAMA A — ŞABLON OLUŞTURMA: Tüm dersleri gün+saat slotlarına yerleştirir,
        /// SALON ATAMASI YAPMAZ. Sonuçtaki her SinavAtama'nın AtananSalonlar listesi BOŞ döner.
        ///   Garantili öncelik sırası:
        ///     1) Ortak dersler — tüm bölümler aynı slot'a çivilenir (slot bazlı).
        ///     2) Uygulamalı dersler — kısıtlı uygulamalı salonlara öncelikli yerleşim.
        ///     3) Kalan dersler — mevcut kurallar çerçevesinde.
        /// </summary>
        public List<SinavAtama> SablonOlustur()
        {
            Uyarilar.Clear();
            _slotBolumSinifMap.Clear();
            _slotBolumDersMap.Clear();
            _slotDersMap.Clear();
            _bolumAtananSlotlar.Clear();
            _slotNormalOgrenciYuku.Clear();
            _slotUygulamaliOgrenciYuku.Clear();
            foreach (var k in _gunSinavSayisi.Keys.ToList())
                _gunSinavSayisi[k] = 0;

            // Uygulamalı ders seçilmiş ama hiç uygulamalı salon tanımlı değilse baştan uyar
            if (_uygulamaliDersler.Count > 0 && _uygulamaliToplamKapasite == 0)
                Uyarilar.Add("⚠ Uygulamalı ders işaretlendi ancak veritabanında 'UYGULAMALI' tipli salon yok! " +
                             "Salon_Tipi_Guncelleme.sql scriptini çalıştırdığınızdan ve salon kodlarının eşleştiğinden emin olun.");

            var subeGruplari = SubeGruplariOlustur();
            var ortakPaketler = OrtakDersPaketleriOlustur(subeGruplari);

            // Ortak ders gruplarını işaretle
            var ortakGrupIdleri = new HashSet<SubeGrubu>();
            foreach (var paket in ortakPaketler)
                foreach (var g in paket.BolumGruplari)
                    ortakGrupIdleri.Add(g);

            var normalGruplar = subeGruplari.Where(g => !ortakGrupIdleri.Contains(g)).ToList();

            // ═════════════════════════════════════════
            // AŞAMA 1: TAKVİM YERLEŞTİRME (salon düşünmeden)
            // Sonuç: slotKey → List<SubeGrubu>
            // ═════════════════════════════════════════
            var slotAtamalari = new Dictionary<string, List<SubeGrubu>>();

            // 1A) Ortak ders paketlerini aynı slot'a çivile
            // Tur 0: sınıf-saat kuralına uygun slotlar denenir.
            // Tur 1: kural esnetilir — ortak dersler farklı sınıf seviyelerini
            //        içerebildiğinden katı saat kuralıyla atanamayabilir.
            foreach (var paket in ortakPaketler)
            {
                bool paketAtandi = false;
                var dengeliSlotlar = SlotlariDengeliSirala();

                // Paketin salon tipi ve toplam öğrenci yükü (paket = aynı ders → tek tip)
                bool paketUygulamali = DersUygulamaliMi(paket.BolumGruplari[0].DersAdi);
                int paketYuk = paket.BolumGruplari.Sum(g => g.ToplamOgrenci);
                int paketTipKapasite = paketUygulamali ? _uygulamaliToplamKapasite : _normalToplamKapasite;

                if (paketYuk > paketTipKapasite)
                {
                    string bolumListe = string.Join(", ", paket.BolumGruplari.Select(g => g.Bolum).Distinct());
                    Uyarilar.Add($"⚠ Ortak Ders '{paket.OrijinalDersAdi}' ({bolumListe}) — toplam {paketYuk} öğrenci, " +
                                 $"{(paketUygulamali ? "uygulamalı" : "normal")} salonların toplam kapasitesini ({paketTipKapasite}) aşıyor. " +
                                 "Tek oturumda sığması imkansız!");
                    continue;
                }

                // KURAL 1 ÖN-KONTROL: paketin kendi içinde aynı bölüm iki farklı derse
                // bağlanıyorsa bu paket hiçbir slota tek parça yerleşemez.
                if (PaketIcindeBolumCakismasiVar(paket))
                {
                    Uyarilar.Add($"⚠ Ortak Ders '{paket.OrijinalDersAdi}' — paketin içinde aynı bölümün " +
                                 "birden fazla farklı dersi var; Kural 1 gereği tek slota çivilenemez.");
                    continue;
                }

                // ═══════════════════════════════════════
                // ATAMA STRATEJİSİ (SIRALI)
                //   Tur 0 (KATI)      : sınıf-saat kuralı + tüm kısıtlar, 17:00 hariç
                //   Tur 1 (ESNEK)     : Kural 2 → sınıf-saat kuralı ve dinlenme molası
                //                       esnetilir; Kural 1 ve kapasite KORUNUR, 17:00 hariç
                //   Tur -1 (İSTİSNA)  : Kontenjan/salon sorunu → SADECE Elektrik-Elektronik (ELM)
                //                       + Mekatronik (MEK) BİRLİKTE başka bir slota (öncelikle
                //                       BAŞKA BİR GÜNE, olmazsa aynı gün farklı saate) kaydırılır;
                //                       diğer bölümler sabit kalır.
                //   Tur 2 (TAŞIRMA)   : Kural 3 → sadece 17:00, hiçbir kısıt yok (garanti atama)
                // ═══════════════════════════════════════
                foreach (int tur in new[] { 0, 1, -1, 2 })
                {
                    if (paketAtandi) break;

                    // İSTİSNA KURALI: paket tek slota sığmadı → yalnızca ELM/MEK kaydırılır
                    if (tur == -1)
                    {
                        paketAtandi = ElmMekKaydirarakAta(paket, slotAtamalari, paketUygulamali);
                        continue;
                    }

                    var turSlotlari = (tur == 2) ? TasmaSlotlariniSirala() : dengeliSlotlar;

                    foreach (var slotIdx in turSlotlari)
                    {
                        if (paketAtandi) break;
                        string slotKey = _tumSlotlar[slotIdx];

                        // KAPASİTE ÖN-KONTROLÜ (Tur 2'de metot içinde otomatik atlanır)
                        if (SlotKapasiteYetersiz(slotKey, paketUygulamali, paketYuk))
                            continue;

                        // Tüm bölüm grupları bu slot'a uygun mu?
                        bool slotUygun = true;
                        foreach (var bolumGrup in paket.BolumGruplari)
                        {
                            // ── KURAL 2 (ESNEK): sınıf-saat kuralı sadece Tur 0'da zorunlu ──
                            if (tur == 0 && !SaatSinifaUygunMu(slotKey, bolumGrup.SinifSeviyesi))
                            { slotUygun = false; break; }

                            // ── KURAL 1 (KESİN): bölüm içi çakışma — Tur 0 ve Tur 1'de asla ihlal edilmez ──
                            if (BolumCakismasiVar(slotKey, bolumGrup.Bolum, bolumGrup.DersAdi))
                            { slotUygun = false; break; }

                            // ── Dinlenme molası (ESNEK): Tur 1'den itibaren esnetilir ──
                            if (tur == 0 && DinlenmeMolasiIhlali(slotIdx, bolumGrup.Bolum, bolumGrup.SinifSeviyesi))
                            { slotUygun = false; break; }
                        }
                        if (!slotUygun) continue;

                        // Tüm bölüm gruplarını bu slot'a ata
                        if (!slotAtamalari.ContainsKey(slotKey))
                            slotAtamalari[slotKey] = new List<SubeGrubu>();

                        foreach (var bolumGrup in paket.BolumGruplari)
                        {
                            slotAtamalari[slotKey].Add(bolumGrup);
                            TakvimHaritalariniGuncelle(slotKey, slotIdx, bolumGrup);
                        }
                        paketAtandi = true;

                        if (tur == 1)
                            Uyarilar.Add($"ℹ Ortak Ders '{paket.OrijinalDersAdi}' — sınıf-saat kuralı esnetilerek " +
                                         $"{slotKey.Replace('|', ' ')} slotuna atandı (Kural 2).");
                        else if (tur == 2)
                            Uyarilar.Add($"ℹ Ortak Ders '{paket.OrijinalDersAdi}' — uygun slot bulunamadığı için " +
                                         $"{slotKey.Replace('|', ' ')} TAŞIRMA slotuna atandı (Kural 3). " +
                                         "Salon/gözetmen yeterliliğini manuel kontrol edin.");
                    }
                }

                if (!paketAtandi)
                {
                    string bolumler = string.Join(", ", paket.BolumGruplari.Select(g => g.Bolum).Distinct());
                    Uyarilar.Add($"⚠ Ortak Ders '{paket.OrijinalDersAdi}' ({bolumler}) — hiçbir slot'a atanamadı! " +
                                 "Takvime 17:00 taşırma slotu ekleyerek çözebilirsiniz.");
                }
            }

            // ═════════════════════════════════════════
            // GARANTİLİ ÖNCELİK SIRASI:
            //   1. ÖNCELİK → Ortak dersler (yukarıda 1A'da slot bazlı çivilendi)
            //   2. ÖNCELİK → Uygulamalı dersler (lab/teknik resim salonları kısıtlı
            //                olduğundan, normal derslerden ÖNCE yerleştirilir)
            //   3. ÖNCELİK → Kalan dersler (mevcut kurallar çerçevesinde)
            // Her öncelik grubu kendi içinde kontenjana göre büyükten küçüğe sıralanır.
            // ═════════════════════════════════════════

            // 1B) Uygulamalı dersler
            var uygulamaliGruplar = normalGruplar
                .Where(g => DersUygulamaliMi(g.DersAdi))
                .OrderByDescending(g => g.ToplamOgrenci)
                .ToList();

            // 1C) Kalan (normal) dersler
            var kalanGruplar = normalGruplar
                .Where(g => !DersUygulamaliMi(g.DersAdi))
                .OrderByDescending(g => g.ToplamOgrenci)
                .ToList();

            var siraliNormalGruplar = uygulamaliGruplar.Concat(kalanGruplar).ToList();

            foreach (var grup in siraliNormalGruplar)
            {
                bool atandi = false;
                var dengeliSlotlar = SlotlariDengeliSirala();

                // Salon tipi ve kapasite ön-kontrolü
                bool grupUygulamali = DersUygulamaliMi(grup.DersAdi);
                int grupTipKapasite = grupUygulamali ? _uygulamaliToplamKapasite : _normalToplamKapasite;

                if (grup.ToplamOgrenci > grupTipKapasite)
                {
                    Uyarilar.Add($"⚠ '{grup.DersAdi}' ({grup.Bolum} {grup.SinifSeviyesi}. sınıf) — toplam {grup.ToplamOgrenci} öğrenci, " +
                                 $"{(grupUygulamali ? "uygulamalı" : "normal")} salonların toplam kapasitesini ({grupTipKapasite}) aşıyor. " +
                                 "Tek oturumda sığması imkansız!");
                    continue;
                }

                // ═══════════════════════════════════════
                // ÜÇ TURLU ATAMA STRATEJİSİ
                //   Tur 0 (KATI)    : sınıf-saat kuralı + tüm kısıtlar, 17:00 hariç
                //   Tur 1 (ESNEK)   : Kural 2 → sınıf-saat kuralı ve dinlenme molası esnetilir;
                //                     Kural 1 (bölüm çakışması) ve kapasite KORUNUR, 17:00 hariç
                //   Tur 2 (TAŞIRMA) : Kural 3 → sadece 17:00, kısıtsız/limitsiz atama
                // ═══════════════════════════════════════
                for (int tur = 0; tur < 3 && !atandi; tur++)
                {
                    var turSlotlari = (tur == 2) ? TasmaSlotlariniSirala() : dengeliSlotlar;

                    foreach (var slotIdx in turSlotlari)
                    {
                        if (atandi) break;
                        string slotKey = _tumSlotlar[slotIdx];

                        // ── KURAL 2 (ESNEK): sınıf-saat kuralı yalnızca Tur 0'da bağlayıcıdır.
                        //    Tur 1'de sınıf seviyesinin birincil saat aralığı dışına çıkılabilir.
                        if (tur == 0 && !SaatSinifaUygunMu(slotKey, grup.SinifSeviyesi))
                            continue;

                        // ── Kapasite ön-kontrolü (Tur 2'de metot içinde otomatik atlanır)
                        if (SlotKapasiteYetersiz(slotKey, grupUygulamali, grup.ToplamOgrenci))
                            continue;

                        // ── KURAL 1 (KESİN / HARD CONSTRAINT) ──────────────────────
                        //    Aynı saatte, aynı bölümden başka bir sınav varsa
                        //    sınıf seviyesi ne olursa olsun ATAMA REDDEDİLİR.
                        //    Bu kontrol Tur 1'de de asla esnetilmez; sadece 17:00
                        //    taşırma slotunda (Tur 2) metot içinde devre dışı kalır.
                        if (BolumCakismasiVar(slotKey, grup.Bolum, grup.DersAdi))
                            continue;

                        // ── Ortak ders bütünlüğü (KESİN, taşırma slotu hariç)
                        if (!TasmaSlotuMu(slotKey) && OrtakDersCakismasiVar(slotKey, grup.DersAdi))
                            continue;

                        // ── Dinlenme molası (ESNEK): Tur 1'den itibaren esnetilir
                        if (tur == 0 && DinlenmeMolasiIhlali(slotIdx, grup.Bolum, grup.SinifSeviyesi))
                            continue;

                        if (!slotAtamalari.ContainsKey(slotKey))
                            slotAtamalari[slotKey] = new List<SubeGrubu>();

                        slotAtamalari[slotKey].Add(grup);
                        TakvimHaritalariniGuncelle(slotKey, slotIdx, grup);
                        atandi = true;

                        if (tur == 1)
                            Uyarilar.Add($"ℹ '{grup.DersAdi}' ({grup.Bolum} {grup.SinifSeviyesi}. sınıf) — " +
                                         $"birincil saat aralığı ({SablonDogrulayici.SinifIcinBeklenenSaat(grup.SinifSeviyesi)}) " +
                                         $"dolu olduğundan {slotKey.Replace('|', ' ')} slotuna esnetilerek atandı (Kural 2).");
                        else if (tur == 2)
                            Uyarilar.Add($"ℹ '{grup.DersAdi}' ({grup.Bolum} {grup.SinifSeviyesi}. sınıf) — " +
                                         $"hiçbir uygun slot kalmadığından {slotKey.Replace('|', ' ')} TAŞIRMA slotuna " +
                                         "atandı (Kural 3). Salon/gözetmen yeterliliğini manuel kontrol edin.");
                    }
                }

                if (!atandi)
                {
                    string subelerStr = string.Join(", ", grup.Subeler.Select(s => s.Sube));
                    Uyarilar.Add($"⚠ '{grup.DersAdi}' — Şube [{subelerStr}] ({grup.Bolum} {grup.SinifSeviyesi}. sınıf) " +
                                 $"hiçbir slot'a atanamadı! Takvime 17:00 taşırma slotu ekleyin.");
                }
            }

            // ═════════════════════════════════════════
            // ŞABLON ÇIKTISI: slot atamaları şube bazında düzleştirilir.
            // Salon ataması YAPILMAZ — AtananSalonlar boş liste olarak döner.
            // ═════════════════════════════════════════
            var sablonSonuclari = new List<SinavAtama>();

            foreach (var kvp in slotAtamalari)
            {
                var parcalar = kvp.Key.Split('|');
                var gun = DateTime.ParseExact(parcalar[0], "yyyy-MM-dd", null);
                var saat = parcalar[1];

                foreach (var grup in kvp.Value)
                {
                    foreach (var sube in grup.Subeler)
                    {
                        sablonSonuclari.Add(new SinavAtama
                        {
                            DersAdi = sube.DersAdi,
                            BolumAdi = sube.BolumAdi,
                            SinifSeviyesi = sube.SinifSeviyesi,
                            Kontenjan = sube.Kontenjan,
                            Sube = sube.Sube,
                            OgretimElemani = sube.OgretimElemani,
                            Gun = gun,
                            Saat = saat,
                            AtananSalonlar = new List<SalonAtama>() // Şablon aşamasında salon YOK
                        });
                    }
                }
            }

            return sablonSonuclari.OrderBy(s => s.Gun).ThenBy(s => s.Saat).ThenBy(s => s.BolumAdi).ThenBy(s => s.DersAdi).ToList();
        }

        /// <summary>
        /// AŞAMA C — TOPTAN SALON DAĞITIMI: Kullanıcının (gerekirse elle düzenlediği) şablonu
        /// girdi alır ve her gün+saat grubunu mevcut SlotIcinTopluSalonDagit ile işler.
        /// Sanal havuz (pooling), blok sabitleme + fallback, minimum artık eşiği, hata oranı
        /// (efektif kontenjan) ve uygulamalı/normal salon ayrımı AYNEN korunur.
        /// NOT: Uyarilar listesi burada temizlenmez — ProgramiOlustur() üzerinden çağrıldığında
        /// Aşama 1 uyarıları kaybolmasın diye. Bağımsız kullanımda yeni bir algoritma örneği kurun.
        /// </summary>
        public List<SinavAtama> SalonlariDagit(List<SinavAtama> sablon)
        {
            var sonuclar = new List<SinavAtama>();
            if (sablon == null || sablon.Count == 0) return sonuclar;

            // Şablondaki her gün+saat slotu ayrı işlenir
            foreach (var slotGrubu in sablon.GroupBy(a => new { a.Gun, a.Saat }))
            {
                // Slottaki atamalardan SubeGrubu yapıları yeniden kurulur
                // (kullanıcı şablonda kontenjan/gün/saat değiştirmiş olabilir)
                var slottakiGruplar = slotGrubu
                    .GroupBy(a => new
                    {
                        Bolum = a.BolumAdi.ToUpperInvariant().Trim(),
                        DersAdi = a.DersAdi.ToUpperInvariant().Trim(),
                        a.SinifSeviyesi
                    })
                    .Select(g => new SubeGrubu
                    {
                        DersAdi = g.First().DersAdi,
                        Bolum = g.First().BolumAdi,
                        SinifSeviyesi = g.First().SinifSeviyesi,
                        Subeler = g.Select(a => new Ders
                        {
                            DersAdi = a.DersAdi,
                            BolumAdi = a.BolumAdi,
                            SinifSeviyesi = a.SinifSeviyesi,
                            Sube = a.Sube,
                            Kontenjan = a.Kontenjan,
                            OgretimElemani = a.OgretimElemani
                        }).ToList(),
                        // Hata oranı düşülmüş efektif toplam
                        ToplamOgrenci = g.Sum(a => EfektifOgrenci(a.BolumAdi, a.Kontenjan))
                    })
                    .ToList();

                // Mevcut toptan salon dağıtımı (havuz + blok + artık eşiği + efektif kontenjan)
                var slotSonuclari = SlotIcinTopluSalonDagit(slottakiGruplar);

                foreach (var ssa in slotSonuclari)
                {
                    sonuclar.Add(new SinavAtama
                    {
                        DersAdi = ssa.Ders.DersAdi,
                        BolumAdi = ssa.Ders.BolumAdi,
                        SinifSeviyesi = ssa.Ders.SinifSeviyesi,
                        Kontenjan = ssa.Ders.Kontenjan,
                        Sube = ssa.Ders.Sube,
                        OgretimElemani = ssa.Ders.OgretimElemani,
                        Gun = slotGrubu.Key.Gun,
                        Saat = slotGrubu.Key.Saat,
                        AtananSalonlar = ssa.Salonlar
                    });
                }
            }

            return sonuclar.OrderBy(s => s.Gun).ThenBy(s => s.Saat).ThenBy(s => s.BolumAdi).ThenBy(s => s.DersAdi).ToList();
        }

        // ═══════════════════════════════════════════════════
        // AŞAMA 2: BİR SLOT İÇİN TOPLU SALON DAĞITIMI
        // ═══════════════════════════════════════════════════

        private const int MINIMUM_ARTIK_ESIGI = 15;

        /// <summary>
        /// Bir saat dilimindeki TÜM ders gruplarına salonları toptan dağıtır.
        /// ─── Akıllı Greedy + Overbooking Tampon ───
        /// 1) Salonları BAZ kapasiteye kadar doldur (overbooking'i KULLANMA).
        /// 2) Artık kişi &lt; 15 ve overbooking tamponuna sığıyorsa → yeni salon AÇMA, tampona em.
        /// 3) Artık kişi ≥ 15 veya tampona sığmıyorsa → yeni salon AÇ.
        /// 4) Blok sabitleme + fallback korunur.
        /// </summary>
        private List<SubeSalonAtama> SlotIcinTopluSalonDagit(List<SubeGrubu> slottakiGruplar)
        {
            var sonuc = new List<SubeSalonAtama>();

            // Bu slot için TÜM salonlar müsait
            var slotKullanilanSalonlar = new HashSet<string>();

            // Tüm şubeleri tek bir düz listeye çevir (büyük kontenjanlar önce)
            var tumSubeler = new List<(Ders sube, SubeGrubu grup)>();
            foreach (var grup in slottakiGruplar)
                foreach (var sube in grup.Subeler)
                    tumSubeler.Add((sube, grup));

            // ═══════════════════════════════════════
            // SANAL HAVUZ (POOLING) — Ortak Dersleri Tek Salonda Birleştirme
            // Kullanıcının tanımladığı her havuz için: havuzla eşleşen şubeler
            // (DersAdi aynı + BolumAdi havuzdaki bölümlerden biri) listeden çıkarılır,
            // yerine toplam EFEKTİF kontenjanlı tek bir SANAL ders eklenir.
            // Salon ataması bu sanal derse tek parça yapılır; metodun sonunda
            // atanan salonlar asıl alt şubelere geri açılır.
            // ═══════════════════════════════════════
            var havuzAltSubeleri = new Dictionary<Ders, List<(Ders sube, SubeGrubu grup)>>();

            foreach (var havuz in _dersHavuzlari)
            {
                if (havuz == null || string.IsNullOrWhiteSpace(havuz.DersAdi))
                    continue;

                // Şube seviyesi üye listesi doluysa o kullanılır; yoksa eski bölüm seviyesi eşleştirme
                bool subeSeviyesi = havuz.Uyeler != null && havuz.Uyeler.Count >= 2;
                if (!subeSeviyesi && (havuz.Bolumler == null || havuz.Bolumler.Count < 2))
                    continue;

                string havuzDersNorm = NormalizeDersAdi(havuz.DersAdi);

                // Bu slotta havuzla eşleşen şubeler
                List<(Ders sube, SubeGrubu grup)> eslesenler;
                if (subeSeviyesi)
                {
                    // ŞUBE SEVİYESİ: Bölüm + Sınıf + Şube birebir eşleşmeli
                    var uyeSet = new HashSet<string>(havuz.Uyeler.Select(u =>
                        $"{u.Bolum.ToUpperInvariant().Trim()}|{u.SinifSeviyesi}|{u.Sube.ToUpperInvariant().Trim()}"));

                    eslesenler = tumSubeler
                        .Where(t => NormalizeDersAdi(t.sube.DersAdi) == havuzDersNorm
                                 && uyeSet.Contains($"{t.sube.BolumAdi.ToUpperInvariant().Trim()}|{t.sube.SinifSeviyesi}|{t.sube.Sube.ToUpperInvariant().Trim()}"))
                        .ToList();
                }
                else
                {
                    // BÖLÜM SEVİYESİ (geriye dönük): bölümün tüm şubeleri havuza girer
                    var havuzBolumSet = new HashSet<string>(havuz.Bolumler.Select(b => b.ToUpperInvariant().Trim()));

                    eslesenler = tumSubeler
                        .Where(t => NormalizeDersAdi(t.sube.DersAdi) == havuzDersNorm
                                 && havuzBolumSet.Contains(t.sube.BolumAdi.ToUpperInvariant().Trim()))
                        .ToList();
                }

                // Birleştirilecek en az 2 şube yoksa havuzlamaya gerek yok
                if (eslesenler.Count < 2) continue;

                // Eşleşen şubeleri listeden çıkar
                foreach (var t in eslesenler)
                    tumSubeler.Remove(t);

                string havuzBolumAdi = "HAVUZ: " + string.Join(" + ",
                    eslesenler.Select(t => $"{t.sube.BolumAdi} ({t.sube.Sube})"));

                // Sanal/geçici havuz dersi — kontenjan, alt şubelerin EFEKTİF kontenjan toplamı
                // (bölüm bazlı hata oranları burada düşüldüğünden, sanal bölüm adı için
                //  hata oranı tanımlı olmasa da doğru sayı dağıtılır)
                var sanalDers = new Ders
                {
                    DersAdi = eslesenler[0].sube.DersAdi,
                    BolumAdi = havuzBolumAdi,
                    SinifSeviyesi = eslesenler[0].sube.SinifSeviyesi,
                    Sube = "HAVUZ",
                    Kontenjan = eslesenler.Sum(t => EfektifOgrenci(t.sube.BolumAdi, t.sube.Kontenjan))
                };

                var sanalGrup = new SubeGrubu
                {
                    DersAdi = sanalDers.DersAdi,
                    Bolum = sanalDers.BolumAdi,
                    SinifSeviyesi = sanalDers.SinifSeviyesi,
                    Subeler = new List<Ders> { sanalDers },
                    ToplamOgrenci = sanalDers.Kontenjan
                };

                tumSubeler.Add((sanalDers, sanalGrup));
                havuzAltSubeleri[sanalDers] = eslesenler;
            }

            // SIRALAMA: Havuz dersleri ÖNCE işlenir — böylece salonlar başka şubelerce
            // kapılmadan havuz tek (veya en az sayıda) salona yerleşir. Havuz en sona
            // kalırsa boş salon bulamayıp diğer salonların kırıntı boşluklarına saçılıyordu.
            // Kendi içlerinde ve sonrasında büyük kontenjan önce gelir.
            tumSubeler = tumSubeler
                .OrderByDescending(s => havuzAltSubeleri.ContainsKey(s.sube) ? 1 : 0)
                .ThenByDescending(s => s.sube.Kontenjan)
                .ToList();

            // Bu slottaki tüm atanmış salonlar (artık emme için)
            var slotAssignedSalons = new List<SlotAssignedSalon>();

            foreach (var (sube, grup) in tumSubeler)
            {
                // Hata Oranı: sınava gelmeyecek % düşülerek EFEKTİF kontenjan dağıtılır
                int kalanOgrenci = EfektifOgrenci(sube.BolumAdi, sube.Kontenjan);
                var subeSalonlari = new List<SalonAtama>();

                // İKİ YÖNLÜ SALON AYRIMI:
                // Uygulamalı dersler SADECE uygulamalı salonlara (lab/teknik resim),
                // normal dersler SADECE normal salonlara atanır.
                bool uygulamaliDers = DersUygulamaliMi(grup.DersAdi);

                // Müsait salonlar (büyükten küçüğe)
                var musaitSalonlar = _salonlar
                    .Where(s => !slotKullanilanSalonlar.Contains(s.SalonKodu))
                    .Where(s => s.UygulamaliMi == uygulamaliDers)
                    .Select(s => new {
                        Salon = s,
                        BazKapasite = s.Kapasite,
                        // Hata oranı artık kontenjanı düşürüyor; salon kapasitesi baz değerinde kalır
                        OverbookingKapasite = s.Kapasite
                    })
                    .OrderByDescending(s => s.BazKapasite)
                    .ToList();

                // BLOK SABİTLEME
                string secilenBlok = null;

                // ═══════════════════════════════════════
                // ADIM A: Aday salonları topla (blok filtreli)
                // ═══════════════════════════════════════
                var adaySalonlar = new List<(int idx, string kod, int bazKap, int overbookKap)>();
                int toplamBazKap = 0;
                int toplamOverbookKap = 0;

                for (int i = 0; i < musaitSalonlar.Count; i++)
                {
                    string blok = BlokKoduCikar(musaitSalonlar[i].Salon.SalonKodu);
                    if (secilenBlok == null) secilenBlok = blok;
                    if (blok != secilenBlok) continue;

                    adaySalonlar.Add((i, musaitSalonlar[i].Salon.SalonKodu, musaitSalonlar[i].BazKapasite, musaitSalonlar[i].OverbookingKapasite));
                    toplamBazKap += musaitSalonlar[i].BazKapasite;
                    toplamOverbookKap += musaitSalonlar[i].OverbookingKapasite;

                    if (toplamBazKap >= kalanOgrenci) break;
                }

                // FALLBACK: Aynı blokta baz kapasite yetmiyorsa diğer blokları ekle
                if (toplamBazKap < kalanOgrenci)
                {
                    for (int i = 0; i < musaitSalonlar.Count; i++)
                    {
                        if (toplamBazKap >= kalanOgrenci) break;
                        string blok = BlokKoduCikar(musaitSalonlar[i].Salon.SalonKodu);
                        if (secilenBlok != null && blok == secilenBlok) continue;
                        if (adaySalonlar.Any(a => a.idx == i)) continue;

                        adaySalonlar.Add((i, musaitSalonlar[i].Salon.SalonKodu, musaitSalonlar[i].BazKapasite, musaitSalonlar[i].OverbookingKapasite));
                        toplamBazKap += musaitSalonlar[i].BazKapasite;
                        toplamOverbookKap += musaitSalonlar[i].OverbookingKapasite;
                    }
                }

                // ═══════════════════════════════════════
                // ADIM B: AKILLI GREEDY — BAZ kapasiteye doldur
                // ═══════════════════════════════════════
                if (adaySalonlar.Count > 0 && kalanOgrenci > 0)
                {
                    // Tek salona sığıyorsa (baz kapasiteyle) direkt ata
                    if (adaySalonlar[0].bazKap >= kalanOgrenci)
                    {
                        var tek = adaySalonlar[0];
                        subeSalonlari.Add(new SalonAtama { SalonKodu = tek.kod, SalonKapasitesi = tek.bazKap, AtananOgrenciSayisi = kalanOgrenci });
                        slotAssignedSalons.Add(new SlotAssignedSalon { SalonKodu = tek.kod, BazKapasite = tek.bazKap, OverbookingKapasite = tek.overbookKap, Atanan = kalanOgrenci });
                        slotKullanilanSalonlar.Add(tek.kod);
                        kalanOgrenci = 0;
                    }
                    else
                    {
                        // ── Birden fazla salon gerekli ──
                        // Greedy: Salonları BAZ kapasiteye kadar sırayla doldur
                        var doldurulanSalonlar = new List<(string kod, int bazKap, int overbookKap, int atanan, int adayIdx)>();

                        int tempKalan = kalanOgrenci;
                        for (int i = 0; i < adaySalonlar.Count; i++)
                        {
                            if (tempKalan <= 0) break;
                            var salon = adaySalonlar[i];
                            int atanacak = Math.Min(salon.bazKap, tempKalan);
                            doldurulanSalonlar.Add((salon.kod, salon.bazKap, salon.overbookKap, atanacak, i));
                            tempKalan -= atanacak;
                        }

                        // ── ARTIK KONTROL ──
                        // Son salona düşen kişi sayısı 15'in altında mı?
                        if (doldurulanSalonlar.Count >= 2)
                        {
                            var sonSalon = doldurulanSalonlar[doldurulanSalonlar.Count - 1];

                            if (sonSalon.atanan > 0 && sonSalon.atanan < MINIMUM_ARTIK_ESIGI)
                            {
                                // Overbooking tamponunda yer var mı kontrol et
                                int toplamTamponBosluk = 0;
                                for (int i = 0; i < doldurulanSalonlar.Count - 1; i++)
                                {
                                    int tamponBos = doldurulanSalonlar[i].overbookKap - doldurulanSalonlar[i].atanan;
                                    toplamTamponBosluk += Math.Max(0, tamponBos);
                                }

                                if (toplamTamponBosluk >= sonSalon.atanan)
                                {
                                    // ✅ TAMPONA EMİLEBİLİR — son salonu iptal et, artığı önceki salonlara dağıt
                                    int emilecek = sonSalon.atanan;
                                    doldurulanSalonlar.RemoveAt(doldurulanSalonlar.Count - 1);

                                    // Artığı eşit bölerek mevcut salonlara dağıt
                                    int salonSayisi = doldurulanSalonlar.Count;
                                    int emTaban = emilecek / salonSayisi;
                                    int emFazla = emilecek % salonSayisi;

                                    for (int i = 0; i < doldurulanSalonlar.Count; i++)
                                    {
                                        int eklenecek = emTaban + (i < emFazla ? 1 : 0);
                                        // Overbooking limitini aşma
                                        int maxEklenebilir = doldurulanSalonlar[i].overbookKap - doldurulanSalonlar[i].atanan;
                                        eklenecek = Math.Min(eklenecek, maxEklenebilir);

                                        var d = doldurulanSalonlar[i];
                                        doldurulanSalonlar[i] = (d.kod, d.bazKap, d.overbookKap, d.atanan + eklenecek, d.adayIdx);
                                        emilecek -= eklenecek;
                                    }

                                    // Hala emilememiş artık varsa (edge case), tekrar dene
                                    if (emilecek > 0)
                                    {
                                        for (int i = 0; i < doldurulanSalonlar.Count && emilecek > 0; i++)
                                        {
                                            int maxEklenebilir = doldurulanSalonlar[i].overbookKap - doldurulanSalonlar[i].atanan;
                                            int ek = Math.Min(emilecek, maxEklenebilir);
                                            if (ek > 0)
                                            {
                                                var d = doldurulanSalonlar[i];
                                                doldurulanSalonlar[i] = (d.kod, d.bazKap, d.overbookKap, d.atanan + ek, d.adayIdx);
                                                emilecek -= ek;
                                            }
                                        }
                                    }

                                    tempKalan = emilecek; // Normalde 0 olmalı
                                }
                                // else: Tampona sığmıyor → son salon açık kalır (mecburi)
                            }
                        }

                        // ── Sonuçları kaydet ──
                        kalanOgrenci = tempKalan;
                        foreach (var d in doldurulanSalonlar)
                        {
                            if (d.atanan <= 0) continue;

                            subeSalonlari.Add(new SalonAtama { SalonKodu = d.kod, SalonKapasitesi = d.bazKap, AtananOgrenciSayisi = d.atanan });
                            slotAssignedSalons.Add(new SlotAssignedSalon { SalonKodu = d.kod, BazKapasite = d.bazKap, OverbookingKapasite = d.overbookKap, Atanan = d.atanan });
                            slotKullanilanSalonlar.Add(d.kod);
                        }
                    }
                }

                // ═══════════════════════════════════════
                // ADIM C: SARKAN ÖĞRENCİ — mevcut salonlardaki tampon boşluklara yerleştir
                // ═══════════════════════════════════════
                if (kalanOgrenci > 0)
                {
                    // EN AZ PARÇALANMA: boşluğu en büyük salondan başla — böylece sarkan
                    // öğrenciler mümkünse TEK salonda toplanır (2+13 gibi bölünmez).
                    // Önce aynı blok
                    foreach (var sa in slotAssignedSalons.OrderByDescending(s => s.OverbookingKapasite - s.Atanan))
                    {
                        if (kalanOgrenci <= 0) break;
                        if (secilenBlok != null && BlokKoduCikar(sa.SalonKodu) != secilenBlok) continue;

                        int bos = sa.OverbookingKapasite - sa.Atanan;
                        if (bos > 0)
                        {
                            int at = Math.Min(bos, kalanOgrenci);
                            sa.Atanan += at;

                            var mevcut = subeSalonlari.FirstOrDefault(x => x.SalonKodu == sa.SalonKodu);
                            if (mevcut != null) mevcut.AtananOgrenciSayisi += at;
                            else subeSalonlari.Add(new SalonAtama { SalonKodu = sa.SalonKodu, SalonKapasitesi = sa.BazKapasite, AtananOgrenciSayisi = at });

                            kalanOgrenci -= at;
                        }
                    }

                    // Hala kaldıysa farklı bloktaki boşluklar (yine en büyük boşluk önce)
                    if (kalanOgrenci > 0)
                    {
                        foreach (var sa in slotAssignedSalons.OrderByDescending(s => s.OverbookingKapasite - s.Atanan))
                        {
                            if (kalanOgrenci <= 0) break;
                            int bos = sa.OverbookingKapasite - sa.Atanan;
                            if (bos > 0)
                            {
                                int at = Math.Min(bos, kalanOgrenci);
                                sa.Atanan += at;

                                var mevcut = subeSalonlari.FirstOrDefault(x => x.SalonKodu == sa.SalonKodu);
                                if (mevcut != null) mevcut.AtananOgrenciSayisi += at;
                                else subeSalonlari.Add(new SalonAtama { SalonKodu = sa.SalonKodu, SalonKapasitesi = sa.BazKapasite, AtananOgrenciSayisi = at });

                                kalanOgrenci -= at;
                            }
                        }
                    }
                }

                if (kalanOgrenci > 0)
                {
                    string salonTipiNot = uygulamaliDers ? " (uygulamalı salon yetersiz)" : "";
                    Uyarilar.Add($"⚠ '{sube.DersAdi}' ({sube.Sube}) ({grup.Bolum}) — " +
                                 $"{kalanOgrenci} öğrenci salon bulunamadığından açıkta kaldı!{salonTipiNot}");
                }

                sonuc.Add(new SubeSalonAtama
                {
                    Ders = sube,
                    Salonlar = subeSalonlari
                });
            }

            // ═══════════════════════════════════════
            // SANAL HAVUZ AÇILIMI
            // Sanal havuz dersine atanan salon listesi, havuzun asıl (çıkarılmış)
            // alt şubelerinin HER BİRİNE ayrı SubeSalonAtama olarak kopyalanır.
            // Böylece çıktılarda her bölüm kendi adına o salon(lar)a atanmış görünür.
            // ═══════════════════════════════════════
            if (havuzAltSubeleri.Count > 0)
            {
                var acilmisSonuc = new List<SubeSalonAtama>();

                foreach (var ssa in sonuc)
                {
                    if (havuzAltSubeleri.TryGetValue(ssa.Ders, out var altSubeler))
                    {
                        foreach (var (altSube, _) in altSubeler)
                        {
                            acilmisSonuc.Add(new SubeSalonAtama
                            {
                                Ders = altSube,
                                // Derin kopya: satırlar grid üzerinde birbirinden bağımsız düzenlenebilsin
                                Salonlar = ssa.Salonlar.Select(s => new SalonAtama
                                {
                                    SalonKodu = s.SalonKodu,
                                    SalonKapasitesi = s.SalonKapasitesi,
                                    AtananOgrenciSayisi = s.AtananOgrenciSayisi
                                }).ToList()
                            });
                        }
                    }
                    else
                    {
                        acilmisSonuc.Add(ssa);
                    }
                }

                return acilmisSonuc;
            }

            return sonuc;
        }

        // Slot içi kullanılan salon bilgisi
        private class SlotAssignedSalon
        {
            public string SalonKodu { get; set; }
            public int BazKapasite { get; set; }
            public int OverbookingKapasite { get; set; }
            public int Atanan { get; set; }
        }

        // ═══════════════════════════════════════════════════
        // ROUND-ROBIN SLOT SIRALAMA
        // ═══════════════════════════════════════════════════

        private List<int> SlotlariDengeliSirala()
        {
            var gunSlotGruplari = new List<(string GunKey, int BaslangicIdx, int SlotSayisi, int SinavSayisi)>();
            foreach (var gun in _gunler)
            {
                string gunKey = gun.ToString("yyyy-MM-dd");
                int bas = _gunBaslangicIdx[gunKey];
                int slotSay = _gunSlotSayisi[gunKey];
                int sinavSay = _gunSinavSayisi.ContainsKey(gunKey) ? _gunSinavSayisi[gunKey] : 0;
                gunSlotGruplari.Add((gunKey, bas, slotSay, sinavSay));
            }

            var sirali = gunSlotGruplari.OrderBy(x => x.SinavSayisi).ThenBy(x => x.BaslangicIdx).ToList();
            var result = new List<int>();
            foreach (var gun in sirali)
                for (int s = 0; s < gun.SlotSayisi; s++)
                {
                    int idx = gun.BaslangicIdx + s;
                    if (idx >= _tumSlotlar.Count) continue;
                    // KURAL 3: 17:00 taşırma slotu normal sıraya girmez — sadece
                    // son çare turunda (Tur 2) kullanılır. Aksi halde erken dolar
                    // ve "kurtarıcı slot" işlevini kaybeder.
                    if (TasmaSlotuMu(_tumSlotlar[idx])) continue;
                    result.Add(idx);
                }
            return result;
        }

        /// <summary>
        /// KURAL 3 — Sadece 17:00 taşırma slotlarını, sınav sayısı en az olan günden
        /// başlayarak döndürür. Bu slotlarda kapasite/çakışma sınırı yoktur;
        /// yine de yükün günlere dengeli yayılması için sıralama korunur.
        /// </summary>
        private List<int> TasmaSlotlariniSirala()
        {
            var liste = new List<(int Idx, int SinavSayisi)>();
            for (int i = 0; i < _tumSlotlar.Count; i++)
            {
                if (!TasmaSlotuMu(_tumSlotlar[i])) continue;
                string gunKey = _tumSlotlar[i].Split('|')[0];
                liste.Add((i, _gunSinavSayisi.TryGetValue(gunKey, out int n) ? n : 0));
            }
            return liste.OrderBy(x => x.SinavSayisi).ThenBy(x => x.Idx).Select(x => x.Idx).ToList();
        }

        // ═══════════════════════════════════════════════════
        // ÇAKIŞMA & SPREAD KONTROLLERİ
        // ═══════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════
        // SINIF SEVİYESİNE GÖRE SAAT KISITI
        // ═══════════════════════════════════════════════════
        private bool SaatSinifaUygunMu(string slotKey, int sinifSeviyesi)
        {
            // slotKey formatı örneğin: "2026-07-07|09:00" şeklindedir. Sadece saati alıyoruz.
            // Kural tek kaynaktan yönetilsin diye SablonDogrulayici'ye devredildi
            // (MainForm şablon düzenlemelerinde de aynı doğrulama kullanılır).
            return SablonDogrulayici.SaatSinifaUygunMu(slotKey.Split('|')[1], sinifSeviyesi);
        }

        private bool BolumSinifCakismasiVar(string slotKey, string bolum, int sinifSeviyesi)
        {
            if (!_slotBolumSinifMap.ContainsKey(slotKey)) return false;
            string aranan = $"{bolum.ToUpperInvariant().Trim()}|{sinifSeviyesi}";
            return _slotBolumSinifMap[slotKey].Contains(aranan);
        }

        // ═══════════════════════════════════════════════════
        // KURAL 3 — 17:00 TAŞIRMA SLOTU
        // ═══════════════════════════════════════════════════

        /// <summary>Bu slot limitsiz taşırma slotu mu? (kapasite/çakışma kontrolleri uygulanmaz)</summary>
        private static bool TasmaSlotuMu(string slotKey)
            => SablonDogrulayici.TasmaSaatiMi(slotKey.Split('|')[1]);

        // ═══════════════════════════════════════════════════
        // KURAL 1 — BÖLÜM İÇİ ÇAKIŞMA (HARD CONSTRAINT)
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Bu slotta, bu bölümden BAŞKA bir ders var mı?
        /// TRUE dönerse atama REDDEDİLİR ve çağıran döngü başka saate geçer.
        /// Sınıf seviyesine BAKILMAZ — Bilgisayar 2 ile Bilgisayar 3 aynı saatte olamaz.
        /// İstisnalar:
        ///   • Aynı ders adı (aynı dersin başka şubesi / ortak ders) → çakışma DEĞİL.
        ///   • 17:00 taşırma slotu (Kural 3) → çakışma kontrolü YOK.
        /// </summary>
        private bool BolumCakismasiVar(string slotKey, string bolum, string dersAdi)
        {
            // KURAL 3: taşırma slotunda bölüm çakışması serbesttir
            if (TasmaSlotuMu(slotKey)) return false;

            if (!_slotBolumDersMap.TryGetValue(slotKey, out var bolumMap)) return false;

            string bolumKey = (bolum ?? "").ToUpperInvariant().Trim();
            if (!bolumMap.TryGetValue(bolumKey, out var mevcutDersler)) return false;
            if (mevcutDersler.Count == 0) return false;

            string dersKey = NormalizeDersAdi(dersAdi);

            // Slotta bu bölümden, bu dersten FARKLI en az bir ders varsa → ÇAKIŞMA
            return mevcutDersler.Any(d => d != dersKey);
        }

        /// <summary>
        /// Bir ortak ders paketinin KENDİ İÇİNDE aynı bölümü iki farklı derse
        /// bağlaması durumunu yakalar (paket toplu atandığı için harita henüz güncel değildir).
        /// </summary>
        private bool PaketIcindeBolumCakismasiVar(OrtakDersPaketi paket)
        {
            var gorulen = new Dictionary<string, string>();
            foreach (var g in paket.BolumGruplari)
            {
                string b = (g.Bolum ?? "").ToUpperInvariant().Trim();
                string d = NormalizeDersAdi(g.DersAdi);
                if (gorulen.TryGetValue(b, out string oncekiDers) && oncekiDers != d)
                    return true;
                gorulen[b] = d;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════
        // İSTİSNA KURALI — ORTAK DERSTE ELM/MEK KAYDIRMA
        // Ortak sınav tek slota (kontenjan/salon sorunu nedeniyle) yerleşemezse,
        // esneme payı olarak YALNIZCA Elektrik-Elektronik (ELM) ve Mekatronik (MEK)
        // bölümleri BİRLİKTE başka bir slota kaydırılır; diğer bölümler birlikte kalır.
        //
        // KAYDIRMA HEDEFİ ÖNCELİĞİ:
        //   1) BAŞKA BİR GÜN (kullanıcı kuralı: "beraber başka günlere erteleme")
        //   2) Bulunamazsa aynı gün içinde FARKLI bir saat
        //
        // NOT: Salon dağıtım aşamasında (GunlukSalonDagitici) bu iki bölümün
        // sınavlarına T4 binası salonları öncelikli olarak verilir.
        // (ESKİ KURAL: Mekatronik + Makina idi.)
        // ═══════════════════════════════════════════════════

        private static bool ElmMekBolumuMu(string bolum)
        {
            string b = (bolum ?? "").ToUpperInvariant().Trim();
            return b.Contains("MEKATRON") || b.Contains("ELEKTR")
                || b == "MEK" || b == "ELM" || b == "EEM";
        }

        private bool ElmMekKaydirarakAta(OrtakDersPaketi paket,
                                         Dictionary<string, List<SubeGrubu>> slotAtamalari,
                                         bool paketUygulamali)
        {
            var elmMek = paket.BolumGruplari.Where(g => ElmMekBolumuMu(g.Bolum)).ToList();
            var diger = paket.BolumGruplari.Where(g => !ElmMekBolumuMu(g.Bolum)).ToList();

            // Kaydırılacak ELM/MEK yoksa veya paket zaten sadece ELM/MEK'ten oluşuyorsa istisna uygulanamaz
            if (elmMek.Count == 0 || diger.Count == 0) return false;

            int digerYuk = diger.Sum(g => g.ToplamOgrenci);
            int elmMekYuk = elmMek.Sum(g => g.ToplamOgrenci);

            bool GrupUygunMu(string slotKey, int slotIdx, List<SubeGrubu> gruplar, bool kati)
            {
                foreach (var g in gruplar)
                {
                    if (kati && !SaatSinifaUygunMu(slotKey, g.SinifSeviyesi)) return false;
                    if (BolumCakismasiVar(slotKey, g.Bolum, g.DersAdi)) return false;       // KURAL 1 asla esnetilmez
                    if (kati && DinlenmeMolasiIhlali(slotIdx, g.Bolum, g.SinifSeviyesi)) return false;
                }
                return true;
            }

            // Ana grup (diğer bölümler) için slot ara — önce katı, sonra esnek kurallarla
            for (int tur = 0; tur < 2; tur++)
            {
                foreach (var slotIdx in SlotlariDengeliSirala())
                {
                    string slotKey = _tumSlotlar[slotIdx];
                    if (TasmaSlotuMu(slotKey)) continue;
                    if (SlotKapasiteYetersiz(slotKey, paketUygulamali, digerYuk)) continue;
                    if (!GrupUygunMu(slotKey, slotIdx, diger, tur == 0)) continue;

                    string anaGun = slotKey.Split('|')[0];

                    // ELM/MEK için kaydırma slotu ara.
                    //   gunTuru 0 → BAŞKA BİR GÜN (öncelikli hedef)
                    //   gunTuru 1 → aynı gün, FARKLI SAAT (fallback)
                    // Her gün tercihi kendi içinde önce KATI (mTur 0), sonra ESNEK (mTur 1) denenir.
                    for (int gunTuru = 0; gunTuru < 2; gunTuru++)
                    for (int mTur = 0; mTur < 2; mTur++)
                    {
                        foreach (var mIdx in SlotlariDengeliSirala())
                        {
                            string mKey = _tumSlotlar[mIdx];
                            if (mKey == slotKey) continue;                 // farklı slot şartı
                            string mGun = mKey.Split('|')[0];
                            if (gunTuru == 0 && mGun == anaGun) continue;  // 1. tercih: BAŞKA GÜN
                            if (gunTuru == 1 && mGun != anaGun) continue;  // 2. tercih: aynı gün farklı saat
                            if (TasmaSlotuMu(mKey)) continue;
                            if (SlotKapasiteYetersiz(mKey, paketUygulamali, elmMekYuk)) continue;
                            if (!GrupUygunMu(mKey, mIdx, elmMek, mTur == 0)) continue;

                            // ── ATAMA: diğerleri ana slota, ELM/MEK kaydırılmış slota ──
                            if (!slotAtamalari.ContainsKey(slotKey))
                                slotAtamalari[slotKey] = new List<SubeGrubu>();
                            foreach (var g in diger)
                            {
                                slotAtamalari[slotKey].Add(g);
                                TakvimHaritalariniGuncelle(slotKey, slotIdx, g);
                            }

                            if (!slotAtamalari.ContainsKey(mKey))
                                slotAtamalari[mKey] = new List<SubeGrubu>();
                            foreach (var g in elmMek)
                            {
                                slotAtamalari[mKey].Add(g);
                                TakvimHaritalariniGuncelle(mKey, mIdx, g);
                            }

                            string elmMekAdlar = string.Join(", ", elmMek.Select(g => g.Bolum).Distinct());
                            Uyarilar.Add($"ℹ Ortak Ders '{paket.OrijinalDersAdi}' — kontenjan/salon sorunu nedeniyle " +
                                         $"İSTİSNA KURALI uygulandı: yalnızca {elmMekAdlar} şubeleri BİRLİKTE " +
                                         $"{mKey.Replace('|', ' ')} slotuna ertelendi ({(mGun == anaGun ? "aynı gün farklı saat" : "farklı gün")}); " +
                                         $"diğer bölümler {slotKey.Replace('|', ' ')} slotunda aynı anda sınava girecek.");
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool OrtakDersCakismasiVar(string slotKey, string dersAdi)
        {
            string n = dersAdi.ToUpperInvariant().Trim();
            if (!_ortakDersler.Contains(n)) return false;
            return _slotDersMap.ContainsKey(slotKey) && _slotDersMap[slotKey].Contains(n);
        }

        private bool DinlenmeMolasiIhlali(int mevcutSlotIdx, string bolum, int sinifSeviyesi)
        {
            // KURAL 3: taşırma slotuna atamada dinlenme molası aranmaz
            if (TasmaSlotuMu(_tumSlotlar[mevcutSlotIdx])) return false;

            // Bölüm + sınıf seviyesi bazında kontrol (öğrenci tek sınıf seviyesinde olduğundan
            // farklı sınıfların sınavları birbirini bloklamamalı)
            string anahtar = $"{bolum.ToUpperInvariant().Trim()}|{sinifSeviyesi}";
            if (!_bolumAtananSlotlar.ContainsKey(anahtar)) return false;

            string mevcutGun = _tumSlotlar[mevcutSlotIdx].Split('|')[0];
            foreach (var onceki in _bolumAtananSlotlar[anahtar])
            {
                // Gün sınırını aşan karşılaştırmaları yok say (dün 15:00 ↔ bugün 09:00 çakışma değildir)
                if (_tumSlotlar[onceki].Split('|')[0] != mevcutGun) continue;
                if (Math.Abs(mevcutSlotIdx - onceki) <= MinDinlenmeMolasi) return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════
        // TAKVİM HARİTA GÜNCELLEME (Aşama 1 için — salon bilgisi yok)
        // ═══════════════════════════════════════════════════

        private void TakvimHaritalariniGuncelle(string slotKey, int slotIdx, SubeGrubu grup)
        {
            if (!_slotBolumSinifMap.ContainsKey(slotKey))
                _slotBolumSinifMap[slotKey] = new HashSet<string>();
            _slotBolumSinifMap[slotKey].Add($"{grup.Bolum.ToUpperInvariant().Trim()}|{grup.SinifSeviyesi}");

            // KURAL 1 — Bölüm → ders adı haritası (sınıf seviyesinden BAĞIMSIZ)
            if (!_slotBolumDersMap.TryGetValue(slotKey, out var bolumMap))
            {
                bolumMap = new Dictionary<string, HashSet<string>>();
                _slotBolumDersMap[slotKey] = bolumMap;
            }
            string bKey = grup.Bolum.ToUpperInvariant().Trim();
            if (!bolumMap.TryGetValue(bKey, out var dersSet))
            {
                dersSet = new HashSet<string>();
                bolumMap[bKey] = dersSet;
            }
            dersSet.Add(NormalizeDersAdi(grup.DersAdi));

            if (!_slotDersMap.ContainsKey(slotKey))
                _slotDersMap[slotKey] = new HashSet<string>();
            _slotDersMap[slotKey].Add(grup.DersAdi.ToUpperInvariant().Trim());

            string bolumSinifAnahtar = $"{grup.Bolum.ToUpperInvariant().Trim()}|{grup.SinifSeviyesi}";
            if (!_bolumAtananSlotlar.ContainsKey(bolumSinifAnahtar))
                _bolumAtananSlotlar[bolumSinifAnahtar] = new List<int>();
            _bolumAtananSlotlar[bolumSinifAnahtar].Add(slotIdx);

            // Slot öğrenci yükünü güncelle (kapasite ön-kontrolü için)
            var yukMap = DersUygulamaliMi(grup.DersAdi) ? _slotUygulamaliOgrenciYuku : _slotNormalOgrenciYuku;
            yukMap[slotKey] = (yukMap.TryGetValue(slotKey, out int mevcutYuk) ? mevcutYuk : 0) + grup.ToplamOgrenci;

            string gunKey = slotKey.Split('|')[0];
            if (_gunSinavSayisi.ContainsKey(gunKey))
                _gunSinavSayisi[gunKey]++;
            else
                _gunSinavSayisi[gunKey] = 1;
        }

        // ═══════════════════════════════════════════════════
        // BLOK KODU ÇIKARMA
        // ═══════════════════════════════════════════════════

        private static string BlokKoduCikar(string salonKodu)
        {
            if (string.IsNullOrEmpty(salonKodu)) return string.Empty;
            int tireIdx = salonKodu.IndexOf('-');
            if (tireIdx > 0) return salonKodu.Substring(0, tireIdx).Trim().ToUpperInvariant();
            return salonKodu.Length >= 2
                ? salonKodu.Substring(0, 2).Trim().ToUpperInvariant()
                : salonKodu.Trim().ToUpperInvariant();
        }
    }

    // ═══════════════════════════════════════════════════
    // İç Yardımcı Sınıflar
    // ═══════════════════════════════════════════════════

    internal class SubeGrubu
    {
        public string DersAdi { get; set; }
        public string Bolum { get; set; }
        public int SinifSeviyesi { get; set; }
        public List<Ders> Subeler { get; set; } = new List<Ders>();
        public int ToplamOgrenci { get; set; }
    }

    internal class SubeSalonAtama
    {
        public Ders Ders { get; set; }
        public List<SalonAtama> Salonlar { get; set; } = new List<SalonAtama>();
    }

    internal class OrtakDersPaketi
    {
        public string NormalizeDersAdi { get; set; }
        public string OrijinalDersAdi { get; set; }
        public List<SubeGrubu> BolumGruplari { get; set; } = new List<SubeGrubu>();
        public int ToplamOgrenci { get; set; }
    }
}
