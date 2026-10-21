#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// "Aydın Abi Excel" resmi matris çıktı formatı üretici.
    /// ─────────────────────────────────────────────────────
    /// Sütunlar: Bölüm | SINIF | Tarih | Saat | Dersler | Kontenjan | [Salon1] | [Salon2] | ... | [SalonN]
    /// Satırlar: Her şube ayrı satır — ilgili salona atanan öğrenci sayısı o salonun hücresine yazılır.
    /// Salonlar alfabetik sıralı.
    /// </summary>
    public static class ExcelMatrisExportHelper
    {
        // ═══════════════════════════════════════════
        // RENK PALETİ (Catppuccin Mocha temalı)
        // ═══════════════════════════════════════════
        private static readonly Color BaslikArkaplan = Color.FromArgb(30, 30, 46);
        private static readonly Color BaslikYazi = Color.FromArgb(205, 214, 244);
        private static readonly Color SalonBaslikArkaplan = Color.FromArgb(49, 50, 68);
        private static readonly Color SatirCift = Color.FromArgb(240, 240, 248);
        private static readonly Color SatirTek = Color.White;
        private static readonly Color DoluHucre = Color.FromArgb(166, 227, 161);     // Yeşil — salon dolu
        private static readonly Color BosHucre = Color.FromArgb(235, 235, 240);       // Açık gri — salon boş
        private static readonly Color KenarlıkRenk = Color.FromArgb(180, 180, 195);
        private static readonly Color BolumBaslik = Color.FromArgb(137, 180, 250);    // Mavi — bölüm başlığı

        /// <summary>
        /// Sınav dağıtım sonuçlarını Aydın Abi matris formatında Excel'e yazar.
        /// </summary>
        /// <param name="sonuclar">Algoritma çıktısı — tüm sınav atamaları</param>
        /// <param name="salonlar">Veritabanındaki tüm salonlar</param>
        /// <param name="dosyaYolu">Çıktı .xlsx dosyasının kaydedileceği yol</param>
        /// <param name="programBasligi">Seçili öğretim yılı / dönem / sınav türü başlığı
        /// (ör. "2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI") — tüm sayfa başlıklarında kullanılır.</param>
        /// <param name="gozetmenler">Gözetmen listesi — görev çizelgesinde bölüm eşleştirmesi için (opsiyonel)</param>
        public static void MatrisExcelOlustur(List<SinavAtama> sonuclar, List<Salon> salonlar,
            string dosyaYolu, string programBasligi = "", List<Gozetmen> gozetmenler = null)
        {
            if (string.IsNullOrWhiteSpace(programBasligi)) programBasligi = "SINAV PROGRAMI";

            if (sonuclar == null || sonuclar.Count == 0)
                throw new ArgumentException("Dışa aktarılacak sınav sonucu bulunamadı.");

            // Salon sırası 'Salon Atanmış Dosya' ile aynı: T4 binası önce, sonra kod
            var siraliSalonlar = GunSalonMatrisExportHelper.T4OnceSirala(salonlar, s => s.SalonKodu).ToList();
            var salonKodlari = siraliSalonlar.Select(s => s.SalonKodu).ToList();

            // Salon kodu → sıra indeksi. Büyük/küçük harf ve boşluk duyarsız: Excel gidiş-dönüşünde
            // kullanıcı "t4-207 " yazsa bile salon çıktıdan sessizce düşmez.
            var salonIndeks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < salonKodlari.Count; i++)
                salonIndeks[(salonKodlari[i] ?? "").Trim()] = i;

            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Sınav Programı Matrisi");

                // ═══════════════════════════════════════════
                // SAYFA AYARLARI
                // ═══════════════════════════════════════════
                ws.View.ShowGridLines = true;
                ws.PrinterSettings.Orientation = eOrientation.Landscape;
                ws.PrinterSettings.FitToPage = true;
                ws.PrinterSettings.FitToWidth = 1;
                ws.PrinterSettings.FitToHeight = 0;

                int currentRow = 1;

                // ═══════════════════════════════════════════
                // ÜST BAŞLIK
                // ═══════════════════════════════════════════
                string donemAciklama = $"{programBasligi} MATRİS TABLOSU";

                int toplamSutun = 6 + salonKodlari.Count; // Sabit 6 sütun + salon sütunları

                ws.Cells[currentRow, 1].Value = "TEKNOLOJİ FAKÜLTESİ";
                var ustBaslikRange = ws.Cells[currentRow, 1, currentRow, toplamSutun];
                ustBaslikRange.Merge = true;
                ustBaslikRange.Style.Font.Bold = true;
                ustBaslikRange.Style.Font.Size = 16;
                ustBaslikRange.Style.Font.Color.SetColor(BaslikYazi);
                ustBaslikRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                ustBaslikRange.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
                ustBaslikRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ustBaslikRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                ws.Row(currentRow).Height = 30;
                currentRow++;

                ws.Cells[currentRow, 1].Value = donemAciklama;
                var donemRange = ws.Cells[currentRow, 1, currentRow, toplamSutun];
                donemRange.Merge = true;
                donemRange.Style.Font.Bold = true;
                donemRange.Style.Font.Size = 13;
                donemRange.Style.Font.Color.SetColor(BaslikYazi);
                donemRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                donemRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(49, 50, 68));
                donemRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Row(currentRow).Height = 25;
                currentRow++;

                // Boş satır
                currentRow++;

                // ═══════════════════════════════════════════
                // SÜTUN BAŞLIKLARI
                // ═══════════════════════════════════════════
                int baslikRow = currentRow;

                // Sabit sütunlar
                ws.Cells[baslikRow, 1].Value = "BÖLÜM";
                ws.Cells[baslikRow, 2].Value = "SINIF";
                ws.Cells[baslikRow, 3].Value = "TARİH";
                ws.Cells[baslikRow, 4].Value = "SAAT";
                ws.Cells[baslikRow, 5].Value = "DERSLER";
                ws.Cells[baslikRow, 6].Value = "KONTENJAN";

                // Salon sütunları
                for (int i = 0; i < salonKodlari.Count; i++)
                {
                    int col = 7 + i;
                    ws.Cells[baslikRow, col].Value = salonKodlari[i];

                    // Salon kapasitesini alt satıra yaz
                    var salon = siraliSalonlar[i];
                    ws.Cells[baslikRow + 1, col].Value = $"({salon.Kapasite})";
                    ws.Cells[baslikRow + 1, col].Style.Font.Size = 8;
                    ws.Cells[baslikRow + 1, col].Style.Font.Italic = true;
                    ws.Cells[baslikRow + 1, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[baslikRow + 1, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[baslikRow + 1, col].Style.Fill.BackgroundColor.SetColor(SalonBaslikArkaplan);
                    ws.Cells[baslikRow + 1, col].Style.Font.Color.SetColor(BaslikYazi);
                }

                // Başlık stili
                var baslikRange = ws.Cells[baslikRow, 1, baslikRow, toplamSutun];
                baslikRange.Style.Font.Bold = true;
                baslikRange.Style.Font.Size = 10;
                baslikRange.Style.Font.Color.SetColor(BaslikYazi);
                baslikRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                baslikRange.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
                baslikRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                baslikRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                baslikRange.Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                baslikRange.Style.Border.Bottom.Color.SetColor(BolumBaslik);
                ws.Row(baslikRow).Height = 28;

                // Salon başlık satırı (kapasite)
                var kapasiteRow = baslikRow + 1;
                // Sabit 6 başlık, salon kodu + kapasite satırlarını dikey olarak kaplar;
                // aksi halde başlıkların altında boş koyu hücre kalır ve tablo kaymış görünür.
                for (int c = 1; c <= 6; c++)
                {
                    var dikeyBaslik = ws.Cells[baslikRow, c, kapasiteRow, c];
                    dikeyBaslik.Merge = true;
                    dikeyBaslik.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    dikeyBaslik.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
                    dikeyBaslik.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }

                currentRow = kapasiteRow + 1;

                // ═══════════════════════════════════════════
                // VERİ SATIRLARI — Bölüm + Sınıf + Gün+Saat sıralı
                // ═══════════════════════════════════════════
                var gunAdlari = new Dictionary<DayOfWeek, string>
                {
                    {DayOfWeek.Monday, "Pazartesi"},
                    {DayOfWeek.Tuesday, "Salı"},
                    {DayOfWeek.Wednesday, "Çarşamba"},
                    {DayOfWeek.Thursday, "Perşembe"},
                    {DayOfWeek.Friday, "Cuma"},
                    {DayOfWeek.Saturday, "Cumartesi"},
                    {DayOfWeek.Sunday, "Pazar"}
                };

                // Sonuçları sırala: Bölüm → Sınıf → Gün → Saat → Ders → Şube
                var siraliSonuclar = sonuclar
                    .OrderBy(s => s.BolumAdi)
                    .ThenBy(s => SinifSirasi(s.SinifSeviyesi))
                    .ThenBy(s => s.Gun)
                    .ThenBy(s => SaatYardimci.Anahtar(s.Saat))
                    .ThenBy(s => s.DersAdi)
                    .ThenBy(s => s.Sube)
                    .ToList();

                string oncekiBolum = "";
                int satirIndex = 0;

                foreach (var atama in siraliSonuclar)
                {
                    // Bölüm değiştiğinde ayırıcı satır ekle
                    string mevcutBolum = $"{atama.BolumAdi}|{atama.SinifSeviyesi}";
                    if (mevcutBolum != oncekiBolum && !string.IsNullOrEmpty(oncekiBolum))
                    {
                        // Boş ayırıcı satır
                        var ayiriciRange = ws.Cells[currentRow, 1, currentRow, toplamSutun];
                        ayiriciRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        ayiriciRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(69, 71, 90));
                        ws.Row(currentRow).Height = 5;
                        currentRow++;
                        satirIndex = 0;
                    }
                    oncekiBolum = mevcutBolum;

                    // Satır arka plan rengi (zebra)
                    Color satirRenk = satirIndex % 2 == 0 ? SatirTek : SatirCift;

                    // Sabit sütunları yaz
                    ws.Cells[currentRow, 1].Value = atama.BolumAdi;
                    ws.Cells[currentRow, 2].Value = atama.SinifSeviyesi == 0 ? "T. Seçmeli" : $"{atama.SinifSeviyesi}. Sınıf";
                    ws.Cells[currentRow, 3].Value = $"{gunAdlari[atama.Gun.DayOfWeek]} {atama.Gun:dd.MM.yyyy}";
                    ws.Cells[currentRow, 4].Value = SaatYardimci.ExcelMetni(atama.Saat);
                    ws.Cells[currentRow, 5].Value = atama.DersEtiketi;
                    ws.Cells[currentRow, 6].Value = atama.Kontenjan;

                    // Sabit sütunlar stili
                    for (int c = 1; c <= 6; c++)
                    {
                        ws.Cells[currentRow, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        ws.Cells[currentRow, c].Style.Fill.BackgroundColor.SetColor(satirRenk);
                        ws.Cells[currentRow, c].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                    ws.Cells[currentRow, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // Salon sütunlarını yaz
                    // Tüm salon sütunlarını önce boş/gri olarak işaretle
                    for (int i = 0; i < salonKodlari.Count; i++)
                    {
                        int col = 7 + i;
                        ws.Cells[currentRow, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        ws.Cells[currentRow, col].Style.Fill.BackgroundColor.SetColor(satirRenk);
                        ws.Cells[currentRow, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    // Atanan salonlara öğrenci sayısını yaz
                    if (atama.AtananSalonlar != null)
                    {
                        foreach (var salonAtama in atama.AtananSalonlar)
                        {
                            if (salonIndeks.TryGetValue((salonAtama.SalonKodu ?? "").Trim(), out int salonIdx))
                            {
                                int col = 7 + salonIdx;
                                ws.Cells[currentRow, col].Value = salonAtama.AtananOgrenciSayisi;
                                ws.Cells[currentRow, col].Style.Font.Bold = true;
                                ws.Cells[currentRow, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                ws.Cells[currentRow, col].Style.Fill.BackgroundColor.SetColor(DoluHucre);
                                ws.Cells[currentRow, col].Style.Font.Color.SetColor(Color.FromArgb(30, 30, 46));
                            }
                        }
                    }

                    currentRow++;
                    satirIndex++;
                }

                // ═══════════════════════════════════════════
                // KENARLKLAR VE SON DÜZENLEMELER
                // ═══════════════════════════════════════════

                // Tüm veri alanına ince kenarlık
                int sonRow = currentRow - 1;
                if (sonRow >= baslikRow)
                {
                    var tumVeriBolgesi = ws.Cells[baslikRow, 1, sonRow, toplamSutun];
                    tumVeriBolgesi.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    tumVeriBolgesi.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    tumVeriBolgesi.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    tumVeriBolgesi.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    tumVeriBolgesi.Style.Border.Top.Color.SetColor(KenarlıkRenk);
                    tumVeriBolgesi.Style.Border.Bottom.Color.SetColor(KenarlıkRenk);
                    tumVeriBolgesi.Style.Border.Left.Color.SetColor(KenarlıkRenk);
                    tumVeriBolgesi.Style.Border.Right.Color.SetColor(KenarlıkRenk);
                }

                // ═══════════════════════════════════════════
                // SÜTUN GENİŞLİKLERİ
                // ═══════════════════════════════════════════
                ws.Column(1).Width = 35; // Bölüm
                ws.Column(2).Width = 10; // Sınıf
                ws.Column(3).Width = 22; // Tarih
                ws.Column(4).Width = 8;  // Saat
                ws.Column(5).Width = 30; // Ders
                ws.Column(6).Width = 12; // Kontenjan

                for (int i = 0; i < salonKodlari.Count; i++)
                    ws.Column(7 + i).Width = 9; // Salon sütunları dar

                // ═══════════════════════════════════════════
                // DONDURMA (Freeze Panes)
                // ═══════════════════════════════════════════
                // Başlık satırını ve ilk 6 sütunu dondur
                ws.View.FreezePanes(baslikRow + 2, 7);

                // ═══════════════════════════════════════════
                // ÖZET SAYFASI
                // ═══════════════════════════════════════════
                // NOT: "Salon Doluluk Özeti" sayfası KALDIRILDI (kullanıcı kararı) —
                // yalnızca ihtiyaç duyulan temel sayfalar dışa aktarılır.

                // ═══════════════════════════════════════════
                // SLOT BAZLI SINAV PLANI SAYFASI
                // ═══════════════════════════════════════════
                SlotBazliOzetOlustur(package, sonuclar, siraliSalonlar, programBasligi);

                // ═══════════════════════════════════════════
                // SINAV PROGRAMI FORMU (Resmi Üniversite Formatı)
                // ═══════════════════════════════════════════
                SinavProgramiFormuOlustur(package, sonuclar, programBasligi);

                // ═══════════════════════════════════════════
                // SINAV PROGRAMI FORMU (GÖZETMENLİ)
                // Yukarıdaki resmi form ile AYNI yapıda; farkı: SINIF sütununda her
                // salonun hemen yanında o salona atanan gözetmen adı da yer alır.
                // Bağımsız bir sayfadır — sade form bozulmadan korunur.
                // ═══════════════════════════════════════════
                SinavProgramiFormuGozetmenliOlustur(package, sonuclar, programBasligi);

                // ═══════════════════════════════════════════
                // GÖREV ÇİZELGESİ — kişi bazlı gün/saat/salon (bölüm bölüm)
                // Gözetmen ataması yapılmışsa oluşturulur
                // ═══════════════════════════════════════════
                GorevCizelgesiOlustur(package, sonuclar, gozetmenler, programBasligi);

                // ═══════════════════════════════════════════
                // KAYDET
                // ═══════════════════════════════════════════
                package.SaveAs(new FileInfo(dosyaYolu));
            }
        }

        /// <summary>
        /// İkinci sayfa olarak salon doluluk özeti oluşturur.
        /// </summary>
        private static void OzetSayfasiOlustur(ExcelPackage package, List<SinavAtama> sonuclar,
            List<Salon> siraliSalonlar, string donemAciklama)
        {
            var ws = package.Workbook.Worksheets.Add("Salon Doluluk Özeti");

            ws.Cells[1, 1].Value = $"SALON DOLULUK ÖZETİ — {donemAciklama}";
            var baslik = ws.Cells[1, 1, 1, 5];
            baslik.Merge = true;
            baslik.Style.Font.Bold = true;
            baslik.Style.Font.Size = 14;
            baslik.Style.Font.Color.SetColor(BaslikYazi);
            baslik.Style.Fill.PatternType = ExcelFillStyle.Solid;
            baslik.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
            baslik.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Başlıklar
            int row = 3;
            ws.Cells[row, 1].Value = "Salon Kodu";
            ws.Cells[row, 2].Value = "Kapasite";
            ws.Cells[row, 3].Value = "Toplam Kullanım";
            ws.Cells[row, 4].Value = "Sınav Sayısı";
            ws.Cells[row, 5].Value = "Ort. Doluluk %";

            var headerRange = ws.Cells[row, 1, row, 5];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(SalonBaslikArkaplan);
            headerRange.Style.Font.Color.SetColor(BaslikYazi);

            row++;

            foreach (var salon in siraliSalonlar)
            {
                // Bu salona atanan tüm sınavları bul
                var salonAtamalari = sonuclar
                    .Where(s => s.AtananSalonlar != null && s.AtananSalonlar.Any(sa => sa.SalonKodu == salon.SalonKodu))
                    .ToList();

                int toplamOgrenci = salonAtamalari
                    .SelectMany(s => s.AtananSalonlar)
                    .Where(sa => sa.SalonKodu == salon.SalonKodu)
                    .Sum(sa => sa.AtananOgrenciSayisi);

                int sinavSayisi = salonAtamalari.Count;
                double ortDoluluk = sinavSayisi > 0 ? (double)toplamOgrenci / (sinavSayisi * salon.Kapasite) * 100 : 0;

                ws.Cells[row, 1].Value = salon.SalonKodu;
                ws.Cells[row, 2].Value = salon.Kapasite;
                ws.Cells[row, 3].Value = toplamOgrenci;
                ws.Cells[row, 4].Value = sinavSayisi;
                ws.Cells[row, 5].Value = Math.Round(ortDoluluk, 1);
                ws.Cells[row, 5].Style.Numberformat.Format = "0.0";

                // Doluluk renklendirmesi
                if (ortDoluluk > 80)
                {
                    ws.Cells[row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[row, 5].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(243, 139, 168)); // Kırmızı
                }
                else if (ortDoluluk > 50)
                {
                    ws.Cells[row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[row, 5].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(249, 226, 175)); // Sarı
                }
                else if (sinavSayisi > 0)
                {
                    ws.Cells[row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[row, 5].Style.Fill.BackgroundColor.SetColor(DoluHucre); // Yeşil
                }

                row++;
            }

            // Sütun genişlikleri
            ws.Column(1).Width = 15;
            ws.Column(2).Width = 12;
            ws.Column(3).Width = 16;
            ws.Column(4).Width = 14;
            ws.Column(5).Width = 16;

            // Kenarlıklar
            var dataRange = ws.Cells[3, 1, row - 1, 5];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        }

        /// <summary>
        /// Slot Bazlı Sınav Planı — her tarih+saat için o slottaki TÜM sınavları ve salonları gösterir.
        /// "Pazartesi 09:00'da hangi sınavlar hangi salonlarda?" sorusuna tek tabloda cevap verir.
        /// </summary>
        private static void SlotBazliOzetOlustur(ExcelPackage package, List<SinavAtama> sonuclar,
            List<Salon> siraliSalonlar, string donemAciklama)
        {
            var ws = package.Workbook.Worksheets.Add("Slot Bazlı Sınav Planı");

            var gunAdlari = new Dictionary<DayOfWeek, string>
            {
                {DayOfWeek.Monday, "Pazartesi"},
                {DayOfWeek.Tuesday, "Salı"},
                {DayOfWeek.Wednesday, "Çarşamba"},
                {DayOfWeek.Thursday, "Perşembe"},
                {DayOfWeek.Friday, "Cuma"},
                {DayOfWeek.Saturday, "Cumartesi"},
                {DayOfWeek.Sunday, "Pazar"}
            };

            // Salon kodu → sütun indeksi eşlemesi (sabit 7 sütundan sonra)
            var salonSutunMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < siraliSalonlar.Count; i++)
                salonSutunMap[(siraliSalonlar[i].SalonKodu ?? "").Trim()] = 8 + i;
            int toplamSutun = 7 + siraliSalonlar.Count;

            // Üst başlık
            ws.Cells[1, 1].Value = $"SLOT BAZLI SINAV PLANI — {donemAciklama}";
            var baslik = ws.Cells[1, 1, 1, toplamSutun];
            baslik.Merge = true;
            baslik.Style.Font.Bold = true;
            baslik.Style.Font.Size = 14;
            baslik.Style.Font.Color.SetColor(BaslikYazi);
            baslik.Style.Fill.PatternType = ExcelFillStyle.Solid;
            baslik.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
            baslik.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Row(1).Height = 30;

            // Sütun başlıkları
            int headerRow = 3;
            string[] basliklar = { "TARİH", "GÜN", "SAAT", "BÖLÜM", "DERS (ŞUBE)", "KONTENJAN", "ATANAN SALONLAR" };
            for (int i = 0; i < basliklar.Length; i++)
            {
                ws.Cells[headerRow, i + 1].Value = basliklar[i];
            }

            // Salon sütun başlıkları: "T1-302" + altına kapasite
            foreach (var salon in siraliSalonlar)
            {
                var hucre = ws.Cells[headerRow, salonSutunMap[salon.SalonKodu]];
                hucre.Value = $"{salon.SalonKodu}\n({salon.Kapasite})";
                hucre.Style.WrapText = true;
            }

            var headerRange = ws.Cells[headerRow, 1, headerRow, toplamSutun];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.Size = 10;
            headerRange.Style.Font.Color.SetColor(BaslikYazi);
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Row(headerRow).Height = 34;

            // Slotları grupla: (Gün, Saat) → List<SinavAtama>
            var slotGruplari = sonuclar
                .GroupBy(s => new { s.Gun, s.Saat })
                .OrderBy(g => g.Key.Gun)
                .ThenBy(g => SaatYardimci.Anahtar(g.Key.Saat))
                .ToList();

            int row = headerRow + 1;
            bool slotCift = false;

            // NOT: Eskiden çok haftalı programlarda araya "n. HAFTA" bandı eklenirdi.
            // Kullanıcı kuralı gereği hafta etiketleri Excel çıktılarından kaldırıldı;
            // günler kesintisiz olarak tarih sırasında listelenir.

            foreach (var slotGrup in slotGruplari)
            {
                var gun = slotGrup.Key.Gun;
                var saat = slotGrup.Key.Saat;
                string tarihStr = gun.ToString("dd.MM.yyyy");
                string gunAdi = gunAdlari.ContainsKey(gun.DayOfWeek) ? gunAdlari[gun.DayOfWeek] : gun.DayOfWeek.ToString();

                // Slot ayırıcı satır (koyu renk)
                var ayiriciRange = ws.Cells[row, 1, row, basliklar.Length];
                ayiriciRange.Merge = true;
                ayiriciRange.Value = $"📅 {gunAdi} {tarihStr} — Saat: {SaatYardimci.ExcelMetni(saat)}";
                ayiriciRange.Style.Font.Bold = true;
                ayiriciRange.Style.Font.Size = 11;
                ayiriciRange.Style.Font.Color.SetColor(BaslikYazi);
                ayiriciRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                ayiriciRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(69, 71, 90));
                ayiriciRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                // Birleştirme 1–7'de kalır (dondurma bölmesi), ama bant rengi salon sütunlarına da uzanır
                if (toplamSutun > basliklar.Length)
                {
                    var bantSalon = ws.Cells[row, basliklar.Length + 1, row, toplamSutun];
                    bantSalon.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    bantSalon.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(69, 71, 90));
                }
                ws.Row(row).Height = 24;
                row++;

                // O slottaki sınavlar
                var slotSinavlari = slotGrup
                    .OrderBy(s => s.BolumAdi)
                    .ThenBy(s => s.DersAdi)
                    .ThenBy(s => s.Sube)
                    .ToList();

                int sinavIdx = 0;
                foreach (var sinav in slotSinavlari)
                {
                    Color satirRenk = sinavIdx % 2 == 0 ? SatirTek : SatirCift;

                    ws.Cells[row, 1].Value = tarihStr;
                    ws.Cells[row, 2].Value = gunAdi;
                    ws.Cells[row, 3].Value = SaatYardimci.ExcelMetni(saat);
                    ws.Cells[row, 4].Value = sinav.BolumAdi;
                    ws.Cells[row, 5].Value = sinav.DersEtiketi;
                    ws.Cells[row, 6].Value = sinav.Kontenjan;

                    // Atanan salonlar — SADE: "T1-211(30), T1-212(27)".
                    // Akademisyen isimleri bu sütunda TEKRARLANMAZ; isimler yalnızca
                    // sağdaki salon kutucuklarının içinde gösterilir.
                    string salonlarStr = "";
                    if (sinav.AtananSalonlar != null && sinav.AtananSalonlar.Count > 0)
                    {
                        salonlarStr = string.Join(", ", GunSalonMatrisExportHelper
                            .T4OnceSirala(sinav.AtananSalonlar, sa => sa.SalonKodu)
                            .Select(sa => $"{sa.SalonKodu}({sa.AtananOgrenciSayisi})"));
                    }
                    ws.Cells[row, 7].Value = salonlarStr;

                    // Stil (salon sütunları dahil tüm satır)
                    for (int c = 1; c <= toplamSutun; c++)
                    {
                        ws.Cells[row, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(satirRenk);
                        ws.Cells[row, c].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                    ws.Cells[row, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // Bu sınavın atandığı salon hücrelerini doldur (yeşil)
                    // Gözetmen atanmışsa öğrenci sayısının altına gözetmen adı yazılır
                    if (sinav.AtananSalonlar != null)
                    {
                        bool gozetmenVar = false;
                        foreach (var sa in sinav.AtananSalonlar)
                        {
                            if (!salonSutunMap.TryGetValue((sa.SalonKodu ?? "").Trim(), out int sc)) continue;
                            var salonHucre = ws.Cells[row, sc];

                            if (!string.IsNullOrWhiteSpace(sa.GozetmenAdSoyad))
                            {
                                salonHucre.Value = $"{sa.AtananOgrenciSayisi}\n{sa.GozetmenAdSoyad}";
                                salonHucre.Style.WrapText = true;
                                salonHucre.Style.Font.Size = 8;
                                gozetmenVar = true;
                            }
                            else
                            {
                                salonHucre.Value = sa.AtananOgrenciSayisi;
                            }

                            salonHucre.Style.Fill.BackgroundColor.SetColor(DoluHucre);
                            salonHucre.Style.Font.Bold = true;
                            salonHucre.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        }

                        if (gozetmenVar)
                            ws.Row(row).Height = 30;
                    }

                    row++;
                    sinavIdx++;
                }

                slotCift = !slotCift;
            }

            // Sütun genişlikleri
            ws.Column(1).Width = 14; // Tarih
            ws.Column(2).Width = 12; // Gün
            ws.Column(3).Width = 8;  // Saat
            ws.Column(4).Width = 38; // Bölüm
            ws.Column(5).Width = 30; // Ders
            ws.Column(6).Width = 12; // Kontenjan

            // ATANAN SALONLAR — içerik kırpılmasın: en uzun listeye göre genişlik,
            // gerekirse metin sarmalanır ve satır yüksekliği içeriğe göre ayarlanır.
            int enUzunSalonMetni = 20;
            for (int rr = headerRow + 1; rr < row; rr++)
            {
                string metin = ws.Cells[rr, 7].Value?.ToString() ?? "";
                if (metin.Length > enUzunSalonMetni) enUzunSalonMetni = metin.Length;
            }
            // Tek satıra sığdır (üst sınır 120); daha uzunsa sarmalayarak göster
            int salonKolonGenislik = Math.Min(enUzunSalonMetni + 4, 120);
            ws.Column(7).Width = salonKolonGenislik;
            for (int rr = headerRow + 1; rr < row; rr++)
            {
                var hucre = ws.Cells[rr, 7];
                string metin = hucre.Value?.ToString() ?? "";
                hucre.Style.WrapText = metin.Length > salonKolonGenislik;
                hucre.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                if (metin.Length > salonKolonGenislik)
                {
                    int satirSayisi = (int)Math.Ceiling(metin.Length / (double)salonKolonGenislik);
                    ws.Row(rr).Height = Math.Max(ws.Row(rr).Height, 15 * satirSayisi);
                }
            }

            for (int i = 0; i < siraliSalonlar.Count; i++)
                ws.Column(8 + i).Width = 10; // Salon sütunları

            // Kenarlıklar
            if (row > headerRow + 1)
            {
                var tumData = ws.Cells[headerRow, 1, row - 1, toplamSutun];
                tumData.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                tumData.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                tumData.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                tumData.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                tumData.Style.Border.Top.Color.SetColor(KenarlıkRenk);
                tumData.Style.Border.Bottom.Color.SetColor(KenarlıkRenk);
                tumData.Style.Border.Left.Color.SetColor(KenarlıkRenk);
                tumData.Style.Border.Right.Color.SetColor(KenarlıkRenk);
            }

            // Dondurma: başlık satırı + ilk 3 sütun (Tarih/Gün/Saat) sabit kalsın,
            // sağa kaydırınca salon sütunları görülebilsin
            ws.View.FreezePanes(headerRow + 1, 4);
        }

        // ═══════════════════════════════════════════════════════════════════
        // SINAV PROGRAMI FORMU — Resmi Üniversite Formatı
        // Her Bölüm + Sınıf için ayrı başlık bloğu
        // Sütunlar: TARİH | SAAT | DERSİN ADI | SINIF (salon) | ÖĞRETİM ELEMANI
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Sınav Programı Formu başlık bloğundaki üç satırın ortak puntosu.</summary>
        private const float FormBaslikPuntosu = 11f;

        /// <summary>
        /// Sınav Programı Formu başlık bloğu — sade ve gözetmenli formda ortak:
        ///   TEKNOLOJİ FAKÜLTESİ
        ///   BİLGİSAYAR MÜHENDİSLİĞİ 1. SINIF      (bölümün tam adı, kısaltma değil)
        ///   2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI
        /// Üç satır da aynı puntoda, kalın ve ortalı yazılır. Bir sonraki boş satırı döner.
        /// </summary>
        private static int FormBaslikBlokuYaz(ExcelWorksheet ws, int satir, int toplamSutun,
                                              string bolumAdi, int sinifSeviyesi, string donemSatiri)
        {
            var satirlar = new (string Metin, double Yukseklik)[]
            {
                ("TEKNOLOJİ FAKÜLTESİ", 22),
                ($"{BolumYardimci.TamAd(bolumAdi)} {SinifBasligi(sinifSeviyesi)}", 20),
                (donemSatiri, 20),
            };

            foreach (var (metin, yukseklik) in satirlar)
            {
                ws.Cells[satir, 1].Value = metin;
                var aralik = ws.Cells[satir, 1, satir, toplamSutun];
                aralik.Merge = true;
                aralik.Style.Font.Bold = true;
                aralik.Style.Font.Size = FormBaslikPuntosu;
                aralik.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                aralik.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                ws.Row(satir).Height = yukseklik;
                satir++;
            }
            return satir;
        }

        private static void SinavProgramiFormuOlustur(ExcelPackage package, List<SinavAtama> sonuclar, string programBasligi)
        {
            var ws = package.Workbook.Worksheets.Add("Sınav Programı Formu");

            // Sayfa ayarları
            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.View.ShowGridLines = false;

            // Renk sabitleri
            var baslikKoyu = Color.FromArgb(30, 30, 46);
            var baslikAcik = Color.FromArgb(205, 214, 244);
            var tabloBaslik = Color.FromArgb(49, 50, 68);
            var kenarlık = Color.Black;

            int toplamSutun = 5; // TARİH, SAAT, DERSİN ADI, SINIF, ÖĞRETİM ELEMANI

            // NOT: Hafta ("n. HAFTA") bantları kullanıcı kuralı gereği kaldırıldı.

            // Bölüm + Sınıf bazında grupla ve sırala
            var bolumSinifGruplari = sonuclar
                .GroupBy(s => new { s.BolumAdi, s.SinifSeviyesi })
                .OrderBy(g => g.Key.BolumAdi)
                .ThenBy(g => SinifSirasi(g.Key.SinifSeviyesi))
                .ToList();

            int currentRow = 1;

            foreach (var grup in bolumSinifGruplari)
            {
                string bolumAdi = grup.Key.BolumAdi;
                int sinifSeviyesi = grup.Key.SinifSeviyesi;

                // ─── BAŞLIK BLOĞU (fakülte / bölüm + sınıf / dönem) ───
                currentRow = FormBaslikBlokuYaz(ws, currentRow, toplamSutun, bolumAdi, sinifSeviyesi, programBasligi);

                // ─── TABLO BAŞLIKLARI ───
                int baslikRow = currentRow;
                string[] basliklar = { "TARİH", "SAAT", "DERSİN ADI", "SINIF", "ÖĞRETİM ELEMANI" };
                for (int i = 0; i < basliklar.Length; i++)
                {
                    ws.Cells[baslikRow, i + 1].Value = basliklar[i];
                }

                var headerRange = ws.Cells[baslikRow, 1, baslikRow, toplamSutun];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.Size = 10;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(tabloBaslik);
                headerRange.Style.Font.Color.SetColor(baslikAcik);
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Top.Color.SetColor(kenarlık);
                headerRange.Style.Border.Bottom.Color.SetColor(kenarlık);
                headerRange.Style.Border.Left.Color.SetColor(kenarlık);
                headerRange.Style.Border.Right.Color.SetColor(kenarlık);
                ws.Row(baslikRow).Height = 24;
                currentRow++;

                // ─── VERİ SATIRLARI ───
                var dersler = grup
                    .OrderBy(s => s.Gun)
                    .ThenBy(s => SaatYardimci.Anahtar(s.Saat))
                    .ThenBy(s => s.DersAdi)
                    .ThenBy(s => s.Sube)
                    .ToList();

                foreach (var sinav in dersler)
                {
                    // TARİH
                    ws.Cells[currentRow, 1].Value = sinav.Gun.ToString("dd.MM.yyyy");

                    // SAAT
                    ws.Cells[currentRow, 2].Value = SaatYardimci.ExcelMetni(sinav.Saat);

                    // DERSİN ADI (şubeleri ayrı göster: "FİZİK II (A)", "FİZİK II (B)")
                    string dersAdi = sinav.DersEtiketi;
                    ws.Cells[currentRow, 3].Value = dersAdi;

                    // SINIF — SADE GÖRÜNÜM: yalnızca salon kodları (kompakt format).
                    // Salon/sınıf başına gözetmen-hoca eşleştirme detayı bu tabloda GÖSTERİLMEZ.
                    string salonKompakt = SalonlariKompaktYaz(sinav.AtananSalonlar);
                    ws.Cells[currentRow, 4].Value = salonKompakt;

                    // ÖĞRETİM ELEMANI — dersin GENEL SORUMLU hocası (sınav listesinden bağlanır)
                    ws.Cells[currentRow, 5].Value = sinav.OgretimElemani;

                    // ── FONT VE SIĞDIRMA: yazılar kutucuklara uyumlu ──
                    // Tarih/Saat kısa alanlar → 9pt; uzun metin sütunları sarmalı 8.5pt
                    ws.Cells[currentRow, 1].Style.Font.Size = 9;
                    ws.Cells[currentRow, 2].Style.Font.Size = 9;
                    ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    ws.Cells[currentRow, 3].Style.Font.Size = 9;
                    ws.Cells[currentRow, 3].Style.WrapText = true;

                    ws.Cells[currentRow, 4].Style.Font.Size = 8.5f;
                    ws.Cells[currentRow, 4].Style.WrapText = true;

                    ws.Cells[currentRow, 5].Style.Font.Size = 9;
                    ws.Cells[currentRow, 5].Style.WrapText = true;

                    // Kenarlıklar
                    for (int c = 1; c <= toplamSutun; c++)
                    {
                        ws.Cells[currentRow, c].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Top.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.Border.Bottom.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.Border.Left.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.Border.Right.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }

                    // ── DİNAMİK SATIR YÜKSEKLİĞİ: sarmalanan metin kadar yer aç ──
                    // Sütun genişliğine göre yaklaşık satır sayısı hesaplanır (taşma/kırpılma önlenir)
                    int dersSatir = TahminiSatirSayisi(dersAdi, 42, 9f);
                    int salonSatir = TahminiSatirSayisi(salonKompakt, 35, 8.5f);
                    int hocaSatir = TahminiSatirSayisi(sinav.OgretimElemani, 35, 9f);
                    int maxSatir = Math.Max(1, Math.Max(dersSatir, Math.Max(salonSatir, hocaSatir)));
                    ws.Row(currentRow).Height = Math.Max(20, maxSatir * 13 + 6);

                    currentRow++;
                }

                // Bölümler arası boşluk
                currentRow += 2;
            }

            // Sütun genişlikleri
            ws.Column(1).Width = 14;  // TARİH
            ws.Column(2).Width = 8;   // SAAT
            ws.Column(3).Width = 42;  // DERSİN ADI
            ws.Column(4).Width = 35;  // SINIF (salonlar)
            ws.Column(5).Width = 35;  // ÖĞRETİM ELEMANI
        }

        // ═══════════════════════════════════════════════════════════════════
        // SINAV PROGRAMI FORMU — GÖZETMENLİ
        // "Sınav Programı Formu" ile aynı resmi düzen; tek farkı tablonun SAĞ ucunda
        // ayrı bir GÖZETMENLER sütunu bulunması.
        //
        // Sütunlar: TARİH | SAAT | DERSİN ADI | SINIF | ÖĞRETİM ELEMANI | GÖZETMENLER
        //
        // NOT (YAPISAL DEĞİŞİKLİK): Gözetmen adları ARTIK salon kodunun yanındaki
        // hücreye ("T4-207 (Ahmet Yılmaz)") yazılmaz. SINIF sütunu sade formdaki gibi
        // yalnızca salon kodlarını taşır; gözetmen adları en sağdaki GÖZETMENLER
        // sütununda "T4-207: Ahmet Yılmaz" biçiminde listelenir.
        //
        // Gözetmen ataması hiç yapılmamışsa sayfa OLUŞTURULMAZ (boş sekme üretilmez).
        // ═══════════════════════════════════════════════════════════════════

        private static void SinavProgramiFormuGozetmenliOlustur(ExcelPackage package, List<SinavAtama> sonuclar, string programBasligi)
        {
            // Hiçbir salona gözetmen atanmamışsa bu sayfaya gerek yok
            bool gozetmenVarMi = sonuclar.Any(s => s.AtananSalonlar != null &&
                s.AtananSalonlar.Any(sa => !string.IsNullOrWhiteSpace(sa.GozetmenAdSoyad)));
            if (!gozetmenVarMi) return;

            var ws = package.Workbook.Worksheets.Add("Sınav Programı Formu (Gözetmenli)");

            // Sayfa ayarları — sade form ile birebir
            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.View.ShowGridLines = false;

            var baslikAcik = Color.FromArgb(205, 214, 244);
            var tabloBaslik = Color.FromArgb(49, 50, 68);
            var kenarlık = Color.Black;

            // TARİH, SAAT, DERSİN ADI, SINIF, ÖĞRETİM ELEMANI, GÖZETMENLER
            // (GÖZETMENLER tablonun SAĞINA eklenen yeni slot — başlık ve merge'ler
            //  6 sütuna göre kurulur ki tablo yapısı kaymasın.)
            const int GozetmenSutunu = 6;
            int toplamSutun = GozetmenSutunu;

            // NOT: Hafta ("n. HAFTA") bantları kullanıcı kuralı gereği kaldırıldı.

            var bolumSinifGruplari = sonuclar
                .GroupBy(s => new { s.BolumAdi, s.SinifSeviyesi })
                .OrderBy(g => g.Key.BolumAdi)
                .ThenBy(g => SinifSirasi(g.Key.SinifSeviyesi))
                .ToList();

            int currentRow = 1;

            foreach (var grup in bolumSinifGruplari)
            {
                string bolumAdi = grup.Key.BolumAdi;
                int sinifSeviyesi = grup.Key.SinifSeviyesi;

                // ─── BAŞLIK BLOĞU (sade formla birebir aynı — "(GÖZETMENLİ)" eki yok) ───
                currentRow = FormBaslikBlokuYaz(ws, currentRow, toplamSutun, bolumAdi, sinifSeviyesi, programBasligi);

                // ─── TABLO BAŞLIKLARI ───
                int baslikRow = currentRow;
                string[] basliklar = { "TARİH", "SAAT", "DERSİN ADI", "SINIF", "ÖĞRETİM ELEMANI", "GÖZETMENLER" };
                for (int i = 0; i < basliklar.Length; i++)
                    ws.Cells[baslikRow, i + 1].Value = basliklar[i];

                var headerRange = ws.Cells[baslikRow, 1, baslikRow, toplamSutun];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.Size = 10;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(tabloBaslik);
                headerRange.Style.Font.Color.SetColor(baslikAcik);
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                headerRange.Style.WrapText = true;
                headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Top.Color.SetColor(kenarlık);
                headerRange.Style.Border.Bottom.Color.SetColor(kenarlık);
                headerRange.Style.Border.Left.Color.SetColor(kenarlık);
                headerRange.Style.Border.Right.Color.SetColor(kenarlık);
                ws.Row(baslikRow).Height = 26;
                currentRow++;

                // ─── VERİ SATIRLARI ───
                var dersler = grup
                    .OrderBy(s => s.Gun)
                    .ThenBy(s => SaatYardimci.Anahtar(s.Saat))
                    .ThenBy(s => s.DersAdi)
                    .ThenBy(s => s.Sube)
                    .ToList();

                foreach (var sinav in dersler)
                {
                    ws.Cells[currentRow, 1].Value = sinav.Gun.ToString("dd.MM.yyyy");
                    ws.Cells[currentRow, 2].Value = SaatYardimci.ExcelMetni(sinav.Saat);

                    // DERSİN ADI — şube parantez içinde: "FİZİK II (A)"
                    string dersAdi = sinav.DersEtiketi;
                    ws.Cells[currentRow, 3].Value = dersAdi;

                    // ── SINIF: sade formdaki gibi YALNIZCA salon kodları ──
                    string salonlar = SalonlariKompaktYaz(sinav.AtananSalonlar);
                    ws.Cells[currentRow, 4].Value = salonlar;

                    ws.Cells[currentRow, 5].Value = sinav.OgretimElemani;

                    // ── YENİ SLOT: GÖZETMENLER (tablonun sağ ucu) ──
                    // "T4-207: Ahmet Yılmaz, T4-210: Ayşe Kaya"
                    string gozetmenler = GozetmenleriYaz(sinav.AtananSalonlar);
                    ws.Cells[currentRow, GozetmenSutunu].Value = gozetmenler;

                    // Font ve sığdırma
                    ws.Cells[currentRow, 1].Style.Font.Size = 9;
                    ws.Cells[currentRow, 2].Style.Font.Size = 9;
                    ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    ws.Cells[currentRow, 3].Style.Font.Size = 9;
                    ws.Cells[currentRow, 3].Style.WrapText = true;

                    ws.Cells[currentRow, 4].Style.Font.Size = 9;
                    ws.Cells[currentRow, 4].Style.WrapText = true;

                    ws.Cells[currentRow, 5].Style.Font.Size = 9;
                    ws.Cells[currentRow, 5].Style.WrapText = true;

                    ws.Cells[currentRow, GozetmenSutunu].Style.Font.Size = 8.5f;
                    ws.Cells[currentRow, GozetmenSutunu].Style.WrapText = true;

                    for (int c = 1; c <= toplamSutun; c++)
                    {
                        ws.Cells[currentRow, c].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        ws.Cells[currentRow, c].Style.Border.Top.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.Border.Bottom.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.Border.Left.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.Border.Right.Color.SetColor(kenarlık);
                        ws.Cells[currentRow, c].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }

                    // Dinamik satır yüksekliği — artık GÖZETMENLER sütunu da hesaba katılır
                    int dersSatir = TahminiSatirSayisi(dersAdi, 42, 9f);
                    int salonSatir = TahminiSatirSayisi(salonlar, 35, 9f);
                    int hocaSatir = TahminiSatirSayisi(sinav.OgretimElemani, 35, 9f);
                    int gozetmenSatir = TahminiSatirSayisi(gozetmenler, 45, 8.5f);
                    int maxSatir = Math.Max(1,
                        Math.Max(Math.Max(dersSatir, salonSatir),
                                 Math.Max(hocaSatir, gozetmenSatir)));
                    ws.Row(currentRow).Height = Math.Max(20, maxSatir * 13 + 6);

                    currentRow++;
                }

                currentRow += 2;
            }

            // Sütun genişlikleri — SINIF sade formdaki gibi, GÖZETMENLER sağda geniş slot
            ws.Column(1).Width = 14;  // TARİH
            ws.Column(2).Width = 8;   // SAAT
            ws.Column(3).Width = 42;  // DERSİN ADI
            ws.Column(4).Width = 35;  // SINIF
            ws.Column(5).Width = 35;  // ÖĞRETİM ELEMANI
            ws.Column(GozetmenSutunu).Width = 45;  // GÖZETMENLER
        }

        /// <summary>
        /// "Sınav Programı Formu (Gözetmenli)" sayfasının GÖZETMENLER sütunu içeriğini üretir.
        /// Biçim: "T4-207: Ahmet Yılmaz, T4-210: Ayşe Kaya".
        /// Aynı gözetmen birden çok salonda görünüyorsa (havuz/tampon paylaşımı) tekrar yazılmaz.
        /// Hiç gözetmen atanmamışsa boş dize döner.
        /// </summary>
        private static string GozetmenleriYaz(List<SalonAtama> salonAtamalari)
        {
            if (salonAtamalari == null || salonAtamalari.Count == 0)
                return "";

            var parcalar = new List<string>();
            var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sa in GunSalonMatrisExportHelper.T4OnceSirala(salonAtamalari, s => s.SalonKodu))
            {
                if (string.IsNullOrWhiteSpace(sa.GozetmenAdSoyad)) continue;

                string anahtar = $"{sa.SalonKodu}|{sa.GozetmenAdSoyad}";
                if (!gorulen.Add(anahtar)) continue;

                parcalar.Add(string.IsNullOrWhiteSpace(sa.SalonKodu)
                    ? sa.GozetmenAdSoyad.Trim()
                    : $"{sa.SalonKodu}: {sa.GozetmenAdSoyad.Trim()}");
            }

            return string.Join(", ", parcalar);
        }

        // ═══════════════════════════════════════════════════════════════════
        // GÖREV ÇİZELGESİ — Öğretim Elemanı (Gözetmen) Bazlı
        // Her bölüm ayrı blok; blok içinde kişi bazında gün | saat | salon | ders satırları.
        // "X hocası hangi gün, hangi saatte, hangi salonda?" sorusuna cevap verir.
        // ═══════════════════════════════════════════════════════════════════

        private static void GorevCizelgesiOlustur(ExcelPackage package, List<SinavAtama> sonuclar,
            List<Gozetmen> gozetmenler, string programBasligi)
        {
            // ── Görev kayıtlarını topla: salon atamalarındaki gözetmen adları üzerinden ──
            var hamGorevler = new List<(string Kisi, DateTime Gun, string Saat, string Salon, string Ders)>();
            foreach (var atama in sonuclar)
            {
                if (atama.AtananSalonlar == null) continue;
                foreach (var sa in atama.AtananSalonlar)
                {
                    if (string.IsNullOrWhiteSpace(sa.GozetmenAdSoyad)) continue;
                    hamGorevler.Add((sa.GozetmenAdSoyad.Trim(), atama.Gun, atama.Saat,
                        sa.SalonKodu, atama.DersEtiketi));
                }
            }

            // Gözetmen ataması hiç yapılmamışsa sayfa oluşturulmaz
            if (hamGorevler.Count == 0) return;

            // Aynı kişi + slot + salonda birden çok şube/ders olabilir (ortak salon) → dersleri birleştir
            var gorevler = hamGorevler
                .GroupBy(x => new { x.Kisi, x.Gun, x.Saat, x.Salon })
                .Select(g => new
                {
                    g.Key.Kisi,
                    g.Key.Gun,
                    g.Key.Saat,
                    g.Key.Salon,
                    Ders = string.Join(", ", g.Select(x => x.Ders).Distinct())
                })
                .ToList();

            // Kişi → bölüm eşleştirmesi (gözetmen listesinden; bulunamazsa DİĞER)
            string KisiBolumu(string adSoyad)
            {
                var g = gozetmenler?.FirstOrDefault(x =>
                    string.Equals(x.AdSoyad?.Trim(), adSoyad, StringComparison.OrdinalIgnoreCase));
                return string.IsNullOrWhiteSpace(g?.Bolum) ? "DİĞER" : g.Bolum.Trim();
            }

            var gunAdlari = new Dictionary<DayOfWeek, string>
            {
                {DayOfWeek.Monday, "Pazartesi"}, {DayOfWeek.Tuesday, "Salı"}, {DayOfWeek.Wednesday, "Çarşamba"},
                {DayOfWeek.Thursday, "Perşembe"}, {DayOfWeek.Friday, "Cuma"}, {DayOfWeek.Saturday, "Cumartesi"}, {DayOfWeek.Sunday, "Pazar"}
            };

            var ws = package.Workbook.Worksheets.Add("Görev Çizelgesi");
            ws.View.ShowGridLines = false;
            ws.PrinterSettings.Orientation = eOrientation.Portrait;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;

            int toplamSutun = 6; // Öğretim Elemanı | Tarih | Gün | Saat | Salon | Ders

            // ── Üst başlık ──
            ws.Cells[1, 1].Value = $"ÖĞRETİM ELEMANI GÖREV ÇİZELGESİ (GÖZETMENLİK) — {programBasligi}";
            var ustBaslik = ws.Cells[1, 1, 1, toplamSutun];
            ustBaslik.Merge = true;
            ustBaslik.Style.Font.Bold = true;
            ustBaslik.Style.Font.Size = 14;
            ustBaslik.Style.Font.Color.SetColor(BaslikYazi);
            ustBaslik.Style.Fill.PatternType = ExcelFillStyle.Solid;
            ustBaslik.Style.Fill.BackgroundColor.SetColor(BaslikArkaplan);
            ustBaslik.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ustBaslik.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Row(1).Height = 30;

            int currentRow = 3;

            // ── Bölüm bölüm bloklar ──
            var bolumGruplari = gorevler
                .GroupBy(x => KisiBolumu(x.Kisi))
                .OrderBy(g => g.Key == "DİĞER" ? 1 : 0) // DİĞER en sona
                .ThenBy(g => g.Key)
                .ToList();

            foreach (var bolumGrubu in bolumGruplari)
            {
                // Bölüm başlığı (mavi bant)
                ws.Cells[currentRow, 1].Value = $"■ {bolumGrubu.Key}";
                var bolumBant = ws.Cells[currentRow, 1, currentRow, toplamSutun];
                bolumBant.Merge = true;
                bolumBant.Style.Font.Bold = true;
                bolumBant.Style.Font.Size = 11;
                bolumBant.Style.Font.Color.SetColor(Color.FromArgb(30, 30, 46));
                bolumBant.Style.Fill.PatternType = ExcelFillStyle.Solid;
                bolumBant.Style.Fill.BackgroundColor.SetColor(BolumBaslik);
                bolumBant.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                bolumBant.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                ws.Row(currentRow).Height = 22;
                currentRow++;

                // Tablo başlıkları
                int baslikRow = currentRow;
                string[] basliklar = { "ÖĞRETİM ELEMANI", "TARİH", "GÜN", "SAAT", "SALON", "DERS (ŞUBE)" };
                for (int i = 0; i < basliklar.Length; i++)
                    ws.Cells[baslikRow, i + 1].Value = basliklar[i];

                var headerRange = ws.Cells[baslikRow, 1, baslikRow, toplamSutun];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.Size = 9;
                headerRange.Style.Font.Color.SetColor(BaslikYazi);
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(SalonBaslikArkaplan);
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                ws.Row(baslikRow).Height = 20;
                currentRow++;

                // Kişi bazlı satırlar (ad soyad sıralı; kişi içinde gün+saat sıralı)
                var kisiGruplari = bolumGrubu
                    .GroupBy(x => x.Kisi)
                    .OrderBy(g => g.Key)
                    .ToList();

                int kisiIdx = 0;
                foreach (var kisiGrubu in kisiGruplari)
                {
                    var kisiGorevleri = kisiGrubu
                        .OrderBy(x => x.Gun)
                        .ThenBy(x => SaatYardimci.Anahtar(x.Saat))
                        .ThenBy(x => x.Salon)
                        .ToList();

                    Color satirRenk = kisiIdx % 2 == 0 ? SatirTek : SatirCift;
                    int kisiIlkSatir = currentRow;

                    foreach (var gorev in kisiGorevleri)
                    {
                        ws.Cells[currentRow, 2].Value = gorev.Gun.ToString("dd.MM.yyyy");
                        ws.Cells[currentRow, 3].Value = gunAdlari.ContainsKey(gorev.Gun.DayOfWeek)
                            ? gunAdlari[gorev.Gun.DayOfWeek] : gorev.Gun.DayOfWeek.ToString();
                        ws.Cells[currentRow, 4].Value = SaatYardimci.ExcelMetni(gorev.Saat);
                        ws.Cells[currentRow, 5].Value = gorev.Salon;
                        ws.Cells[currentRow, 6].Value = gorev.Ders;

                        for (int c = 1; c <= toplamSutun; c++)
                        {
                            var hucre = ws.Cells[currentRow, c];
                            hucre.Style.Font.Size = 9;
                            hucre.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            hucre.Style.Fill.BackgroundColor.SetColor(satirRenk);
                            hucre.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        }
                        ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        ws.Cells[currentRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        ws.Cells[currentRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        ws.Cells[currentRow, 5].Style.Font.Bold = true;
                        ws.Cells[currentRow, 6].Style.WrapText = true;
                        ws.Row(currentRow).Height = Math.Max(18, TahminiSatirSayisi(gorev.Ders, 40, 9f) * 13 + 5);

                        currentRow++;
                    }

                    // Kişi adı: görev satırları boyunca birleştirilmiş tek hücre
                    var kisiHucre = ws.Cells[kisiIlkSatir, 1, currentRow - 1, 1];
                    kisiHucre.Merge = true;
                    kisiHucre.Value = $"{kisiGrubu.Key} ({kisiGorevleri.Count})";
                    kisiHucre.Style.Font.Bold = true;
                    kisiHucre.Style.Font.Size = 9;
                    kisiHucre.Style.WrapText = true;
                    kisiHucre.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    kisiHucre.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                    kisiIdx++;
                }

                // Bölüm bloğu kenarlıkları
                var blokRange = ws.Cells[baslikRow, 1, currentRow - 1, toplamSutun];
                blokRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                blokRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                blokRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                blokRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                blokRange.Style.Border.Top.Color.SetColor(KenarlıkRenk);
                blokRange.Style.Border.Bottom.Color.SetColor(KenarlıkRenk);
                blokRange.Style.Border.Left.Color.SetColor(KenarlıkRenk);
                blokRange.Style.Border.Right.Color.SetColor(KenarlıkRenk);

                // Bölümler arası boşluk
                currentRow += 2;
            }

            // Sütun genişlikleri
            ws.Column(1).Width = 28; // Öğretim Elemanı
            ws.Column(2).Width = 12; // Tarih
            ws.Column(3).Width = 11; // Gün
            ws.Column(4).Width = 8;  // Saat
            ws.Column(5).Width = 12; // Salon
            ws.Column(6).Width = 40; // Ders
        }

        /// <summary>
        /// Sarmalanan (WrapText) bir hücrenin kaç satıra böleceğini yaklaşık hesaplar.
        /// Sütun genişliği "karakter" cinsindendir; font küçüldükçe satıra sığan karakter artar.
        /// </summary>
        /// <summary>
        /// Sınıf seviyesi ders kodunun ilk rakamından gelir; kodu 0 ile başlayan dersler
        /// (BIL0xx vb.) TEKNİK SEÇMELİ derslerdir — çıktıda "0. SINIF" yerine böyle yazılır.
        /// Hesaplamadaki 0 değeri DEĞİŞMEZ (algoritma çakışma kontrolünde kullanır).
        /// </summary>
        private static string SinifBasligi(int sinif)
            => sinif == 0 ? "TEKNİK SEÇMELİ DERSLER" : $"{sinif}. SINIF";

        /// <summary>Seçmeli grubu (0) her bölümün sınıf blokları SONUNA sıralanır.</summary>
        private static int SinifSirasi(int sinif) => sinif == 0 ? int.MaxValue : sinif;

        private static int TahminiSatirSayisi(string metin, double sutunGenisligi, float fontBoyutu)
        {
            if (string.IsNullOrEmpty(metin)) return 1;
            // Excel sütun genişliği ~11pt Calibri karakteri baz alır; küçük fontta daha çok karakter sığar
            double sigacakKarakter = Math.Max(4, sutunGenisligi * (11.0 / fontBoyutu) * 0.95);
            return Math.Max(1, (int)Math.Ceiling(metin.Length / sigacakKarakter));
        }

        /// <summary>
        /// Salon listesini resmi formattaki kompakt notasyona dönüştürür.
        /// Örn: T4-207, T4-210 → "T4-207-210"
        ///      T1-107, T1-201, T1-202, T1-204 → "T1-107-201-202-204"
        ///      Farklı bloklar varsa virgülle ayrılır: "T4-207-210, T1-107"
        /// </summary>
        // NOT: Eski "SalonlariGozetmenliYaz" (salon kodunun yanına gözetmen adı yazan)
        // yardımcı kaldırıldı. Gözetmen adları artık ayrı GÖZETMENLER sütununa
        // GozetmenleriYaz() ile yazılıyor.

        private static string SalonlariKompaktYaz(List<SalonAtama> salonAtamalari)
        {
            if (salonAtamalari == null || salonAtamalari.Count == 0)
                return "";

            // Salonları blok bazında grupla
            var blokGruplari = new Dictionary<string, List<string>>();

            foreach (var sa in salonAtamalari.OrderBy(s => s.SalonKodu))
            {
                string kod = sa.SalonKodu ?? "";
                int tireIdx = kod.IndexOf('-');

                if (tireIdx > 0)
                {
                    string blok = kod.Substring(0, tireIdx); // "T4"
                    string numara = kod.Substring(tireIdx + 1); // "207"

                    if (!blokGruplari.ContainsKey(blok))
                        blokGruplari[blok] = new List<string>();
                    blokGruplari[blok].Add(numara);
                }
                else
                {
                    // Tire yoksa salon kodunu olduğu gibi ekle
                    if (!blokGruplari.ContainsKey(""))
                        blokGruplari[""] = new List<string>();
                    blokGruplari[""].Add(kod);
                }
            }

            // Her bloğu kompakt formatta yaz
            var parcalar = new List<string>();
            foreach (var kvp in GunSalonMatrisExportHelper.T4OnceSirala(blokGruplari, k => k.Key))
            {
                if (string.IsNullOrEmpty(kvp.Key))
                {
                    // Tiresiz salon kodları
                    parcalar.AddRange(kvp.Value);
                }
                else
                {
                    // BlokKodu-Numara1-Numara2-...
                    parcalar.Add(kvp.Key + "-" + string.Join("-", kvp.Value));
                }
            }

            return string.Join(", ", parcalar);
        }
    }
}
