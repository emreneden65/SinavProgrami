#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// SQLite veritabanı işlemlerini yöneten yardımcı sınıf.
    ///
    /// TAŞINABİLİRLİK: Veritabanı, exe'nin yanındaki tek bir "SinavDB.db" dosyasıdır.
    /// Hiçbir sunucu kurulumu gerekmez — klasörü kopyalayan herkes çalıştırabilir.
    /// Dosya yoksa ilk açılışta <see cref="VeritabaniniHazirla"/> tarafından oluşturulur.
    /// </summary>
    public class DatabaseHelper
    {
        // ============================================================
        // VERİTABANI DOSYASI — exe'nin yanında durur
        // ============================================================
        private static string _dbYolu =
            Path.Combine(AppContext.BaseDirectory, "SinavDB.db");

        private static string _connectionString =
            new SqliteConnectionStringBuilder { DataSource = _dbYolu }.ToString();

        /// <summary>Veritabanı dosyasının tam yolu.</summary>
        public static string VeritabaniYolu => _dbYolu;

        /// <summary>
        /// Connection string'i dışarıdan ayarlamak için kullanılır.
        /// </summary>
        public static string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value;
        }

        /// <summary>
        /// Veritabanı dosyasını başka bir konuma yönlendirir (ör. ağ paylaşımı).
        /// </summary>
        public static void VeritabaniniAyarla(string dosyaYolu)
        {
            _dbYolu = dosyaYolu;
            _connectionString = new SqliteConnectionStringBuilder { DataSource = _dbYolu }.ToString();
        }

        // ============================================================
        // ŞEMA KURULUMU (ilk açılışta otomatik çalışır)
        // ============================================================

        /// <summary>
        /// Veritabanı dosyası ve tablolar yoksa oluşturur; varsa eksik kolonları tamamlar.
        /// Program.cs içinden, MainForm açılmadan önce çağrılır.
        /// Yeni bir bilgisayarda hiçbir kurulum gerektirmemesini bu metot sağlar.
        /// </summary>
        public static void VeritabaniniHazirla()
        {
            var klasor = Path.GetDirectoryName(_dbYolu);
            if (!string.IsNullOrEmpty(klasor) && !Directory.Exists(klasor))
                Directory.CreateDirectory(klasor);

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                Calistir(connection, @"
                    CREATE TABLE IF NOT EXISTS Salonlar (
                        ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Salon_Kodu  TEXT    NOT NULL,
                        Kapasite    INTEGER NOT NULL DEFAULT 0,
                        Salon_Tipi  TEXT    NOT NULL DEFAULT 'NORMAL'
                    );");

                Calistir(connection, @"
                    CREATE TABLE IF NOT EXISTS Dersler (
                        ID            INTEGER PRIMARY KEY AUTOINCREMENT,
                        BolumAdi      TEXT    NOT NULL DEFAULT '',
                        SinifSeviyesi INTEGER NOT NULL DEFAULT 0,
                        DersAdi       TEXT    NOT NULL DEFAULT ''
                    );");

                Calistir(connection, @"
                    CREATE TABLE IF NOT EXISTS KayitliDersler (
                        DersID        INTEGER PRIMARY KEY AUTOINCREMENT,
                        Donem         TEXT    NOT NULL DEFAULT '',
                        BolumAdi      TEXT    NOT NULL DEFAULT '',
                        SinifSeviyesi INTEGER NOT NULL DEFAULT 0,
                        DersAdi       TEXT    NOT NULL DEFAULT '',
                        Sube          TEXT    NOT NULL DEFAULT '',
                        Kontenjan     INTEGER NOT NULL DEFAULT 0
                    );");

                Calistir(connection, @"
                    CREATE TABLE IF NOT EXISTS Gozetmenler (
                        ID       INTEGER PRIMARY KEY AUTOINCREMENT,
                        Kod      TEXT    NOT NULL DEFAULT '',
                        AdSoyad  TEXT    NOT NULL,
                        Bolum    TEXT    NOT NULL,
                        Agirlik  REAL    NOT NULL DEFAULT 1.0,
                        AktifMi  INTEGER NOT NULL DEFAULT 1,
                        UNIQUE (AdSoyad, Bolum)
                    );");

                Calistir(connection, @"
                    CREATE TABLE IF NOT EXISTS GozetmenGorevGecmisi (
                        ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                        GozetmenID  INTEGER NOT NULL REFERENCES Gozetmenler(ID),
                        SinavTarihi TEXT    NOT NULL,
                        Saat        TEXT    NOT NULL,
                        SalonKodu   TEXT    NOT NULL,
                        DersAdi     TEXT    NOT NULL,
                        DersBolumu  TEXT    NOT NULL,
                        OrtakMi     INTEGER NOT NULL DEFAULT 0,
                        DestekMi    INTEGER NOT NULL DEFAULT 0,
                        KayitZamani TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                        UNIQUE (GozetmenID, SinavTarihi, Saat)
                    );");

                Calistir(connection, "CREATE INDEX IF NOT EXISTS IX_Salonlar_Kodu     ON Salonlar(Salon_Kodu);");
                Calistir(connection, "CREATE INDEX IF NOT EXISTS IX_Gozetmenler_Bolum ON Gozetmenler(Bolum);");
                Calistir(connection, "CREATE INDEX IF NOT EXISTS IX_Gorev_Gozetmen    ON GozetmenGorevGecmisi(GozetmenID);");
                Calistir(connection, "CREATE INDEX IF NOT EXISTS IX_Gorev_TarihSaat   ON GozetmenGorevGecmisi(SinavTarihi, Saat);");
                Calistir(connection, "CREATE INDEX IF NOT EXISTS IX_KayitliDersler_Filtre ON KayitliDersler(BolumAdi, SinifSeviyesi);");

                // Eski bir .db dosyasında Salon_Tipi kolonu yoksa ekle (geriye uyumluluk)
                if (!KolonVarMi(connection, "Salonlar", "Salon_Tipi"))
                    Calistir(connection, "ALTER TABLE Salonlar ADD COLUMN Salon_Tipi TEXT NOT NULL DEFAULT 'NORMAL';");

                // İlk kurulum: exe yanındaki CSV tohum dosyalarını içeri al
                BaslangicVerisiniYukle(connection);
            }
        }

        private static void Calistir(SqliteConnection connection, string sql)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private static bool KolonVarMi(SqliteConnection connection, string tablo, string kolon)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({tablo});";
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        if (string.Equals(reader.GetString(1), kolon, StringComparison.OrdinalIgnoreCase))
                            return true;
            }
            return false;
        }

        private static long TekSayi(SqliteConnection connection, string sql)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                var sonuc = cmd.ExecuteScalar();
                return sonuc == null || sonuc == DBNull.Value ? 0 : Convert.ToInt64(sonuc);
            }
        }

        private static bool TabloVarMi(SqliteConnection connection, string tablo) =>
            TekSayi(connection, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tablo}'") > 0;

        // ============================================================
        // BAŞLANGIÇ VERİSİ (CSV tohumlama)
        // ------------------------------------------------------------
        // Yeni bir bilgisayarda Salonlar tablosunun boş kalmaması için,
        // exe'nin yanındaki "Baslangic\Salonlar.csv" dosyası (varsa) bir
        // kereye mahsus içeri alınır. Dosya biçimi (ilk satır başlık):
        //     Salon_Kodu;Kapasite;Salon_Tipi
        //     T4-283;60;UYGULAMALI
        // Bu CSV'yi eski SQL Server veritabanınızdan üretmek için
        // "verileri_disari_aktar.ps1" betiğini kullanın.
        // ============================================================
        private static void BaslangicVerisiniYukle(SqliteConnection connection)
        {
            try
            {
                if (TekSayi(connection, "SELECT COUNT(*) FROM Salonlar") > 0) return;

                string csv = Path.Combine(AppContext.BaseDirectory, "Baslangic", "Salonlar.csv");
                if (!File.Exists(csv)) return;

                using (var transaction = connection.BeginTransaction())
                {
                    bool ilkSatir = true;
                    foreach (var satir in File.ReadAllLines(csv, System.Text.Encoding.UTF8))
                    {
                        if (ilkSatir) { ilkSatir = false; continue; }          // başlık
                        if (string.IsNullOrWhiteSpace(satir)) continue;
                        if (satir.TrimStart().StartsWith("#")) continue;       // açıklama satırı

                        var p = satir.Split(';');
                        if (p.Length < 2) continue;

                        string kod = p[0].Trim();
                        if (kod.Length == 0) continue;
                        int.TryParse(p[1].Trim(), out int kapasite);
                        string tip = p.Length > 2 && p[2].Trim().Length > 0
                                     ? p[2].Trim().ToUpperInvariant()
                                     : "NORMAL";

                        using (var cmd = new SqliteCommand(
                            "INSERT INTO Salonlar (Salon_Kodu, Kapasite, Salon_Tipi) VALUES ($k, $c, $t);",
                            connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("$k", kod);
                            cmd.Parameters.AddWithValue("$c", kapasite);
                            cmd.Parameters.AddWithValue("$t", tip);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                // Tohumlama başarısız olursa uygulama yine de açılmalı.
                System.Diagnostics.Debug.WriteLine($"Başlangıç verisi yüklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Veritabanı bağlantısını test eder.
        /// </summary>
        public static bool BaglantiyiTest()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bağlantı hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Salonlar tablosundaki tüm kayıtları getirir.
        /// </summary>
        public static List<Salon> SalonlariGetir()
        {
            var salonlar = new List<Salon>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    if (!TabloVarMi(connection, "Salonlar")) return salonlar;

                    // Salon_Tipi kolonu henüz eklenmemişse eski sorguyla devam et (geriye uyumluluk)
                    bool tipKolonuVar = KolonVarMi(connection, "Salonlar", "Salon_Tipi");

                    string query = tipKolonuVar
                        ? "SELECT ID, Salon_Kodu, Kapasite, COALESCE(Salon_Tipi, 'NORMAL') FROM Salonlar ORDER BY Kapasite ASC"
                        : "SELECT ID, Salon_Kodu, Kapasite, 'NORMAL' FROM Salonlar ORDER BY Kapasite ASC";

                    using (var command = new SqliteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            salonlar.Add(new Salon
                            {
                                ID = reader.GetInt32(0),
                                SalonKodu = reader.GetString(1),
                                Kapasite = reader.GetInt32(2),
                                SalonTipi = reader.GetString(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Salonlar tablosu okunurken hata oluştu: {ex.Message}", ex);
            }

            return salonlar;
        }

        /// <summary>
        /// Salonlar tablosunu verilen listeyle sıfırdan yazar.
        /// (Excel'den salon listesi içeri almak için kullanılabilir.)
        /// </summary>
        public static int SalonlariKaydet(List<Salon> salonlar, bool oncekileriSil = true)
        {
            if (salonlar == null) return 0;
            int yazilan = 0;

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        if (oncekileriSil)
                            using (var sil = new SqliteCommand("DELETE FROM Salonlar;", connection, transaction))
                                sil.ExecuteNonQuery();

                        foreach (var s in salonlar)
                        {
                            if (string.IsNullOrWhiteSpace(s?.SalonKodu)) continue;
                            using (var cmd = new SqliteCommand(
                                "INSERT INTO Salonlar (Salon_Kodu, Kapasite, Salon_Tipi) VALUES ($k, $c, $t);",
                                connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("$k", s.SalonKodu.Trim());
                                cmd.Parameters.AddWithValue("$c", s.Kapasite);
                                cmd.Parameters.AddWithValue("$t",
                                    string.IsNullOrWhiteSpace(s.SalonTipi) ? "NORMAL" : s.SalonTipi.ToUpperInvariant());
                                yazilan += cmd.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Salonlar kaydedilirken hata oluştu: {ex.Message}", ex);
            }

            return yazilan;
        }

        // ============================================================
        // GÖZETMENLER + GÖREV GEÇMİŞİ
        // ============================================================

        /// <summary>
        /// Gozetmenler tablosundaki AKTİF kayıtları getirir.
        /// Tablo yoksa boş liste döner (Excel'den yükleme akışı bozulmaz).
        /// </summary>
        public static List<Gozetmen> GozetmenleriGetir()
        {
            var liste = new List<Gozetmen>();
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    if (!TabloVarMi(connection, "Gozetmenler")) return liste;

                    const string query = @"SELECT Kod, AdSoyad, Bolum, Agirlik
                                           FROM Gozetmenler WHERE AktifMi = 1
                                           ORDER BY Bolum, AdSoyad";
                    using (var command = new SqliteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                        while (reader.Read())
                            liste.Add(new Gozetmen
                            {
                                Kod = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                AdSoyad = reader.GetString(1),
                                Bolum = reader.GetString(2),
                                Agirlik = reader.IsDBNull(3) ? 1.0 : reader.GetDouble(3)
                            });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Gozetmenler tablosu okunurken hata oluştu: {ex.Message}", ex);
            }
            return liste;
        }

        /// <summary>
        /// Gözetmen atama sonuçlarını dönem boyu iş yükü takibi için
        /// GozetmenGorevGecmisi tablosuna yazar. Aynı slottaki mükerrer kayıtlar
        /// sessizce atlanır; tablo yoksa hiçbir şey yapmaz.
        /// </summary>
        /// <returns>Yazılan kayıt sayısı.</returns>
        public static int GozetmenGorevleriniKaydet(List<SinavAtama> atamalar,
                                                    ISet<string> ortakDersAdlari = null)
        {
            if (atamalar == null || atamalar.Count == 0) return 0;
            int yazilan = 0;

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    if (!TabloVarMi(connection, "GozetmenGorevGecmisi")) return 0;

                    const string insert = @"
                        INSERT INTO GozetmenGorevGecmisi
                            (GozetmenID, SinavTarihi, Saat, SalonKodu, DersAdi, DersBolumu, OrtakMi, DestekMi)
                        SELECT g.ID, $Tarih, $Saat, $Salon, $Ders, $Bolum, $Ortak,
                               CASE WHEN g.Bolum <> $Bolum THEN 1 ELSE 0 END
                        FROM Gozetmenler g
                        WHERE g.AdSoyad = $AdSoyad
                          AND NOT EXISTS (SELECT 1 FROM GozetmenGorevGecmisi h
                                          WHERE h.GozetmenID = g.ID
                                            AND h.SinavTarihi = $Tarih AND h.Saat = $Saat)";

                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var sinav in atamalar)
                        {
                            if (sinav.AtananSalonlar == null) continue;
                            bool ortak = ortakDersAdlari != null &&
                                         ortakDersAdlari.Contains(sinav.DersAdi ?? "");

                            foreach (var salon in sinav.AtananSalonlar)
                            {
                                if (string.IsNullOrWhiteSpace(salon.GozetmenAdSoyad)) continue;
                                using (var cmd = new SqliteCommand(insert, connection, transaction))
                                {
                                    // Tarih, SQLite'ta metin olarak ISO biçiminde tutulur (sıralanabilir)
                                    cmd.Parameters.AddWithValue("$Tarih", sinav.Gun.Date.ToString("yyyy-MM-dd"));
                                    cmd.Parameters.AddWithValue("$Saat", sinav.Saat ?? "");
                                    cmd.Parameters.AddWithValue("$Salon", salon.SalonKodu ?? "");
                                    cmd.Parameters.AddWithValue("$Ders", sinav.DersAdi ?? "");
                                    cmd.Parameters.AddWithValue("$Bolum", sinav.BolumAdi ?? "");
                                    cmd.Parameters.AddWithValue("$Ortak", ortak ? 1 : 0);
                                    cmd.Parameters.AddWithValue("$AdSoyad", salon.GozetmenAdSoyad);
                                    yazilan += cmd.ExecuteNonQuery();
                                }
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Gözetmen görev geçmişi yazılırken hata oluştu: {ex.Message}", ex);
            }
            return yazilan;
        }

        // ============================================================
        // DERSLER TABLOSU (DERS BULUTU / KATALOG)
        // ============================================================

        /// <summary>
        /// Dersler tablosundan (ders bulutu) Bölüm + Sınıf filtresine göre
        /// DISTINCT ders adlarını getirir. cmbDersAdi ComboBox'ını doldurmak için kullanılır.
        /// </summary>
        public static async Task<List<string>> DersBulutundanDersAdlariGetirAsync(string bolumAdi, int sinifSeviyesi)
        {
            var dersAdlari = new List<string>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"SELECT DISTINCT DersAdi
                                     FROM Dersler
                                     WHERE BolumAdi = $BolumAdi
                                       AND SinifSeviyesi = $SinifSeviyesi
                                     ORDER BY DersAdi";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("$BolumAdi", bolumAdi ?? "");
                        command.Parameters.AddWithValue("$SinifSeviyesi", sinifSeviyesi);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string dersAdi = reader["DersAdi"]?.ToString() ?? "";
                                if (!string.IsNullOrWhiteSpace(dersAdi))
                                    dersAdlari.Add(dersAdi);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ders bulutu sorgu hatası: {ex.Message}");
            }

            return dersAdlari;
        }

        // ============================================================
        // KAYITLIDersler TABLOSU İŞLEMLERİ
        // Tablo sütunları: DersID, Donem, BolumAdi, SinifSeviyesi, DersAdi, Sube, Kontenjan
        // ============================================================

        /// <summary>
        /// KayitliDersler tablosundaki TÜM dersleri getirir.
        /// </summary>
        public static List<KayitliDers> GetDersler()
        {
            var dersler = new List<KayitliDers>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    string query = "SELECT DersID, Donem, BolumAdi, SinifSeviyesi, DersAdi, Sube, Kontenjan FROM KayitliDersler ORDER BY BolumAdi, SinifSeviyesi, DersAdi, Sube";

                    using (var command = new SqliteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dersler.Add(new KayitliDers
                            {
                                DersID = Convert.ToInt32(reader["DersID"]),
                                Donem = reader["Donem"]?.ToString() ?? "",
                                BolumAdi = reader["BolumAdi"]?.ToString() ?? "",
                                SinifSeviyesi = Convert.ToInt32(reader["SinifSeviyesi"]),
                                DersAdi = reader["DersAdi"]?.ToString() ?? "",
                                Sube = SubeEtiketi.Normalize(reader["Sube"] == DBNull.Value ? null : reader["Sube"]),
                                Kontenjan = reader["Kontenjan"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Kontenjan"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Dersler tablosu okunurken hata oluştu: {ex.Message}", ex);
            }

            return dersler;
        }

        /// <summary>
        /// Bölüm + Sınıf filtresine göre dersleri getirir.
        /// </summary>
        public static async Task<List<KayitliDers>> DersleriFiltreleAsync(string bolumAdi, int sinifSeviyesi)
        {
            var dersler = new List<KayitliDers>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"SELECT DersID, Donem, BolumAdi, SinifSeviyesi, DersAdi, Sube, Kontenjan
                                     FROM KayitliDersler
                                     WHERE BolumAdi = $BolumAdi
                                       AND SinifSeviyesi = $SinifSeviyesi
                                     ORDER BY DersAdi, Sube";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("$BolumAdi", bolumAdi ?? "");
                        command.Parameters.AddWithValue("$SinifSeviyesi", sinifSeviyesi);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                dersler.Add(new KayitliDers
                                {
                                    DersID = Convert.ToInt32(reader["DersID"]),
                                    Donem = reader["Donem"]?.ToString() ?? "",
                                    BolumAdi = reader["BolumAdi"]?.ToString() ?? "",
                                    SinifSeviyesi = Convert.ToInt32(reader["SinifSeviyesi"]),
                                    DersAdi = reader["DersAdi"]?.ToString() ?? "",
                                    Sube = SubeEtiketi.Normalize(reader["Sube"] == DBNull.Value ? null : reader["Sube"]),
                                    Kontenjan = reader["Kontenjan"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Kontenjan"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Dersler filtrelenirken hata oluştu: {ex.Message}", ex);
            }

            return dersler;
        }

        /// <summary>
        /// Tüm kayıtlı dersleri getirir.
        /// Sınav programı oluşturulurken kontenjanı dolu olan dersleri çekmek için kullanılır.
        /// </summary>
        public static async Task<List<KayitliDers>> DersleriGetirAsync()
        {
            var dersler = new List<KayitliDers>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"SELECT DersID, Donem, BolumAdi, SinifSeviyesi, DersAdi, Sube, Kontenjan
                                     FROM KayitliDersler
                                     ORDER BY BolumAdi, SinifSeviyesi, DersAdi, Sube";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                dersler.Add(new KayitliDers
                                {
                                    DersID = Convert.ToInt32(reader["DersID"]),
                                    Donem = reader["Donem"]?.ToString() ?? "",
                                    BolumAdi = reader["BolumAdi"]?.ToString() ?? "",
                                    SinifSeviyesi = Convert.ToInt32(reader["SinifSeviyesi"]),
                                    DersAdi = reader["DersAdi"]?.ToString() ?? "",
                                    Sube = SubeEtiketi.Normalize(reader["Sube"] == DBNull.Value ? null : reader["Sube"]),
                                    Kontenjan = reader["Kontenjan"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Kontenjan"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Dersler okunurken hata oluştu: {ex.Message}", ex);
            }

            return dersler;
        }

        /// <summary>
        /// Belirli bir dersin Kontenjan değerini günceller (DersID ile).
        /// </summary>
        public static async Task KontenjanGuncelleAsync(int dersID, int yeniKontenjan)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "UPDATE KayitliDersler SET Kontenjan = $Kontenjan WHERE DersID = $DersID";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("$Kontenjan", yeniKontenjan);
                        command.Parameters.AddWithValue("$DersID", dersID);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Kontenjan güncellenirken hata oluştu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Dersin tüm alanlarını (inline düzenleme sonucu) günceller.
        /// </summary>
        public static async Task DersGuncelleAsync(KayitliDers ders)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"UPDATE KayitliDersler
                                     SET DersAdi = $DersAdi,
                                         BolumAdi = $BolumAdi,
                                         SinifSeviyesi = $SinifSeviyesi,
                                         Sube = $Sube,
                                         Kontenjan = $Kontenjan
                                     WHERE DersID = $DersID";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("$DersAdi", ders.DersAdi.ToUpperInvariant().Trim());
                        command.Parameters.AddWithValue("$BolumAdi", ders.BolumAdi.ToUpperInvariant().Trim());
                        command.Parameters.AddWithValue("$SinifSeviyesi", ders.SinifSeviyesi);
                        command.Parameters.AddWithValue("$Sube", SubeEtiketi.Normalize(ders.Sube));
                        command.Parameters.AddWithValue("$Kontenjan", ders.Kontenjan);
                        command.Parameters.AddWithValue("$DersID", ders.DersID);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ders güncellenirken hata oluştu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Yeni bir dersi KayitliDersler tablosuna ekler.
        /// Aynı kayıt zaten varsa ekleme yapmaz ve false döndürür.
        /// </summary>
        public static async Task<bool> KayitliDersEkleAsync(KayitliDers ders)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Duplicate kontrolü
                    string checkQuery = @"SELECT COUNT(*) FROM KayitliDersler
                                          WHERE BolumAdi = $BolumAdi
                                            AND SinifSeviyesi = $SinifSeviyesi
                                            AND DersAdi = $DersAdi
                                            AND Sube = $Sube";

                    using (var checkCmd = new SqliteCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("$BolumAdi", ders.BolumAdi.ToUpperInvariant().Trim());
                        checkCmd.Parameters.AddWithValue("$SinifSeviyesi", ders.SinifSeviyesi);
                        checkCmd.Parameters.AddWithValue("$DersAdi", ders.DersAdi.ToUpperInvariant().Trim());
                        checkCmd.Parameters.AddWithValue("$Sube", SubeEtiketi.Normalize(ders.Sube));

                        // SQLite COUNT(*) long döner → Convert şart
                        long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
                        if (count > 0)
                            return false; // Zaten mevcut
                    }

                    // INSERT
                    string insertQuery = @"INSERT INTO KayitliDersler (Donem, BolumAdi, SinifSeviyesi, DersAdi, Sube, Kontenjan)
                                           VALUES ($Donem, $BolumAdi, $SinifSeviyesi, $DersAdi, $Sube, $Kontenjan)";

                    using (var insertCmd = new SqliteCommand(insertQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("$Donem", "");
                        insertCmd.Parameters.AddWithValue("$BolumAdi", ders.BolumAdi.ToUpperInvariant().Trim());
                        insertCmd.Parameters.AddWithValue("$SinifSeviyesi", ders.SinifSeviyesi);
                        insertCmd.Parameters.AddWithValue("$DersAdi", ders.DersAdi.ToUpperInvariant().Trim());
                        insertCmd.Parameters.AddWithValue("$Sube", SubeEtiketi.Normalize(ders.Sube));
                        insertCmd.Parameters.AddWithValue("$Kontenjan", ders.Kontenjan);

                        await insertCmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Kayıtlı ders eklenirken hata oluştu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Yeni bir dersi KayitliDersler tablosuna ekler ve üretilen ID'yi döner.
        /// Aynı kayıt zaten varsa 0 döndürür.
        /// </summary>
        public static async Task<int> KayitliDersEkleGeriIdAsync(KayitliDers ders)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Duplicate kontrolü
                    string checkQuery = @"SELECT COUNT(*) FROM KayitliDersler
                                          WHERE BolumAdi = $BolumAdi
                                            AND SinifSeviyesi = $SinifSeviyesi
                                            AND DersAdi = $DersAdi
                                            AND Sube = $Sube";

                    using (var checkCmd = new SqliteCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("$BolumAdi", ders.BolumAdi.ToUpperInvariant().Trim());
                        checkCmd.Parameters.AddWithValue("$SinifSeviyesi", ders.SinifSeviyesi);
                        checkCmd.Parameters.AddWithValue("$DersAdi", ders.DersAdi.ToUpperInvariant().Trim());
                        checkCmd.Parameters.AddWithValue("$Sube", SubeEtiketi.Normalize(ders.Sube));

                        long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
                        if (count > 0)
                            return 0; // Zaten mevcut
                    }

                    // INSERT + üretilen ID (SQL Server SCOPE_IDENTITY karşılığı)
                    string insertQuery = @"INSERT INTO KayitliDersler (Donem, BolumAdi, SinifSeviyesi, DersAdi, Sube, Kontenjan)
                                           VALUES ($Donem, $BolumAdi, $SinifSeviyesi, $DersAdi, $Sube, $Kontenjan);
                                           SELECT last_insert_rowid();";

                    using (var insertCmd = new SqliteCommand(insertQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("$Donem", "");
                        insertCmd.Parameters.AddWithValue("$BolumAdi", ders.BolumAdi.ToUpperInvariant().Trim());
                        insertCmd.Parameters.AddWithValue("$SinifSeviyesi", ders.SinifSeviyesi);
                        insertCmd.Parameters.AddWithValue("$DersAdi", ders.DersAdi.ToUpperInvariant().Trim());
                        insertCmd.Parameters.AddWithValue("$Sube", SubeEtiketi.Normalize(ders.Sube));
                        insertCmd.Parameters.AddWithValue("$Kontenjan", ders.Kontenjan);

                        object result = await insertCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Kayıtlı ders eklenirken hata oluştu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Belirli bir kayıtlı dersi DersID'ye göre siler.
        /// </summary>
        public static async Task<int> KayitliDersSilAsync(int dersID)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string deleteQuery = "DELETE FROM KayitliDersler WHERE DersID = $DersID";

                    using (var command = new SqliteCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("$DersID", dersID);
                        return await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Kayıtlı ders silinirken hata oluştu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// KayitliDersler tablosundan benzersiz bölüm adlarını getirir.
        /// Overbooking grid'ini doldurmak için kullanılır.
        /// </summary>
        public static async Task<List<string>> GetDistinctBolumlerAsync()
        {
            var bolumler = new List<string>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"SELECT DISTINCT BolumAdi
                                     FROM KayitliDersler
                                     ORDER BY BolumAdi";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string bolum = reader["BolumAdi"]?.ToString() ?? "";
                                if (!string.IsNullOrWhiteSpace(bolum))
                                    bolumler.Add(bolum);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bölüm listesi sorgu hatası: {ex.Message}");
            }

            return bolumler;
        }

        /// <summary>
        /// Excel'den okunan dersleri toplu olarak KayitliDersler tablosuna ekler.
        /// Her ders için duplicate kontrolü yapar — zaten varsa atlar.
        /// Transaction kullanarak atomik işlem sağlar.
        /// </summary>
        /// <returns>(eklenen, atlanan) sayıları</returns>
        public static async Task<(int Eklenen, int Atlanan)> TopluDersEkleAsync(List<Ders> dersler)
        {
            int eklenen = 0;
            int atlanan = 0;

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var ders in dersler)
                            {
                                // Duplicate kontrolü
                                string checkQuery = @"SELECT COUNT(*) FROM KayitliDersler
                                                      WHERE BolumAdi = $BolumAdi
                                                        AND SinifSeviyesi = $SinifSeviyesi
                                                        AND DersAdi = $DersAdi
                                                        AND Sube = $Sube";

                                using (var checkCmd = new SqliteCommand(checkQuery, connection, transaction))
                                {
                                    checkCmd.Parameters.AddWithValue("$BolumAdi", ders.BolumAdi ?? "");
                                    checkCmd.Parameters.AddWithValue("$SinifSeviyesi", ders.SinifSeviyesi);
                                    checkCmd.Parameters.AddWithValue("$DersAdi", ders.DersAdi ?? "");
                                    checkCmd.Parameters.AddWithValue("$Sube", SubeEtiketi.Normalize(ders.Sube));

                                    long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
                                    if (count > 0)
                                    {
                                        atlanan++;
                                        continue;
                                    }
                                }

                                // INSERT
                                string insertQuery = @"INSERT INTO KayitliDersler (Donem, BolumAdi, SinifSeviyesi, DersAdi, Sube, Kontenjan)
                                                       VALUES ($Donem, $BolumAdi, $SinifSeviyesi, $DersAdi, $Sube, $Kontenjan)";

                                using (var insertCmd = new SqliteCommand(insertQuery, connection, transaction))
                                {
                                    insertCmd.Parameters.AddWithValue("$Donem", "");
                                    insertCmd.Parameters.AddWithValue("$BolumAdi", ders.BolumAdi ?? "");
                                    insertCmd.Parameters.AddWithValue("$SinifSeviyesi", ders.SinifSeviyesi);
                                    insertCmd.Parameters.AddWithValue("$DersAdi", ders.DersAdi ?? "");
                                    insertCmd.Parameters.AddWithValue("$Sube", SubeEtiketi.Normalize(ders.Sube));
                                    insertCmd.Parameters.AddWithValue("$Kontenjan", ders.Kontenjan);

                                    await insertCmd.ExecuteNonQueryAsync();
                                    eklenen++;
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Toplu ders ekleme hatası: {ex.Message}\n\n{eklenen} ders eklenmişti ancak hata nedeniyle geri alındı.", ex);
            }

            return (eklenen, atlanan);
        }

        /// <summary>
        /// KayitliDersler tablosundaki TÜM kayıtları siler.
        /// Uygulamayı fabrika ayarlarına döndürmek için kullanılır.
        /// </summary>
        /// <returns>Silinen kayıt sayısı</returns>
        public static async Task<int> TumKayitliDersleriSilAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string deleteQuery = "DELETE FROM KayitliDersler";

                    using (var command = new SqliteCommand(deleteQuery, connection))
                    {
                        return await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Tüm dersler silinirken hata oluştu: {ex.Message}", ex);
            }
        }
    }
}
