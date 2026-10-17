# ═══════════════════════════════════════════════════════════════
#  MEVCUT SQL SERVER VERİSİNİ DIŞARI AKTAR
#  ───────────────────────────────────────────────────────────────
#  Eski SinavDB (SQL Server) veritabanındaki Salonlar tablosunu
#  "Baslangic\Salonlar.csv" dosyasına yazar.
#
#  Uygulama artık SQLite kullanıyor; bu CSV, yeni veritabanı ilk
#  kez oluşturulurken otomatik olarak içeri alınır.
#
#  BU BETİĞİ YALNIZCA SQL SERVER'IN KURULU OLDUĞU (eski) BİLGİSAYARDA
#  VE YALNIZCA BİR KEZ ÇALIŞTIRMANIZ YETERLİDİR.
#
#  Çalıştırmak için: verileri_disari_aktar.bat dosyasına çift tıklayın.
# ═══════════════════════════════════════════════════════════════

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Eski SQL Server bağlantınız — farklıysa burayı düzenleyin
$baglanti = 'Server=.\SQLEXPRESS;Database=SinavDB;Trusted_Connection=True;TrustServerCertificate=True;'

$hedefKlasor = Join-Path $PSScriptRoot 'Baslangic'
$hedefDosya  = Join-Path $hedefKlasor 'Salonlar.csv'

Write-Host ''
Write-Host '════════════════════════════════════════════' -ForegroundColor Cyan
Write-Host '  SALON LİSTESİ DIŞARI AKTARILIYOR...'        -ForegroundColor Cyan
Write-Host '════════════════════════════════════════════' -ForegroundColor Cyan
Write-Host ''

try {
    Add-Type -AssemblyName System.Data

    $conn = New-Object System.Data.SqlClient.SqlConnection $baglanti
    $conn.Open()

    # Salon_Tipi kolonu eski şemada olmayabilir — varlığını kontrol et
    $kontrol = $conn.CreateCommand()
    $kontrol.CommandText = "SELECT COL_LENGTH('Salonlar','Salon_Tipi')"
    $tipVar = $kontrol.ExecuteScalar()

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = if ($null -ne $tipVar -and $tipVar -isnot [System.DBNull]) {
        "SELECT Salon_Kodu, Kapasite, COALESCE(Salon_Tipi,'NORMAL') AS Salon_Tipi FROM Salonlar ORDER BY Salon_Kodu"
    } else {
        "SELECT Salon_Kodu, Kapasite, 'NORMAL' AS Salon_Tipi FROM Salonlar ORDER BY Salon_Kodu"
    }

    $satirlar = New-Object System.Collections.Generic.List[string]
    $satirlar.Add('Salon_Kodu;Kapasite;Salon_Tipi')   # başlık satırı

    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $kod = [string]$reader['Salon_Kodu']
        $kap = [int]$reader['Kapasite']
        $tip = [string]$reader['Salon_Tipi']
        $satirlar.Add("$kod;$kap;$tip")
    }
    $reader.Close()
    $conn.Close()

    if (-not (Test-Path $hedefKlasor)) { New-Item -ItemType Directory -Path $hedefKlasor | Out-Null }

    # BOM'suz UTF-8 — Türkçe karakterler doğru okunur
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($hedefDosya, $satirlar, $utf8)

    $adet = $satirlar.Count - 1
    Write-Host "  $adet salon aktarıldı." -ForegroundColor Green
    Write-Host "  Dosya: $hedefDosya"
    Write-Host ''
    Write-Host '  Şimdi yayinla.bat dosyasını çalıştırın; bu CSV, yeni'  -ForegroundColor DarkGray
    Write-Host '  SinavDB.db oluşturulurken otomatik olarak yüklenecek.' -ForegroundColor DarkGray
    Write-Host ''
}
catch {
    Write-Host ''
    Write-Host "  [HATA] $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ''
    Write-Host '  SQL Server bu bilgisayarda kurulu değilse bu betiğe gerek yok.' -ForegroundColor Yellow
    Write-Host '  Baslangic\Salonlar.csv dosyasını Excel/Not Defteri ile elle'   -ForegroundColor Yellow
    Write-Host '  doldurabilirsiniz. Biçim:  Salon_Kodu;Kapasite;Salon_Tipi'     -ForegroundColor Yellow
    Write-Host ''
    exit 1
}
