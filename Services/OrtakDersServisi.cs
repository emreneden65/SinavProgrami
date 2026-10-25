#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Ortak / havuz seçiminde listelenen bir şube satırı.</summary>
    public sealed class HavuzSubeSecim
    {
        public string DersAdi { get; init; }
        public string DersKodu { get; init; }
        public string Bolum { get; init; }
        public int Sinif { get; init; }
        public string Sube { get; init; }
        public int Kontenjan { get; init; }

        public override string ToString() => $"{Bolum} — {Sinif}. Sınıf, Şube {Sube} ({Kontenjan} öğr.)";
    }

    /// <summary>Ortak ders adayı: bir ders (kod ya da ad) ve okutulduğu bölümler.</summary>
    public sealed class OrtakDersAday
    {
        public string DersKodu { get; init; }
        public string DersAdi { get; init; }
        public List<string> Bolumler { get; init; }
        public int ToplamKontenjan { get; init; }

        /// <summary>Birden çok bölümde okutulan ders ortak ders olarak önerilir.</summary>
        public bool Onerilen => Bolumler.Count >= 2;
    }

    /// <summary>
    /// ORTAK DERSLER VE HAVUZLAR — eski "Havuz / Ortak Ders Yönetimi" penceresinin kuralları.
    ///  • Ortak dersler: işaretlenen dersler tüm bölümlerinde AYNI gün ve saate çivilenir. Elle seçim
    ///    yapılınca otomatik tespit devre dışı kalır (seçim, eski pencere kapanırken olduğu gibi
    ///    "önce temizle, sonra işaretlileri ekle" biçiminde kaydedilir).
    ///  • Havuz: bir ders için farklı bölümlerden seçilen şubeler salon atamasında tek kontenjan
    ///    gibi birleştirilip mümkünse aynı salona konur.
    /// Veri kaynağı önce sınav listesi, o yoksa yüklü şablondur.
    /// </summary>
    public class OrtakDersServisi
    {
        private readonly CalismaDurumu _d;

        public OrtakDersServisi(CalismaDurumu durum)
        {
            _d = durum;
        }

        /// <summary>Seçim kaynağı: sınav listesi; o yoksa şablondaki yerleşimler (kodsuz).</summary>
        public List<HavuzSubeSecim> Kaynak()
        {
            if (_d.Dersler.Count > 0)
                return _d.Dersler.Select(d => new HavuzSubeSecim
                {
                    DersAdi = (d.DersAdi ?? "").Trim(), DersKodu = (d.DersKodu ?? "").Trim(),
                    Bolum = d.BolumAdi, Sinif = d.SinifSeviyesi, Sube = d.Sube, Kontenjan = d.Kontenjan
                }).ToList();

            if (_d.SablonSonuclari != null && _d.SablonSonuclari.Count > 0)
                return _d.SablonSonuclari.Select(a => new HavuzSubeSecim
                {
                    DersAdi = (a.DersAdi ?? "").Trim(), DersKodu = "",
                    Bolum = a.BolumAdi, Sinif = a.SinifSeviyesi, Sube = a.Sube, Kontenjan = a.Kontenjan
                }).ToList();

            return new List<HavuzSubeSecim>();
        }

        // ═══════════════════════════════════════════════════════════════
        //  ORTAK DERSLER
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Adaylar: ders (kod, kodsuzsa ad) bazında; çok bölümlüler önce, sonra ada göre.</summary>
        public List<OrtakDersAday> Adaylar()
            => Kaynak()
                .GroupBy(k => string.IsNullOrEmpty(k.DersKodu) ? k.DersAdi.ToUpperInvariant() : k.DersKodu.ToUpperInvariant())
                .Select(g => new OrtakDersAday
                {
                    DersKodu = g.First().DersKodu,
                    DersAdi = g.First().DersAdi,
                    Bolumler = g.Select(x => x.Bolum).Distinct().OrderBy(b => b).ToList(),
                    ToplamKontenjan = g.Sum(x => x.Kontenjan)
                })
                .OrderByDescending(a => a.Bolumler.Count).ThenBy(a => a.DersAdi)
                .ToList();

        /// <summary>Elle seçim yapıldıysa kayıttaki seçim; yapılmadıysa öneri (birden çok bölümde okutulan).</summary>
        public bool SeciliMi(OrtakDersAday aday)
            => _d.OrtakSecimYapildi
                ? _d.ManuelOrtakKodlar.Contains(aday.DersKodu) || _d.ManuelOrtakDersAdlari.Contains(aday.DersAdi)
                : aday.Onerilen;

        /// <summary>
        /// Elle belirleme açılınca öneriler işaretli başlar (eski pencerenin ilk açılışı gibi);
        /// kapatılınca otomatik tespite dönülür.
        /// </summary>
        public void ElleBelirle(bool elle)
        {
            if (elle == _d.OrtakSecimYapildi) return;
            if (elle)
            {
                SecimiKaydet(Adaylar().Where(a => a.Onerilen));
            }
            else
            {
                _d.OrtakSecimYapildi = false;
                _d.ManuelOrtakKodlar.Clear();
                _d.ManuelOrtakDersAdlari.Clear();
                _d.DurumYaz("⭐ Ortak dersler otomatik belirlenecek (elle seçim kaldırıldı).", DurumTuru.Bilgi);
                _d.Bildir();
            }
        }

        public void Ayarla(OrtakDersAday aday, bool ortak)
        {
            var secili = Adaylar().Where(a => a.DersAdi == aday.DersAdi && a.DersKodu == aday.DersKodu ? ortak : SeciliMi(a));
            SecimiKaydet(secili);
        }

        public void OnerilenleriIsaretle() => SecimiKaydet(Adaylar().Where(a => a.Onerilen));

        public void SecimiTemizle() => SecimiKaydet(Enumerable.Empty<OrtakDersAday>());

        /// <summary>İşaretli adayları kaydeder: kod (varsa) ve ad listeleri baştan yazılır.</summary>
        private void SecimiKaydet(IEnumerable<OrtakDersAday> secilenler)
        {
            // Seçim, mevcut işaretlere bakarak hesaplanıyor olabilir — listeler temizlenmeden önce sabitlenir
            var liste = secilenler.ToList();
            _d.ManuelOrtakKodlar.Clear();
            _d.ManuelOrtakDersAdlari.Clear();
            foreach (var a in liste)
            {
                if (!string.IsNullOrEmpty(a.DersKodu)) _d.ManuelOrtakKodlar.Add(a.DersKodu);
                _d.ManuelOrtakDersAdlari.Add(a.DersAdi);
            }
            _d.OrtakSecimYapildi = true;
            OzetiYaz();
            _d.Bildir();
        }

        // ═══════════════════════════════════════════════════════════════
        //  HAVUZLAR
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Havuz kurulabilecek dersler: en az 2 farklı bölüm×şube birleşimi olanlar.</summary>
        public List<string> HavuzDersleri()
            => Kaynak()
                .GroupBy(k => k.DersAdi, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(x => $"{x.Bolum}|{x.Sube}").Distinct().Count() >= 2)
                .Select(g => g.Key).OrderBy(d => d).ToList();

        public List<HavuzSubeSecim> HavuzSubeleri(string dersAdi)
            => Kaynak()
                .Where(k => string.Equals(k.DersAdi, dersAdi, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k.Bolum).ThenBy(k => k.Sube)
                .ToList();

        /// <summary>Seçilen şubelerden havuz kurar; en az 2 şube gerekir. Hata varsa açıklamasını döner.</summary>
        public string HavuzOlustur(string dersAdi, IReadOnlyCollection<HavuzSubeSecim> secilenler)
        {
            if (string.IsNullOrEmpty(dersAdi) || secilenler == null || secilenler.Count < 2)
                return "Havuz için en az 2 şube seçmelisiniz.";

            var havuz = new DersHavuzu { DersAdi = dersAdi };
            foreach (var sec in secilenler)
            {
                havuz.Uyeler.Add(new HavuzUyesi { Bolum = sec.Bolum, Sube = sec.Sube, SinifSeviyesi = sec.Sinif });
                if (!havuz.Bolumler.Contains(sec.Bolum)) havuz.Bolumler.Add(sec.Bolum);
            }
            _d.SablonHavuzlari.Add(havuz);
            OzetiYaz();
            _d.Bildir();
            return null;
        }

        public void HavuzSil(DersHavuzu havuz)
        {
            if (!_d.SablonHavuzlari.Remove(havuz)) return;
            OzetiYaz();
            _d.Bildir();
        }

        /// <summary>Tanımlı havuzun okunur hali: "FİZİK I → BİL(A) + İNŞ(B)".</summary>
        public static string HavuzMetni(DersHavuzu havuz)
            => $"{havuz.DersAdi} → {string.Join(" + ", havuz.Uyeler.Select(u => $"{u.Bolum}({u.Sube})"))}";

        private void OzetiYaz()
            => _d.DurumYaz(_d.OrtakSecimYapildi
                    ? $"⭐ {_d.ManuelOrtakDersAdlari.Count} ortak ders (manuel) • 🔗 {_d.SablonHavuzlari.Count} havuz tanımlı."
                    : $"⭐ Ortak dersler otomatik • 🔗 {_d.SablonHavuzlari.Count} havuz tanımlı.",
                DurumTuru.Basari);
    }
}
