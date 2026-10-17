#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Algorithm
{
    /// <summary>
    /// Şablon üzerinde tespit edilen tek bir kural ihlalini temsil eder.
    /// İhlaller ENGELLEYİCİ DEĞİLDİR — kullanıcı bilerek kural dışı yerleşim yapabilir,
    /// sadece görsel olarak uyarılır (fakülte gerçeğinde elle istisna yapılabiliyor).
    /// </summary>
    public class SablonIhlal
    {
        /// <summary>İhlalin ilişkili olduğu şablon ataması (şube satırı).</summary>
        public SinavAtama Atama { get; set; }

        /// <summary>Kullanıcıya gösterilecek Türkçe ihlal açıklaması.</summary>
        public string Mesaj { get; set; }
    }

    /// <summary>
    /// Şablon (salonsuz takvim yerleşimi) doğrulayıcısı.
    /// SinavDagitimAlgoritma içindeki private kontrol mantıklarının liste-bazlı,
    /// dışarıdan çağrılabilir karşılığıdır. MainForm hem ilk şablon oluşturmada
    /// hem de her elle düzenlemeden sonra AYNI doğrulamayı bu sınıf üzerinden yapar.
    /// Kontroller: bölüm+sınıf çakışması, ortak ders bütünlüğü, sınıf-saat kuralı,
    /// dinlenme molası, slot kapasite aşımı (salon tipi bazında, efektif kontenjanla).
    /// </summary>
    public class SablonDogrulayici
    {
        // "yyyy-MM-dd" → o günün sıralı saat listesi (dinlenme molası indeks hesabı için)
        private readonly Dictionary<string, List<string>> _gunSaatleri = new();
        // Salon tipi bazında toplam kapasiteler (slot kapasite kontrolü için)
        private readonly int _normalToplamKapasite;
        private readonly int _uygulamaliToplamKapasite;
        // Uygulamalı ders adları (normalize edilmiş)
        private readonly HashSet<string> _uygulamaliDersler = new();
        // Bölüm bazlı hata oranı (%) — efektif kontenjan hesabı için
        private readonly Dictionary<string, double> _bolumHataOranlari;
        // Aynı bölüm+sınıfın iki sınavı arasındaki minimum slot boşluğu
        private readonly int _minDinlenmeMolasi;

        public SablonDogrulayici(Dictionary<DateTime, List<string>> takvimSlotlari,
                                 List<Salon> salonlar,
                                 IEnumerable<string> uygulamaliDersler,
                                 Dictionary<string, double> bolumHataOranlari = null,
                                 int minDinlenmeMolasi = 1)
        {
            if (takvimSlotlari != null)
            {
                foreach (var kvp in takvimSlotlari)
                    _gunSaatleri[kvp.Key.ToString("yyyy-MM-dd")] = kvp.Value.OrderBy(s => s).ToList();
            }

            if (salonlar != null)
            {
                _normalToplamKapasite = salonlar.Where(s => !s.UygulamaliMi).Sum(s => s.Kapasite);
                _uygulamaliToplamKapasite = salonlar.Where(s => s.UygulamaliMi).Sum(s => s.Kapasite);
            }

            if (uygulamaliDersler != null)
            {
                foreach (var ad in uygulamaliDersler)
                    if (!string.IsNullOrWhiteSpace(ad))
                        _uygulamaliDersler.Add(NormalizeDersAdi(ad));
            }

            _bolumHataOranlari = bolumHataOranlari ?? new Dictionary<string, double>();
            _minDinlenmeMolasi = minDinlenmeMolasi;
        }

        /// <summary>
        /// Şablonun tamamını doğrular ve tespit edilen tüm ihlalleri döndürür.
        /// Aynı atama birden fazla ihlale karışabilir (her biri ayrı kayıt).
        /// </summary>
        public List<SablonIhlal> Dogrula(List<SinavAtama> sablon)
        {
            var ihlaller = new List<SablonIhlal>();
            if (sablon == null || sablon.Count == 0) return ihlaller;

            // ── 0) Slot takvimde tanımlı mı? ──
            foreach (var a in sablon)
            {
                string gunKey = a.Gun.ToString("yyyy-MM-dd");
                if (!_gunSaatleri.TryGetValue(gunKey, out var saatler) || !saatler.Contains(a.Saat))
                    ihlaller.Add(new SablonIhlal
                    {
                        Atama = a,
                        Mesaj = $"'{a.DersAdi}' — {a.Gun:dd.MM.yyyy} {a.Saat} slotu takvimde tanımlı değil."
                    });
            }

            // ── 1) KURAL 1 — BÖLÜM İÇİ ÇAKIŞMA (HARD CONSTRAINT) ──
            // Aynı gün+saatte, aynı BÖLÜME ait birden fazla FARKLI ders olamaz.
            // Sınıf seviyesi ARTIK dikkate alınmaz: Bilgisayar 2. sınıf ile
            // Bilgisayar 3. sınıf aynı saatte olamaz.
            // İstisna: 17:00 taşırma slotu (Kural 3) bu kontrolden muaftır.
            var bolumGruplari = sablon
                .Where(a => !TasmaSaatiMi(a.Saat))
                .GroupBy(a => new
                {
                    Gun = a.Gun.Date,
                    a.Saat,
                    Bolum = (a.BolumAdi ?? "").ToUpperInvariant().Trim()
                });
            foreach (var grup in bolumGruplari)
            {
                // Aynı dersin birden fazla şubesi çakışma DEĞİLDİR — ders adına göre ayırt et
                var farkliDersler = grup.Select(a => NormalizeDersAdi(a.DersAdi)).Distinct().ToList();
                if (farkliDersler.Count > 1)
                {
                    var sinifListesi = string.Join(", ",
                        grup.Select(a => a.SinifSeviyesi).Distinct().OrderBy(s => s).Select(s => s + ". sınıf"));

                    foreach (var a in grup)
                        ihlaller.Add(new SablonIhlal
                        {
                            Atama = a,
                            Mesaj = $"BÖLÜM İÇİ ÇAKIŞMA: '{a.BolumAdi}' bölümünün aynı slotta " +
                                    $"({a.Gun:dd.MM.yyyy} {a.Saat}) {farkliDersler.Count} farklı sınavı var " +
                                    $"[{sinifListesi}]. Bir bölümden aynı saate yalnızca 1 sınav atanabilir."
                        });
                }
            }

            // ── 2) Ortak ders bütünlüğü: birden fazla bölümde geçen ders tek slotta olmalı ──
            var ortakDersGruplari = sablon
                .GroupBy(a => NormalizeDersAdi(a.DersAdi))
                .Where(g => g.Select(a => (a.BolumAdi ?? "").ToUpperInvariant().Trim()).Distinct().Count() > 1);
            foreach (var grup in ortakDersGruplari)
            {
                var slotlar = grup.Select(a => $"{a.Gun:yyyy-MM-dd}|{a.Saat}").Distinct().ToList();
                if (slotlar.Count > 1)
                {
                    foreach (var a in grup)
                        ihlaller.Add(new SablonIhlal
                        {
                            Atama = a,
                            Mesaj = $"Ortak ders bütünlüğü bozuldu: '{a.DersAdi}' farklı bölümlerde " +
                                    $"{slotlar.Count} ayrı slota dağılmış (tek slota çivilenmeli)."
                        });
                }
            }

            // ── 3) Sınıf-saat kuralı ──
            foreach (var a in sablon)
            {
                if (!SaatSinifaUygunMu(a.Saat, a.SinifSeviyesi))
                    ihlaller.Add(new SablonIhlal
                    {
                        Atama = a,
                        Mesaj = $"Sınıf-saat kuralı ihlali: {a.SinifSeviyesi}. sınıf dersi '{a.DersAdi}' " +
                                $"{a.Saat} saatinde (beklenen: {SinifIcinBeklenenSaat(a.SinifSeviyesi)})."
                    });
            }

            // ── 4) Dinlenme molası: aynı bölüm+sınıfın aynı gündeki sınavları arasında minimum boşluk ──
            var dinlenmeGruplari = sablon.GroupBy(a => new
            {
                Bolum = (a.BolumAdi ?? "").ToUpperInvariant().Trim(),
                a.SinifSeviyesi,
                Gun = a.Gun.Date
            });
            foreach (var grup in dinlenmeGruplari)
            {
                string gunKey = grup.Key.Gun.ToString("yyyy-MM-dd");
                if (!_gunSaatleri.TryGetValue(gunKey, out var saatler)) continue;

                // Farklı derslerin bulunduğu farklı saatlerin indeks çiftleri karşılaştırılır
                // (aynı slot zaten 1 no'lu çakışma kontrolüne girer)
                // KURAL 3: taşırma slotundaki sınavlar dinlenme molası hesabına girmez
                var saatIdxleri = grup
                    .Where(a => !TasmaSaatiMi(a.Saat))
                    .Select(a => new { Atama = a, Idx = saatler.IndexOf(a.Saat) })
                    .Where(x => x.Idx >= 0)
                    .ToList();

                foreach (var x in saatIdxleri)
                {
                    bool ihlalVar = saatIdxleri.Any(y =>
                        y.Idx != x.Idx && Math.Abs(y.Idx - x.Idx) <= _minDinlenmeMolasi);
                    if (ihlalVar)
                        ihlaller.Add(new SablonIhlal
                        {
                            Atama = x.Atama,
                            Mesaj = $"Dinlenme molası ihlali: {x.Atama.BolumAdi} {x.Atama.SinifSeviyesi}. sınıfın " +
                                    $"{grup.Key.Gun:dd.MM.yyyy} günündeki sınavları arasında yeterli boşluk yok " +
                                    $"(min {_minDinlenmeMolasi} slot)."
                        });
                }
            }

            // ── 5) Slot kapasite aşımı: salon tipi bazında efektif öğrenci toplamı ──
            foreach (var slotGrubu in sablon.GroupBy(a => new { Gun = a.Gun.Date, a.Saat }))
            {
                // KURAL 3: 17:00 taşırma slotu limitsizdir, kapasite aşımı uyarısı verilmez
                if (TasmaSaatiMi(slotGrubu.Key.Saat)) continue;

                foreach (bool uygulamali in new[] { false, true })
                {
                    int kapasite = uygulamali ? _uygulamaliToplamKapasite : _normalToplamKapasite;
                    if (kapasite <= 0) continue; // salon bilgisi yoksa kapasite kontrolü atlanır

                    var tipAtamalari = slotGrubu
                        .Where(a => DersUygulamaliMi(a.DersAdi) == uygulamali)
                        .ToList();
                    if (tipAtamalari.Count == 0) continue;

                    int toplamYuk = tipAtamalari.Sum(a => EfektifOgrenci(a.BolumAdi, a.Kontenjan));
                    if (toplamYuk > kapasite)
                    {
                        foreach (var a in tipAtamalari)
                            ihlaller.Add(new SablonIhlal
                            {
                                Atama = a,
                                Mesaj = $"Slot kapasite aşımı: {slotGrubu.Key.Gun:dd.MM.yyyy} {slotGrubu.Key.Saat} slotunda " +
                                        $"{(uygulamali ? "uygulamalı" : "normal")} salon yükü {toplamYuk} kişi, " +
                                        $"toplam kapasite {kapasite}."
                            });
                    }
                }
            }

            return ihlaller;
        }

        // ═══════════════════════════════════════════════════
        // ORTAK STATİK KURALLAR (algoritma ile tek kaynak)
        // ═══════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════
        // KURAL 3 — 17:00 TAŞIRMA SLOTU
        // Bu saat "kurtarıcı slot"tur: kapasite, bölüm çakışması,
        // sınıf-saat kuralı ve dinlenme molası kontrollerinin
        // HİÇBİRİ uygulanmaz. Sınırsız sayıda sınav atanabilir.
        // ═══════════════════════════════════════════════════
        public const string TASMA_SAATI = "17:00";

        /// <summary>Verilen saat, limitsiz taşırma slotu mu?</summary>
        public static bool TasmaSaatiMi(string saat)
            => !string.IsNullOrWhiteSpace(saat) && saat.Trim().StartsWith("17:");

        /// <summary>Sınıf seviyesine göre saat kısıtı (1→11:00, 2→15:00, 3→09:00, 4→13:00).</summary>
        public static bool SaatSinifaUygunMu(string saat, int sinifSeviyesi)
        {
            // KURAL 3: taşırma slotunda sınıf-saat kuralı aranmaz
            if (TasmaSaatiMi(saat)) return true;

            return sinifSeviyesi switch
            {
                1 => saat == "11:00", // 1. Sınıflar sadece 11:00'da
                2 => saat == "15:00", // 2. Sınıflar sadece 15:00'da
                3 => saat == "09:00", // 3. Sınıflar sadece 09:00'da
                4 => saat == "13:00", // 4. Sınıflar sadece 13:00'da
                _ => true             // 5. sınıf veya tanımsız bir durum gelirse serbest bırak
            };
        }

        /// <summary>Sınıf seviyesi için beklenen saati döndürür (uyarı mesajları için).</summary>
        public static string SinifIcinBeklenenSaat(int sinifSeviyesi)
        {
            return sinifSeviyesi switch
            {
                1 => "11:00",
                2 => "15:00",
                3 => "09:00",
                4 => "13:00",
                _ => "serbest"
            };
        }

        /// <summary>Ders adını Türkçe karakterlerden arındırıp normalize eder (ortak ders eşleştirme).</summary>
        public static string NormalizeDersAdi(string dersAdi)
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
        // İÇ YARDIMCILAR
        // ═══════════════════════════════════════════════════

        private bool DersUygulamaliMi(string dersAdi)
            => _uygulamaliDersler.Contains(NormalizeDersAdi(dersAdi));

        private double BolumHataOrani(string bolum)
        {
            if (string.IsNullOrWhiteSpace(bolum)) return 0;
            string b = bolum.ToUpperInvariant().Trim();
            foreach (var kvp in _bolumHataOranlari)
                if (kvp.Key.ToUpperInvariant().Trim() == b)
                    return kvp.Value;
            return 0;
        }

        /// <summary>Efektif öğrenci: kontenjandan sınava gelmeyecek % düşülür.</summary>
        private int EfektifOgrenci(string bolum, int kontenjan)
        {
            double yuzde = BolumHataOrani(bolum);
            if (yuzde <= 0) return kontenjan;
            return Math.Max(1, (int)Math.Ceiling(kontenjan * (1 - yuzde / 100.0)));
        }
    }
}
