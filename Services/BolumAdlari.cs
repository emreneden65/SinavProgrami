#nullable disable
using System.Text;

namespace SinavProgrami.Services
{
    /// <summary>
    /// BÖLÜM ADI YARDIMCILARI — eski arayüzde MainForm içinde duran bölüm adı kuralları
    /// (BolumNormAnahtar, BolumKisalt, KodBolumDisiMi) aynen taşınmıştır.
    /// </summary>
    public static class BolumAdlari
    {
        /// <summary>Fakültenin sabit bölüm listesi (eski arayüzdeki bölüm kutusunun öğeleri).</summary>
        public static readonly string[] StandartBolumler =
        {
            "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ", "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ", "İNŞAAT MÜHENDİSLİĞİ BÖLÜMÜ",
            "MAKİNE MÜHENDİSLİĞİ BÖLÜMÜ", "MEKATRONİK MÜHENDİSLİĞİ BÖLÜMÜ", "METALURJİ VE MALZEME MÜHENDİSLİĞİ BÖLÜMÜ"
        };

        /// <summary>
        /// Bölüm adını karşılaştırma anahtarına indirger: Türkçe karakter sadeleştirilir,
        /// yalnızca harfler tutulur (küçük), sondaki "bolumu"/"bolum" eki atılır.
        /// "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ" ≡ "Bilgisayar Mühendisliği".
        /// </summary>
        public static string BolumNormAnahtar(string bolum)
        {
            if (string.IsNullOrWhiteSpace(bolum)) return "";
            var sb = new StringBuilder();
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
            // "ve" bağlacı farkı: "elektrikveelektronik" ≡ "elektrikelektronik"
            s = s.Replace("veelektronik", "elektronik").Replace("vemalzeme", "malzeme");
            // yazım farkı: makina ≡ makine
            s = s.Replace("makina", "makine");
            return s;
        }

        /// <summary>
        /// Bölüm adını fakülte formatındaki kısaltmaya çevirir (BİL, EEM, İNŞ, MAK, MEK, MEM) —
        /// şablon tablosunda kullanılan yazım. Tanımsız bölümler için adın ilk 3 harfi kullanılır.
        /// </summary>
        public static string BolumKisalt(string bolumAdi)
        {
            if (string.IsNullOrWhiteSpace(bolumAdi)) return "?";
            string b = bolumAdi.ToUpperInvariant();
            if (b.Contains("BİLGİSAYAR") || b.Contains("BILGISAYAR")) return "BİL";
            if (b.Contains("ELEKTR")) return "EEM";
            if (b.Contains("İNŞAAT") || b.Contains("INSAAT")) return "İNŞ";
            if (b.Contains("MAKİNE") || b.Contains("MAKINE") || b.Contains("MAKİNA") || b.Contains("MAKINA")) return "MAK";
            if (b.Contains("MEKATRON")) return "MEK";
            if (b.Contains("METALURJ")) return "MEM";
            return b.Trim().Length >= 3 ? b.Trim().Substring(0, 3) : b.Trim();
        }

        /// <summary>
        /// Ders kodu bölüm öneklerinden (BIL, ELM, INS, MAK, MEK, MEM) biriyle
        /// BAŞLAMIYORSA ders "ortak/esnetilebilir" kabul edilir (örn. ATA101, TUR102, FIZ...).
        /// </summary>
        public static bool KodBolumDisiMi(string dersKodu)
        {
            if (string.IsNullOrWhiteSpace(dersKodu)) return false;
            var sb = new StringBuilder();
            foreach (char c in dersKodu.Trim())
            {
                if (char.IsDigit(c)) break;
                if (char.IsLetter(c)) sb.Append(char.ToUpperInvariant(c == 'i' ? 'I' : c));
            }
            string onek = sb.ToString().Replace("İ", "I");
            return onek.Length > 0 &&
                   onek != "BIL" && onek != "ELM" && onek != "INS" &&
                   onek != "MAK" && onek != "MEK" && onek != "MEM" && onek != "EEM";
        }
    }
}
