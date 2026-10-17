-- ═══════════════════════════════════════════════════════════════
-- SALON TİPİ GÜNCELLEMESİ
-- Salonlar tablosuna Salon_Tipi kolonu ekler ve uygulamalı
-- salonları (lab, teknik resim sınıfı) işaretler.
-- Program bu kolonu okuyarak uygulamalı dersleri SADECE bu
-- salonlara, normal dersleri SADECE normal salonlara atar.
-- ═══════════════════════════════════════════════════════════════

-- 1) Kolonu ekle (yoksa)
IF COL_LENGTH('Salonlar', 'Salon_Tipi') IS NULL
    ALTER TABLE Salonlar ADD Salon_Tipi NVARCHAR(20) NOT NULL DEFAULT 'NORMAL';
GO

-- 2) Uygulamalı salonları işaretle
-- (Kendi lab/teknik resim salon kodlarına göre listeyi düzenle)
UPDATE Salonlar
SET Salon_Tipi = 'UYGULAMALI'
WHERE Salon_Kodu IN (
    'T4-283',  -- Bilgisayar Laboratuvarı T4
    'T4-289',  -- Bilgisayar Laboratuvarı T4
    'T1-184',  -- Bilgisayar Laboratuvarı T1
    'T1-185'   -- Teknik Resim Sınıfı T1
);
GO

-- 3) Kontrol: tipleri listele
SELECT Salon_Kodu, Kapasite, Salon_Tipi FROM Salonlar ORDER BY Salon_Tipi DESC, Salon_Kodu;

-- NOT: Daha önce tespit edilen çift kayıtları da temizlemek için
-- (aynı Salon_Kodu'ndan birden fazla varsa küçük ID'li kalır):
-- DELETE FROM Salonlar WHERE ID NOT IN (SELECT MIN(ID) FROM Salonlar GROUP BY Salon_Kodu);
