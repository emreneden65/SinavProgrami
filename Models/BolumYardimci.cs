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
    }
}
