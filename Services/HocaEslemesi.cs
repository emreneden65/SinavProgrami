#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>
    /// HOCA EŞLEMESİ — kalıcı kayıt (eski MainForm'daki HocaEslemesi* metotlarının taşınmış hali).
    /// İlk yüklenen sınav listesi Excel'indeki Öğretim Elemanı bilgisi bu sözlüğe alınır ve
    /// dosyaya kaydedilir; Sınav Programı Formu'nun hoca sütunu (ders listesi bellekte olmasa da)
    /// bu kayıttan doldurulur. Dosya oturum kaydıyla aynı klasördedir.
    /// </summary>
    public class HocaEslemesi
    {
        /// <summary>"dersNorm|BÖLÜM|ŞUBE", "dersNorm|BÖLÜM" ve "dersNorm" → hoca adı.</summary>
        private Dictionary<string, string> _eslesme = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string DosyaYolu => Path.Combine(OturumKaydi.Klasor, "hoca_eslemesi.json");

        public int KayitSayisi => _eslesme.Count;

        private static string NAd(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s.ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        private static string BolumKisalt(string b)
        {
            if (string.IsNullOrWhiteSpace(b)) return "?";
            string u = b.ToUpperInvariant();
            if (u.Contains("BİLG") || u.Contains("BILG")) return "BİL";
            if (u.Contains("ELEKTR") || u.Contains("EEM")) return "EEM";
            if (u.Contains("İNŞA") || u.Contains("INSA") || u.Contains("INS")) return "INS";
            if (u.Contains("MEKATRON") || u.Contains("MEK")) return "MEK";
            if (u.Contains("METALURJ") || u.Contains("MALZEME") || u.Contains("MEM")) return "MEM";
            if (u.Contains("MAKİN") || u.Contains("MAKIN") || u.Contains("MAK")) return "MAK";
            return u.Trim().Length >= 3 ? u.Trim().Substring(0, 3) : u.Trim();
        }

        /// <summary>Şube etiketini A/B/C/D harf kolonuna indirger (eski "INT" → "D").</summary>
        private static string SubeHarf(string sube) => SubeEtiketi.Normalize(sube, SubeEtiketi.DordunculSube);

        /// <summary>Kaydedilmiş eşlemeyi dosyadan yükler (açılışta).</summary>
        public void Yukle()
        {
            try
            {
                if (!File.Exists(DosyaYolu)) return;
                var okunan = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(DosyaYolu));
                if (okunan != null)
                    _eslesme = new Dictionary<string, string>(okunan, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz("Hoca eşlemesi okunamadı", ex);
            }
        }

        /// <summary>Ders listesindeki hoca bilgilerini eşlemeye ekler ve dosyaya kaydeder.</summary>
        public void GuncelleVeKaydet(IEnumerable<Ders> dersler)
        {
            try
            {
                foreach (var d in dersler)
                {
                    if (string.IsNullOrWhiteSpace(d.OgretimElemani)) continue;
                    string hoca = d.OgretimElemani.Trim();
                    string dersNorm = NAd(d.DersAdi);
                    string kok = $"{dersNorm}|{BolumKisalt(d.BolumAdi)}";
                    _eslesme[$"{kok}|{SubeHarf(d.Sube)}"] = hoca;                // şube bazlı (üzerine yazar)
                    if (!_eslesme.ContainsKey(kok)) _eslesme[kok] = hoca;            // ders + bölüm
                    if (!_eslesme.ContainsKey(dersNorm)) _eslesme[dersNorm] = hoca;  // yalnız ders (son çare)
                }

                Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu));
                File.WriteAllText(DosyaYolu, JsonSerializer.Serialize(_eslesme, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz("Hoca eşlemesi kaydedilemedi", ex);
            }
        }

        /// <summary>
        /// Atamalardaki BOŞ Öğretim Elemanı alanlarını eşlemeden doldurur. Bellekteki ders listesi
        /// doluysa eşleme önce tazelenir. Sıra: ders+bölüm+şube → ders+bölüm → yalnız ders.
        /// </summary>
        public void Doldur(List<SinavAtama> atamalar, IReadOnlyCollection<Ders> guncelDersler)
        {
            if (atamalar == null || atamalar.Count == 0) return;
            if (guncelDersler != null && guncelDersler.Count > 0) GuncelleVeKaydet(guncelDersler);
            if (_eslesme.Count == 0) return;

            foreach (var a in atamalar)
            {
                if (!string.IsNullOrWhiteSpace(a.OgretimElemani)) continue;
                string dersNorm = NAd(a.DersAdi);
                string kok = $"{dersNorm}|{BolumKisalt(a.BolumAdi)}";
                if (_eslesme.TryGetValue($"{kok}|{SubeHarf(a.Sube)}", out string hoca) ||
                    _eslesme.TryGetValue(kok, out hoca) ||
                    _eslesme.TryGetValue(dersNorm, out hoca))
                    a.OgretimElemani = hoca;
            }
        }
    }
}
