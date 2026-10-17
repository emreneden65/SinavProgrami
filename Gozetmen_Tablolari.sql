-- ═══════════════════════════════════════════════════════════════════
--  GÖZETMEN ATAMA MODÜLÜ — VERİTABANI KURGUSU
--  Gözetmenler + dönem boyu iş yükü takibi (görev geçmişi).
--  İş kuralları:
--    1) Öncelikli Bölüm: sınavın bölümünün hocaları önce atanır;
--       yetersizse ortalama yükü en az bölümlerden destek çağrılır.
--    2) Adil Dağıtım: dönem boyu görev sayısı farkı minimum tutulur
--       (Agirlik: kapasite katsayısı — yüksekse daha çok görev alabilir).
--    3) Ortak dersler dengeleme havuzudur (GorevGecmisi.OrtakMi).
-- ═══════════════════════════════════════════════════════════════════

-- ── 1) GÖZETMENLER ──────────────────────────────────────────────────
IF OBJECT_ID('dbo.Gozetmenler', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Gozetmenler
    (
        ID              INT IDENTITY(1,1) PRIMARY KEY,
        Kod             NVARCHAR(10)  NOT NULL DEFAULT(''),   -- B1, E4, MA2...
        AdSoyad         NVARCHAR(100) NOT NULL,
        Bolum           NVARCHAR(100) NOT NULL,               -- ders listesindeki bölüm adıyla eşleşir
        Agirlik         FLOAT         NOT NULL DEFAULT(1.0),  -- kapasite katsayısı (yüksek → daha çok görev)
        AktifMi         BIT           NOT NULL DEFAULT(1),    -- izinli/raporlu gözetmenleri atama dışı bırakmak için
        CONSTRAINT UQ_Gozetmen UNIQUE (AdSoyad, Bolum)
    );
    CREATE INDEX IX_Gozetmenler_Bolum ON dbo.Gozetmenler(Bolum) INCLUDE (AktifMi, Agirlik);
END
GO

-- ── 2) GÖREV GEÇMİŞİ (dönem boyu iş yükü) ──────────────────────────
-- Her satır = bir gözetmenin bir slotta bir salondaki görevi.
-- Dönem boyu yük = COUNT(*) GROUP BY GozetmenID → Kural 2'nin kalıcı sayacı.
IF OBJECT_ID('dbo.GozetmenGorevGecmisi', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GozetmenGorevGecmisi
    (
        ID              INT IDENTITY(1,1) PRIMARY KEY,
        GozetmenID      INT           NOT NULL REFERENCES dbo.Gozetmenler(ID),
        SinavTarihi     DATE          NOT NULL,
        Saat            NVARCHAR(5)   NOT NULL,               -- '09:00'
        SalonKodu       NVARCHAR(20)  NOT NULL,
        DersAdi         NVARCHAR(150) NOT NULL,
        DersBolumu      NVARCHAR(100) NOT NULL,
        OrtakMi         BIT           NOT NULL DEFAULT(0),    -- Kural 3: dengeleme havuzu işareti
        DestekMi        BIT           NOT NULL DEFAULT(0),    -- Kural 1: bölüm dışından destek ataması mı
        KayitZamani     DATETIME2     NOT NULL DEFAULT(SYSDATETIME()),
        -- Kural 1 (çakışma koruması): bir gözetmen aynı slotta tek salonda olabilir
        CONSTRAINT UQ_GorevSlot UNIQUE (GozetmenID, SinavTarihi, Saat)
    );
    CREATE INDEX IX_Gorev_Gozetmen ON dbo.GozetmenGorevGecmisi(GozetmenID);
    CREATE INDEX IX_Gorev_TarihSaat ON dbo.GozetmenGorevGecmisi(SinavTarihi, Saat);
END
GO

-- ── 3) İŞ YÜKÜ GÖRÜNÜMÜ (raporlama / denge kontrolü) ───────────────
IF OBJECT_ID('dbo.vw_GozetmenIsYuku', 'V') IS NOT NULL
    DROP VIEW dbo.vw_GozetmenIsYuku;
GO
CREATE VIEW dbo.vw_GozetmenIsYuku AS
SELECT
    g.ID,
    g.Kod,
    g.AdSoyad,
    g.Bolum,
    g.Agirlik,
    COUNT(h.ID)                                        AS ToplamGorev,
    SUM(CASE WHEN h.OrtakMi  = 1 THEN 1 ELSE 0 END)    AS OrtakDersGorevi,
    SUM(CASE WHEN h.DestekMi = 1 THEN 1 ELSE 0 END)    AS DestekGorevi,
    -- Ağırlıklı yük: adalet karşılaştırması bu kolon üzerinden yapılır
    CAST(COUNT(h.ID) / NULLIF(g.Agirlik, 0) AS FLOAT)  AS AgirlikliYuk
FROM dbo.Gozetmenler g
LEFT JOIN dbo.GozetmenGorevGecmisi h ON h.GozetmenID = g.ID
GROUP BY g.ID, g.Kod, g.AdSoyad, g.Bolum, g.Agirlik;
GO

-- ── 4) BÖLÜM KAPASİTE GÖRÜNÜMÜ (Destek/Yedek kuralı için) ──────────
-- Ortalama yükü EN DÜŞÜK bölüm, destek çağrılırken önceliklidir.
IF OBJECT_ID('dbo.vw_BolumGozetmenKapasitesi', 'V') IS NOT NULL
    DROP VIEW dbo.vw_BolumGozetmenKapasitesi;
GO
CREATE VIEW dbo.vw_BolumGozetmenKapasitesi AS
SELECT
    g.Bolum,
    COUNT(DISTINCT g.ID)                               AS GozetmenSayisi,
    SUM(g.Agirlik)                                     AS ToplamKapasite,
    COUNT(h.ID)                                        AS ToplamGorev,
    CAST(COUNT(h.ID) / NULLIF(SUM(g.Agirlik), 0) AS FLOAT) AS OrtalamaYuk
FROM dbo.Gozetmenler g
LEFT JOIN dbo.GozetmenGorevGecmisi h ON h.GozetmenID = g.ID
WHERE g.AktifMi = 1
GROUP BY g.Bolum;
GO
