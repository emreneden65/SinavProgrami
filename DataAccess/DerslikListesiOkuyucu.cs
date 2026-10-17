#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// "TEKNOLOJİ FAKÜLTESİ DERSLİK VERİLERİ" Excel'ini okur ve <see cref="Salon"/> listesi üretir.
    ///
    /// Beklenen düzen (yan yana birden çok blok olabilir — örn. T1 solda, T4 sağda):
    ///   DERSLİK | SINAV DÜZEYİNDEKİ ÖĞR. SAYISI | DERS DÜZEYİNDEKİ ÖĞR. SAYISI | DONANIM
    ///   T1-107  | 62(31)                        | 155                          |
    ///   ...
    ///   TOPLAM  | ...                            (atlanır)
    ///   [birleşik bölüm başlığı: "TEKNİK RESİM SINIFLARI" / "BİLGİSAYAR LABARATUVARLARI"]
    ///   T1-105  | 52 | 52 |                     (bu bölümdekiler UYGULAMALI sayılır)
    ///
    /// Kapasite = SINAV DÜZEYİNDEKİ sütunun İLK sayısı ("62(31)" → 62, "60(+20)" → 60).
    /// Bloklar konumdan değil "DERSLİK" başlık hücresinden bulunur; kullanıcı satır/sütun
    /// eklese de okuma kaymaz.
    /// </summary>
    public static class DerslikListesiOkuyucu
    {
        public class Sonuc
        {
            public List<Salon> Salonlar { get; } = new List<Salon>();
            public List<string> Uyarilar { get; } = new List<string>();
        }

        private static readonly Regex SalonKoduDeseni = new Regex(@"^[A-Za-zÇĞİÖŞÜçğıöşü]+\d*\s*-\s*\d+[A-Za-z]?$");

        public static Sonuc Oku(string dosyaYolu)
        {
            var sonuc = new Sonuc();
            using var paket = new ExcelPackage(new FileInfo(dosyaYolu));

            var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ws in paket.Workbook.Worksheets)
            {
                if (ws.Dimension == null) continue;
                int sonSatir = ws.Dimension.End.Row, sonKolon = ws.Dimension.End.Column;

                // "DERSLİK" başlık hücreleri: (satır, derslik kolonu)
                var bloklar = new List<(int Row, int Col)>();
                for (int r = 1; r <= sonSatir; r++)
                    for (int c = 1; c <= sonKolon; c++)
                        if (Normalize(ws.Cells[r, c].Text) == "DERSLIK") bloklar.Add((r, c));

                foreach (var (baslikRow, kodKolon) in bloklar)
                {
                    // Kapasite kolonu: aynı başlık satırında "SINAV" geçen ilk kolon; yoksa hemen sağ
                    int kapKolon = kodKolon + 1;
                    for (int c = kodKolon + 1; c <= Math.Min(sonKolon, kodKolon + 4); c++)
                        if (Normalize(ws.Cells[baslikRow, c].Text).Contains("SINAV")) { kapKolon = c; break; }

                    bool uygulamali = false;
                    for (int r = baslikRow + 1; r <= sonSatir; r++)
                    {
                        // Aynı kolonda yeni bir "DERSLİK" başlığı → bu blok bitti
                        if (Normalize(ws.Cells[r, kodKolon].Text) == "DERSLIK") break;

                        string bolum = BolumBasligi(ws, r, kodKolon, kapKolon);
                        if (bolum != null)
                        {
                            uygulamali = bolum.Contains("LAB") || bolum.Contains("TEKNIK") ||
                                         bolum.Contains("RESIM") || bolum.Contains("UYGULAMA") || bolum.Contains("CIZIM");
                            continue;
                        }

                        string kod = (ws.Cells[r, kodKolon].Text ?? "").Trim();
                        if (kod.Length == 0) continue;
                        if (Normalize(kod).StartsWith("TOPLAM")) continue;
                        if (!SalonKoduDeseni.IsMatch(kod)) continue;   // salon olmayan metinler

                        kod = Regex.Replace(kod, @"\s+", "").ToUpperInvariant();
                        int? kapasite = IlkSayi(ws.Cells[r, kapKolon].Value);
                        if (kapasite == null || kapasite <= 0)
                        {
                            sonuc.Uyarilar.Add($"{ws.Name} satır {r} — {kod}: sınav kapasitesi okunamadı, atlandı.");
                            continue;
                        }
                        if (!gorulen.Add(kod))
                        {
                            sonuc.Uyarilar.Add($"{kod} dosyada birden fazla kez geçiyor — ilk kayıt kullanıldı.");
                            continue;
                        }

                        sonuc.Salonlar.Add(new Salon
                        {
                            SalonKodu = kod,
                            Kapasite = kapasite.Value,
                            SalonTipi = uygulamali ? "UYGULAMALI" : "NORMAL"
                        });
                    }
                }
            }

            if (sonuc.Salonlar.Count == 0)
                sonuc.Uyarilar.Add("Dosyada 'DERSLİK' başlıklı bir tablo ya da derslik satırı bulunamadı.");
            return sonuc;
        }

        /// <summary>
        /// Satır, blok genişliği boyunca birleşik bir bölüm başlığıysa (örn. "BİLGİSAYAR
        /// LABARATUVARLARI") normalize metnini döndürür; değilse null.
        /// </summary>
        private static string BolumBasligi(ExcelWorksheet ws, int r, int kodKolon, int kapKolon)
        {
            for (int c = kodKolon; c <= kapKolon + 2; c++)
            {
                var cell = ws.Cells[r, c];
                if (!cell.Merge) continue;
                string adres = ws.MergedCells[r, c];
                if (adres == null) continue;
                var mr = new ExcelAddress(adres);
                if (mr.Start.Row != mr.End.Row || mr.End.Column - mr.Start.Column < 1) continue;   // yatay bant değil
                string metin = Normalize(ws.Cells[mr.Start.Row, mr.Start.Column].Text);
                if (metin.Length > 0) return metin;
            }
            return null;
        }

        private static int? IlkSayi(object v)
        {
            if (v == null) return null;
            if (v is double d) return (int)Math.Round(d);
            if (v is int i) return i;
            var m = Regex.Match(v.ToString(), @"\d+");
            return m.Success ? int.Parse(m.Value) : (int?)null;
        }

        /// <summary>Türkçe harfleri ASCII'ye indirip büyük harfe çevirir ("Derslik" / "DERSLİK" → "DERSLIK").</summary>
        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.Trim())
            {
                switch (ch)
                {
                    case 'İ': case 'ı': case 'i': sb.Append('I'); break;
                    case 'Ş': case 'ş': sb.Append('S'); break;
                    case 'Ğ': case 'ğ': sb.Append('G'); break;
                    case 'Ç': case 'ç': sb.Append('C'); break;
                    case 'Ö': case 'ö': sb.Append('O'); break;
                    case 'Ü': case 'ü': sb.Append('U'); break;
                    default: sb.Append(char.ToUpperInvariant(ch)); break;
                }
            }
            return sb.ToString();
        }
    }
}
