#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Gözetmen Excel'i okunduktan sonra, uygulamadan önce gösterilen özet.</summary>
    public sealed class GozetmenListesiOnizlemesi
    {
        public List<Gozetmen> Gozetmenler { get; init; }
        public List<string> Uyarilar { get; init; }

        /// <summary>Bölüm başına kişi sayısı (bölüm adına göre sıralı).</summary>
        public List<(string Bolum, int Kisi)> Bolumler => Gozetmenler
            .GroupBy(g => g.Bolum ?? "")
            .Select(g => (g.Key, g.Count()))
            .OrderBy(x => x.Key, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true))
            .ToList();

        public bool Bos => Gozetmenler.Count == 0;
    }

    /// <summary>Gözetmen atamasının sonucu (diyalogda gösterilir).</summary>
    public sealed class GozetmenAtamaSonucu
    {
        public int ToplamAtama { get; init; }
        public List<string> Uyarilar { get; init; }

        /// <summary>Ağırlık grubuna göre ortalama görev: (ağırlık, ortalama sınav, kişi).</summary>
        public List<(double Agirlik, double Ortalama, int Kisi)> AgirlikOzeti { get; init; }
    }

    public enum GozetmenAlani
    {
        Kod,
        AdSoyad,
        Bolum,
        Agirlik
    }

    /// <summary>
    /// GÖZETMENLER — gözetmen listesi yükleme, tabloda düzeltme, ekleme / silme ve salonlara gözetmen
    /// atama (eski BtnGozetmenYukle_Click, DgvGozetmenler_CellEndEdit, GozetmenEkle, GozetmenleriSil,
    /// BtnGozetmenAta_Click ve yardımcıları; kurallar ve mesajlar aynen).
    /// </summary>
    public class GozetmenServisi
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        /// <summary>Gözetmen Excel'inde görülen yazımla 6 standart bölüm (açılır liste için).</summary>
        public static readonly string[] StandartGozetmenBolumleri =
        {
            "Bilgisayar Mühendisliği", "Elektrik Elektronik Mühendisliği", "İnşaat Mühendisliği",
            "Makine Mühendisliği", "Mekatronik Mühendisliği", "Metalurji ve Malzeme Mühendisliği"
        };

        private readonly CalismaDurumu _d;

        public GozetmenServisi(CalismaDurumu durum)
        {
            _d = durum;
        }

        // ═══════════════════════════════════════════════════════════════
        // LİSTE YÜKLEME
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Gözetmen Excel'ini okur; çalışma durumuna dokunmaz (arka planda çalışabilir).</summary>
        public static GozetmenListesiOnizlemesi Onizle(string yol)
        {
            var okunanlar = ExcelImportHelper.GozetmenleriOku(yol, out var uyarilar);
            return new GozetmenListesiOnizlemesi { Gozetmenler = okunanlar, Uyarilar = uyarilar };
        }

        /// <summary>Okunan listeyi geçerli gözetmen listesi yapar (mevcut liste tamamen değişir).</summary>
        public void ListeyiUygula(GozetmenListesiOnizlemesi onizleme)
        {
            _d.GozetmenListesi.Clear();
            _d.GozetmenListesi.AddRange(onizleme.Gozetmenler);
            _d.DurumYaz($"✅ {onizleme.Gozetmenler.Count} gözetmen yüklendi.", DurumTuru.Basari);
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        // YARDIMCILAR (eski adlarıyla)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Bölüm açılır listesinin seçenekleri: listede kullanılan yazımlar aynen + henüz görünmeyen
        /// standart bölümler (Türkçe sıralı).
        /// </summary>
        public List<string> GozetmenBolumSecenekleri()
        {
            var secenekler = _d.GozetmenListesi
                .Select(g => g.Bolum?.Trim())
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .ToList();
            var anahtarlar = new HashSet<string>(secenekler.Select(BolumAdlari.BolumNormAnahtar));
            secenekler.AddRange(StandartGozetmenBolumleri.Where(b => !anahtarlar.Contains(BolumAdlari.BolumNormAnahtar(b))));
            return secenekler.OrderBy(b => b, StringComparer.Create(Tr, ignoreCase: true)).ToList();
        }

        public static string GozetmenAdiTemizle(string ad) => Regex.Replace(ad ?? "", @"\s+", " ").Trim();

        /// <summary>Ad karşılaştırma anahtarı: "Ali Veli" ≡ "ALİ VELİ" ≡ "ALI VELI".</summary>
        public static string GozetmenAdAnahtari(string ad)
        {
            var sb = new StringBuilder();
            foreach (char ch in ad ?? "")
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
            return sb.ToString();
        }

        /// <summary>Ad / kod geçerli ve benzersiz mi? Sorun varsa açıklamasını, yoksa null döner.</summary>
        public string GozetmenBilgisiHatasi(string adSoyad, string kod, Gozetmen haric)
        {
            if (string.IsNullOrWhiteSpace(adSoyad)) return "Ad Soyad boş bırakılamaz.";
            var ayni = _d.GozetmenListesi.FirstOrDefault(g => g != haric && GozetmenAdAnahtari(g.AdSoyad) == GozetmenAdAnahtari(adSoyad));
            if (ayni != null)
                return $"'{ayni.AdSoyad}' zaten listede.\nGözetmen atamaları ada göre yapıldığı için aynı ad iki kez bulunamaz.";
            if (!string.IsNullOrEmpty(kod) && kod.Any(char.IsDigit) &&
                _d.GozetmenListesi.Any(g => g != haric && string.Equals(g.Kod?.Trim(), kod, StringComparison.OrdinalIgnoreCase)))
                return $"'{kod}' kodu başka bir gözetmende kullanılıyor.";
            return null;
        }

        /// <summary>Mevcut programdaki salon görevlerinde gözetmen adını değiştirir; kaç satırın değiştiğini döner.</summary>
        public int GozetmenAdiniGorevlerdeDegistir(string eskiAd, string yeniAd)
        {
            int sayac = 0;
            foreach (var salon in (_d.SonSonuclar ?? new List<SinavAtama>()).SelectMany(s => s.AtananSalonlar ?? new List<SalonAtama>()))
            {
                if (!string.Equals(salon.GozetmenAdSoyad?.Trim(), eskiAd, StringComparison.OrdinalIgnoreCase)) continue;
                salon.GozetmenAdSoyad = yeniAd;
                sayac++;
            }
            return sayac;
        }

        /// <summary>Gözetmenin mevcut programdaki görev sayısı (aynı slot + salon tek görev sayılır).</summary>
        public int GozetmenGorevSayisi(string adSoyad)
            => (_d.SonSonuclar ?? new List<SinavAtama>())
                .SelectMany(s => (s.AtananSalonlar ?? new List<SalonAtama>())
                    .Where(a => string.Equals(a.GozetmenAdSoyad?.Trim(), adSoyad, StringComparison.OrdinalIgnoreCase))
                    .Select(a => (s.Gun, s.Saat, a.SalonKodu)))
                .Distinct()
                .Count();

        public static string AgirlikMetni(double agirlik) => agirlik.ToString("0.0#");

        // ═══════════════════════════════════════════════════════════════
        // DÜZENLEME
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Tablodaki hücre düzenlemesi. Hata varsa açıklamasını döner (değer uygulanmaz), yoksa null.</summary>
        public string AlaniDegistir(Gozetmen gozetmen, GozetmenAlani alan, string deger)
        {
            switch (alan)
            {
                case GozetmenAlani.AdSoyad:
                {
                    string yeniAd = GozetmenAdiTemizle(deger);
                    if (yeniAd == gozetmen.AdSoyad) return null;
                    string hata = GozetmenBilgisiHatasi(yeniAd, null, gozetmen);
                    if (hata != null) return hata;

                    string eskiAd = gozetmen.AdSoyad;
                    gozetmen.AdSoyad = yeniAd;
                    int guncellenen = GozetmenAdiniGorevlerdeDegistir(eskiAd, yeniAd);
                    _d.DurumYaz($"✏ '{eskiAd}' → '{yeniAd}' olarak düzeltildi" +
                                (guncellenen > 0 ? $" ({guncellenen} salon görevinde de güncellendi)." : "."), DurumTuru.Uyari);
                    break;
                }
                case GozetmenAlani.Kod:
                {
                    string yeniKod = deger?.Trim() ?? "";
                    string hata = GozetmenBilgisiHatasi(gozetmen.AdSoyad, yeniKod, gozetmen);
                    if (hata != null) return hata;
                    if (yeniKod == (gozetmen.Kod ?? "")) return null;
                    gozetmen.Kod = yeniKod;
                    break;
                }
                case GozetmenAlani.Bolum:
                {
                    string yeniBolum = deger?.Trim();
                    if (string.IsNullOrEmpty(yeniBolum) || yeniBolum == gozetmen.Bolum) return null;
                    gozetmen.Bolum = yeniBolum;
                    _d.DurumYaz($"✏ '{gozetmen.AdSoyad}' bölümü '{yeniBolum}' olarak değiştirildi" +
                                (gozetmen.AtananSinavSayisi > 0 ? " — bölüme göre dağıtım için 'Gözetmen Ata'yı tekrar çalıştırın." : "."),
                                DurumTuru.Uyari);
                    break;
                }
                default:
                {
                    string ham = deger?.Replace(',', '.').Trim();
                    if (!double.TryParse(ham, NumberStyles.Float, CultureInfo.InvariantCulture, out double agirlik) || agirlik <= 0)
                        return "Ağırlık 0'dan büyük bir sayı olmalıdır (örn: 1.0, 1.5, 2.0).";
                    if (agirlik == gozetmen.Agirlik) return null;
                    gozetmen.Agirlik = agirlik;
                    _d.DurumYaz($"✏ '{gozetmen.AdSoyad}' ağırlığı {agirlik:0.0#} olarak güncellendi.", DurumTuru.Uyari);
                    break;
                }
            }
            _d.Bildir();
            return null;
        }

        /// <summary>Elle gözetmen ekler; ad ve bölüm zorunlu. Hata varsa açıklamasını döner.</summary>
        public string GozetmenEkle(string adSoyad, string bolum, string kod, double agirlik, out Gozetmen eklenen)
        {
            eklenen = null;
            string ad = GozetmenAdiTemizle(adSoyad);
            kod = kod?.Trim() ?? "";
            string hata = GozetmenBilgisiHatasi(ad, kod, null)
                          ?? (string.IsNullOrWhiteSpace(bolum) ? "Bölüm seçin." : null)
                          ?? (agirlik <= 0 ? "Ağırlık 0'dan büyük bir sayı olmalıdır (örn: 1.0, 1.5, 2.0)." : null);
            if (hata != null) return hata;

            eklenen = new Gozetmen { AdSoyad = ad, Bolum = bolum.Trim(), Kod = kod, Agirlik = agirlik, AtananSinavSayisi = 0 };
            _d.GozetmenListesi.Add(eklenen);
            _d.DurumYaz($"➕ '{eklenen.AdSoyad}' gözetmen listesine eklendi" +
                        (_d.SonSonuclar != null && _d.SonSonuclar.Count > 0 ? " — görev alması için 'Gözetmen Ata'yı tekrar çalıştırın." : "."),
                        DurumTuru.Basari);
            _d.Bildir();
            return null;
        }

        /// <summary>Silme onayında gösterilecek metin (görevi olanlar uyarılır).</summary>
        public string SilmeOnayMetni(IReadOnlyList<Gozetmen> silinecekler)
        {
            int gorev = silinecekler.Sum(g => GozetmenGorevSayisi(g.AdSoyad));
            string kimler = silinecekler.Count == 1
                ? $"'{silinecekler[0].AdSoyad}'"
                : $"{silinecekler.Count} gözetmen ({string.Join(", ", silinecekler.Take(5).Select(g => g.AdSoyad))}" +
                  (silinecekler.Count > 5 ? ", ..." : "") + ")";
            string mesaj = $"{kimler} gözetmen listesinden silinecek.";
            if (gorev > 0)
                mesaj += $"\n\nMevcut programda {gorev} salon görevi var; bu görevler gözetmensiz kalacak.\n" +
                         "'Gözetmen Ata' ile yeniden dağıtabilir ya da gözetmenli taslakta elle doldurabilirsiniz.";
            return mesaj + "\n\nEmin misiniz?";
        }

        /// <summary>Gözetmenleri siler; programdaki görevleri boşaltılır. Boşalan görev sayısını döner.</summary>
        public int GozetmenleriSil(IReadOnlyList<Gozetmen> silinecekler)
        {
            if (silinecekler == null || silinecekler.Count == 0) return 0;
            int gorev = silinecekler.Sum(g => GozetmenGorevSayisi(g.AdSoyad));
            foreach (var g in silinecekler)
            {
                _d.GozetmenListesi.Remove(g);
                GozetmenAdiniGorevlerdeDegistir(g.AdSoyad, "");
            }
            _d.DurumYaz((silinecekler.Count == 1
                            ? $"🗑 '{silinecekler[0].AdSoyad}' gözetmen listesinden silindi"
                            : $"🗑 {silinecekler.Count} gözetmen listeden silindi") +
                        (gorev > 0 ? $" — {gorev} salon görevi boş kaldı, 'Gözetmen Ata'yı tekrar çalıştırın." : "."), DurumTuru.Uyari);
            _d.Bildir();
            return gorev;
        }

        // ═══════════════════════════════════════════════════════════════
        // GÖZETMEN ATA
        // ═══════════════════════════════════════════════════════════════

        public string AtamaEksigi()
        {
            if (_d.SonSonuclar == null || _d.SonSonuclar.Count == 0)
                return "Önce 5. adımda 'Salonlu Excel yükle' (veya 'Salonları ata') ile salon atamalarını yükleyin!";
            if (_d.GozetmenListesi.Count == 0)
                return "Önce 'Gözetmen listesi yükle' ile gözetmen listesini yükleyin!";
            return null;
        }

        /// <summary>
        /// Gözetmen esnetmesinin yapılabileceği ortak dersler: elle seçim varsa o liste; değilse ders
        /// kodu bölüm önekiyle başlamayan dersler; sınav listesi de yoksa null (algoritma kendisi bulur).
        /// </summary>
        public List<string> GozetmenOrtakDersleri()
        {
            if (_d.OrtakSecimYapildi) return _d.ManuelOrtakDersAdlari.ToList();
            if (_d.Dersler.Count > 0)
                return _d.Dersler.Where(d => BolumAdlari.KodBolumDisiMi(d.DersKodu))
                                 .Select(d => d.DersAdi)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToList();
            return null;
        }

        /// <summary>
        /// Salonlara gözetmen atar (sonuçlar ve gözetmen sayaçları yerinde güncellenir). Arayüz kilitliyken
        /// arka planda çalışabilir; ardından <see cref="AtamaTamamlandi"/> arayüz iş parçacığında çağrılır.
        /// </summary>
        public GozetmenAtamaSonucu GozetmenleriAta()
        {
            var algoritma = new GozetmenAtamaAlgoritmasi();
            algoritma.GozetmenleriAta(_d.SonSonuclar, _d.GozetmenListesi, GozetmenOrtakDersleri());
            return new GozetmenAtamaSonucu
            {
                ToplamAtama = algoritma.ToplamAtama,
                Uyarilar = algoritma.Uyarilar.ToList(),
                AgirlikOzeti = AgirlikOzeti()
            };
        }

        public void AtamaTamamlandi(GozetmenAtamaSonucu sonuc)
        {
            // Gözetmen atandı → taslak açılır; yeniden atama önceki düzenlenmiş dosyayı geçersiz kılar
            var k = _d.Kilitler;
            k.MatrisExport = true;
            k.GozetmenliYukle = false;
            k.FinalExport = false;

            if (sonuc.Uyarilar.Count > 0)
                _d.DurumYaz($"⚠ {sonuc.ToplamAtama} salon-gözetmen ataması yapıldı, {sonuc.Uyarilar.Count} uyarı!", DurumTuru.Uyari);
            else
                _d.DurumYaz($"✅ Gözetmen ataması tamamlandı — {sonuc.ToplamAtama} salon için gözetmen atandı.", DurumTuru.Basari);
            _d.Bildir();
        }

        /// <summary>Ağırlık gruplarına göre ortalama görev sayısı (ağırlığın etkisi görünsün diye).</summary>
        public List<(double Agirlik, double Ortalama, int Kisi)> AgirlikOzeti()
            => _d.GozetmenListesi
                .GroupBy(g => g.Agirlik)
                .OrderByDescending(gr => gr.Key)
                .Select(gr => (gr.Key, gr.Average(g => (double)g.AtananSinavSayisi), gr.Count()))
                .ToList();

        /// <summary>Arama: ad, kod ya da bölümde geçen (Türkçe, büyük/küçük harf duyarsız).</summary>
        public static List<Gozetmen> Filtrele(IEnumerable<Gozetmen> liste, string filtre)
        {
            filtre = filtre?.Trim();
            if (string.IsNullOrEmpty(filtre)) return liste.ToList();
            var ci = Tr.CompareInfo;
            bool Var(string s) => s != null && ci.IndexOf(s, filtre, CompareOptions.IgnoreCase) >= 0;
            return liste.Where(g => Var(g.AdSoyad) || Var(g.Kod) || Var(g.Bolum)).ToList();
        }
    }
}
