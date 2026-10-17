#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OfficeOpenXml;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// Excel'den ders programı okuma motoru.
    /// EPPlus kütüphanesi ile .xlsx dosyasından ders bilgilerini çeker.
    /// </summary>
    public static class ExcelImportHelper
    {
        private static List<string> _sonHatalar = new List<string>();
        public static List<string> SonImportHatalari => _sonHatalar;

        /// <summary>
        /// Sütun başlıklarını boşluksuz, küçük harfli ve Türkçe karakterleri dönüştürülmüş hale getirir.
        /// </summary>
        private static string NormalizeHeader(string header)
        {
            if (header == null) return "";

            // Hem büyük hem de küçük harfli Türkçe/aksanlı karakterlerin doğrudan temiz ASCII karşılıkları
            var charMapping = new Dictionary<char, char>
            {
                { 'ı', 'i' }, { 'I', 'i' },
                { 'İ', 'i' }, { 'i', 'i' },
                { 'ş', 's' }, { 'Ş', 's' },
                { 'ğ', 'g' }, { 'Ğ', 'g' },
                { 'ç', 'c' }, { 'Ç', 'c' },
                { 'ö', 'o' }, { 'Ö', 'o' },
                { 'ü', 'u' }, { 'Ü', 'u' },
                { 'â', 'a' }, { 'Â', 'a' },
                { 'î', 'i' }, { 'Î', 'i' },
                { 'û', 'u' }, { 'Û', 'u' }
            };

            var sb = new StringBuilder();
            foreach (char c in header)
            {
                if (charMapping.TryGetValue(c, out char normalizedChar))
                    sb.Append(normalizedChar);
                else
                    sb.Append(c);
            }

            // Boşlukları kaldır ve küçük harfe çevir
            return sb.ToString().ToLowerInvariant().Replace(" ", "");
        }

        /// <summary>
        /// Başlık satırından sütun indekslerini akıllı ve esnek olarak bulur.
        /// Bölüm sütunu için çok aşamalı öncelik sistemi ve içerik doğrulaması uygular.
        /// </summary>
        private static Dictionary<string, int> SutunlariBulAkilli(ExcelWorksheet ws, int sutunSayisi)
        {
            var map = new Dictionary<string, int>();
            int satirSayisi = ws.Dimension?.Rows ?? 0;

            // ── BÖLÜM SÜTUNU: Çok aşamalı akıllı tespit ──
            // Kara liste kelimeleri — bu kelimelerden herhangi birini içeren sütun bölüm adı olamaz
            string[] kodKaraListe = { "kod", "kodu", "code", "birimkodu", "bolumkodu", "id", "no" };

            // Öncelik 1: bolumprogram, bolumadi, departmentname gibi kesin eşleşmeler
            // Öncelik 2: program, department, dept (tek başına, kod kelimesi içermeyen)
            // Öncelik 3: bolum (tek başına, kod kelimesi içermeyen)
            var bolumAdaylari = new List<(int col, int oncelik)>();

            for (int col = 1; col <= sutunSayisi; col++)
            {
                string baslikRaw = GetCellText(ws, 1, col);
                string baslik = NormalizeHeader(baslikRaw);
                if (string.IsNullOrEmpty(baslik)) continue;

                // Kara listedeki kelimelerden herhangi birini içeriyorsa bu sütun bölüm adı olamaz
                bool karaListede = false;
                foreach (var kara in kodKaraListe)
                {
                    if (baslik.Contains(kara))
                    {
                        karaListede = true;
                        break;
                    }
                }
                if (karaListede) continue;

                // Öncelik 1: Çok spesifik bileşik ifadeler
                if (baslik.Contains("bolumprogram") || baslik.Contains("bolumadi") ||
                    baslik.Contains("departmentname") || baslik.Contains("programadi"))
                {
                    bolumAdaylari.Add((col, 1));
                }
                // Öncelik 2: program, department, dept (genellikle bölüm adı sütunlarında geçer)
                else if (baslik.Contains("program") || baslik.Contains("department") || baslik.Contains("dept"))
                {
                    bolumAdaylari.Add((col, 2));
                }
                // Öncelik 3: Sadece bolum içeren (ama kod içermeyen — zaten kara listede elendi)
                else if (baslik.Contains("bolum"))
                {
                    bolumAdaylari.Add((col, 3));
                }
            }

            // Adayları öncelik sırasına göre dene, içerik doğrulamasından geçeni seç
            bolumAdaylari.Sort((a, b) => a.oncelik.CompareTo(b.oncelik));
            foreach (var aday in bolumAdaylari)
            {
                if (BolumIcerikGecerliMi(ws, aday.col, satirSayisi))
                {
                    map["bolum"] = aday.col;
                    break;
                }
            }

            // İçerik doğrulamasından hiçbiri geçemediyse, en yüksek öncelikli adayı al (veri olmayabilir)
            if (!map.ContainsKey("bolum") && bolumAdaylari.Count > 0)
            {
                map["bolum"] = bolumAdaylari[0].col;
            }

            // ── DİĞER SÜTUNLAR: Standart esnek eşleştirme ──
            for (int col = 1; col <= sutunSayisi; col++)
            {
                string baslikRaw = GetCellText(ws, 1, col);
                string baslik = NormalizeHeader(baslikRaw);
                if (string.IsNullOrEmpty(baslik)) continue;

                // Bölüm zaten bulundu, tekrar eşleştirme
                if (map.ContainsKey("bolum") && map["bolum"] == col) continue;

                // Öğretim Elemanı (Dersi Veren Hoca / Akademisyen / Öğretmen / Ad Soyad)
                if (!map.ContainsKey("ogretimelemani") &&
                    (baslik.Contains("ogretimelemani") || baslik.Contains("ogretimuyesi") || 
                     baslik.Contains("ogretmen") || baslik.Contains("adsoyad") || 
                     baslik.Contains("adisoyadi") || baslik.Contains("derssorumlusu") || 
                     baslik.Contains("dersiveren") || baslik.Contains("sorumlu") || 
                     baslik.Contains("hoca") || baslik.Contains("instructor") || 
                     baslik.Contains("lecturer") || baslik.Contains("teacher") || 
                     baslik.Contains("ogrt") || baslik.Contains("ogrelemani") || 
                     baslik.Contains("ogr.el") || baslik.Contains("ogr.elemani") ||
                     baslik.Contains("akademisyen") || baslik.Contains("ogretimgorevlisi") ||
                     baslik.Contains("ogretimel")))
                {
                    map["ogretimelemani"] = col;
                }
                // Dersin Kodu: dersinkodu, derskodu, code, derskod, dersinkod
                else if (!map.ContainsKey("dersinkodu") && 
                    (baslik.Contains("dersinkodu") || baslik.Contains("derskodu") || baslik.Contains("code") || baslik.Contains("derskod") || baslik.Contains("dersinkod")))
                {
                    map["dersinkodu"] = col;
                }
                // Ders Adı: ders, course, subject, ad, adi
                else if (!map.ContainsKey("dersadi") && 
                    (baslik.Contains("ders") || baslik.Contains("course") || baslik.Contains("subject") || baslik == "ad" || baslik == "adi"))
                {
                    map["dersadi"] = col;
                }
                // Şube: sube, grup, section, group, branch
                else if (!map.ContainsKey("sube") && 
                    (baslik.Contains("sube") || baslik.Contains("grup") || baslik.Contains("section") || baslik.Contains("group") || baslik.Contains("branch")))
                {
                    map["sube"] = col;
                }
                // Kontenjan: kontenjan, kapasite, ogrenci, capacity, count, kisi, size
                else if (!map.ContainsKey("kontenjan") && 
                    (baslik.Contains("kontenjan") || baslik.Contains("kapasite") || 
                     baslik.Contains("ogrenci") || baslik.Contains("capacity") || 
                     baslik.Contains("count") || baslik.Contains("kisi") || baslik.Contains("size")))
                {
                    map["kontenjan"] = col;
                }
                // Öğretim Türü: ogretimturu, ogretim, ogr, teaching, type
                else if (!map.ContainsKey("ogretimturu") &&
                    (baslik.Contains("ogretim") || baslik.Contains("teaching")))
                {
                    map["ogretimturu"] = col;
                }
            }

            return map;
        }

        /// <summary>
        /// Bölüm sütunu için içerik doğrulaması (Güvenlik Duvarı).
        /// İlk birkaç veri satırını kontrol eder: eğer hücrelerde gerçek metin değil de
        /// yalnızca sayılar, kodlar veya "1/9" gibi kısa ifadeler varsa false döner.
        /// </summary>
        private static bool BolumIcerikGecerliMi(ExcelWorksheet ws, int col, int satirSayisi)
        {
            int kontrolSayisi = Math.Min(5, satirSayisi - 1); // İlk 5 veri satırını kontrol et
            if (kontrolSayisi <= 0) return true; // Veri yoksa başlığa güven

            int gecerliMetinSayisi = 0;

            for (int row = 2; row <= 1 + kontrolSayisi; row++)
            {
                string hucre = GetCellText(ws, row, col);
                if (string.IsNullOrWhiteSpace(hucre)) continue;

                // Sayı, slash, boşluk ve nokta dışında en az bir harf var mı kontrol et
                string temiz = hucre.Replace("/", "").Replace(" ", "").Replace(".", "").Replace(",", "").Replace("-", "");

                // Tamamen sayısal mı? (Örn: "1", "19", "1/9", "2.0")
                bool tamamenSayisal = true;
                foreach (char c in temiz)
                {
                    if (!char.IsDigit(c))
                    {
                        tamamenSayisal = false;
                        break;
                    }
                }

                if (tamamenSayisal && temiz.Length > 0)
                    continue; // Bu hücre sayısal — bölüm adı olamaz

                // Çok kısa metinler de kod olabilir (3 karakter veya daha az: "BM", "EE", vb.)
                if (hucre.Trim().Length <= 3)
                    continue; // Muhtemelen bir kısa kod

                // Gerçek metin içeriyor (Örn: "BİLGİSAYAR MÜHENDİSLİĞİ")
                gecerliMetinSayisi++;
            }

            // Kontrol edilen satırlardan en az 1 tanesinde gerçek metin varsa bu sütun geçerlidir
            return gecerliMetinSayisi > 0;
        }

        private const string rawBolumFlag = "__BILINMEYEN_BOLUM__";

        /// <summary>
        /// Excel'deki bölüm adını sistemimizdeki 6 standart bölüm adından birine eşleştirir.
        /// Fakülte adları, kısaltmalar, farklı yazılışlar otomatik olarak tanınır.
        /// Eşleşme bulunamazsa rawBolumFlag döner (bilinmeyen bölüm — atlanacak).
        /// </summary>
        private static string BolumAdiNormalize(string rawBolum)
        {
            if (string.IsNullOrWhiteSpace(rawBolum)) return rawBolumFlag;

            // Normalize et: küçük harf, Türkçe karakter temizle, fazla boşlukları sil
            string temiz = NormalizeHeader(rawBolum); // boşluksuz, küçük, ascii

            // Bilinen 6 bölüm ve anahtar kelimeleri
            // Sıralama önemli — daha spesifik olanlar önce kontrol edilmeli
            var bolumEslestirme = new (string[] anahtarlar, string kanonAdı)[]
            {
                (new[] { "metalurji", "malzeme" },          "METALURJİ VE MALZEME MÜHENDİSLİĞİ BÖLÜMÜ"),
                (new[] { "mekatronik" },                     "MEKATRONİK MÜHENDİSLİĞİ BÖLÜMÜ"),
                (new[] { "elektrik", "elektronik" },         "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ"),
                (new[] { "bilgisayar" },                     "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ"),
                (new[] { "insaat" },                         "İNŞAAT MÜHENDİSLİĞİ BÖLÜMÜ"),
                (new[] { "makine", "makina" },               "MAKİNE MÜHENDİSLİĞİ BÖLÜMÜ"),
            };

            foreach (var (anahtarlar, kanonAdi) in bolumEslestirme)
            {
                foreach (var anahtar in anahtarlar)
                {
                    if (temiz.Contains(anahtar))
                        return kanonAdi;
                }
            }

            // Eşleşme bulunamadı — bilinmeyen bölüm flag'i döndür
            return rawBolumFlag;
        }

        /// <summary>
        /// Verilen Excel dosyasını okuyarak List&lt;Ders&gt; döndürür.
        /// </summary>
        public static List<Ders> ExcelDersleriOku(string dosyaYolu)
        {
            if (!File.Exists(dosyaYolu))
                throw new FileNotFoundException($"Excel dosyası bulunamadı: {dosyaYolu}");

            var dersler = new List<Ders>();
            var hatalar = new List<string>();

            using (var package = new ExcelPackage(new FileInfo(dosyaYolu)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                if (worksheet == null)
                    throw new Exception("Excel dosyasında çalışma sayfası bulunamadı.");

                int satirSayisi = worksheet.Dimension?.Rows ?? 0;
                int sutunSayisi = worksheet.Dimension?.Columns ?? 0;

                if (satirSayisi < 2)
                    throw new Exception("Excel dosyası boş veya sadece başlık satırı içeriyor. En az 2 satır olmalıdır.");

                var sutunMap = SutunlariBulAkilli(worksheet, sutunSayisi);

                // Gerekli ana sütunlar (Bölüm, Ders Adı, Dersin Kodu, Kontenjan)
                string[] gerekliSutunlar = { "bolum", "dersadi", "dersinkodu", "kontenjan" };
                foreach (var gerekli in gerekliSutunlar)
                {
                    if (!sutunMap.ContainsKey(gerekli))
                    {
                        string beklenen = gerekli switch
                        {
                            "bolum" => "Bölüm (Program)",
                            "dersadi" => "Ders Adı (Ders)",
                            "dersinkodu" => "Dersin Kodu (Ders Kodu, Code)",
                            "kontenjan" => "Kontenjan (Öğrenci Sayısı, Kapasite)",
                            _ => gerekli
                        };

                        throw new Exception($"Excel dosyasında '{beklenen}' sütunu bulunamadı veya eşleştirilemedi.\n\n" +
                            $"Bulunan sütun başlıkları: {string.Join(", ", GetHeaderNames(worksheet, sutunSayisi))}");
                    }
                }

                for (int row = 2; row <= satirSayisi; row++)
                {
                    try
                    {
                        string bolumRaw = GetCellText(worksheet, row, sutunMap["bolum"]);
                        string dersAdi = GetCellText(worksheet, row, sutunMap["dersadi"]);
                        string dersKodu = GetCellText(worksheet, row, sutunMap["dersinkodu"]);
                        
                        // Şube isteğe bağlı
                        string sube = "";
                        if (sutunMap.ContainsKey("sube"))
                            sube = GetCellText(worksheet, row, sutunMap["sube"]);
                        
                        string kontenjanStr = GetCellText(worksheet, row, sutunMap["kontenjan"]);

                        string ogretimElemani = "";
                        if (sutunMap.ContainsKey("ogretimelemani"))
                            ogretimElemani = GetCellText(worksheet, row, sutunMap["ogretimelemani"]);

                        // Öğretim Türü kontrolü — İkinci Öğretim derslerini atla
                        if (sutunMap.ContainsKey("ogretimturu"))
                        {
                            string ogretimTuru = GetCellText(worksheet, row, sutunMap["ogretimturu"]);
                            string ogretimNorm = NormalizeHeader(ogretimTuru);
                            if (ogretimNorm.Contains("ikinci") || ogretimNorm.Contains("2.") || 
                                ogretimNorm.Contains("2ogretim") || ogretimNorm.Contains("io") ||
                                ogretimNorm.Contains("secondeducation"))
                            {
                                hatalar.Add($"Satır {row}: İkinci Öğretim dersi — atlandı.");
                                continue;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(dersAdi) && string.IsNullOrWhiteSpace(bolumRaw))
                            continue;

                        if (string.IsNullOrWhiteSpace(dersAdi))
                        {
                            hatalar.Add($"Satır {row}: Ders adı boş.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(bolumRaw))
                        {
                            hatalar.Add($"Satır {row}: Bölüm adı boş.");
                            continue;
                        }

                        // Ortak dersler filtresi — Türk Dili, Atatürk İlkeleri vb. atla
                        string dersAdiNorm = NormalizeHeader(dersAdi);
                        if (dersAdiNorm.Contains("turkdili") || dersAdiNorm.Contains("ataturk") ||
                            dersAdiNorm.Contains("inkilap") || dersAdiNorm.Contains("inkilaptarihi") ||
                            dersAdiNorm.Contains("inkılap") || dersAdiNorm.Contains("inklap"))
                        {
                            hatalar.Add($"Satır {row}: Ortak ders '{dersAdi}' — atlandı.");
                            continue;
                        }

                        // Tasarım/Proje dersleri filtresi — yazılı sınavı yapılmayan dersler atlanır
                        if (dersAdiNorm.Contains("tasarim") || dersAdiNorm.Contains("bitirme") ||
                            dersAdiNorm.Contains("proje") || dersAdiNorm.Contains("meslekiegitim") ||
                            dersAdiNorm.Contains("sunum"))
                        {
                            hatalar.Add($"Satır {row}: Tasarım/Proje dersi '{dersAdi}' — sınavı yapılmadığından atlandı.");
                            continue;
                        }

                        // Ders adında ikinci öğretim kontrolü (öğretim türü sütunu olmaması ihtimaline karşı)
                        if (dersAdiNorm.Contains("ikinciogretim") || dersAdiNorm.Contains("ikinciogr") || 
                            dersAdiNorm.Contains("2ogretim") || dersAdiNorm.Contains("secondeducation"))
                        {
                            hatalar.Add($"Satır {row}: İkinci Öğretim dersi '{dersAdi}' — atlandı.");
                            continue;
                        }

                        // Bölüm adını standart isimlere dönüştür
                        string bolum = BolumAdiNormalize(bolumRaw);

                        // Bölüm adı bilinen 6 bölümden biri değilse → atla (Teknoloji Fak. vb.)
                        if (bolum == rawBolumFlag)
                        {
                            hatalar.Add($"Satır {row}: Bilinmeyen bölüm '{bolumRaw}' — atlandı.");
                            continue;
                        }

                        // Bölüm adı hala sayısal/anlamsız mı kontrol et (güvenlik duvarı)
                        string bolumTemiz = bolum.Replace("/", "").Replace(" ", "").Replace(".", "").Replace(",", "").Replace("-", "");
                        bool bolumSayisal = bolumTemiz.Length > 0 && bolumTemiz.All(c => char.IsDigit(c));
                        if (bolumSayisal || bolum.Trim().Length <= 3)
                        {
                            hatalar.Add($"Satır {row}: Bölüm değeri '{bolumRaw}' geçersiz (sayısal veya çok kısa). Satır atlandı.");
                            continue;
                        }

                        // Dersin kodundan sınıf seviyesi tespiti (İlk rakam: 1, 2, 3, 4 olmalı)
                        char? ilkRakam = null;
                        foreach (char c in dersKodu)
                        {
                            if (char.IsDigit(c))
                            {
                                ilkRakam = c;
                                break;
                            }
                        }

                        if (ilkRakam == null || (ilkRakam != '1' && ilkRakam != '2' && ilkRakam != '3' && ilkRakam != '4'))
                        {
                            // Kusurlu/geçersiz satırı tamamen pas geç!
                            hatalar.Add($"Satır {row}: Geçersiz Ders Kodu '{dersKodu}'. Sınıf seviyesi (1-4) tespit edilemediğinden satır atlandı.");
                            continue;
                        }

                        int sinifSeviyesi = ilkRakam.Value - '0';

                        // Kontenjan parse
                        int kontenjan = 0;
                        if (!string.IsNullOrWhiteSpace(kontenjanStr))
                        {
                            string temiz = kontenjanStr.Trim().Replace(",", "").Replace(".", "");
                            if (!int.TryParse(temiz, out kontenjan) || kontenjan < 0)
                            {
                                hatalar.Add($"Satır {row}: Kontenjan geçersiz '{kontenjanStr}'. 0 kullanıldı.");
                                kontenjan = 0;
                            }
                        }

                        // ── ŞUBE NORMALİZASYONU ──────────────────────────────
                        // Excel'de şube sütunu çoğu zaman TAM SAYI olarak gelir (1, 2, 3, 4).
                        // Bu değerler harf formatına çevrilir: 1→A, 2→B, 3→C, 4→"D şubesi".
                        // Eski dosyalardaki "INT" etiketi de "D" olarak normalize edilir.
                        string subeHarf = SubeEtiketi.Normalize(sube);

                        dersler.Add(new Ders
                        {
                            BolumAdi = bolum,
                            SinifSeviyesi = sinifSeviyesi,
                            DersAdi = dersAdi.Trim().ToUpperInvariant(),
                            Sube = subeHarf,
                            Kontenjan = kontenjan,
                            OgretimElemani = ogretimElemani.Trim()
                        });
                    }
                    catch (Exception ex)
                    {
                        hatalar.Add($"Satır {row}: Okuma hatası — {ex.Message}");
                    }
                }
            }

            _sonHatalar = hatalar;
            return dersler;
        }

        /// <summary>
        /// Detaylı hata ve uyarı raporlayan gelişmiş versiyon.
        /// </summary>
        public static List<Ders> ExcelDersleriOkuDetayli(string dosyaYolu, out List<string> uyarilar)
        {
            var dersler = ExcelDersleriOku(dosyaYolu);
            uyarilar = _sonHatalar;
            return dersler;
        }

        private static string GetCellText(ExcelWorksheet ws, int row, int col)
        {
            var value = ws.Cells[row, col].Value;
            return value?.ToString()?.Trim() ?? "";
        }

        // =============================================
        // GÖZETMEN (ARAŞTIRMA GÖREVLİSİ) IMPORT
        // Desteklenen formatlar:
        // 1) Fakülte "GÖREVLER" formatı: [Bölüm] | Kod (B1, E4, MA2...) | Ad Soyad
        //    (üçlü satır içinde otomatik bulunur, bölüm bloklar halinde taşınır)
        // 2) Basit format: Sütun 1 = Ad Soyad, Sütun 2 = Bölüm
        // =============================================

        /// <summary>Gözetmen kodu deseni: 1-3 büyük harf + isteğe bağlı 1-2 rakam (B1, E12, MA2, MK...)</summary>
        private static readonly System.Text.RegularExpressions.Regex GozetmenKodRegex =
            new System.Text.RegularExpressions.Regex(@"^[A-ZÇĞİÖŞÜ]{1,3}[0-9]{0,2}$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Gözetmen Excel dosyasını okur. Önce fakülte GÖREVLER formatını dener;
        /// hiç kayıt bulunamazsa basit 'Ad Soyad | Bölüm' formatına düşer.
        /// Ağırlık varsayılan 1.0 olarak başlar.
        /// </summary>
        public static List<Gozetmen> GozetmenleriOku(string dosyaYolu, out List<string> uyarilar)
        {
            uyarilar = new List<string>();
            var gozetmenler = new List<Gozetmen>();

            using (var package = new ExcelPackage(new FileInfo(dosyaYolu)))
            {
                var ws = package.Workbook.Worksheets.FirstOrDefault();
                if (ws?.Dimension == null)
                    throw new Exception("Excel dosyasında okunabilir bir sayfa bulunamadı.");

                KodluGorevlerFormatiniOku(ws, gozetmenler, uyarilar);

                if (gozetmenler.Count == 0)
                    BasitGozetmenFormatiniOku(ws, gozetmenler, uyarilar);
            }

            return gozetmenler;
        }

        /// <summary>
        /// Fakülte GÖREVLER formatı: her satırda 'Kod + Ad Soyad' ikilisini arar.
        /// Kodun solundaki hücre doluysa yeni bölüm bloğu başlamıştır (carry-forward).
        /// Bölümü boş olan ek personelin bölümü kod önekinden (MA → Makine...) türetilir.
        /// </summary>
        private static void KodluGorevlerFormatiniOku(ExcelWorksheet ws,
            List<Gozetmen> gozetmenler, List<string> uyarilar)
        {
            int satirSayisi = ws.Dimension.End.Row;
            int sutunSayisi = Math.Min(ws.Dimension.End.Column, 12); // kimlik bilgileri ilk sütunlardadır

            string sonBolum = "";
            var prefixBolumleri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // "MA" → "Makine Mühendisliği"
            var eklenenKodlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var eklenenAdlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int row = 1; row <= satirSayisi; row++)
            {
                for (int col = 2; col <= sutunSayisi; col++)
                {
                    string kod = GetCellText(ws, row, col);
                    if (!GozetmenKodRegex.IsMatch(kod)) continue;

                    string adSoyad = GetCellText(ws, row, col + 1);
                    if (!AdSoyadGecerliMi(adSoyad)) continue;

                    string prefix = new string(kod.TakeWhile(char.IsLetter).ToArray());

                    // Kodun solundaki hücre doluysa yeni bölüm bloğu başlıyor
                    string bolumHucre = GetCellText(ws, row, col - 1);
                    if (bolumHucre.Length > 3 && bolumHucre.Any(char.IsLetter))
                    {
                        sonBolum = bolumHucre.Trim();
                        prefixBolumleri[prefix] = sonBolum;
                    }

                    string bolum = prefixBolumleri.TryGetValue(prefix, out var pb) ? pb
                                 : (!string.IsNullOrWhiteSpace(sonBolum) ? sonBolum : prefix);

                    // Mükerrer kontrolü: rakamlı kodlar benzersizdir (B4 iki kez gelirse ikincisi atlanır);
                    // rakamsız kodlarda (MA, MK) ada göre kontrol edilir
                    bool kodMukerrer = kod.Any(char.IsDigit) && !eklenenKodlar.Add(kod);
                    bool adMukerrer = !eklenenAdlar.Add(adSoyad);
                    if (kodMukerrer || adMukerrer)
                    {
                        uyarilar.Add($"Satır {row}: '{kod} — {adSoyad}' mükerrer kayıt — atlandı.");
                        break;
                    }

                    gozetmenler.Add(new Gozetmen
                    {
                        Kod = kod,
                        AdSoyad = adSoyad,
                        Bolum = bolum,
                        Agirlik = 1.0,
                        AtananSinavSayisi = 0
                    });
                    break; // bu satırdaki kişi bulundu, sonraki satıra geç
                }
            }
        }

        /// <summary>Ad Soyad doğrulaması: en az 2 kelime, rakam içermez, en az 5 karakter.</summary>
        private static bool AdSoyadGecerliMi(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Length < 5) return false;
            if (s.Any(char.IsDigit)) return false;
            if (!s.Any(char.IsLetter)) return false;
            return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length >= 2;
        }

        /// <summary>Basit format: Sütun 1 = Ad Soyad, Sütun 2 = Bölüm. Başlık satırı otomatik atlanır.</summary>
        private static void BasitGozetmenFormatiniOku(ExcelWorksheet ws,
            List<Gozetmen> gozetmenler, List<string> uyarilar)
        {
            int satirSayisi = ws.Dimension.End.Row;

            string ilkHucre = NormalizeHeader(GetCellText(ws, 1, 1));
            string ikinciHucre = NormalizeHeader(GetCellText(ws, 1, 2));
            bool baslikSatiri = ilkHucre.Contains("adsoyad") ||
                                (ilkHucre == "ad" || ilkHucre == "isim" || ilkHucre == "gozetmen") ||
                                ikinciHucre.Contains("bolum") || ikinciHucre.Contains("birim");
            int baslangicSatiri = baslikSatiri ? 2 : 1;

            for (int row = baslangicSatiri; row <= satirSayisi; row++)
            {
                string adSoyad = GetCellText(ws, row, 1);
                string bolum = GetCellText(ws, row, 2);

                if (string.IsNullOrWhiteSpace(adSoyad) && string.IsNullOrWhiteSpace(bolum))
                    continue; // tamamen boş satır

                if (string.IsNullOrWhiteSpace(adSoyad) || string.IsNullOrWhiteSpace(bolum))
                {
                    uyarilar.Add($"Satır {row}: Ad Soyad veya Bölüm eksik — atlandı.");
                    continue;
                }

                if (gozetmenler.Any(g => string.Equals(g.AdSoyad, adSoyad, StringComparison.OrdinalIgnoreCase)))
                {
                    uyarilar.Add($"Satır {row}: '{adSoyad}' zaten listede — atlandı.");
                    continue;
                }

                gozetmenler.Add(new Gozetmen
                {
                    AdSoyad = adSoyad,
                    Bolum = bolum.Trim(),
                    Agirlik = 1.0,
                    AtananSinavSayisi = 0
                });
            }
        }

        private static List<string> GetHeaderNames(ExcelWorksheet ws, int sutunSayisi)
        {
            var headers = new List<string>();
            for (int col = 1; col <= sutunSayisi; col++)
            {
                string h = GetCellText(ws, 1, col);
                if (!string.IsNullOrWhiteSpace(h))
                    headers.Add(h);
            }
            return headers;
        }
    }
}
