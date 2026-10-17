#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Algorithm
{
    /// <summary>
    /// ADIM 2+ — Otomatik Şablon Dağıtım Algoritması.
    /// ──────────────────────────────────────────────────────────
    /// Dersleri (Adım 1'de çekilenler) seçilen takvimin (gün+saat) slotlarına KURALLARA göre
    /// dağıtır. Salon ataması YAPMAZ — sadece hangi dersin hangi gün+saatte, hangi bölümde
    /// olacağını belirler. Sonuç, dağıtım şablonuna (Sınav Haftası Dağıtım Şablonu) yazılır;
    /// kullanıcı beğenmezse Excel'i indirip elle düzenleyip geri yükleyebilir.
    ///
    /// KURALLAR:
    ///  1) Ortak dersler (kodu bölüm önekiyle başlamayanlar) ÖNCE yerleştirilir ve istisna
    ///     dışında tüm bölümlerinde AYNI gün+saatte olur.
    ///  2) Bir ortak dersin toplam öğrencisi tek oturumda NORMAL salonlara sığmazsa,
    ///     Elektrik-Elektronik (ELM/EEM) ve Mekatronik (MEK) bölümlerine düşen kısım
    ///     BİRLİKTE ayrılıp farklı bir slota (tercihen başka bir güne) konur.
    ///  3) Aynı (gün+saat) slotunda bir bölümden en fazla bir sınav olur (sınıf farketmeksizin).
    ///  4) SAAT-SINIF TERCİHİ (SOFT CONSTRAINT): 09:00→3. sınıf, 11:00→1. sınıf,
    ///     13:00→4. VEYA 0. sınıf (eşit öncelikli), 15:00→2. sınıf.
    ///     Tercih edilen saat ÖNCELİKLİDİR; slot yükü dengeleme (denge kuralı) yalnızca
    ///     tercih saatinin içinde gün seçerken devreye girer. Tercih saatinde hiç uygun
    ///     slot kalmadıysa (bölüm çakışması / günlük 2 sınav limiti / kapasite) ders
    ///     17:00 dahil diğer saatlere taşar ve orada denge kuralı belirleyici olur.
    /// </summary>
    public class SablonDagitimAlgoritmasi
    {
        private static readonly string[] Bolumler = { "BİL", "EEM", "INS", "MAK", "MEK", "MEM" };
        private static readonly HashSet<string> BolumKodOnekleri =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BIL", "ELM", "INS", "MAK", "MEK", "MEM" };

        // ═══════════════════════════════════════════════════════════════
        // SAAT ↔ SINIF SEVİYESİ TERCİHİ (SOFT CONSTRAINT)
        // ───────────────────────────────────────────────────────────────
        //   09:00 → 3. Sınıflar
        //   11:00 → 1. Sınıflar
        //   13:00 → 4. Sınıflar VEYA 0. Sınıflar (hazırlık/seviyesiz) — eşit öncelikli
        //   15:00 → 2. Sınıflar
        //
        // ESNEKTİR: tercih edilen saatte yer yoksa (bölüm çakışması, günlük sınav
        // limiti veya kapasite) ders diğer saatlere taşar. Amaç, uyum oranını
        // mümkün olan en yüksek seviyede tutmaktır.
        // ═══════════════════════════════════════════════════════════════
        private static readonly Dictionary<int, string> SinifSaat = new Dictionary<int, string>
        {
            { 3, "09:00" },
            { 1, "11:00" },
            { 4, "13:00" },
            { 0, "13:00" },   // 0. sınıf (hazırlık vb.) — 4. sınıfla EŞİT öncelikli
            { 2, "15:00" }
        };
        // Faz-2 işleme sırası (tercih edilen saatteki sınıflar önce).
        // 4 ve 0 aynı saati paylaştığından aynı öncelik değerini alır.
        private static readonly Dictionary<int, int> SinifOncelik = new Dictionary<int, int>
        {
            { 3, 0 }, { 1, 1 }, { 4, 2 }, { 0, 2 }, { 2, 3 }
        };

        // Tercih uyum istatistiği (rapor amaçlı)
        private int _tercihUyan;
        private int _tercihToplam;

        // Yayılma (gün aralığı) uyum istatistiği — çok haftalı dönemlerde raporlanır
        private int _yayilmaUyan;
        private int _yayilmaToplam;

        public List<string> Uyarilar { get; } = new List<string>();

        private int _normalKapasite;
        private List<DateTime> _gunler;
        private Dictionary<DateTime, List<string>> _takvim;
        private List<string> _tumSaatler;
        private readonly HashSet<string> _dolu = new HashSet<string>();                 // "dept|gun|saat"
        private readonly Dictionary<string, int> _slotYuk = new Dictionary<string, int>(); // "gun|saat" → toplam öğrenci

        // GÜNLÜK SINAV LİMİTİ (KISIT): aynı bölüm + aynı sınıf seviyesine
        // bir günde EN FAZLA 2 sınav atanabilir. "dept|sinif|gun" → sınav sayısı
        private readonly Dictionary<string, int> _gunlukSinavSayisi = new Dictionary<string, int>();
        private const int GunlukSinavLimiti = 2;

        private static string GunlukAnahtar(string dept, int sinif, DateTime g)
            => $"{dept}|{sinif}|{g:yyyy-MM-dd}";

        /// <summary>Bu bölüm+sınıf bu günde limite (2) ulaştı mı? TRUE → o güne atama YASAK.</summary>
        private bool GunlukLimitDolu(string dept, int sinif, DateTime g)
            => _gunlukSinavSayisi.TryGetValue(GunlukAnahtar(dept, sinif, g), out int v) && v >= GunlukSinavLimiti;

        // ═══════════════════════════════════════════════════════════════
        // YAYILMA KURALI (SOFT CONSTRAINT) — ÇOK HAFTALI DÖNEMLER İÇİN
        // ───────────────────────────────────────────────────────────────
        // Final dönemi (2 hafta / 10 gün) seçildiğinde sınavların ilk haftaya
        // yığılmaması için: aynı BÖLÜM + SINIF SEVİYESİnin iki sınavı arasında
        // en az MinGunAraligi kadar boş gün bırakılmaya çalışılır.
        //
        // ESNEKTİR — yalnızca slot tercih sırasını etkiler, slotu YASAKLAMAZ.
        // Kısıt sağlanamıyorsa ders yine de yerleşir (yerleşememe riski yok).
        //
        // Takvim 1 haftalık (≤7 gün) ise kural devre dışıdır: vize haftasında
        // günler zaten sıkışıktır, aralık zorlaması yerleşimi bozar.
        // ═══════════════════════════════════════════════════════════════
        private const int MinGunAraligi = 2;     // 2 gün fark = arada 1 boş gün
        // Vize gün sayısını (7) aşan takvimler çok haftalı kabul edilir.
        // Final = 10 gün (2 × Pzt-Cum) → kural etkin;  Vize = 7 gün → kural kapalı.
        private static readonly int YayilmaEsigi = SinavTipi.VizeGunSayisi;

        /// <summary>Bölüm+sınıf → o bölüm/sınıfa sınav atanmış günler.</summary>
        private readonly Dictionary<string, List<DateTime>> _sinifSinavGunleri =
            new Dictionary<string, List<DateTime>>();

        /// <summary>Çok haftalı dönem mi? (Takvim gün sayısına göre otomatik belirlenir.)</summary>
        private bool _yayilmaAktif;

        private static string SinifAnahtar(string dept, int sinif) => $"{dept}|{sinif}";

        /// <summary>
        /// Bu bölüm+sınıfın mevcut sınavlarına olan EN YAKIN gün mesafesi.
        /// Hiç sınavı yoksa int.MaxValue (kısıt yok) döner.
        /// </summary>
        private int EnYakinSinavMesafesi(string dept, int sinif, DateTime g)
        {
            if (!_sinifSinavGunleri.TryGetValue(SinifAnahtar(dept, sinif), out var gunler) || gunler.Count == 0)
                return int.MaxValue;

            int enYakin = int.MaxValue;
            foreach (var mevcut in gunler)
            {
                int fark = Math.Abs((g.Date - mevcut.Date).Days);
                if (fark < enYakin) enYakin = fark;
            }
            return enYakin;
        }

        /// <summary>
        /// Yayılma cezası: aynı sınıfın başka bir sınavına ne kadar yakınsa o kadar yüksek.
        /// 0 = kural sağlanıyor (yeterince uzak) veya kural kapalı.
        /// Slot seçiminde birincil sıralama ölçütü olarak kullanılır.
        /// </summary>
        private int YayilmaCezasi(string dept, int sinif, DateTime g)
        {
            if (!_yayilmaAktif) return 0;
            int mesafe = EnYakinSinavMesafesi(dept, sinif, g);
            if (mesafe == int.MaxValue || mesafe >= MinGunAraligi) return 0;
            return MinGunAraligi - mesafe;   // 0 gün fark → 2, 1 gün fark → 1
        }

        private class Birim
        {
            public string Bolum;   // BİL/EEM/INS/MAK/MEK/MEM
            public string Kod;
            public string DersAdi;
            public int Sinif;
            public bool Ortak;
            public Dictionary<string, int> Harfler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public int Toplam => Harfler.Values.Sum();
        }

        // MANUEL ORTAK DERS SEÇİMİ: kullanıcı seçim yaptıysa otomatik (kod önekine göre)
        // tespit DEVRE DIŞI kalır; yalnızca bu setteki ders kodları ortak sayılır.
        private HashSet<string> _manuelOrtakKodlar;

        // HATA ORANI (bölüm bazlı, %): şablon EFEKTİF kontenjanla (sınava gelmeyecek
        // öğrenci düşülmüş) kurulur; salon dağıtımı şablonu esas aldığından tüm
        // kontenjan hesapları otomatik olarak bu değerlere göre yapılır.
        private Dictionary<string, double> _bolumHataOranlari;

        /// <summary>Dersleri takvim slotlarına dağıtır; salonsuz SinavAtama listesi döndürür.</summary>
        /// <param name="manuelOrtakKodlar">null → otomatik tespit (kod öneki); değilse SADECE bu kodlar ortak kabul edilir.</param>
        /// <param name="bolumHataOranlari">Bölüm → sınava gelmeyecek öğrenci yüzdesi (0-100).
        /// Şablondaki tüm kontenjanlar bu oranlar düşülerek (efektif) yazılır.</param>
        public List<SinavAtama> Dagit(List<Ders> dersler, Dictionary<DateTime, List<string>> takvim, int normalKapasite,
                                      IEnumerable<string> manuelOrtakKodlar = null,
                                      Dictionary<string, double> bolumHataOranlari = null)
        {
            Uyarilar.Clear();
            _dolu.Clear();
            _slotYuk.Clear();
            _gunlukSinavSayisi.Clear();
            _sinifSinavGunleri.Clear();
            _tercihUyan = 0;
            _tercihToplam = 0;
            _yayilmaUyan = 0;
            _yayilmaToplam = 0;
            _manuelOrtakKodlar = manuelOrtakKodlar == null
                ? null
                : new HashSet<string>(manuelOrtakKodlar.Select(k => (k ?? "").Trim().ToUpperInvariant()),
                                      StringComparer.OrdinalIgnoreCase);
            _bolumHataOranlari = bolumHataOranlari;

            if (_bolumHataOranlari != null && _bolumHataOranlari.Any(kv => kv.Value > 0))
                Uyarilar.Add("ℹ Hata oranları uygulandı — şablon EFEKTİF kontenjanlarla oluşturuldu: " +
                             string.Join(", ", _bolumHataOranlari.Where(kv => kv.Value > 0)
                                                                 .Select(kv => $"{kv.Key} %{kv.Value:0.#}")));
            _normalKapasite = normalKapasite;
            _takvim = takvim ?? new Dictionary<DateTime, List<string>>();
            _gunler = _takvim.Keys.OrderBy(d => d).ToList();
            _tumSaatler = _takvim.Values.SelectMany(v => v).Distinct().OrderBy(s => s).ToList();

            if (_gunler.Count == 0 || _tumSaatler.Count == 0)
            {
                Uyarilar.Add("Takvimde sınav günü/saati yok — dağıtım yapılamadı.");
                return new List<SinavAtama>();
            }
            if (_normalKapasite <= 0)
                Uyarilar.Add("Normal salon kapasitesi 0 — kapasite kontrolü atlandı (Kural 2 uygulanmayabilir).");

            // ── TOPLAM GÜN SAYISI → YAYILMA KURALI ──
            // Takvim 7 günden uzunsa (Final = 2 hafta) sınavların ilk haftaya
            // yığılmaması için gün aralığı kuralı devreye girer.
            _yayilmaAktif = _gunler.Count > YayilmaEsigi;
            if (_yayilmaAktif)
            {
                int hafta = SinavTipi.HaftaSayisiHesapla(_gunler);
                Uyarilar.Add($"ℹ Çok haftalı dönem algılandı: {_gunler.Count} sınav günü / {hafta} hafta " +
                             $"(hafta sonları boş). " +
                             $"Yayılma kuralı etkin — aynı bölüm+sınıfın sınavları arasında " +
                             $"en az {MinGunAraligi - 1} boş gün bırakılmaya çalışılacak.");
            }

            var birimler = BirimleriKur(dersler);
            var sonuc = new List<SinavAtama>();

            // Yerleştirmeyi kaydetmek için birim→(gun,saat)
            var atananlar = new List<(Birim B, DateTime Gun, string Saat)>();

            void Yerlestir(Birim u, DateTime g, string s)
            {
                _dolu.Add(Anahtar(u.Bolum, g, s));
                string sk = SlotAnahtar(g, s);
                _slotYuk[sk] = (_slotYuk.TryGetValue(sk, out int v) ? v : 0) + u.Toplam;
                // Günlük sınav limiti sayacı (bölüm + sınıf + gün)
                string gk = GunlukAnahtar(u.Bolum, u.Sinif, g);
                _gunlukSinavSayisi[gk] = (_gunlukSinavSayisi.TryGetValue(gk, out int gv) ? gv : 0) + 1;

                // SOFT CONSTRAINT istatistiği: saat-sınıf tercihine uyuldu mu?
                if (SinifSaat.TryGetValue(u.Sinif, out var trc) && _tumSaatler.Contains(trc))
                {
                    _tercihToplam++;
                    if (s == trc) _tercihUyan++;
                }

                // YAYILMA KURALI istatistiği + kayıt (bölüm+sınıfın sınav günleri)
                if (_yayilmaAktif)
                {
                    int mesafe = EnYakinSinavMesafesi(u.Bolum, u.Sinif, g);
                    if (mesafe != int.MaxValue)
                    {
                        _yayilmaToplam++;
                        if (mesafe >= MinGunAraligi) _yayilmaUyan++;
                    }
                }
                string sak = SinifAnahtar(u.Bolum, u.Sinif);
                if (!_sinifSinavGunleri.TryGetValue(sak, out var sgList))
                {
                    sgList = new List<DateTime>();
                    _sinifSinavGunleri[sak] = sgList;
                }
                sgList.Add(g.Date);

                atananlar.Add((u, g, s));
            }

            // ── FAZ 1: ORTAK DERSLER (koda göre, büyükten küçüğe) ──
            var ortakKodlar = birimler.Where(b => b.Ortak)
                .GroupBy(b => b.Kod)
                .OrderByDescending(gr => gr.Sum(x => x.Toplam))
                .ToList();

            foreach (var grup in ortakKodlar)
            {
                var units = grup.ToList();
                int tot = units.Sum(u => u.Toplam);
                string pref = SinifSaat.TryGetValue(units[0].Sinif, out var p) ? p : null;

                if (_normalKapasite <= 0 || tot <= _normalKapasite)
                {
                    var slot = OrtakSlotBul(units, pref, tot, _normalKapasite > 0)
                            ?? OrtakSlotBul(units, pref, tot, false);
                    if (slot != null)
                        foreach (var u in units) Yerlestir(u, slot.Value.Gun, slot.Value.Saat);
                    else
                        Uyarilar.Add($"Ortak ders {grup.Key} ({units[0].DersAdi}): tüm bölümlere uygun ortak slot bulunamadı.");
                }
                else
                {
                    // KURAL 2 — ELM (Elektrik-Elektronik) + MEK (Mekatronik) BİRLİKTE ayrılır
                    // (ESKİ KURAL: MEK + MAK idi.)
                    var ana = units.Where(u => !ElmMekMi(u.Bolum)).ToList();
                    var ayri = units.Where(u => ElmMekMi(u.Bolum)).ToList();
                    int anaTot = ana.Sum(u => u.Toplam);

                    (DateTime Gun, string Saat)? s1 = null;
                    if (ana.Count > 0)
                    {
                        s1 = OrtakSlotBul(ana, pref, anaTot, _normalKapasite > 0)
                          ?? OrtakSlotBul(ana, pref, anaTot, false);
                        if (s1 != null) foreach (var u in ana) Yerlestir(u, s1.Value.Gun, s1.Value.Saat);
                    }

                    (DateTime Gun, string Saat)? s2 = null;
                    if (ayri.Count > 0)
                    {
                        // ELM+MEK BİRLİKTE ertelenir. ÖNCELİK: ana slottan FARKLI BİR GÜN.
                        // Uygun başka gün yoksa aynı günün farklı bir saatine düşülür.
                        // tur 0 → farklı gün zorunlu, tur 1 → aynı gün farklı saat de kabul
                        for (int tur = 0; tur < 2 && s2 == null; tur++)
                        {
                            foreach (var (g, s) in SlotSirasi(pref))
                            {
                                if (s1 != null && g == s1.Value.Gun && s == s1.Value.Saat) continue;
                                if (tur == 0 && s1 != null && g == s1.Value.Gun) continue;       // FARKLI GÜN şartı
                                if (ayri.Any(u => GunlukLimitDolu(u.Bolum, u.Sinif, g))) continue; // günlük limit
                                if (ayri.All(u => !_dolu.Contains(Anahtar(u.Bolum, g, s)))) { s2 = (g, s); break; }
                            }
                        }
                        if (s2 != null) foreach (var u in ayri) Yerlestir(u, s2.Value.Gun, s2.Value.Saat);
                    }

                    Uyarilar.Add($"Ortak ders {grup.Key} ({units[0].DersAdi}) toplam {tot} > normal kapasite {_normalKapasite}: " +
                                 $"ELM+MEK ayrıldı (ana: {(s1 != null ? $"{s1.Value.Gun:dd.MM} {s1.Value.Saat}" : "?")}, " +
                                 $"ELM-MEK: {(s2 != null ? $"{s2.Value.Gun:dd.MM} {s2.Value.Saat}" : "?")}).");
                }
            }

            // ── FAZ 2: BÖLÜM DERSLERİ ──
            var bolumDersleri = birimler.Where(b => !b.Ortak)
                .OrderBy(b => SinifOncelik.TryGetValue(b.Sinif, out int o) ? o : 9)
                .ThenByDescending(b => b.Toplam)
                .ToList();

            foreach (var u in bolumDersleri)
            {
                string pref = SinifSaat.TryGetValue(u.Sinif, out var p) ? p : null;
                (DateTime Gun, string Saat)? yer = null;

                // ── SAAT TERCİHİ (SOFT CONSTRAINT) + DENGE KURALI ─────────
                // ÖNCELİK SIRASI (kullanıcı kararı: tercih öncelikli, denge ikincil):
                //   1) Dersin sınıf seviyesine karşılık gelen TERCİH SAATİ denenir
                //      (09→3, 11→1, 13→4 veya 0, 15→2). Bu saatteki günler arasında
                //      yük dengesi gözetilerek en az yüklü gün seçilir.
                //   2) Tercih saatinde hiç uygun slot yoksa (bölüm çakışması, günlük
                //      sınav limiti veya kapasite) TÜM saatlere açılır; burada denge
                //      kuralı belirleyicidir.
                // Her aşamada önce kapasite kontrollü, sonra kontrolsüz denenir.
                //
                // İSTİSNA: Ortak dersler (FAZ 1) bu akıştan geçmez — onlar zaten
                // SlotSirasi(pref) ile tercih saatini ilk sırada dener.
                bool tercihEdilebilir = !string.IsNullOrEmpty(pref) && _tumSaatler.Contains(pref);

                // (yalnizcaTercihSaati, kapasiteKontrolu) aşamaları.
                // NOT: Kapasite, tercihten DAHA GÜÇLÜ bir kısıttır — bu yüzden
                // "tercih saati (kapasite zorlanır)" aşaması, "tüm saatler (kapasite
                // uygun)" aşamasından SONRA gelir. Böylece tercihe uymak uğruna
                // salonlar taşırılmaz.
                var asamalar = new List<(bool SadeceTercih, bool CapChk)>();
                if (tercihEdilebilir)
                    asamalar.Add((true, true));    // 1) tercih saati + kapasite uygun
                asamalar.Add((false, true));       // 2) tüm saatler + kapasite uygun
                if (tercihEdilebilir)
                    asamalar.Add((true, false));   // 3) tercih saati, kapasite zorlanır
                asamalar.Add((false, false));      // 4) tüm saatler, kapasite zorlanır

                foreach (var (sadeceTercih, capChk) in asamalar)
                {
                    int enIyiCeza = int.MaxValue;
                    int enAzYuk = int.MaxValue;
                    int sira = 0, enIyiSira = int.MaxValue;

                    foreach (var (g, s) in SlotSirasi(pref))
                    {
                        sira++;
                        // 1. aşama: yalnızca tercih edilen saatteki slotlar değerlendirilir
                        if (sadeceTercih && s != pref) continue;
                        if (_dolu.Contains(Anahtar(u.Bolum, g, s))) continue;
                        // KISIT: aynı bölüm+sınıfa aynı günde en fazla 2 sınav
                        if (GunlukLimitDolu(u.Bolum, u.Sinif, g)) continue;
                        int yuk = _slotYuk.TryGetValue(SlotAnahtar(g, s), out int y) ? y : 0;
                        if (capChk && _normalKapasite > 0 && yuk + u.Toplam > _normalKapasite)
                            continue;

                        // YAYILMA CEZASI: aynı bölüm+sınıfın diğer sınavlarına yakınlık.
                        // Çok haftalı dönemlerde birincil ölçüt; tek haftada daima 0.
                        int ceza = YayilmaCezasi(u.Bolum, u.Sinif, g);

                        // Sıralama: 1) yayılma cezası az  2) slot yükü az  3) tercih sırası
                        bool dahaIyi =
                            ceza < enIyiCeza ||
                            (ceza == enIyiCeza && yuk < enAzYuk) ||
                            (ceza == enIyiCeza && yuk == enAzYuk && sira < enIyiSira);

                        if (dahaIyi)
                        {
                            enIyiCeza = ceza;
                            enAzYuk = yuk;
                            enIyiSira = sira;
                            yer = (g, s);
                        }
                    }
                    if (yer != null) break;
                }

                if (yer != null) Yerlestir(u, yer.Value.Gun, yer.Value.Saat);
                else Uyarilar.Add($"{u.Bolum} {u.Kod} ({u.DersAdi}): uygun slot bulunamadı — yerleştirilemedi. " +
                                  "(Olası neden: bölüm çakışması veya GÜNLÜK 2 SINAV LİMİTİ; takvime gün/saat ekleyin.)");
            }

            // ── SAAT-SINIF TERCİHİ UYUM RAPORU ──
            if (_tercihToplam > 0)
            {
                double oran = _tercihUyan * 100.0 / _tercihToplam;
                Uyarilar.Add($"ℹ Saat-sınıf tercihi uyumu: {_tercihUyan}/{_tercihToplam} ders (%{oran:0.#}). " +
                             "Tercih: 09:00→3. sınıf, 11:00→1. sınıf, 13:00→4. veya 0. sınıf, 15:00→2. sınıf. " +
                             "Uymayan dersler bölüm çakışması, günlük 2 sınav limiti veya kapasite nedeniyle taşmıştır.");
            }

            // ── YAYILMA KURALI UYUM RAPORU (çok haftalı dönemler) ──
            if (_yayilmaAktif && _yayilmaToplam > 0)
            {
                double yOran = _yayilmaUyan * 100.0 / _yayilmaToplam;
                Uyarilar.Add($"ℹ Yayılma (gün aralığı) uyumu: {_yayilmaUyan}/{_yayilmaToplam} sınav (%{yOran:0.#}) " +
                             $"aynı bölüm+sınıfın diğer sınavlarından en az {MinGunAraligi} gün uzağa yerleşti.");
            }

            // ── SinavAtama listesi üret (her şube ayrı) ──
            foreach (var (b, g, s) in atananlar)
                foreach (var kv in b.Harfler.Where(x => x.Value > 0))
                    sonuc.Add(new SinavAtama
                    {
                        DersAdi = b.DersAdi,
                        BolumAdi = b.Bolum,   // kısaltma (BİL/EEM/...)
                        SinifSeviyesi = b.Sinif,
                        Sube = kv.Key,
                        Kontenjan = kv.Value,
                        Gun = g,
                        Saat = s
                    });

            return sonuc;
        }

        // ── Ortak ders için: tüm birimlerin uygun olduğu (opsiyonel kapasiteli) ilk slot ──
        // Uygunluk = bölüm o slotta boş + GÜNLÜK 2 SINAV LİMİTİ (bölüm+sınıf) aşılmıyor.
        private (DateTime Gun, string Saat)? OrtakSlotBul(List<Birim> units, string pref, int ihtiyac, bool capCheck)
        {
            (DateTime Gun, string Saat)? enIyi = null;
            int enIyiCeza = int.MaxValue;

            foreach (var (g, s) in SlotSirasi(pref))
            {
                if (!units.All(u => !_dolu.Contains(Anahtar(u.Bolum, g, s)))) continue;
                if (units.Any(u => GunlukLimitDolu(u.Bolum, u.Sinif, g))) continue; // KISIT: günde en fazla 2
                if (capCheck && _normalKapasite > 0 &&
                    (_slotYuk.TryGetValue(SlotAnahtar(g, s), out int yuk) ? yuk : 0) + ihtiyac > _normalKapasite)
                    continue;

                // Yayılma kuralı kapalıysa (tek haftalık dönem) ilk uygun slot döner —
                // eski davranış birebir korunur.
                if (!_yayilmaAktif) return (g, s);

                // Çok haftalı dönemde: ortak dersin TÜM bölümleri için toplam yayılma cezası
                // en düşük olan slot seçilir (eşitlikte tercih sırası korunur).
                int ceza = units.Sum(u => YayilmaCezasi(u.Bolum, u.Sinif, g));
                if (ceza == 0) return (g, s);          // kusursuz slot — aramayı uzatma
                if (ceza < enIyiCeza)
                {
                    enIyiCeza = ceza;
                    enIyi = (g, s);
                }
            }
            return enIyi;
        }

        /// <summary>Tercih edilen saat önce olacak şekilde tüm (gün, saat) slotlarını sıralar.</summary>
        private IEnumerable<(DateTime Gun, string Saat)> SlotSirasi(string pref)
        {
            var saatSira = new List<string>();
            if (!string.IsNullOrEmpty(pref) && _tumSaatler.Contains(pref)) saatSira.Add(pref);
            foreach (var s in _tumSaatler) if (s != pref) saatSira.Add(s);

            foreach (var saat in saatSira)
                foreach (var gun in _gunler)
                    if (_takvim.TryGetValue(gun, out var gs) && gs.Contains(saat))
                        yield return (gun, saat);
        }

        private static string Anahtar(string dept, DateTime g, string s) => $"{dept}|{g:yyyy-MM-dd}|{s}";
        private static string SlotAnahtar(DateTime g, string s) => $"{g:yyyy-MM-dd}|{s}";

        // ── Dersleri (kod+bölüm) birimlerine indir; İÖ aynı harf kolonuna toplanır ──
        private List<Birim> BirimleriKur(List<Ders> dersler)
        {
            var map = new Dictionary<(string, string), Birim>();
            foreach (var d in dersler ?? new List<Ders>())
            {
                if (string.IsNullOrWhiteSpace(d.DersKodu)) continue;
                int grup = IlkRakam(d.DersKodu);
                if (grup < 0) continue;

                string bolum = BolumKisalt(d.BolumAdi);
                string kod = d.DersKodu.Trim().ToUpperInvariant();
                var key = (bolum, kod);
                if (!map.TryGetValue(key, out var b))
                {
                    b = new Birim
                    {
                        Bolum = bolum,
                        Kod = kod,
                        DersAdi = d.DersAdi,
                        Sinif = d.SinifSeviyesi,
                        // MANUEL SEÇİM varsa yalnızca o geçerli; yoksa otomatik (kod öneki) tespit
                        Ortak = _manuelOrtakKodlar != null
                            ? _manuelOrtakKodlar.Contains(kod)
                            : !BolumKodOnekleri.Contains(KodOneki(kod))
                    };
                    map[key] = b;
                }
                string harf = HarfKolonu(d.Sube);
                if (!b.Harfler.ContainsKey(harf)) b.Harfler[harf] = 0;
                // HATA ORANI: kontenjan, bölümün "gelmeyecek öğrenci" yüzdesi düşülerek yazılır
                b.Harfler[harf] += Math.Max(0, EfektifKontenjan(d.BolumAdi, d.Kontenjan));
            }
            return map.Values.ToList();
        }

        /// <summary>
        /// Hata oranı düşülmüş EFEKTİF kontenjan.
        /// Örn: 120 kontenjan, %20 hata oranı → 96 öğrenci olarak planlanır.
        /// </summary>
        private int EfektifKontenjan(string bolumAdi, int kontenjan)
        {
            if (kontenjan <= 0 || _bolumHataOranlari == null) return Math.Max(0, kontenjan);

            // Bölüm eşleştirmesi NORMALİZE anahtarla yapılır — "X MÜHENDİSLİĞİ" ile
            // "X MÜHENDİSLİĞİ BÖLÜMÜ" gibi yazım farkları oranın kaçmasına yol açmaz.
            string aranan = BolumOranAnahtari(bolumAdi);
            foreach (var kv in _bolumHataOranlari)
            {
                if (BolumOranAnahtari(kv.Key) != aranan) continue;
                if (kv.Value <= 0 || kv.Value > 100) break;
                return Math.Max(1, (int)Math.Ceiling(kontenjan * (1 - kv.Value / 100.0)));
            }
            return kontenjan;
        }

        /// <summary>Bölüm adını oran eşleştirme anahtarına indirger (harfler, küçük; "bölümü" eki atılır).</summary>
        private static string BolumOranAnahtari(string bolum)
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
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            }
            string s = sb.ToString();
            if (s.EndsWith("bolumu")) s = s.Substring(0, s.Length - 6);
            else if (s.EndsWith("bolum")) s = s.Substring(0, s.Length - 5);
            s = s.Replace("veelektronik", "elektronik").Replace("vemalzeme", "malzeme").Replace("makina", "makine");
            return s;
        }

        private static int IlkRakam(string kod)
        {
            foreach (char c in kod) if (char.IsDigit(c)) return c - '0';
            return -1;
        }

        private static string KodOneki(string kod)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in kod ?? "")
            {
                if (char.IsDigit(c)) break;
                if (char.IsLetter(c)) sb.Append(c);
            }
            return sb.ToString().ToUpperInvariant();
        }

        /// <summary>Şube etiketini A/B/C/D harf kolonuna indirger (eski "INT" → "D").</summary>
        private static string HarfKolonu(string sube)
            => SubeEtiketi.Normalize(sube, SubeEtiketi.DordunculSube);

        /// <summary>
        /// KURAL 2 istisnasına tabi bölümler: Elektrik-Elektronik Mühendisliği (ELM/EEM)
        /// ve Mekatronik Mühendisliği (MEK). Kapasite yetmediğinde bu iki bölüm
        /// BİRLİKTE başka bir slota (tercihen başka güne) ertelenir.
        /// </summary>
        private static bool ElmMekMi(string bolumKisa)
        {
            string b = (bolumKisa ?? "").Trim().ToUpperInvariant();
            return b == "MEK" || b == "EEM" || b == "ELM";
        }

        private static string BolumKisalt(string bolumAdi)
        {
            if (string.IsNullOrWhiteSpace(bolumAdi)) return "?";
            string b = bolumAdi.ToUpperInvariant();
            if (b.Contains("BİLG") || b.Contains("BILG")) return "BİL";
            if (b.Contains("ELEKTR")) return "EEM";
            if (b.Contains("İNŞA") || b.Contains("INSA")) return "INS";
            if (b.Contains("MAKİN") || b.Contains("MAKIN") || b.Contains("MAKİNA") || b.Contains("MAKINA")) return "MAK";
            if (b.Contains("MEKATRON")) return "MEK";
            if (b.Contains("METALURJ") || b.Contains("MALZEME")) return "MEM";
            return b.Trim().Length >= 3 ? b.Trim().Substring(0, 3) : b.Trim();
        }
    }
}
