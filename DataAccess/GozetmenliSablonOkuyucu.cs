#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// ADIM 7 — Kullanıcının elle düzenlediği gözetmenli Excel'i (ExcelMatrisExportHelper
    /// çıktısı) "Slot Bazlı Sınav Planı" sayfasından geri okur.
    ///
    /// Sayfa düzeni (ExcelMatrisExportHelper.SlotBazliOzetOlustur):
    ///   başlık: TARİH | GÜN | SAAT | BÖLÜM | DERS (ŞUBE) | KONTENJAN | ATANAN SALONLAR | [salon1..N]
    ///   salon başlığı: "T4-207\n(40)"     salon hücresi: "25\nAhmet Yılmaz" veya 25
    ///   slot ayırıcı satırları (📅 ...) DERS kolonu boş olduğu için atlanır.
    ///
    /// Bu sayfada SINIF / ÖĞRETİM ELEMANI kolonu yoktur; bu yüzden okuyucu sıfırdan atama
    /// ÜRETMEZ — bellekteki atamalarla eşleştirip yalnızca salon/öğrenci/gözetmen bilgisini
    /// günceller (<see cref="Uygula"/>).
    /// </summary>
    public static class GozetmenliSablonOkuyucu
    {
        public const string SayfaAdi = "Slot Bazlı Sınav Planı";
        private const int SalonBaslangicKolon = 8;

        /// <summary>Excel'deki tek ders satırı.</summary>
        public class Satir
        {
            public DateTime Gun;
            public string Saat;
            public string Bolum;
            public string DersEtiketi;      // "FİZİK II (A)"
            public List<SalonAtama> Salonlar = new List<SalonAtama>();
            public int ExcelSatirNo;
        }

        public class Sonuc
        {
            public List<Satir> Satirlar { get; } = new List<Satir>();
            public List<string> Uyarilar { get; } = new List<string>();
        }

        public static Sonuc Oku(string dosyaYolu)
        {
            var sonuc = new Sonuc();
            using var paket = new ExcelPackage(new FileInfo(dosyaYolu));

            var ws = paket.Workbook.Worksheets.FirstOrDefault(w =>
                string.Equals(w.Name, SayfaAdi, StringComparison.OrdinalIgnoreCase));
            if (ws?.Dimension == null)
            {
                sonuc.Uyarilar.Add($"Dosyada '{SayfaAdi}' sayfası bulunamadı.");
                return sonuc;
            }

            // Başlık satırı sabit 3 kabul edilmez — kullanıcı üste satır eklemiş olabilir.
            int baslikRow = BaslikSatiriBul(ws);
            if (baslikRow < 0)
            {
                sonuc.Uyarilar.Add("'TARİH' başlık satırı bulunamadı.");
                return sonuc;
            }

            // Salon kolonları: "T4-207\n(40)" → kod + kapasite
            var salonlar = new Dictionary<int, (string Kod, int Kap)>();
            for (int c = SalonBaslangicKolon; c <= ws.Dimension.End.Column; c++)
            {
                string ham = ws.Cells[baslikRow, c].Value?.ToString();
                if (string.IsNullOrWhiteSpace(ham)) continue;
                var parca = ham.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                string kod = parca[0].Trim();
                int kap = parca.Length > 1 ? (SayiCoz(parca[1]) ?? 0) : 0;
                salonlar[c] = (kod, kap);
            }

            for (int r = baslikRow + 1; r <= ws.Dimension.End.Row; r++)
            {
                // Slot ayırıcı satırları (📅 ...) 1–7. kolonlarda birleşiktir; EPPlus birleşik
                // aralığın değerini her hücrede döndürdüğü için Merge ile ayrıca elenir.
                if (ws.Cells[r, 5].Merge) continue;
                string ders = ws.Cells[r, 5].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(ders)) continue;          // boş satır

                var gun = HucreTarih(ws.Cells[r, 1].Value);
                if (gun == null)
                {
                    sonuc.Uyarilar.Add($"Satır {r} — '{ders}': tarih okunamadı, atlandı.");
                    continue;
                }

                var satir = new Satir
                {
                    Gun = gun.Value,
                    Saat = SaatCoz(ws.Cells[r, 3].Value),
                    Bolum = ws.Cells[r, 4].Value?.ToString()?.Trim() ?? "",
                    DersEtiketi = ders,
                    ExcelSatirNo = r
                };

                foreach (var kv in salonlar)
                {
                    string ham = ws.Cells[r, kv.Key].Value?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(ham)) continue;

                    // 1. satır = öğrenci sayısı, kalan satırlar = gözetmen adı
                    var parca = ham.Split(new[] { '\n', '\r' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    int? sayi = SayiCoz(parca[0]);
                    string gozetmen = parca.Length > 1 ? parca[1].Trim()
                                    : (sayi == null ? parca[0].Trim() : "");   // yalnızca isim yazılmışsa

                    satir.Salonlar.Add(new SalonAtama
                    {
                        SalonKodu = kv.Value.Kod,
                        SalonKapasitesi = kv.Value.Kap,
                        AtananOgrenciSayisi = sayi ?? 0,
                        GozetmenAdSoyad = gozetmen
                    });

                    if (sayi == null)
                        sonuc.Uyarilar.Add($"Satır {r} — {kv.Value.Kod}: öğrenci sayısı yok (0 kabul edildi).");
                }

                sonuc.Satirlar.Add(satir);
            }
            return sonuc;
        }

        /// <summary>
        /// Okunan satırları bellekteki atamalara uygular. Eşleşme: gün + saat + bölüm + ders etiketi.
        /// Eşleşmeyen satırlar uyarı olarak <see cref="Sonuc.Uyarilar"/>'a eklenir.
        /// </summary>
        /// <returns>Güncellenen atama sayısı.</returns>
        public static int Uygula(List<SinavAtama> hedef, Sonuc okuma)
        {
            string Anahtar(DateTime g, string s, string b, string d) =>
                $"{g:yyyy-MM-dd}|{SaatYardimci.Normalize(s)}|{(b ?? "").Trim().ToUpperInvariant()}|{(d ?? "").Trim().ToUpperInvariant()}";

            var harita = new Dictionary<string, SinavAtama>();
            foreach (var a in hedef)
            {
                string k = Anahtar(a.Gun, a.Saat, a.BolumAdi, a.DersEtiketi);
                if (!harita.ContainsKey(k)) harita[k] = a;
            }

            int guncellenen = 0;
            foreach (var s in okuma.Satirlar)
            {
                if (!harita.TryGetValue(Anahtar(s.Gun, s.Saat, s.Bolum, s.DersEtiketi), out var atama))
                {
                    // Kullanıcı tarih/saat/ders adını değiştirmiş olabilir → sessizce yutma, uyar
                    okuma.Uyarilar.Add($"Satır {s.ExcelSatirNo} — '{s.DersEtiketi}' ({s.Gun:dd.MM.yyyy} {s.Saat}): " +
                                       "uygulamadaki programda eşleşen sınav yok, satır uygulanmadı.");
                    continue;
                }
                foreach (var sa in s.Salonlar) sa.Sube = atama.Sube;
                atama.AtananSalonlar = s.Salonlar;
                guncellenen++;
            }
            return guncellenen;
        }

        // ── Yardımcılar (GunSalonMatrisOkuyucu ile aynı mantık) ──
        private static int BaslikSatiriBul(ExcelWorksheet ws)
        {
            for (int r = 1; r <= Math.Min(ws.Dimension.End.Row, 10); r++)
            {
                string v = (ws.Cells[r, 1].Value?.ToString() ?? "").Trim().ToUpperInvariant();
                if (v == "TARİH" || v == "TARIH") return r;
            }
            return -1;
        }

        private static int? SayiCoz(object v)
        {
            if (v == null) return null;
            if (v is double d) return (int)Math.Round(d);
            if (v is int i) return i;
            var m = Regex.Match(v.ToString(), @"\d+");
            return m.Success ? int.Parse(m.Value) : (int?)null;
        }

        private static DateTime? HucreTarih(object v)
        {
            if (v is DateTime dt) return dt.Date;
            if (v is double d && d > 40000 && d < 80000) { try { return DateTime.FromOADate(d).Date; } catch { } }
            if (v is string s && DateTime.TryParse(s, new CultureInfo("tr-TR"), DateTimeStyles.None, out var p)) return p.Date;
            return null;
        }

        private static string SaatCoz(object v)
        {
            if (v is DateTime dt) return SaatYardimci.Saat(dt);
            if (v is TimeSpan ts) return SaatYardimci.GunKesrindenSaat(ts.TotalDays);
            if (v is double d && d >= 0 && d < 1) return SaatYardimci.GunKesrindenSaat(d);
            return SaatYardimci.Normalize(v?.ToString());
        }
    }
}
