#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// ADIM 2 — "Şablon Excel Oluştur".
    /// ──────────────────────────────────────────────────────────
    /// Boş dağıtım şablonunu (Templates\dagitim_sablonu.xlsx) temel alır ve:
    ///   1) "Çekilmiş Dersler" sayfasını doldurur:
    ///        • Bölüm dersleri (kodu BIL/ELM/INS/MAK/MEK/MEM ile başlayanlar) → kod ilk-rakamı
    ///          bloklarına (0–4). Her blok 8 sütun: Bölüm|Sınıf|Saat|Ders Adı|A|B|C|D.
    ///        • Ortak dersler (kodu bölüm önekiyle başlamayanlar: FIZ/MAT/TKN/ING vb.) →
    ///          blokların altına "ORTAK DERSLER" başlıklı DÜZ liste (bölüm başına ayrı satır).
    ///        • Aynı ders kodunun tüm şubeleri (Normal + İkinci Öğretim) tek satırda toplanır;
    ///          şube öğrenci sayıları ilgili harf kolonuna eklenir (toplam).
    ///   2) "Sınav Haftası Dağıtım Şablonu" sayfasına seçilen sınav TARİHLERİNİ (takvim) yazar;
    ///      her günün blok saat satırlarını o günün seçili saatlerine göre kurar.
    ///   3) Seçilen her gün için örnek "Sınav Günü" sayfasını (salon kolonlarıyla) klonlar;
    ///      örnek/şablon gün sayfalarını çıktıdan siler.
    /// </summary>
    public static class CekilmisDerslerExportHelper
    {
        private const string CekilmisSayfaAdi = "Çekilmiş Dersler";
        private const int VeriBaslangicSatiri = 5;   // 3=grup, 4=kolon başlığı, 5+=veri
        private const int BlokGenisligi = 8;          // Bölüm|Sınıf|Saat|Ders Adı|A|B|C|D
        private const int MaxGrup = 4;                // 0..4 → 5 blok

        /// <summary>Şablon sayfasındaki tarih satırı (tüm günler bu satırda yan yana).</summary>
        private const int IlkTarihSatiri = 2;

        /// <summary>Şablondaki 6 bölüm (satır sırası korunur).</summary>
        private static readonly string[] Bolumler = { "BİL", "EEM", "INS", "MAK", "MEK", "MEM" };

        /// <summary>Bölümlere ait ders kodu önekleri. Bunlarla başlamayan kodlar "ortak"tır.</summary>
        private static readonly HashSet<string> BolumKodOnekleri =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BIL", "ELM", "INS", "MAK", "MEK", "MEM" };

        private static readonly Dictionary<string, int> BolumSira = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "BİL", 0 }, { "EEM", 1 }, { "INS", 2 }, { "MAK", 3 }, { "MEK", 4 }, { "MEM", 5 }
        };

        public static string SablonYolu =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "dagitim_sablonu.xlsx");

        // Pivotlanmış tek ders kaydı
        private class DersKayit
        {
            public string Bolum;      // kısaltma (BİL/EEM/...)
            public string Kod;
            public int Grup;          // kod ilk rakamı (0..)
            public int Sinif;
            public string DersAdi;
            public bool Ortak;
            public Dictionary<string, int> Harfler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ders listesini + seçilen takvimi kullanarak dağıtım şablonu .xlsx üretir.
        /// </summary>
        /// <param name="dersler">Adım 1'de okunan tüm dersler (Normal + İÖ).</param>
        /// <param name="takvim">Seçilen sınav günleri → o günün saatleri.</param>
        /// <param name="cikisYolu">Kaydedilecek .xlsx yolu.</param>
        /// <param name="uyarilar">Bilgilendirici uyarılar.</param>
        /// <param name="derslikler">Kullanıcının yüklediği derslikler — gün sayfalarının salon sütunları.</param>
        public static void Olustur(List<Ders> dersler, Dictionary<DateTime, List<string>> takvim,
                                   string cikisYolu, out List<string> uyarilar,
                                   List<SinavAtama> dagitim = null, List<Salon> derslikler = null)
        {
            uyarilar = new List<string>();
            if (dersler == null) dersler = new List<Ders>();
            if (takvim == null) takvim = new Dictionary<DateTime, List<string>>();

            if (!File.Exists(SablonYolu))
                throw new FileNotFoundException(
                    "Boş dağıtım şablonu bulunamadı:\n" + SablonYolu +
                    "\n\nTemplates\\dagitim_sablonu.xlsx dosyasının çıktı klasörüne kopyalandığından emin olun.");

            // ── Şubeleri (kod+bölüm) bazında topla; İÖ aynı harf kolonuna eklenir ──
            var kayitlar = new Dictionary<(string Bolum, string Kod), DersKayit>();
            foreach (var d in dersler)
            {
                if (string.IsNullOrWhiteSpace(d.DersKodu)) continue;

                int grup = IlkRakam(d.DersKodu);
                if (grup < 0)
                {
                    uyarilar.Add($"{d.DersKodu} ({d.DersAdi}): kodda rakam yok — atlandı.");
                    continue;
                }

                string bolumKisa = BolumKisalt(d.BolumAdi);
                string kod = d.DersKodu.Trim().ToUpperInvariant();
                var key = (bolumKisa, kod);

                if (!kayitlar.TryGetValue(key, out var kayit))
                {
                    kayit = new DersKayit
                    {
                        Bolum = bolumKisa,
                        Kod = kod,
                        Grup = grup,
                        Sinif = d.SinifSeviyesi,
                        DersAdi = d.DersAdi,
                        Ortak = !BolumKodOnekleri.Contains(KodOneki(kod))
                    };
                    kayitlar[key] = kayit;
                }

                string harf = HarfKolonu(d.Sube);
                if (!kayit.Harfler.ContainsKey(harf)) kayit.Harfler[harf] = 0;
                kayit.Harfler[harf] += Math.Max(0, d.Kontenjan);
            }

            // ── Bölüm dersleri: kod ilk-rakamına göre 5 blok ──
            var bloklar = new List<List<DersKayit>>();
            for (int g = 0; g <= MaxGrup; g++)
            {
                var liste = kayitlar.Values
                    .Where(k => !k.Ortak && k.Grup == g)
                    .OrderBy(k => BolumSira.TryGetValue(k.Bolum, out int s) ? s : 99)
                    .ThenBy(k => k.Kod, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                bloklar.Add(liste);
            }

            // Kod grubu 4'ten büyük bölüm dersleri (şablonda blok yok) → uyar
            foreach (var k in kayitlar.Values.Where(k => !k.Ortak && k.Grup > MaxGrup))
                uyarilar.Add($"{k.Kod} ({k.DersAdi}): kod {k.Grup} ile başlıyor, şablonda 0–{MaxGrup} bloğu var — atlandı.");

            // ── Ortak dersler: düz liste (koda göre, sonra bölüme göre) ──
            var ortakListe = kayitlar.Values
                .Where(k => k.Ortak)
                .OrderBy(k => k.Kod, StringComparer.OrdinalIgnoreCase)
                .ThenBy(k => BolumSira.TryGetValue(k.Bolum, out int s) ? s : 99)
                .ToList();

            var gunler = takvim.Keys.OrderBy(d => d).ToList();

            using (var paket = new ExcelPackage(new FileInfo(SablonYolu)))
            {
                CekilmisDersleriYaz(paket, bloklar, ortakListe);
                SablonTakvimiYaz(paket, gunler, takvim, dagitim, uyarilar);
                GunSayfalariOlustur(paket, gunler, takvim, derslikler, uyarilar);

                // Şablon dosyasındaki eski "INT" başlıklarını "D" olarak güncelle ve
                // D şubesi başlıklarındaki mavi dolguyu kaldır.
                // (Şablon .xlsx'e dokunulmaz; yalnızca üretilen çıktıda düzeltilir.)
                EskiIntBasliklariniDuzelt(paket);

                var cikis = new FileInfo(cikisYolu);
                if (cikis.Exists) cikis.Delete();
                paket.SaveAs(cikis);
            }
        }

        /// <summary>
        /// Boş şablon dosyasında 4. şube kolonunun başlığı tarihsel olarak "INT" yazar.
        /// Sistem artık bu şubeyi "D" olarak adlandırdığından, üretilen çıktıdaki tüm
        /// "INT" başlık hücreleri "D" ile değiştirilir. Şablon dosyasının kendisine
        /// DOKUNULMAZ — biçimlendirmesi bozulmasın diye değişiklik yalnızca bellekteki
        /// pakete (yani çıktıya) uygulanır.
        ///
        /// AYRICA: D (eski INT) şubesi başlık hücrelerine şablondan gelen MAVİ arka plan
        /// dolgusu KALDIRILIR — D şubesi varsayılan/şeffaf arka planla üretilir.
        /// </summary>
        private static void EskiIntBasliklariniDuzelt(ExcelPackage paket)
        {
            foreach (var ws in paket.Workbook.Worksheets)
            {
                if (ws.Dimension == null) continue;
                int sonSatir = Math.Min(ws.Dimension.End.Row, 10);   // başlıklar üst satırlardadır
                int sonKolon = ws.Dimension.End.Column;

                for (int r = 1; r <= sonSatir; r++)
                    for (int c = 1; c <= sonKolon; c++)
                    {
                        if (!(ws.Cells[r, c].Value is string s)) continue;
                        string t = s.Trim();

                        bool intBaslik = string.Equals(t, "INT", StringComparison.OrdinalIgnoreCase);
                        bool dBaslik = string.Equals(t, SubeEtiketi.DordunculSube, StringComparison.OrdinalIgnoreCase);
                        if (!intBaslik && !dBaslik) continue;

                        if (intBaslik)
                            ws.Cells[r, c].Value = SubeEtiketi.DordunculSube;

                        // D şubesi başlığı → dolgu YOK (varsayılan/şeffaf arka plan)
                        ws.Cells[r, c].Style.Fill.PatternType = ExcelFillStyle.None;
                    }
            }
        }

        // ═══════════════ 1) ÇEKİLMİŞ DERSLER ═══════════════

        private static void CekilmisDersleriYaz(ExcelPackage paket, List<List<DersKayit>> bloklar, List<DersKayit> ortakListe)
        {
            var ws = paket.Workbook.Worksheets[CekilmisSayfaAdi];
            if (ws == null) throw new Exception($"Şablonda '{CekilmisSayfaAdi}' sayfası bulunamadı.");

            // Eski veri + varsa eski ortak bölümünü temizle (geniş alan)
            // Önce veri alanındaki eski birleştirmeler çözülür: yalnızca Value=null yapmak
            // merge'leri bırakıyor, aşağıdaki "ORTAK DERSLER" başlık merge'ü bunlarla
            // çakışınca EPPlus hata veriyor ya da başlık kaymış görünüyordu.
            int temizSonSatir = VeriBaslangicSatiri + 400;
            int temizSonKolon = (MaxGrup + 1) * BlokGenisligi;
            foreach (var adres in ws.MergedCells.Where(m => m != null).ToList())
            {
                var mr = new ExcelAddress(adres);
                if (mr.Start.Row >= VeriBaslangicSatiri && mr.Start.Row <= temizSonSatir &&
                    mr.Start.Column <= temizSonKolon)
                    ws.Cells[adres].Merge = false;
            }
            for (int r = VeriBaslangicSatiri; r <= VeriBaslangicSatiri + 400; r++)
                for (int c = 1; c <= (MaxGrup + 1) * BlokGenisligi; c++)
                    ws.Cells[r, c].Value = null;

            int enAltSatir = VeriBaslangicSatiri - 1;

            // 5 rakam bloğu
            for (int g = 0; g <= MaxGrup; g++)
            {
                int basSutun = 1 + g * BlokGenisligi;
                var liste = bloklar[g];
                for (int i = 0; i < liste.Count; i++)
                {
                    int satir = VeriBaslangicSatiri + i;
                    SatirYaz(ws, satir, basSutun, liste[i]);
                    if (satir > enAltSatir) enAltSatir = satir;
                }
            }

            // "ORTAK DERSLER" düz listesi — blokların altında
            if (ortakListe.Count > 0)
            {
                int baslikSatir = enAltSatir + 3;
                var baslik = ws.Cells[baslikSatir, 1, baslikSatir, BlokGenisligi];
                baslik.Merge = true;
                baslik.Value = "ORTAK DERSLER (kodu bölüm kısaltmasıyla başlamayan dersler)";
                baslik.Style.Font.Bold = true;
                baslik.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

                int kolonBaslik = baslikSatir + 1;
                string[] basliklar = { "Bölüm", "Sınıf", "Saat", "Ders Adı", "A", "B", "C", "D" };
                for (int c = 0; c < basliklar.Length; c++)
                {
                    var h = ws.Cells[kolonBaslik, 1 + c];
                    h.Value = basliklar[c];
                    h.Style.Font.Bold = true;
                    h.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                for (int i = 0; i < ortakListe.Count; i++)
                    SatirYaz(ws, kolonBaslik + 1 + i, 1, ortakListe[i]);
            }
        }

        private static void SatirYaz(ExcelWorksheet ws, int satir, int basSutun, DersKayit k)
        {
            ws.Cells[satir, basSutun + 0].Value = k.Bolum;      // Bölüm
            ws.Cells[satir, basSutun + 1].Value = k.Sinif;      // Sınıf
            // basSutun+2 = Saat → boş (manuel)
            ws.Cells[satir, basSutun + 3].Value = k.DersAdi;    // Ders Adı
            YazHarf(ws, satir, basSutun + 4, k.Harfler, "A");
            YazHarf(ws, satir, basSutun + 5, k.Harfler, "B");
            YazHarf(ws, satir, basSutun + 6, k.Harfler, "C");
            YazHarf(ws, satir, basSutun + 7, k.Harfler, SubeEtiketi.DordunculSube);

            var rng = ws.Cells[satir, basSutun, satir, basSutun + BlokGenisligi - 1];
            rng.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            rng.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            rng.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            rng.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            ws.Cells[satir, basSutun + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[satir, basSutun + 4, satir, basSutun + 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // ═══════════════ 2) ŞABLON TAKVİMİ (tarih + saat satırları) ═══════════════

        /// <summary>
        /// TEK BANTLI ŞABLON TAKVİMİ.
        /// ──────────────────────────────────────────────────────────────
        /// Seçilen TÜM sınav günleri tek bantta, yan yana yazılır:
        ///
        ///   satır 2: tarihler | satır 3: kolon başlıkları | satır 4+: veri
        ///
        /// Takvim hafta sınırında BÖLÜNMEZ — dönem Perşembe başlasa da günler alt alta
        /// iki banda dağılmaz. Şablon dosyasında 7 gün bloğu hazırdır; daha fazla gün
        /// seçildiyse (Final = 10 gün) eksik bloklar 1. bloğun biçimiyle sağa eklenir.
        /// <c>DagitimSablonuOkuyucu</c> tarih satırındaki tüm blokları okuduğundan
        /// kullanıcı dosyayı elle doldurup geri yükleyebilir.
        /// </summary>
        private static void SablonTakvimiYaz(ExcelPackage paket, List<DateTime> gunler,
                                             Dictionary<DateTime, List<string>> takvim, List<SinavAtama> dagitim,
                                             List<string> uyarilar)
        {
            var ws = SablonSayfasiBul(paket);
            if (ws?.Dimension == null) return;

            // Otomatik dağıtım varsa: (bölüm|gün|saat) → hücre içeriği (ders adı, sınıf, harf kontenjanları)
            var hucreler = DagitimHucreleri(dagitim, uyarilar);

            int sonKolon = ws.Dimension.End.Column;
            int sablonBlokSayisi = Math.Max(1, sonKolon / BlokGenisligi);   // şablonda 7 blok
            int blokSayisi = Math.Max(sablonBlokSayisi, gunler.Count);
            int maxSaat = takvim.Count > 0 ? takvim.Values.Max(v => v.Count) : 5;
            int veriSatirSayisi = Bolumler.Length * Math.Max(maxSaat, 1);

            int tarihSatiri = IlkTarihSatiri;
            int baslikSatiri = tarihSatiri + 1;
            int veriBaslangic = tarihSatiri + 2;
            int veriBitis = veriBaslangic + veriSatirSayisi - 1;

            // Şablonda olmayan gün blokları (8. ve sonrası) — veri yazılmadan ÖNCE eklenir ki
            // 1. bloğun ham şablon biçimi kopyalansın (zebra dolgusu taşınmasın).
            for (int blok = sablonBlokSayisi; blok < blokSayisi; blok++)
                BlokEkle(ws, blok, tarihSatiri, baslikSatiri, veriBitis);

            for (int blok = 0; blok < blokSayisi; blok++)
            {
                int col = 1 + blok * BlokGenisligi;

                if (blok >= gunler.Count)
                {
                    // Kullanılmayan gün bloğu — tarih ve içerik temizlenir
                    ws.Cells[tarihSatiri, col].Value = null;
                    for (int r = veriBaslangic; r <= veriBitis; r++)
                        for (int c = 0; c < BlokGenisligi; c++) ws.Cells[r, col + c].Value = null;
                    continue;
                }

                DateTime gun = gunler[blok];
                var saatler = (takvim.TryGetValue(gun, out var s) ? s : new List<string>())
                    .OrderBy(SaatYardimci.Anahtar).ToList();

                var tarihH = ws.Cells[tarihSatiri, col];
                tarihH.Value = gun;
                tarihH.Style.Numberformat.Format = "dd.mm.yyyy";
                tarihH.Style.Font.Bold = true;
                tarihH.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                int r2 = veriBaslangic;
                int saatIndex = 0;
                foreach (var saat in saatler)
                {
                    int blokBas = r2; // bu saat grubunun ilk satırı
                    foreach (var bolum in Bolumler)
                    {
                        ws.Cells[r2, col + 0].Value = bolum;   // Bölüm
                        ws.Cells[r2, col + 1].Value = null;    // Sınıf
                        ws.Cells[r2, col + 2].Value = saat;    // Saat
                        for (int c = 3; c < BlokGenisligi; c++) ws.Cells[r2, col + c].Value = null;

                        // Otomatik dağıtım hücresi doldur
                        if (hucreler.TryGetValue(HucreAnahtar(bolum, gun, saat), out var icerik))
                        {
                            ws.Cells[r2, col + 1].Value = icerik.Sinif;      // Sınıf
                            ws.Cells[r2, col + 3].Value = icerik.DersAdi;    // Ders Adı
                            YazHarf(ws, r2, col + 4, icerik.Harfler, "A");
                            YazHarf(ws, r2, col + 5, icerik.Harfler, "B");
                            YazHarf(ws, r2, col + 6, icerik.Harfler, "C");
                            YazHarf(ws, r2, col + 7, icerik.Harfler, SubeEtiketi.DordunculSube);
                        }

                        var rng = ws.Cells[r2, col, r2, col + BlokGenisligi - 1];
                        rng.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        rng.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        rng.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        rng.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        r2++;
                    }

                    // ── GÖRSEL SAAT AYRIMI ──
                    // Her saat grubu (6 bölüm satırı) net biçimde ayrılır:
                    //  • grubun üstü/altı KALIN çerçeve,
                    //  • gruplar dönüşümlü açık zemin (zebra),
                    //  • Saat kolonu kalın yazı + orta hizalı.
                    var saatBlogu = ws.Cells[blokBas, col, r2 - 1, col + BlokGenisligi - 1];
                    ws.Cells[blokBas, col, blokBas, col + BlokGenisligi - 1]
                        .Style.Border.Top.Style = ExcelBorderStyle.Medium;
                    ws.Cells[r2 - 1, col, r2 - 1, col + BlokGenisligi - 1]
                        .Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                    if (saatIndex % 2 == 1)
                    {
                        saatBlogu.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        saatBlogu.Style.Fill.BackgroundColor.SetColor(
                            System.Drawing.Color.FromArgb(235, 238, 245)); // açık gri-mavi
                    }
                    var saatKolonu = ws.Cells[blokBas, col + 2, r2 - 1, col + 2];
                    saatKolonu.Style.Font.Bold = true;
                    saatKolonu.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    saatIndex++;
                }

                // Bloktaki artık satırları temizle
                for (; r2 <= veriBitis; r2++)
                    for (int c = 0; c < BlokGenisligi; c++) ws.Cells[r2, col + c].Value = null;
            }

            // Veri alanının altında şablondan kalmış satırları temizle
            int temizSonKolon = Math.Max(sonKolon, blokSayisi * BlokGenisligi);
            for (int r = veriBitis + 1; r <= ws.Dimension.End.Row; r++)
                for (int c = 1; c <= temizSonKolon; c++) ws.Cells[r, c].Value = null;
        }

        /// <summary>
        /// Şablon dosyasında hazır olmayan bir gün bloğunu (8. ve sonrası) 1. bloğun
        /// biçimiyle oluşturur: sütun genişlikleri, hücre stilleri, kolon başlıkları ve
        /// 8 sütuna birleştirilmiş tarih hücresi kopyalanır.
        /// </summary>
        private static void BlokEkle(ExcelWorksheet ws, int blok, int tarihSatiri, int baslikSatiri, int veriBitis)
        {
            int col = 1 + blok * BlokGenisligi;
            for (int c = 0; c < BlokGenisligi; c++)
            {
                ws.Column(col + c).Width = ws.Column(1 + c).Width;
                for (int r = tarihSatiri; r <= veriBitis; r++)
                    ws.Cells[r, col + c].StyleID = ws.Cells[r, 1 + c].StyleID;
                ws.Cells[baslikSatiri, col + c].Value = ws.Cells[baslikSatiri, 1 + c].Value;
            }
            ws.Cells[tarihSatiri, col, tarihSatiri, col + BlokGenisligi - 1].Merge = true;
        }

        // ═══════════════ 3) GÜN SAYFALARI (örnek salon sayfasını klonla) ═══════════════

        private static void GunSayfalariOlustur(ExcelPackage paket, List<DateTime> gunler,
                                                Dictionary<DateTime, List<string>> takvim, List<Salon> derslikler,
                                                List<string> uyarilar)
        {
            // Örnek gün sayfalarını (Bölüm/Tarih/Dersler yapısındaki) tespit et
            var gunImzali = paket.Workbook.Worksheets
                .Where(w => GunSayfasiImzasiVar(w))
                .ToList();

            var kaynak = gunImzali.FirstOrDefault();
            if (kaynak == null)
            {
                uyarilar.Add("Örnek 'Sınav Günü' sayfası bulunamadı — gün sayfaları oluşturulamadı.");
                return;
            }

            // Silinecek eski sayfalar: tüm örnek gün sayfaları + "Sayfa3"
            var silinecek = new List<string>(gunImzali.Select(w => w.Name));
            var sayfa3 = paket.Workbook.Worksheets.FirstOrDefault(w =>
                w.Name.Replace(" ", "").Equals("Sayfa3", StringComparison.OrdinalIgnoreCase));
            if (sayfa3 != null && !silinecek.Contains(sayfa3.Name)) silinecek.Add(sayfa3.Name);

            int kaynakSonSatir = kaynak.Dimension?.End.Row ?? 32;

            foreach (var gun in gunler)
            {
                // KURAL: Sekme adında "Sınav Günü" gibi ibare YASAK — yalnızca tarih (GG.AA.YYYY)
                string ad = GuvenliSayfaAdi($"{gun:dd.MM.yyyy}", paket);
                var yeni = paket.Workbook.Worksheets.Add(ad, kaynak);

                var saatler = (takvim.TryGetValue(gun, out var s) ? s : new List<string>())
                    .OrderBy(SaatYardimci.Anahtar).ToList();

                // Örnek sayfadan klonlanan TÜM veri hücreleri temizlenir — eskiden yalnızca
                // 1–9. kolonlar temizleniyordu; salon kolonlarındaki (10+) örnek değerler
                // yeni gün sayfalarına taşınıp yanlış satırlarda görünüyordu.
                int klonSonKolon = Math.Max(9, yeni.Dimension?.End.Column ?? 9);
                int klonSonSatir = Math.Max(kaynakSonSatir, 3 + saatler.Count * Bolumler.Length);
                for (int rr = 3; rr <= klonSonSatir; rr++)
                    for (int c = 1; c <= klonSonKolon; c++) yeni.Cells[rr, c].Value = null;

                SalonSutunlariniKur(yeni, derslikler, klonSonSatir);

                int r = 3; // r1=salon adları, r2=başlık/kapasite, r3+=veri
                foreach (var saat in saatler)
                    foreach (var bolum in Bolumler)
                    {
                        yeni.Cells[r, 1].Value = bolum;   // Bölüm
                        yeni.Cells[r, 2].Value = null;    // SINIF
                        yeni.Cells[r, 3].Value = gun;     // Tarih
                        yeni.Cells[r, 3].Style.Numberformat.Format = "dd.mm.yyyy";
                        yeni.Cells[r, 4].Value = saat;    // Saat
                        for (int c = 5; c <= 9; c++) yeni.Cells[r, c].Value = null; // Dersler/A/B/C/D
                        r++;
                    }
            }

            // Örnek/şablon gün sayfalarını sil
            foreach (var isim in silinecek)
            {
                try { paket.Workbook.Worksheets.Delete(isim); } catch { /* yoksa geç */ }
            }
        }

        /// <summary>Gün sayfalarında salon sütunlarının başladığı sütun (J).</summary>
        private const int GunSayfasiSalonKolonu = 10;

        /// <summary>
        /// Klonlanan gün sayfasının salon sütunlarını yüklenen derslik listesiyle yeniden kurar.
        /// Şablon dosyasındaki örnek salonlar (belirli bir okulun derslikleri) ve onların
        /// konuma bağlı uygulamalı dolguları çıktıya taşınmaz; sıra ve dolgu
        /// 'Salon Atanmış Dosya' ile aynıdır.
        /// </summary>
        private static void SalonSutunlariniKur(ExcelWorksheet ws, List<Salon> derslikler, int sonSatir)
        {
            const int ilk = GunSayfasiSalonKolonu;
            var sirali = GunSalonMatrisExportHelper.SalonSutunSirasi(derslikler);
            int ornekSon = Math.Max(ilk, ws.Dimension?.End.Column ?? ilk);
            int sonKol = ilk + sirali.Count - 1;

            // Şablonda dolgusuz (normal salon) bir sütun — şablondan fazla salon varsa biçim kaynağı
            int normalKaynak = Enumerable.Range(ilk, ornekSon - ilk + 1)
                .FirstOrDefault(c => ws.Cells[3, c].Style.Fill.PatternType == ExcelFillStyle.None, ilk);

            for (int i = 0; i < sirali.Count; i++)
            {
                int kol = ilk + i;
                if (kol > ornekSon)
                {
                    ws.Column(kol).Width = ws.Column(normalKaynak).Width;
                    for (int r = 1; r <= sonSatir; r++)
                        ws.Cells[r, kol].StyleID = ws.Cells[r, normalKaynak].StyleID;
                }

                var sutun = ws.Cells[1, kol, sonSatir, kol];
                if (sirali[i].UygulamaliMi)
                {
                    sutun.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sutun.Style.Fill.BackgroundColor.SetColor(GunSalonMatrisExportHelper.UygulamaliKolonDolgu);
                }
                else sutun.Style.Fill.PatternType = ExcelFillStyle.None;

                ws.Cells[1, kol].Value = sirali[i].SalonKodu;
                ws.Cells[2, kol].Value = sirali[i].Kapasite;
            }

            // Kalan örnek sütunlar temizlenir; şablonun sağ dış kenarlığı son salon sütununa taşınır
            if (sonKol < ornekSon)
            {
                if (sirali.Count > 0)
                    for (int r = 1; r <= sonSatir; r++)
                        ws.Cells[r, sonKol].Style.Border.Right.Style = ws.Cells[r, ornekSon].Style.Border.Right.Style;
                ws.Cells[1, sonKol + 1, sonSatir, ornekSon].Clear();
            }
        }

        // ═══════════════ YARDIMCILAR ═══════════════

        private static void YazHarf(ExcelWorksheet ws, int satir, int sutun, Dictionary<string, int> harfler, string harf)
        {
            if (harfler.TryGetValue(harf, out int deger) && deger > 0)
                ws.Cells[satir, sutun].Value = deger;
        }

        private class HucreIcerik
        {
            public string DersAdi;
            public int Sinif;
            public Dictionary<string, int> Harfler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Otomatik dağıtım sonucunu (bölüm|gün|saat) → hücre içeriği sözlüğüne çevirir.</summary>
        private static Dictionary<string, HucreIcerik> DagitimHucreleri(List<SinavAtama> dagitim, List<string> uyarilar = null)
        {
            var map = new Dictionary<string, HucreIcerik>();
            if (dagitim == null) return map;

            foreach (var a in dagitim)
            {
                // Anahtar KISALTMA ile kurulur: okuma tarafı (SablonTakvimiYaz) "BİL" gibi
                // kısaltmayla arıyor; BolumAdi tam ad gelirse hücreler hiç dolmuyordu.
                string key = HucreAnahtar(BolumKisalt(a.BolumAdi), a.Gun, a.Saat);
                if (!map.TryGetValue(key, out var ic))
                {
                    ic = new HucreIcerik { DersAdi = a.DersAdi, Sinif = a.SinifSeviyesi };
                    map[key] = ic;
                }
                else if (!string.Equals(ic.DersAdi, a.DersAdi, StringComparison.OrdinalIgnoreCase))
                {
                    // Aynı bölüm+gün+saat hücresine ikinci FARKLI ders düştü — şablonda tek satır
                    // olduğundan kontenjanı ilk derse KARIŞTIRILMAZ, uyarı verilip atlanır.
                    uyarilar?.Add($"{a.Gun:dd.MM.yyyy} {a.Saat} {BolumKisalt(a.BolumAdi)}: '{a.DersAdi}' ile " +
                                  $"'{ic.DersAdi}' aynı şablon hücresine düşüyor — '{a.DersAdi}' şablona yazılmadı.");
                    continue;
                }
                string harf = HarfKolonu(a.Sube);
                if (!ic.Harfler.ContainsKey(harf)) ic.Harfler[harf] = 0;
                ic.Harfler[harf] += Math.Max(0, a.Kontenjan);
            }
            return map;
        }

        private static string HucreAnahtar(string bolumKisa, DateTime gun, string saat)
            => $"{bolumKisa}|{gun:yyyy-MM-dd}|{SaatYardimci.Normalize(saat)}";

        private static ExcelWorksheet SablonSayfasiBul(ExcelPackage paket)
        {
            // 1) Tam ad (en güvenilir)
            var tam = paket.Workbook.Worksheets.FirstOrDefault(w => w.Name == "Sınav Haftası Dağıtım Şablonu");
            if (tam != null) return tam;

            // 2) ASCII-normalize (Türkçe büyük/küçük harf tuzağına düşmeden)
            var norm = paket.Workbook.Worksheets.FirstOrDefault(w =>
            {
                string n = AsciiNorm(w.Name);
                return n.Contains("dagitim") && n.Contains("sablon");
            });
            if (norm != null) return norm;

            // 3) Son çare: ilk sayfa (şablonda ilk sayfa dağıtım şablonudur)
            return paket.Workbook.Worksheets.FirstOrDefault(w => w.Dimension != null);
        }

        /// <summary>Türkçe karakterleri ASCII'ye indirger, küçük harfe çevirir (harf/rakam dışını atar).</summary>
        private static string AsciiNorm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case 'ı': case 'İ': case 'I': case 'i': case 'î': case 'Î': sb.Append('i'); break;
                    case 'ş': case 'Ş': sb.Append('s'); break;
                    case 'ğ': case 'Ğ': sb.Append('g'); break;
                    case 'ç': case 'Ç': sb.Append('c'); break;
                    case 'ö': case 'Ö': sb.Append('o'); break;
                    case 'ü': case 'Ü': sb.Append('u'); break;
                    case 'â': case 'Â': sb.Append('a'); break;
                    default:
                        if (c >= 'A' && c <= 'Z') sb.Append((char)(c + 32));
                        else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Bir sayfanın örnek "Sınav Günü" yapısında olup olmadığını (row2: Tarih + Dersler) kontrol eder.</summary>
        private static bool GunSayfasiImzasiVar(ExcelWorksheet w)
        {
            if (w?.Dimension == null) return false;
            if (w.Name == CekilmisSayfaAdi) return false;
            if (SablonSayfasiBulAdi(w.Name)) return false;

            bool tarih = false, dersler = false;
            int sonK = Math.Min(w.Dimension.End.Column, 12);
            for (int c = 1; c <= sonK; c++)
            {
                string v = AsciiNorm(w.Cells[2, c].Value?.ToString());
                if (v == "tarih") tarih = true;
                if (v == "dersler") dersler = true;
            }
            return tarih && dersler;
        }

        private static bool SablonSayfasiBulAdi(string ad)
        {
            string n = AsciiNorm(ad);
            return n.Contains("dagitim") && n.Contains("sablon");
        }

        /// <summary>Excel için geçerli, benzersiz sayfa adı üretir (≤31 karakter, yasak karakterler temizli).</summary>
        private static string GuvenliSayfaAdi(string istenen, ExcelPackage paket)
        {
            string temiz = istenen;
            foreach (char c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                temiz = temiz.Replace(c, '-');
            if (temiz.Length > 31) temiz = temiz.Substring(0, 31);

            string sonuc = temiz;
            int i = 2;
            while (paket.Workbook.Worksheets.Any(w => string.Equals(w.Name, sonuc, StringComparison.OrdinalIgnoreCase)))
            {
                string ek = $" ({i++})";
                sonuc = (temiz.Length + ek.Length > 31 ? temiz.Substring(0, 31 - ek.Length) : temiz) + ek;
            }
            return sonuc;
        }

        /// <summary>Kod numarasının ilk rakamı; yoksa -1.</summary>
        private static int IlkRakam(string kod)
        {
            foreach (char c in kod)
                if (char.IsDigit(c)) return c - '0';
            return -1;
        }

        /// <summary>Kodun baştaki harf öneki (ilk rakama kadar). Örn. "FIZ112" → "FIZ".</summary>
        private static string KodOneki(string kod)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in kod ?? "")
            {
                if (char.IsDigit(c)) break;
                if (char.IsLetter(c)) sb.Append(c);
            }
            return sb.ToString().ToUpperInvariant();
        }

        /// <summary>Şube etiketini A/B/C/D harf kolonuna indirger (eski "INT" → "D").</summary>
        private static string HarfKolonu(string sube)
            => SubeEtiketi.Normalize(sube, SubeEtiketi.DordunculSube);

        private static string BolumKisalt(string bolumAdi) => BolumYardimci.Kisalt(bolumAdi);
    }
}
