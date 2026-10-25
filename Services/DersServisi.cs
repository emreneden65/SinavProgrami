#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Sınav listesi Excel'i okunduktan sonra, uygulamadan önce gösterilen özet.</summary>
    public sealed class SinavListesiOnizlemesi
    {
        public List<Ders> Dersler { get; init; }
        public List<string> Uyarilar { get; init; }

        /// <summary>Bölüm başına farklı ders kodu ve şube sayısı (bölüm adına göre sıralı).</summary>
        public List<BolumOzeti> Bolumler { get; init; }

        public int SubeSayisi => Dersler.Count;
        public int KodSayisi => Dersler.Select(d => d.DersKodu).Distinct().Count();
        public bool Bos => Dersler.Count == 0;
    }

    public sealed class BolumOzeti
    {
        public string Bolum { get; init; }
        public int KodSayisi { get; init; }
        public int SubeSayisi { get; init; }
    }

    /// <summary>Hata oranı tablosunun bir satırı.</summary>
    public sealed class HataOraniSatiri
    {
        /// <summary>Görünen bölüm adı (ders listesindeki yazım öncelikli).</summary>
        public string Bolum { get; init; }
        public double Oran { get; init; }

        /// <summary>Tabloda görünen biçim ("12,5" / "12.5" — sistem ayarına göre).</summary>
        public string Metin => Oran.ToString("0.#");
    }

    /// <summary>Ders tablosunda düzenlenebilen alanlar.</summary>
    public enum DersAlani
    {
        DersAdi,
        Bolum,
        Sinif,
        Sube,
        Kontenjan
    }

    /// <summary>
    /// DERSLER — sınav listesi yükleme, ders tablosunda satır içi düzenleme / ekleme / silme,
    /// uygulamalı dersler ve bölüm bazlı hata oranları. Eski arayüzdeki BtnExcelImport_Click,
    /// DgvDersler_CellEndEdit, BtnDersSil_Click, uygulamalı ders ve hata oranı tablolarının
    /// kuralları aynen taşınmıştır.
    /// </summary>
    public class DersServisi
    {
        private static readonly CompareInfo TrKarsilastir = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

        private readonly CalismaDurumu _d;
        private readonly HocaEslemesi _hoca;

        public DersServisi(CalismaDurumu durum, HocaEslemesi hoca)
        {
            _d = durum;
            _hoca = hoca;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SINAV LİSTESİ YÜKLEME
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Dosyayı okur ve özetini çıkarır; çalışma durumuna dokunmaz (arka planda çalışabilir).</summary>
        public static SinavListesiOnizlemesi Onizle(string dosyaYolu)
        {
            var okuma = SinavListesiOkuyucu.Oku(dosyaYolu);
            return new SinavListesiOnizlemesi
            {
                Dersler = okuma.Dersler,
                Uyarilar = okuma.Uyarilar,
                Bolumler = okuma.Dersler
                    .GroupBy(d => d.BolumAdi)
                    .Select(g => new BolumOzeti
                    {
                        Bolum = g.Key,
                        KodSayisi = g.Select(x => x.DersKodu).Distinct().Count(),
                        SubeSayisi = g.Count()
                    })
                    .OrderBy(b => b.Bolum)
                    .ToList()
            };
        }

        /// <summary>
        /// Onaylanan listeyi uygular. Hoca bilgileri kalıcı eşlemeye kaydedilir; yeni liste
        /// yeni şablon gerektirdiği için şablon ve sonrası sıfırlanır.
        /// Eski arayüzden farkı (onaylı düzeltmeler): takvim KORUNUR (eskisi sessizce siliyordu);
        /// eski salon ataması ve 'Salonlu Excel Çıkart' kilidi de sıfırlanır.
        /// </summary>
        public void SinavListesiniUygula(SinavListesiOnizlemesi onizleme)
        {
            _d.Dersler.Clear();
            _d.Dersler.AddRange(onizleme.Dersler);
            _hoca.GuncelleVeKaydet(_d.Dersler);

            _d.SablonSonuclari = null;
            _d.SablonIntNotlari.Clear();
            _d.SonSonuclar = null;
            _d.GunSalonSatirlari = null;
            _d.UygulamaliDersleriBuda();

            var k = _d.Kilitler;
            k.OtomatikDagit = true;
            k.SalonAta = false;
            k.SalonExport = false;
            k.GozetmenAta = false;
            k.MatrisExport = false;
            k.GozetmenliYukle = false;
            k.FinalExport = false;

            _d.DurumYaz($"✅ {onizleme.Dersler.Count} şube yüklendi. Şimdi 4. adımda 'Otomatik Şablon' oluşturun.", DurumTuru.Basari);
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        //  DERS TABLOSU — satır içi düzenleme / ekleme / silme
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Arama: ders adında ya da bölümde geçen (Türkçe, büyük/küçük harf duyarsız).</summary>
        public static List<Ders> Filtrele(IEnumerable<Ders> dersler, string filtre)
        {
            filtre = filtre?.Trim();
            if (string.IsNullOrEmpty(filtre)) return dersler.ToList();
            return dersler
                .Where(d => TrKarsilastir.IndexOf(d.DersAdi ?? "", filtre, CompareOptions.IgnoreCase) >= 0
                         || TrKarsilastir.IndexOf(d.BolumAdi ?? "", filtre, CompareOptions.IgnoreCase) >= 0)
                .ToList();
        }

        public const string SinifHataMesaji = "Sınıf 0 ile 9 arasında bir tam sayı olmalıdır.";

        /// <summary>
        /// Hücre değerlerini eski tablodaki kurallarla düzeltir. Sınıf eski tablodan farklı olarak 1–4'e
        /// sıkıştırılmaz: sınav listesi okuyucusu gibi 0–9 kabul edilir, aralık dışı ya da sayı olmayan değer hatadır.
        /// </summary>
        private static (string DersAdi, string Bolum, int Sinif, string Sube, int Kontenjan, bool SinifGecerli) Duzelt(
            string dersAdi, string bolum, string sinif, string sube, string kontenjan)
        {
            string ad = dersAdi?.Trim().ToUpperInvariant() ?? "";
            string bol = bolum?.Trim() ?? "";
            string sb = sube?.Trim().ToUpperInvariant() ?? "A";
            if (string.IsNullOrEmpty(sb)) sb = "A";

            bool sinifGecerli = int.TryParse(sinif?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sn)
                                && sn >= 0 && sn <= 9;

            int kont = 0;
            if (kontenjan != null)
            {
                int.TryParse(kontenjan, out kont);
                if (kont < 0) kont = 0;
            }
            return (ad, bol, sn, sb, kont, sinifGecerli);
        }

        /// <summary>
        /// Tek hücre düzenlemesi. Satırın diğer hücreleri de eski tablodaki gibi yeniden okunur ve
        /// düzeltilir; yalnızca sınıf, başka bir alan düzenlenirken olduğu gibi kalır (0 olan sınıf 1'e çekilmez).
        /// Hata varsa açıklamasını döner (değer uygulanmaz), yoksa null.
        /// </summary>
        public string DersAlaniniDegistir(Ders ders, DersAlani alan, string yeniDeger)
        {
            var n = Duzelt(
                alan == DersAlani.DersAdi ? yeniDeger : ders.DersAdi,
                alan == DersAlani.Bolum ? yeniDeger : ders.BolumAdi,
                alan == DersAlani.Sinif ? yeniDeger : ders.SinifSeviyesi.ToString(),
                alan == DersAlani.Sube ? yeniDeger : ders.Sube,
                alan == DersAlani.Kontenjan ? yeniDeger : ders.Kontenjan.ToString());

            // Düzenlenen alanın değeri aynı kaldıysa (yalnızca hücreye girilip çıkıldıysa) işlem yok
            bool alanAyni = alan switch
            {
                DersAlani.DersAdi => n.DersAdi == ders.DersAdi,
                DersAlani.Bolum => n.Bolum == ders.BolumAdi,
                DersAlani.Sinif => n.Sinif == ders.SinifSeviyesi,
                DersAlani.Sube => n.Sube == ders.Sube,
                _ => n.Kontenjan == ders.Kontenjan
            };
            if (alan == DersAlani.Sinif && !n.SinifGecerli) return SinifHataMesaji;
            if (alanAyni) return null;

            if (string.IsNullOrEmpty(n.DersAdi) || string.IsNullOrEmpty(n.Bolum))
                return "Ders Adı ve Bölüm boş bırakılamaz!";

            ders.DersAdi = n.DersAdi;
            ders.BolumAdi = n.Bolum;
            if (alan == DersAlani.Sinif) ders.SinifSeviyesi = n.Sinif;
            ders.Sube = n.Sube;
            ders.Kontenjan = n.Kontenjan;

            _d.UygulamaliDersleriBuda();
            _d.DurumYaz($"✏ '{ders.DersAdi}' güncellendi.", DurumTuru.Uyari);
            _d.Bildir();
            return null;
        }

        /// <summary>Elle yeni ders (şube) ekler; ders adı ve bölüm zorunludur. Hata varsa açıklamasını döner.</summary>
        public string DersEkle(string dersAdi, string bolum, string sinif, string sube, string kontenjan, out Ders eklenen)
        {
            eklenen = null;
            var n = Duzelt(dersAdi, bolum, string.IsNullOrWhiteSpace(sinif) ? "1" : sinif, sube, kontenjan);
            if (string.IsNullOrEmpty(n.DersAdi) || string.IsNullOrEmpty(n.Bolum))
                return "Ders Adı ve Bölüm boş bırakılamaz!";
            if (!n.SinifGecerli) return SinifHataMesaji;

            eklenen = new Ders
            {
                DersAdi = n.DersAdi,
                BolumAdi = n.Bolum,
                SinifSeviyesi = n.Sinif,
                Sube = n.Sube,
                Kontenjan = n.Kontenjan
            };
            _d.Dersler.Add(eklenen);
            _d.UygulamaliDersleriBuda();
            _d.DurumYaz($"➕ '{n.DersAdi}' başarıyla listeye eklendi.", DurumTuru.Basari);
            _d.Bildir();
            return null;
        }

        /// <summary>Seçilen şubeleri listeden siler (onay çağıran tarafta alınır).</summary>
        public void DersleriSil(IReadOnlyCollection<Ders> silinecekler)
        {
            if (silinecekler == null || silinecekler.Count == 0) return;
            foreach (var ders in silinecekler)
                _d.Dersler.Remove(ders);
            _d.UygulamaliDersleriBuda();
            _d.DurumYaz(silinecekler.Count == 1
                ? $"🗑 '{silinecekler.First().DersAdi}' listeden silindi."
                : $"🗑 {silinecekler.Count} şube listeden silindi.", DurumTuru.Uyari);
            _d.Bildir();
        }

        /// <summary>Elle eklemede önerilen bölüm adları: listedeki yazımlar, yoksa sabit bölüm listesi.</summary>
        public List<string> BolumSecenekleri()
        {
            var liste = _d.Dersler.Select(d => d.BolumAdi?.Trim())
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b)
                .ToList();
            return liste.Count > 0 ? liste : BolumAdlari.StandartBolumler.ToList();
        }

        // ═══════════════════════════════════════════════════════════════
        //  UYGULAMALI DERSLER — yalnızca UYGULAMALI (lab / teknik resim) salonlara atanır
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Ders listesindeki farklı ders adları (uygulamalı işaretlenebilecekler).</summary>
        public List<string> UygulamaliAdaylari()
            => _d.Dersler
                .Select(d => d.DersAdi)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a)
                .ToList();

        public static List<string> AdlariFiltrele(IEnumerable<string> adlar, string filtre)
        {
            filtre = filtre?.Trim();
            if (string.IsNullOrEmpty(filtre)) return adlar.ToList();
            return adlar.Where(a => TrKarsilastir.IndexOf(a, filtre, CompareOptions.IgnoreCase) >= 0).ToList();
        }

        public void UygulamaliAyarla(string dersAdi, bool uygulamali)
        {
            if (string.IsNullOrEmpty(dersAdi)) return;
            if (uygulamali) _d.UygulamaliDersler.Add(dersAdi);
            else _d.UygulamaliDersler.Remove(dersAdi);
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        //  HATA ORANI — bölüm bazlı "sınava gelmeyecek kişi" yüzdesi.
        //  Örn: 120 kontenjan + %20 → salonlara 96 kişi gibi dağıtılır.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tablodaki bölümler: ders listesindeki adlar, sonra sabit bölüm listesi; "X MÜHENDİSLİĞİ"
        /// ile "X MÜHENDİSLİĞİ BÖLÜMÜ" gibi yazım farkları tek satırdır (ders listesindeki yazım görünür).
        /// </summary>
        public List<HataOraniSatiri> HataOraniSatirlari()
        {
            var gorulen = new Dictionary<string, string>(StringComparer.Ordinal);   // normalize anahtar → görünen ad
            foreach (var b in _d.Dersler.Select(d => d.BolumAdi).Concat(BolumAdlari.StandartBolumler))
            {
                if (string.IsNullOrWhiteSpace(b)) continue;
                string ad = b.Trim();
                string anahtar = BolumAdlari.BolumNormAnahtar(ad);
                if (anahtar.Length == 0 || gorulen.ContainsKey(anahtar)) continue;
                gorulen[anahtar] = ad;
            }
            return gorulen.Values.OrderBy(b => b)
                .Select(b => new HataOraniSatiri { Bolum = b, Oran = HataOraniBul(b) })
                .ToList();
        }

        /// <summary>Kaydedilmiş hata oranını normalize bölüm anahtarıyla bulur (yazım farkına dayanıklı).</summary>
        public double HataOraniBul(string bolum)
        {
            if (_d.BolumHataOranlari.TryGetValue(bolum, out double v)) return v;
            string anahtar = BolumAdlari.BolumNormAnahtar(bolum);
            foreach (var kv in _d.BolumHataOranlari)
                if (BolumAdlari.BolumNormAnahtar(kv.Key) == anahtar) return kv.Value;
            return 0;
        }

        /// <summary>Tabloya yazılan yüzdeyi kaydeder (0–100); geçersizse açıklamasını döner.</summary>
        public string HataOraniAyarla(string bolum, string ham)
        {
            if (string.IsNullOrEmpty(bolum)) return null;
            string temiz = ham?.Replace("%", "").Replace(',', '.').Trim();
            if (double.TryParse(temiz, NumberStyles.Float, CultureInfo.InvariantCulture, out double oran)
                && oran >= 0 && oran <= 100)
            {
                _d.BolumHataOranlari[bolum] = oran;
                _d.Bildir();
                return null;
            }
            return "Lütfen 0 ile 100 arasında geçerli bir yüzde giriniz.";
        }

        /// <summary>
        /// Algoritmalara verilen hata oranları: tablodaki her bölüm için görünen değer (0.# yuvarlaması
        /// dahil) okunur, 0'dan büyük ve 100'den küçük-eşit olanlar alınır (eski HataOranlariniOku).
        /// </summary>
        public Dictionary<string, double> HataOranlariniOku()
        {
            var sonuc = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var satir in HataOraniSatirlari())
            {
                string ham = satir.Metin.Replace("%", "").Replace(',', '.').Trim();
                if (double.TryParse(ham, NumberStyles.Float, CultureInfo.InvariantCulture, out double oran)
                    && oran > 0 && oran <= 100)
                    sonuc[satir.Bolum] = oran;
            }
            return sonuc;
        }
    }
}
