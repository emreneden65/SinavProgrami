#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SinavProgrami.Models;
using SinavProgrami.Algorithm;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// AŞAMA 2 çıktısı — Günlük Salon Matrisi Excel'i.
    /// ──────────────────────────────────────────────────────────
    /// <see cref="GunlukSalonDagitici"/> sonucunu, seçilen her sınav günü için ayrı bir
    /// "Sınav Günü DD.MM.YYYY" sayfasına matris olarak yazar:
    ///   Satırlar  = o günün dersleri (saat → bölüm sırasıyla)
    ///   Kolonlar  = DB'deki salonlar (T4 binası önce), başlıkta kapasite
    ///   Hücre     = o dersin o salona atanan öğrenci sayısı (matris karesi işaretli)
    /// </summary>
    public static class GunSalonMatrisExportHelper
    {
        private static readonly Color BaslikArkaplan = Color.FromArgb(55, 65, 81);      // koyu gri — şablon referans sayfası
        private static readonly Color BaslikYazi = Color.FromArgb(245, 245, 250);
        // ÖRNEK ŞABLON PALETİ (kullanıcının "örnek dağıtım.xlsx" dosyasından birebir):
        //   Uygulamalı salon kolonları  → açık altın (FFF2CC)
        //   D şubesi başlık hücresi (I2) → DOLGUSUZ (varsayılan/şeffaf arka plan)
        internal static readonly Color UygulamaliKolonDolgu = Color.FromArgb(255, 242, 204);

        private static readonly string[] SabitBasliklar = { "Bölüm", "SINIF", "Tarih", "Saat", "Dersler", "A", "B", "C", "D" };
        private static readonly string[] Bolumler = { "BİL", "EEM", "INS", "MAK", "MEK", "MEM" };
        private const int SalonBaslangicKolon = 10; // A..I = 9 sabit kolon, salonlar 10'dan başlar

        /// <summary>
        /// Gün sayfalarındaki salon sütunlarının sırası: kapasite > 0, T4 binası önce, sonra kod sırası.
        /// Şablon Excel'inin gün sayfaları da aynı sırayı kullanır.
        /// </summary>
        public static List<Salon> SalonSutunSirasi(List<Salon> salonlar)
            => T4OnceSirala((salonlar ?? new List<Salon>()).Where(s => s.Kapasite > 0), s => s.SalonKodu)
                .ToList();

        /// <summary>
        /// Tüm çıktılarda ortak salon sırası: T4 binası önce, sonra salon kodu.
        /// (Salon atanmış dosya, gözetmenli taslak / final ve uygulamadaki matris aynı sırayı kullanır.)
        /// </summary>
        public static IOrderedEnumerable<T> T4OnceSirala<T>(IEnumerable<T> kaynak, Func<T, string> salonKodu)
            => kaynak.OrderBy(x => T4mu(salonKodu(x)) ? 0 : 1).ThenBy(salonKodu);

        public static void YazVeKaydet(List<GunlukSalonDagitici.GunSatir> satirlar, List<Salon> salonlar,
                                       List<SinavAtama> sablonSonuclari, Dictionary<DateTime, List<string>> takvim,
                                       string cikisYolu)
        {
            satirlar = satirlar ?? new List<GunlukSalonDagitici.GunSatir>();

            var salonSirali = SalonSutunSirasi(salonlar);
            var salonKolon = new Dictionary<string, int>();
            for (int i = 0; i < salonSirali.Count; i++)
                salonKolon[salonSirali[i].SalonKodu] = SalonBaslangicKolon + i;
            int sonKolon = SalonBaslangicKolon + Math.Max(salonSirali.Count, 1) - 1;

            using (var paket = new ExcelPackage())
            {
                // Referans için sınav dağıtım haftası sayfası (salonsuz yerleşim)
                SablonSayfasiEkle(paket, sablonSonuclari, takvim);

                foreach (var gunGrup in satirlar.GroupBy(s => s.Gun).OrderBy(g => g.Key))
                {
                    // KURAL: Sekme adında "Sınav Günü" gibi ibare YASAK — yalnızca tarih (GG.AA.YYYY)
                    var ws = paket.Workbook.Worksheets.Add(SayfaAdi($"{gunGrup.Key:dd.MM.yyyy}", paket));

                    // ══ ÖRNEK ŞABLON FORMATI (birebir) ══
                    // row1: A1:I1 birleşik boş + J1'den itibaren salon kodları (Calibri 12,
                    //       wrap, döndürme YOK); row2: sabit başlıklar (Calibri 10) + kapasiteler.
                    ws.Cells[1, 1, 1, 9].Merge = true;
                    ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                    for (int i = 0; i < salonSirali.Count; i++)
                    {
                        var h = ws.Cells[1, SalonBaslangicKolon + i];
                        h.Value = salonSirali[i].SalonKodu;
                        h.Style.Font.Size = 12;
                        h.Style.WrapText = true;
                        h.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        h.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }

                    for (int c = 0; c < SabitBasliklar.Length; c++)
                    {
                        var h = ws.Cells[2, 1 + c];
                        h.Value = SabitBasliklar[c];
                        h.Style.Font.Size = 10;
                        h.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        h.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                    // D şubesi başlığı — dolgu YOK (varsayılan/şeffaf arka plan)
                    ws.Cells[2, 9].Style.Fill.PatternType = ExcelFillStyle.None;

                    for (int i = 0; i < salonSirali.Count; i++)
                    {
                        var h = ws.Cells[2, SalonBaslangicKolon + i];
                        h.Value = salonSirali[i].Kapasite;
                        h.Style.Font.Size = 10;
                        h.Style.WrapText = true;
                        h.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        h.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }

                    // ── Veri satırları ──
                    // KURAL (TAM LİSTE): Sadece dolu satırlar filtrelenip yazılmaz —
                    // takvimdeki her saat × her bölüm için satır açılır; o hücreye sınav
                    // atanmamışsa satır BOŞ bırakılır ama LİSTEDE GÖRÜNÜR.
                    int r = 3;
                    var gunDersleri = gunGrup.ToList();
                    var yazilan = new HashSet<GunlukSalonDagitici.GunSatir>();
                    var saatBloklari = new List<(int Bas, int Son)>(); // örnek şablondaki medium çerçeveli saat blokları

                    var gunSaatleri = (takvim != null && takvim.TryGetValue(gunGrup.Key, out var ts) && ts.Count > 0
                            ? ts : gunDersleri.Select(d => d.Saat).Distinct().ToList())
                        .OrderBy(SaatYardimci.Anahtar).ToList();

                    void SaatYaz(int satirNo, string saat)
                    {
                        // Örnek şablondaki gibi gerçek saat değeri — 13.00 biçiminde görünür
                        if (TimeSpan.TryParse(saat, out var t))
                        {
                            ws.Cells[satirNo, 4].Value = t.TotalDays;
                            ws.Cells[satirNo, 4].Style.Numberformat.Format = SaatYardimci.ExcelSaatBicimi;
                        }
                        else ws.Cells[satirNo, 4].Value = SaatYardimci.ExcelMetni(saat);
                        ws.Cells[satirNo, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    void DersSatiriYaz(GunlukSalonDagitici.GunSatir ders)
                    {
                        ws.Cells[r, 1].Value = ders.Bolum;
                        ws.Cells[r, 2].Value = ders.Sinif;
                        ws.Cells[r, 3].Value = ders.Gun;
                        ws.Cells[r, 3].Style.Numberformat.Format = "dd.mm.yyyy";
                        SaatYaz(r, ders.Saat);
                        string etiket = ders.DersAdi + (ders.Ortak ? "  (ortak)" : "");
                        if (ders.Havuz && !string.IsNullOrEmpty(ders.HavuzEtiketi))
                            etiket += $"  [{ders.HavuzEtiketi}]";
                        ws.Cells[r, 5].Value = etiket;
                        HarfYaz(ws, r, 6, ders.Subeler, "A");
                        HarfYaz(ws, r, 7, ders.Subeler, "B");
                        HarfYaz(ws, r, 8, ders.Subeler, "C");
                        HarfYaz(ws, r, 9, ders.Subeler, SubeEtiketi.DordunculSube);

                        // SADE GÖRÜNÜM: kutucukta YALNIZCA atanan ŞUBE adı yazılır ("A", "A+B").
                        // Öğrenci sayısı yazılmaz — geri okuma sayıya ihtiyaç duymaz;
                        // sayılar A/B/C/D kolonlarından türetilir.
                        foreach (var kv in ders.SalonDagilimi)
                            if (salonKolon.TryGetValue(kv.Key, out int col) && kv.Value > 0)
                            {
                                var cell = ws.Cells[r, col];
                                string sube = ders.SalonSubeEtiketi(kv.Key);
                                cell.Value = string.IsNullOrEmpty(sube) ? (object)kv.Value : sube;
                                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            }
                        r++;
                    }

                    foreach (var saat in gunSaatleri)
                    {
                        int blokBas = r;
                        foreach (var bolum in Bolumler)
                        {
                            var eslesenler = gunDersleri
                                // Bölüm normalize edilerek karşılaştırılır: "BIL" ile "BİL" eşleşmezse
                                // ders aşağıdaki "bilinmeyenler" döngüsüyle sayfa sonuna kayıyordu.
                                .Where(d => SaatYardimci.Normalize(d.Saat) == SaatYardimci.Normalize(saat)
                                            && BolumYardimci.Kisalt(d.Bolum) == bolum)
                                .OrderBy(d => d.DersAdi).ToList();

                            if (eslesenler.Count == 0)
                            {
                                // BOŞ SATIR — sınav atanmamış olsa da tam liste için satır açılır
                                ws.Cells[r, 1].Value = bolum;
                                ws.Cells[r, 3].Value = gunGrup.Key;
                                ws.Cells[r, 3].Style.Numberformat.Format = "dd.mm.yyyy";
                                SaatYaz(r, saat);
                                r++;
                            }
                            else
                            {
                                foreach (var ders in eslesenler) { DersSatiriYaz(ders); yazilan.Add(ders); }
                            }
                        }
                        saatBloklari.Add((blokBas, r - 1));
                    }

                    // Standart bölüm listesi dışında kalan (bilinmeyen kısaltmalı) dersler kaybolmasın
                    foreach (var ders in gunDersleri
                        .Where(d => !yazilan.Contains(d))
                        .OrderBy(d => SaatYardimci.Anahtar(d.Saat)).ThenBy(d => BolumSira(d.Bolum)).ThenBy(d => d.DersAdi))
                        DersSatiriYaz(ders);

                    // ── Kenarlık + boyut (ÖRNEK ŞABLONA BİREBİR) ──
                    int sonSatir = Math.Max(r - 1, 2);

                    // 1) Tüm alan ince (thin) çerçeve
                    var tumAlan = ws.Cells[1, 1, sonSatir, sonKolon];
                    tumAlan.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    tumAlan.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    tumAlan.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    tumAlan.Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    // 2) Uygulamalı salon kolonları — örnekteki açık altın dolgu (tüm kolon boyunca)
                    for (int i = 0; i < salonSirali.Count; i++)
                        if (salonSirali[i].UygulamaliMi)
                        {
                            var kolonAlani = ws.Cells[1, SalonBaslangicKolon + i, sonSatir, SalonBaslangicKolon + i];
                            kolonAlani.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            kolonAlani.Style.Fill.BackgroundColor.SetColor(UygulamaliKolonDolgu);
                        }

                    // 3) Kalın (medium) çerçeveler:
                    //    • her saat bloğunun üstü/altı  • sol dış kenar (veri satırları)
                    //    • salon alanı ayracı (J kolonu solu)  • sağ dış kenar  • salon başlık üstü
                    foreach (var (bas, son) in saatBloklari)
                    {
                        ws.Cells[bas, 1, bas, sonKolon].Style.Border.Top.Style = ExcelBorderStyle.Medium;
                        ws.Cells[son, 1, son, sonKolon].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                    }
                    ws.Cells[3, 1, sonSatir, 1].Style.Border.Left.Style = ExcelBorderStyle.Medium;
                    ws.Cells[1, SalonBaslangicKolon, sonSatir, SalonBaslangicKolon].Style.Border.Left.Style = ExcelBorderStyle.Medium;
                    ws.Cells[1, sonKolon, sonSatir, sonKolon].Style.Border.Right.Style = ExcelBorderStyle.Medium;
                    ws.Cells[1, SalonBaslangicKolon, 1, sonKolon].Style.Border.Top.Style = ExcelBorderStyle.Medium;

                    // 4) Hizalama/yazı tipi: Bölüm sola, sayısal kolonlar ortaya (Calibri 11 varsayılan)
                    ws.Cells[3, 1, sonSatir, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    ws.Cells[3, 1, sonSatir, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    ws.Cells[3, 2, sonSatir, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[3, 3, sonSatir, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[3, 6, sonSatir, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // 5) Satır yükseklikleri: 15.75; her saat bloğunun son satırı 16.5
                    for (int rr = 1; rr <= sonSatir; rr++) ws.Row(rr).Height = 15.75;
                    foreach (var (_, son) in saatBloklari) ws.Row(son).Height = 16.5;

                    // 6) Tarih kolonu: varsayılan genişlikte "########" göründüğünden genişletilir
                    ws.Column(3).Width = 11;
                }

                if (paket.Workbook.Worksheets.Count == 0)
                    paket.Workbook.Worksheets.Add("Bos");

                var cikis = new FileInfo(cikisYolu);
                if (cikis.Exists) cikis.Delete();
                paket.SaveAs(cikis);
            }
        }

        // ── Yardımcılar ──
        private static void HarfYaz(ExcelWorksheet ws, int r, int c, Dictionary<string, int> subeler, string harf)
        {
            if (subeler != null && subeler.TryGetValue(harf, out int v) && v > 0)
            {
                ws.Cells[r, c].Value = v;
                ws.Cells[r, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
        }

        /// <summary>Salonsuz yerleşimi (şablon) referans amaçlı gün-blokları halinde ekler.</summary>
        private static void SablonSayfasiEkle(ExcelPackage paket, List<SinavAtama> sablon, Dictionary<DateTime, List<string>> takvim)
        {
            var ws = paket.Workbook.Worksheets.Add("Sınav Haftası Dağıtım Şablonu");
            sablon = sablon ?? new List<SinavAtama>();
            takvim = takvim ?? new Dictionary<DateTime, List<string>>();

            var gunler = (takvim.Count > 0 ? takvim.Keys.AsEnumerable() : sablon.Select(a => a.Gun.Date))
                .Distinct().OrderBy(d => d).ToList();

            // (bölüm|gün|saat) → içerik
            var luk = new Dictionary<string, (int Sinif, string Ad, Dictionary<string, int> H)>();
            foreach (var a in sablon)
            {
                string key = BolumKisalt(a.BolumAdi) + "|" + a.Gun.ToString("yyyy-MM-dd") + "|" + a.Saat;
                if (!luk.TryGetValue(key, out var ic))
                { ic = (a.SinifSeviyesi, a.DersAdi, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)); luk[key] = ic; }
                else if (!string.Equals(ic.Ad, a.DersAdi, StringComparison.OrdinalIgnoreCase)
                         && !ic.Ad.Split(" / ").Contains(a.DersAdi, StringComparer.OrdinalIgnoreCase))
                {
                    // Aynı bölüm+gün+saatte ikinci FARKLI ders: eskiden adı kayboluyor, kontenjanı
                    // ilk dersin harf kolonlarına ekleniyordu. Artık iki ad da görünür.
                    ic = (ic.Sinif, ic.Ad + " / " + a.DersAdi, ic.H);
                    luk[key] = ic;
                }
                string harf = HarfKolonu(a.Sube);
                ic.H[harf] = (ic.H.TryGetValue(harf, out int v) ? v : 0) + Math.Max(0, a.Kontenjan);
            }

            const int blokGen = 8;
            string[] basliklar = { "Bölüm", "Sınıf", "Saat", "Ders Adı", "A", "B", "C", "D" };
            for (int i = 0; i < gunler.Count; i++)
            {
                int col = 1 + i * blokGen;
                var day = gunler[i];
                var th = ws.Cells[2, col];
                th.Value = day; th.Style.Numberformat.Format = "dd.mm.yyyy"; th.Style.Font.Bold = true;

                for (int c = 0; c < basliklar.Length; c++)
                {
                    var hh = ws.Cells[3, col + c];
                    hh.Value = basliklar[c]; hh.Style.Font.Bold = true;
                    hh.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    hh.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
                    hh.Style.Font.Color.SetColor(BaslikYazi);
                    hh.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                var saatler = (takvim.TryGetValue(day, out var ss) ? ss
                        : sablon.Where(a => a.Gun.Date == day).Select(a => a.Saat).Distinct().ToList())
                    .OrderBy(SaatYardimci.Anahtar).ToList();

                int r = 4;
                int saatIndex = 0;
                foreach (var saat in saatler)
                {
                    int blokBas = r;
                    foreach (var bol in Bolumler)
                    {
                        ws.Cells[r, col + 0].Value = bol;
                        ws.Cells[r, col + 2].Value = SaatYardimci.ExcelMetni(saat);
                        string key = bol + "|" + day.ToString("yyyy-MM-dd") + "|" + saat;
                        if (luk.TryGetValue(key, out var ic))
                        {
                            ws.Cells[r, col + 1].Value = ic.Sinif;
                            ws.Cells[r, col + 3].Value = ic.Ad;
                            HarfYaz(ws, r, col + 4, ic.H, "A");
                            HarfYaz(ws, r, col + 5, ic.H, "B");
                            HarfYaz(ws, r, col + 6, ic.H, "C");
                            HarfYaz(ws, r, col + 7, ic.H, SubeEtiketi.DordunculSube);
                        }
                        var rng = ws.Cells[r, col, r, col + blokGen - 1];
                        rng.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        rng.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        rng.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        rng.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        r++;
                    }

                    // GÖRSEL SAAT AYRIMI: kalın blok çerçevesi + dönüşümlü zemin + bold saat
                    var saatBlogu = ws.Cells[blokBas, col, r - 1, col + blokGen - 1];
                    ws.Cells[blokBas, col, blokBas, col + blokGen - 1].Style.Border.Top.Style = ExcelBorderStyle.Medium;
                    ws.Cells[r - 1, col, r - 1, col + blokGen - 1].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                    if (saatIndex % 2 == 1)
                    {
                        saatBlogu.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        saatBlogu.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(235, 238, 245));
                    }
                    var saatKolonu = ws.Cells[blokBas, col + 2, r - 1, col + 2];
                    saatKolonu.Style.Font.Bold = true;
                    saatKolonu.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    saatIndex++;
                }
                ws.Column(col).Width = 11;      // gün başlığı tarihi "########" görünmesin
                ws.Column(col + 3).Width = 26;
            }
        }

        private static string BolumKisalt(string bolumAdi) => BolumYardimci.Kisalt(bolumAdi);

        /// <summary>Şube etiketini A/B/C/D harf kolonuna indirger (eski "INT" → "D").</summary>
        private static string HarfKolonu(string sube)
            => SubeEtiketi.Normalize(sube, SubeEtiketi.DordunculSube);

        private static bool T4mu(string salonKodu)
            => (salonKodu ?? "").Trim().ToUpperInvariant().StartsWith("T4");

        private static int BolumSira(string b)
        {
            switch (b) { case "BİL": return 0; case "EEM": return 1; case "INS": return 2;
                         case "MAK": return 3; case "MEK": return 4; case "MEM": return 5; default: return 9; }
        }

        private static string SayfaAdi(string istenen, ExcelPackage paket)
        {
            string temiz = istenen;
            foreach (char c in new[] { ':', '\\', '/', '?', '*', '[', ']' }) temiz = temiz.Replace(c, '-');
            if (temiz.Length > 31) temiz = temiz.Substring(0, 31);
            string sonuc = temiz; int i = 2;
            while (paket.Workbook.Worksheets.Any(w => string.Equals(w.Name, sonuc, StringComparison.OrdinalIgnoreCase)))
            {
                string ek = $" ({i++})";
                sonuc = (temiz.Length + ek.Length > 31 ? temiz.Substring(0, 31 - ek.Length) : temiz) + ek;
            }
            return sonuc;
        }
    }
}
