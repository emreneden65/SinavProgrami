#nullable disable

namespace SinavProgrami.Models
{
    /// <summary>
    /// Bölüm adını şablonlardaki 3 harfli kısaltmaya (BİL/EEM/INS/MAK/MEK/MEM) indirger.
    /// Önceden CekilmisDerslerExportHelper ve GunSalonMatrisExportHelper'da kuralları
    /// birbirinden farklı iki kopya vardı; aynı ders iki çıktıda farklı bölüm satırına
    /// düşebiliyordu. Tüm export/okuma kodu bu tek sürümü kullanır.
    /// </summary>
    public static class BolumYardimci
    {
        public static string Kisalt(string bolumAdi)
        {
            if (string.IsNullOrWhiteSpace(bolumAdi)) return "?";
            string b = bolumAdi.Trim().ToUpperInvariant();
            if (b.Contains("BİLG") || b.Contains("BILG") || b == "BİL" || b == "BIL") return "BİL";
            if (b.Contains("ELEKTR") || b.Contains("EEM")) return "EEM";
            if (b.Contains("İNŞA") || b.Contains("INSA") || b.Contains("İNŞ") || b.Contains("INS")) return "INS";
            if (b.Contains("MEKATRON") || b.Contains("MEK")) return "MEK";
            if (b.Contains("METALURJ") || b.Contains("MALZEME") || b.Contains("MEM")) return "MEM";
            if (b.Contains("MAKİN") || b.Contains("MAKIN") || b.Contains("MAK")) return "MAK";
            return b.Length >= 3 ? b.Substring(0, 3) : b;
        }

        /// <summary>
        /// Bölümün resmi tam adı (büyük harf, "BÖLÜMÜ" eki olmadan) — Excel form başlıkları için.
        /// Kısaltma ("BİL", "ELM") ya da herhangi bir yazımdaki tam ad verilebilir;
        /// tanınmayan bölüm olduğu gibi döner.
        /// </summary>
        public static string TamAd(string bolumAdi)
        {
            string kisa = (bolumAdi ?? "").Trim().ToUpperInvariant() == "ELM" ? "EEM" : Kisalt(bolumAdi);
            switch (kisa)
            {
                case "BİL": return "BİLGİSAYAR MÜHENDİSLİĞİ";
                case "EEM": return "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ";
                case "INS": return "İNŞAAT MÜHENDİSLİĞİ";
                case "MAK": return "MAKİNE MÜHENDİSLİĞİ";
                case "MEK": return "MEKATRONİK MÜHENDİSLİĞİ";
                case "MEM": return "METALURJİ VE MALZEME MÜHENDİSLİĞİ";
                default: return (bolumAdi ?? "").Trim();
            }
        }
    }
}
