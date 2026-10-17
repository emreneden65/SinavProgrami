#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Algorithm
{
    /// <summary>
    /// AŞAMA 2 — Günlük Salon Dağıtıcı.
    /// ──────────────────────────────────────────────────────────
    /// Doldurulmuş "Sınav Haftası Dağıtım Şablonu"ndaki (salonsuz) yerleşimi alır ve
    /// GÜN GÜN, SLOT SLOT her dersi salonlara (DB'deki salonlar) dağıtır.
    ///
    /// KURALLAR:
    ///  1) ŞUBE İZOLASYONU — farklı şubeler ASLA aynı salona karıştırılmaz.
    ///     Her şube kendi içinde izole edilerek ayrı salon(lar)a yerleştirilir.
    ///  2) HAVUZ İSTİSNASI — kullanıcının tanımladığı DersHavuzu üyeleri (farklı
    ///     bölüm/şubeler) tek kontenjan gibi birleştirilir ve MÜMKÜNSE TEK salona,
    ///     değilse en az sayıda salona birlikte yerleştirilir (izolasyonu ezer).
    ///  3) Ortak dersler (aynı ders adı ≥2 bölümde) öncelikli işlenir.
    ///  4) Elektrik-Elektronik (ELM/EEM) + Mekatronik (MEK) sınavları ÖNCE T4 binası
    ///     salonlarına atanır; T4 dolarsa diğer binalara taşar.
    ///     (Diğer bölümler önce T4-dışını kullanır.)
    ///  5) Bir slotta bir salon YALNIZCA tek gruba (tek şube veya tek havuz) tahsis edilir.
    /// </summary>
    public class GunlukSalonDagitici
    {
        public List<string> Uyarilar { get; } = new List<string>();

        /// <summary>Bir gün-sayfası satırı: bir dersin bir slottaki salon dağılımı.</summary>
        public class GunSatir
        {
            public DateTime Gun;
            public string Saat;
            public string Bolum;      // kısaltma (BİL/EEM/INS/MAK/MEK/MEM)
            public int Sinif;
            public string DersAdi;
            public bool Ortak;
            public bool Uygulamali;   // uygulamalı (lab/çizim) dersi mi
            public bool Havuz;        // bu satırın en az bir şubesi bir havuza dahil mi
            public string HavuzEtiketi = ""; // örn. "HAVUZ: BİL(A)+INS(B)"
            public string OgretimElemani = ""; // dersin genel sorumlu hocası (Sınav Programı Formu için)
            public Dictionary<string, int> Subeler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // A/B/C/D
            public int Toplam;

            /// <summary>salonKodu → toplam öğrenci (geriye dönük uyumluluk).</summary>
            public Dictionary<string, int> SalonDagilimi = new Dictionary<string, int>();

            /// <summary>
            /// ŞEFFAFLIK: salonKodu → (şube harfi → öğrenci sayısı).
            /// Salon planında kontenjan kutucuğuna şube adı yazdırmak için kullanılır.
            /// </summary>
            public Dictionary<string, Dictionary<string, int>> SalonSubeDagilimi
                = new Dictionary<string, Dictionary<string, int>>();

            /// <summary>Bir salona atanan şubelerin etiketi: "A" / "A+B".</summary>
            public string SalonSubeEtiketi(string salonKodu)
                => SalonSubeDagilimi.TryGetValue(salonKodu, out var m) && m.Count > 0
                    ? string.Join("+", m.Keys.OrderBy(k => k))
                    : "";
        }

        // ── İç yerleşim birimleri ──
        private class SubeBirimi           // izole yerleşim birimi: TEK şube
        {
            public GunSatir Satir;
            public string Harf;            // A/B/C/D
            public int Adet;
        }

        private class HavuzBirimi          // havuz yerleşim birimi: birden çok şube tek kontenjan
        {
            public DersHavuzu Havuz;
            public List<SubeBirimi> Uyeler = new List<SubeBirimi>();
            public int Toplam => Uyeler.Sum(u => u.Adet);
            public string Etiket => "HAVUZ: " + string.Join(" + ", Uyeler.Select(u => $"{u.Satir.Bolum}({u.Harf})"));
        }

        // ── HAVUZ KAYDIRMA (FALLBACK) ──────────────────────────────
        // Havuzun toplamı, slotundaki boş salon kapasitesine sığmazsa havuz
        // SONRAKİ slotlara (önce aynı gün, sonra diğer günler) ertelenir;
        // yeterli boş kapasiteli ilk slota tek parça yerleştirilir.
        private class BekleyenHavuzUyesi { public GunSatir KaynakSatir; public string Harf; public int Adet; }

        private class BekleyenHavuz
        {
            public DersHavuzu Havuz;
            public string DersAdi;
            public bool Uygulamali;
            public bool HepsiElmMek;
            public DateTime KaynakGun;
            public string KaynakSaat;
            public List<BekleyenHavuzUyesi> Uyeler = new List<BekleyenHavuzUyesi>();
            public int Toplam => Uyeler.Sum(u => u.Adet);
        }

        /// <summary>Salon kodundan bina/blok kodu: "T1-104" → "T1".</summary>
        private static string BlokKodu(string salonKodu)
        {
            string s = (salonKodu ?? "").Trim().ToUpperInvariant();
            int i = s.IndexOf('-');
            if (i > 0) return s.Substring(0, i);
            return s.Length >= 2 ? s.Substring(0, 2) : s;
        }

        /// <summary>Şablonu (salonsuz) günlük salon matrisine dağıtır.</summary>
        /// <param name="uygulamaliDersler">Uygulamalı işaretli ders adları — bu dersler yalnızca
        /// uygulamalı salonlara atanır; uygulamalı salonlar diğer dersler için yok sayılır.</param>
        /// <param name="dersHavuzlari">Kullanıcı tanımlı şube havuzları — üyeleri tek salonda birleştirilir.</param>
        /// <param name="manuelOrtakDersler">null → otomatik tespit (aynı ad ≥2 bölüm);
        /// değilse SADECE bu ders adları ortak kabul edilir (manuel seçim).</param>
        public List<GunSatir> Dagit(List<SinavAtama> sablon, List<Salon> salonlar,
                                    IEnumerable<string> uygulamaliDersler = null,
                                    List<DersHavuzu> dersHavuzlari = null,
                                    IEnumerable<string> manuelOrtakDersler = null)
        {
            Uyarilar.Clear();
            var sonuc = new List<GunSatir>();
            if (sablon == null || sablon.Count == 0) return sonuc;

            var havuzlar = dersHavuzlari ?? new List<DersHavuzu>();
            var uygDersSet = new HashSet<string>((uygulamaliDersler ?? Enumerable.Empty<string>())
                .Select(NormAd));

            // Kapasitesi > 0 salonlar (0 kapasiteli satırlar atlanır)
            var kullanilabilir = (salonlar ?? new List<Salon>()).Where(s => s.Kapasite > 0).ToList();
            if (kullanilabilir.Count == 0)
            {
                Uyarilar.Add("Kapasitesi > 0 salon bulunamadı — dağıtım yapılamadı.");
                return sonuc;
            }

            // Uygulamalı salonlar ayrı havuz; normal salonlar T4 / diğer diye ikiye bölünür
            var uygulamaliSalonlar = kullanilabilir.Where(s => s.UygulamaliMi).OrderBy(s => s.SalonKodu).ToList();
            var normalKullanilabilir = kullanilabilir.Where(s => !s.UygulamaliMi).ToList();
            var t4Salonlar = normalKullanilabilir.Where(s => T4mu(s.SalonKodu)).OrderBy(s => s.SalonKodu).ToList();
            var digerSalonlar = normalKullanilabilir.Where(s => !T4mu(s.SalonKodu)).OrderBy(s => s.SalonKodu).ToList();

            // ── Ders birimleri: (Gün, Saat, Bölüm, Ders, Sınıf) → şubeler ──
            var birimler = sablon
                .GroupBy(a => new
                {
                    a.Gun,
                    a.Saat,
                    Bolum = BolumKisalt(a.BolumAdi),
                    DersAdi = (a.DersAdi ?? "").Trim(),
                    a.SinifSeviyesi
                })
                .Select(g =>
                {
                    var satir = new GunSatir
                    {
                        Gun = g.Key.Gun,
                        Saat = g.Key.Saat,
                        Bolum = g.Key.Bolum,
                        DersAdi = g.Key.DersAdi,
                        Sinif = g.Key.SinifSeviyesi
                    };
                    foreach (var a in g)
                    {
                        string h = HarfKolonu(a.Sube);
                        if (!satir.Subeler.ContainsKey(h)) satir.Subeler[h] = 0;
                        satir.Subeler[h] += Math.Max(0, a.Kontenjan);
                        if (string.IsNullOrWhiteSpace(satir.OgretimElemani) &&
                            !string.IsNullOrWhiteSpace(a.OgretimElemani))
                            satir.OgretimElemani = a.OgretimElemani;
                    }
                    satir.Toplam = satir.Subeler.Values.Sum();
                    satir.Uygulamali = uygDersSet.Contains(NormAd(satir.DersAdi));
                    return satir;
                })
                .ToList();

            // ── Ortak ders tespiti ──
            // MANUEL SEÇİM varsa yalnızca kullanıcının işaretlediği dersler ortaktır;
            // yoksa otomatik: aynı ders adı ≥2 farklı bölümde.
            if (manuelOrtakDersler != null)
            {
                var manuelSet = new HashSet<string>(manuelOrtakDersler.Select(NormAd));
                foreach (var b in birimler)
                    b.Ortak = manuelSet.Contains(NormAd(b.DersAdi));
            }
            else
            {
                var dersBolum = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var b in birimler)
                {
                    string ad = NormAd(b.DersAdi);
                    if (!dersBolum.TryGetValue(ad, out var set)) { set = new HashSet<string>(); dersBolum[ad] = set; }
                    set.Add(b.Bolum);
                }
                foreach (var b in birimler)
                    b.Ortak = dersBolum.TryGetValue(NormAd(b.DersAdi), out var set) && set.Count >= 2;
            }

            // ── HAVUZ ÖN-KONTROLÜ: üyeleri farklı slotlara dağılmış havuzları uyar ──
            foreach (var havuz in havuzlar)
            {
                if (havuz == null || string.IsNullOrWhiteSpace(havuz.DersAdi)) continue;
                var slotlari = birimler
                    .Where(b => HavuzaUyuyor(havuz, b, null))
                    .Select(b => $"{b.Gun:dd.MM.yyyy} {b.Saat}")
                    .Distinct().ToList();
                if (slotlari.Count > 1)
                    Uyarilar.Add($"⚠ Havuz '{havuz.DersAdi}' — üyeleri şablonda FARKLI slotlarda: " +
                                 string.Join(", ", slotlari) +
                                 ". Havuz kuralı gereği hepsi aynı gün+saatte olmalı; şablonu düzeltin. " +
                                 "Aynı slottaki üyeler yine de tek salonda birleştirilecek.");
            }

            // ── Gün gün, slot slot dağıt ──
            var bekleyenHavuzlar = new List<BekleyenHavuz>(); // kapasite yetersizliğinden ertelenen havuzlar
            foreach (var gunGrup in birimler.GroupBy(b => b.Gun).OrderBy(g => g.Key))
            {
                foreach (var slotGrup in gunGrup.GroupBy(b => b.Saat).OrderBy(g => g.Key))
                {
                    // Bu slotta bir salon YALNIZCA tek gruba tahsis edilir (paylaşım yok).
                    var kullanilan = new HashSet<string>();
                    var slotBirimleri = slotGrup.ToList();

                    // ── 1) HAVUZ BİRİMLERİNİ KUR ──
                    // Havuza dahil (satir, şube) çiftleri düz şube listesinden düşülür.
                    var havuzdaki = new HashSet<(GunSatir, string)>();
                    var havuzBirimleri = new List<HavuzBirimi>();

                    foreach (var havuz in havuzlar)
                    {
                        if (havuz == null || string.IsNullOrWhiteSpace(havuz.DersAdi)) continue;

                        var hb = new HavuzBirimi { Havuz = havuz };
                        foreach (var satir in slotBirimleri)
                        {
                            foreach (var harf in satir.Subeler.Keys.ToList())
                            {
                                if (havuzdaki.Contains((satir, harf))) continue;
                                if (!HavuzaUyuyor(havuz, satir, harf)) continue;
                                hb.Uyeler.Add(new SubeBirimi { Satir = satir, Harf = harf, Adet = satir.Subeler[harf] });
                            }
                        }

                        if (hb.Uyeler.Count < 2) continue; // birleştirilecek en az 2 şube yoksa havuzlama yok

                        foreach (var u in hb.Uyeler)
                        {
                            havuzdaki.Add((u.Satir, u.Harf));
                            u.Satir.Havuz = true;
                            u.Satir.HavuzEtiketi = hb.Etiket;
                        }
                        havuzBirimleri.Add(hb);
                    }

                    // ── 2) DÜZ ŞUBE BİRİMLERİ (izolasyon: her şube ayrı birim) ──
                    var subeBirimleri = new List<SubeBirimi>();
                    foreach (var satir in slotBirimleri)
                        foreach (var kv in satir.Subeler)
                            if (kv.Value > 0 && !havuzdaki.Contains((satir, kv.Key)))
                                subeBirimleri.Add(new SubeBirimi { Satir = satir, Harf = kv.Key, Adet = kv.Value });

                    // Yerel yardımcı: verilen salon poollarındaki toplam BOŞ kapasite
                    int BosKapasite(List<List<Salon>> plr) => plr.SelectMany(p => p)
                        .Where(s => !kullanilan.Contains(s.SalonKodu))
                        .Sum(s => s.Kapasite);

                    // ── 2.5) KAYDIRILMIŞ HAVUZLAR — ertelenen havuz bu slota sığıyorsa buraya yerleşir ──
                    for (int bi = 0; bi < bekleyenHavuzlar.Count; bi++)
                    {
                        var bek = bekleyenHavuzlar[bi];
                        var bekPool = SalonPoollari(bek.Uygulamali, bek.HepsiElmMek,
                                                    uygulamaliSalonlar, t4Salonlar, digerSalonlar);
                        if (BosKapasite(bekPool) < bek.Toplam) continue; // bu slotta da yer yok

                        // Üyeler bu slota KLON satırlar olarak taşınır; kaynak satırlardan düşülür
                        var hbYeni = new HavuzBirimi { Havuz = bek.Havuz };
                        foreach (var uye in bek.Uyeler)
                        {
                            var klon = new GunSatir
                            {
                                Gun = gunGrup.Key,
                                Saat = slotGrup.Key,
                                Bolum = uye.KaynakSatir.Bolum,
                                Sinif = uye.KaynakSatir.Sinif,
                                DersAdi = uye.KaynakSatir.DersAdi,
                                Ortak = uye.KaynakSatir.Ortak,
                                Uygulamali = uye.KaynakSatir.Uygulamali,
                                OgretimElemani = uye.KaynakSatir.OgretimElemani,
                                Havuz = true
                            };
                            klon.Subeler[uye.Harf] = uye.Adet;
                            klon.Toplam = uye.Adet;
                            slotBirimleri.Add(klon);
                            hbYeni.Uyeler.Add(new SubeBirimi { Satir = klon, Harf = uye.Harf, Adet = uye.Adet });

                            // Kaynak satırdan bu şubenin kontenjanını düş
                            if (uye.KaynakSatir.Subeler.TryGetValue(uye.Harf, out int mevcutAdet))
                            {
                                int kalanAdet = mevcutAdet - uye.Adet;
                                if (kalanAdet > 0) uye.KaynakSatir.Subeler[uye.Harf] = kalanAdet;
                                else uye.KaynakSatir.Subeler.Remove(uye.Harf);
                            }
                            uye.KaynakSatir.Toplam = uye.KaynakSatir.Subeler.Values.Sum();
                            if (uye.KaynakSatir.Toplam <= 0 && uye.KaynakSatir.SalonDagilimi.Count == 0)
                                sonuc.Remove(uye.KaynakSatir); // tamamen boşalan kaynak satır listeden çıkar
                        }
                        foreach (var u2 in hbYeni.Uyeler)
                            u2.Satir.HavuzEtiketi = hbYeni.Etiket + " — KAYDIRILDI";

                        havuzBirimleri.Insert(0, hbYeni); // bu slotta öncelikli yerleşsin
                        Uyarilar.Add($"ℹ Havuz '{bek.DersAdi}' — {bek.KaynakGun:dd.MM.yyyy} {bek.KaynakSaat} slotunda " +
                                     $"kapasite yetersizdi; {gunGrup.Key:dd.MM.yyyy} {slotGrup.Key} slotuna KAYDIRILDI. " +
                                     "Bölüm çakışması olup olmadığını şablonda doğrulayın.");
                        bekleyenHavuzlar.RemoveAt(bi);
                        bi--;
                    }

                    // ── 3) HAVUZLAR ÖNCE — tek salon garantisi için salonlar kapılmadan ──
                    foreach (var hb in havuzBirimleri.OrderByDescending(h => h.Toplam))
                    {
                        bool hepsiElmMek = hb.Uyeler.All(u => ElmMek(u.Satir.Bolum));
                        bool uygulamali = hb.Uyeler.Any(u => u.Satir.Uygulamali);
                        var poollar = SalonPoollari(uygulamali, hepsiElmMek, uygulamaliSalonlar, t4Salonlar, digerSalonlar);

                        int toplam = hb.Toplam;

                        // FALLBACK (KAYDIRMA): slotun boş salon kapasitesi havuza yetmiyorsa
                        // havuz SONRAKİ slota/güne ertelenir (bölünmek yerine tek parça taşınır).
                        if (BosKapasite(poollar) < toplam)
                        {
                            var bek = new BekleyenHavuz
                            {
                                Havuz = hb.Havuz,
                                DersAdi = hb.Havuz?.DersAdi ?? hb.Uyeler[0].Satir.DersAdi,
                                Uygulamali = uygulamali,
                                HepsiElmMek = hepsiElmMek,
                                KaynakGun = gunGrup.Key,
                                KaynakSaat = slotGrup.Key
                            };
                            foreach (var u in hb.Uyeler)
                                bek.Uyeler.Add(new BekleyenHavuzUyesi { KaynakSatir = u.Satir, Harf = u.Harf, Adet = u.Adet });
                            bekleyenHavuzlar.Add(bek);
                            Uyarilar.Add($"⚠ Havuz '{bek.DersAdi}' ({hb.Etiket}) — {gunGrup.Key:dd.MM.yyyy} {slotGrup.Key} " +
                                         $"slotunda boş kapasite ({BosKapasite(poollar)}) havuza ({toplam}) yetmiyor; " +
                                         "sonraki uygun slota kaydırılmak üzere ertelendi.");
                            continue;
                        }

                        // 3a) TEK SALON — BÜYÜK→BÜYÜK: havuzun sığdığı EN BÜYÜK boş salon
                        Salon tekSalon = null;
                        foreach (var pool in poollar)
                        {
                            tekSalon = pool
                                .Where(s => !kullanilan.Contains(s.SalonKodu) && s.Kapasite >= toplam)
                                .OrderByDescending(s => s.Kapasite)
                                .FirstOrDefault();
                            if (tekSalon != null) break;
                        }

                        if (tekSalon != null)
                        {
                            kullanilan.Add(tekSalon.SalonKodu);
                            foreach (var u in hb.Uyeler)
                                Kaydet(u.Satir, tekSalon.SalonKodu, u.Harf, u.Adet);
                        }
                        else
                        {
                            // 3b) SIĞMADI — en az sayıda salona böl (büyük salonlar önce),
                            //     üyeler sırayla doldurulur; bir üye gerekirse bölünür.
                            Uyarilar.Add($"⚠ Havuz '{hb.Havuz.DersAdi}' ({hb.Etiket}) — toplam {toplam} öğrenci " +
                                         "tek boş salona sığmadı; en az sayıda salona bölünerek atandı.");

                            var uyeKuyruk = new Queue<SubeBirimi>(hb.Uyeler.Select(u => new SubeBirimi { Satir = u.Satir, Harf = u.Harf, Adet = u.Adet }));
                            var aktif = uyeKuyruk.Count > 0 ? uyeKuyruk.Dequeue() : null;
                            int kalanToplam = toplam;

                            foreach (var pool in poollar)
                            {
                                foreach (var salon in pool.OrderByDescending(s => s.Kapasite))
                                {
                                    if (kalanToplam <= 0 || aktif == null) break;
                                    if (kullanilan.Contains(salon.SalonKodu)) continue;

                                    int salonKalan = salon.Kapasite;
                                    kullanilan.Add(salon.SalonKodu);

                                    while (salonKalan > 0 && aktif != null)
                                    {
                                        int al = Math.Min(salonKalan, aktif.Adet);
                                        if (al > 0)
                                        {
                                            Kaydet(aktif.Satir, salon.SalonKodu, aktif.Harf, al);
                                            aktif.Adet -= al;
                                            salonKalan -= al;
                                            kalanToplam -= al;
                                        }
                                        if (aktif.Adet <= 0)
                                            aktif = uyeKuyruk.Count > 0 ? uyeKuyruk.Dequeue() : null;
                                    }
                                }
                                if (kalanToplam <= 0 || aktif == null) break;
                            }

                            if (kalanToplam > 0)
                                Uyarilar.Add($"{slotGrup.First().Gun:dd.MM.yyyy} {slotGrup.Key} — Havuz '{hb.Havuz.DersAdi}': " +
                                             $"{kalanToplam} kişi bu slottaki boş salonlara sığmadı (kapasite yetersiz).");
                        }
                    }

                    // ── 4) DÜZ ŞUBELER — İZOLE: her şube ayrı salon(lar)a, karma atama YOK ──
                    // Sıra: ortak önce → ELM/MEK önce → büyük şube önce
                    var sirali = subeBirimleri
                        .OrderBy(u => u.Satir.Ortak ? 0 : 1)
                        .ThenBy(u => ElmMek(u.Satir.Bolum) ? 0 : 1)
                        .ThenByDescending(u => u.Adet)
                        .ToList();

                    foreach (var u in sirali)
                    {
                        var poollar = SalonPoollari(u.Satir.Uygulamali, ElmMek(u.Satir.Bolum),
                                                    uygulamaliSalonlar, t4Salonlar, digerSalonlar);

                        // BİNA BÜTÜNLÜĞÜ: bu dersin daha önce yerleşen şubelerinin binaları.
                        // Yeni şube MÜMKÜN MERTEBE aynı binaya atanır; farklı bina yalnızca
                        // kapasitenin yetmediği mutlak zorunlu durumda (uyarıyla) kullanılır.
                        var dersBinalari = new HashSet<string>(u.Satir.SalonSubeDagilimi.Keys.Select(BlokKodu));

                        Salon EnBuyukSigan(bool binaKisiti)
                        {
                            foreach (var pool in poollar)
                            {
                                var aday = pool
                                    .Where(s => !kullanilan.Contains(s.SalonKodu) && s.Kapasite >= u.Adet)
                                    .Where(s => !binaKisiti || dersBinalari.Count == 0 ||
                                                dersBinalari.Contains(BlokKodu(s.SalonKodu)))
                                    .OrderByDescending(s => s.Kapasite) // BÜYÜK KONTENJAN → BÜYÜK SALON
                                    .FirstOrDefault();
                                if (aday != null) return aday;
                            }
                            return null;
                        }

                        // 4a) TEK SALON — önce AYNI BİNA, mutlak zorunlulukta farklı bina
                        var secilenSalon = EnBuyukSigan(true) ?? EnBuyukSigan(false);
                        if (secilenSalon != null)
                        {
                            if (dersBinalari.Count > 0 && !dersBinalari.Contains(BlokKodu(secilenSalon.SalonKodu)))
                                Uyarilar.Add($"⚠ {u.Satir.Gun:dd.MM.yyyy} {u.Satir.Saat} — {u.Satir.Bolum} / {u.Satir.DersAdi} " +
                                             $"Şube {u.Harf}: BİNA BÜTÜNLÜĞÜ İSTİSNASI — aynı binada uygun salon kalmadığından " +
                                             $"{BlokKodu(secilenSalon.SalonKodu)} binasına atandı.");
                            kullanilan.Add(secilenSalon.SalonKodu);
                            Kaydet(u.Satir, secilenSalon.SalonKodu, u.Harf, u.Adet);
                            continue;
                        }

                        // 4b) ÇOK SALON — şube bölünür (yine izole); önce aynı binadaki boş salonlar
                        int kalan = u.Adet;
                        bool binaDisiKullanildi = false;
                        foreach (var pool in poollar)
                        {
                            var salonSirasi = pool
                                .Where(s => !kullanilan.Contains(s.SalonKodu))
                                .OrderBy(s => dersBinalari.Count == 0 ||
                                              dersBinalari.Contains(BlokKodu(s.SalonKodu)) ? 0 : 1)
                                .ThenByDescending(s => s.Kapasite)
                                .ToList();

                            foreach (var salon in salonSirasi)
                            {
                                if (kalan <= 0) break;
                                int al = Math.Min(kalan, salon.Kapasite);
                                kullanilan.Add(salon.SalonKodu);
                                Kaydet(u.Satir, salon.SalonKodu, u.Harf, al);
                                if (dersBinalari.Count > 0 && !dersBinalari.Contains(BlokKodu(salon.SalonKodu)))
                                    binaDisiKullanildi = true;
                                dersBinalari.Add(BlokKodu(salon.SalonKodu));
                                kalan -= al;
                            }
                            if (kalan <= 0) break;
                        }
                        if (binaDisiKullanildi)
                            Uyarilar.Add($"⚠ {u.Satir.Gun:dd.MM.yyyy} {u.Satir.Saat} — {u.Satir.Bolum} / {u.Satir.DersAdi} " +
                                         $"Şube {u.Harf}: BİNA BÜTÜNLÜĞÜ İSTİSNASI — kapasite zorunluluğuyla birden fazla binaya bölündü.");
                        if (kalan > 0)
                            Uyarilar.Add($"{u.Satir.Gun:dd.MM.yyyy} {u.Satir.Saat} — {u.Satir.Bolum} / {u.Satir.DersAdi} " +
                                         $"Şube {u.Harf}: {kalan} kişi bu slottaki boş salonlara sığmadı (kapasite yetersiz).");
                    }

                    sonuc.AddRange(slotBirimleri);
                }
            }

            // Hiçbir slota sığmayan (kaydırılamayan) havuzlar — üyeleri kaynak
            // slotlarında salonsuz kaldı; kullanıcı kapasite/takvimi gözden geçirmeli.
            foreach (var bek in bekleyenHavuzlar)
                Uyarilar.Add($"⚠ Havuz '{bek.DersAdi}' — {bek.KaynakGun:dd.MM.yyyy} {bek.KaynakSaat} slotundan " +
                             $"kaydırılamadı: takvimdeki HİÇBİR slotta {bek.Toplam} kişilik boş kapasite yok. " +
                             "İlgili şubeler salonsuz bırakıldı; salon kapasitelerini veya takvimi genişletin.");

            return sonuc
                .OrderBy(s => s.Gun).ThenBy(s => s.Saat)
                .ThenBy(s => BolumSira(s.Bolum)).ThenBy(s => s.DersAdi)
                .ToList();
        }

        /// <summary>Satırın salon-şube dağılımına yazar; SalonDagilimi'ni senkron tutar.</summary>
        private static void Kaydet(GunSatir satir, string salonKodu, string harf, int adet)
        {
            if (adet <= 0) return;
            if (!satir.SalonSubeDagilimi.TryGetValue(salonKodu, out var m))
            {
                m = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                satir.SalonSubeDagilimi[salonKodu] = m;
            }
            m[harf] = (m.TryGetValue(harf, out int v) ? v : 0) + adet;
            satir.SalonDagilimi[salonKodu] =
                (satir.SalonDagilimi.TryGetValue(salonKodu, out int t) ? t : 0) + adet;
        }

        private static List<List<Salon>> SalonPoollari(bool uygulamali, bool elmMekOncelik,
            List<Salon> uygulamaliSalonlar, List<Salon> t4Salonlar, List<Salon> digerSalonlar)
        {
            if (uygulamali) return new List<List<Salon>> { uygulamaliSalonlar };
            return elmMekOncelik
                ? new List<List<Salon>> { t4Salonlar, digerSalonlar }
                : new List<List<Salon>> { digerSalonlar, t4Salonlar };
        }

        /// <summary>
        /// Bir GunSatir (+ opsiyonel şube harfi) verilen havuza dahil mi?
        /// harf null ise satırın HERHANGİ bir şubesinin uyması yeterlidir.
        /// </summary>
        private static bool HavuzaUyuyor(DersHavuzu havuz, GunSatir satir, string harf)
        {
            if (NormAd(satir.DersAdi) != NormAd(havuz.DersAdi)) return false;

            bool subeSeviyesi = havuz.Uyeler != null && havuz.Uyeler.Count > 0;
            if (subeSeviyesi)
            {
                return havuz.Uyeler.Any(uye =>
                    BolumKisalt(uye.Bolum) == satir.Bolum
                    && uye.SinifSeviyesi == satir.Sinif
                    && (harf == null || HarfKolonu(uye.Sube) == harf));
            }

            // Geriye dönük: bölüm seviyesi — bölümün tüm şubeleri havuza girer
            if (havuz.Bolumler == null || havuz.Bolumler.Count < 2) return false;
            return havuz.Bolumler.Any(b => BolumKisalt(b) == satir.Bolum);
        }

        /// <summary>GunSatir listesini gözetmen/matris için SinavAtama listesine çevirir.</summary>
        public static List<SinavAtama> SinavAtamalarinaCevir(List<GunSatir> satirlar, List<Salon> salonlar)
        {
            var kapMap = (salonlar ?? new List<Salon>())
                .GroupBy(s => s.SalonKodu).ToDictionary(g => g.Key, g => g.First().Kapasite);

            var liste = new List<SinavAtama>();
            foreach (var s in satirlar ?? new List<GunSatir>())
            {
                var atama = new SinavAtama
                {
                    DersAdi = s.DersAdi,
                    BolumAdi = s.Bolum,
                    SinifSeviyesi = s.Sinif,
                    Kontenjan = s.Toplam,
                    Sube = string.Join("+", s.Subeler.Keys),
                    OgretimElemani = s.OgretimElemani,
                    Gun = s.Gun,
                    Saat = s.Saat,
                    AtananSalonlar = s.SalonDagilimi
                        .OrderBy(kv => kv.Key)
                        .Select(kv => new SalonAtama
                        {
                            SalonKodu = kv.Key,
                            SalonKapasitesi = kapMap.TryGetValue(kv.Key, out int k) ? k : 0,
                            AtananOgrenciSayisi = kv.Value,
                            Sube = s.SalonSubeEtiketi(kv.Key)
                        }).ToList()
                };
                liste.Add(atama);
            }
            return liste;
        }

        // ── Yardımcılar ──
        private static bool T4mu(string salonKodu)
            => (salonKodu ?? "").Trim().ToUpperInvariant().StartsWith("T4");

        /// <summary>
        /// T4 binası önceliğine sahip bölümler: Elektrik-Elektronik Mühendisliği (ELM/EEM)
        /// ve Mekatronik Mühendisliği (MEK).
        /// (ESKİ KURAL: Mekatronik + Makine idi — kullanıcı kuralı gereği ELM+MEK oldu.)
        /// </summary>
        private static bool ElmMek(string bolumKisa)
        {
            string b = (bolumKisa ?? "").Trim().ToUpperInvariant();
            return b == "MEK" || b == "EEM" || b == "ELM";
        }

        private static int BolumSira(string b)
        {
            switch (b) { case "BİL": return 0; case "EEM": return 1; case "INS": return 2;
                         case "MAK": return 3; case "MEK": return 4; case "MEM": return 5; default: return 9; }
        }

        private static string NormAd(string ad)
        {
            if (string.IsNullOrEmpty(ad)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in ad.ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Şube etiketini A/B/C/D harf kolonuna indirger (eski "INT" → "D").</summary>
        private static string HarfKolonu(string sube)
            => SubeEtiketi.Normalize(sube, SubeEtiketi.DordunculSube);

        private static string BolumKisalt(string bolumAdi)
        {
            if (string.IsNullOrWhiteSpace(bolumAdi)) return "?";
            string b = bolumAdi.ToUpperInvariant();
            if (b.Contains("BİLG") || b.Contains("BILG")) return "BİL";
            if (b.Contains("ELEKTR") || b.Contains("EEM")) return "EEM";
            if (b.Contains("İNŞA") || b.Contains("INSA") || b.Contains("İNŞ") || b.Contains("INS")) return "INS";
            if (b.Contains("MEKATRON") || b.Contains("MEK")) return "MEK";
            if (b.Contains("METALURJ") || b.Contains("MALZEME") || b.Contains("MEM")) return "MEM";
            if (b.Contains("MAKİN") || b.Contains("MAKIN") || b.Contains("MAKİNA") || b.Contains("MAKINA") || b.Contains("MAK")) return "MAK";
            return b.Trim().Length >= 3 ? b.Trim().Substring(0, 3) : b.Trim();
        }
    }
}
