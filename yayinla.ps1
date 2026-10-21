# ═══════════════════════════════════════════════════════════════
#  SINAV PROGRAMI — YAYINLAMA (PUBLISH)
#  ───────────────────────────────────────────────────────────────
#  Uygulamayı, .NET kurulu OLMAYAN bilgisayarlarda da çalışacak
#  şekilde "Yayin" klasörüne kurar ve masaüstüne kısayol oluşturur.
#
#  Çalıştırmak için: yayinla.bat dosyasına çift tıklayın.
# ═══════════════════════════════════════════════════════════════

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$proje    = Join-Path $PSScriptRoot 'SinavProgrami.csproj'
$cikis    = Join-Path $PSScriptRoot 'Yayin'
$exeAdi   = 'SinavProgrami.exe'
$kisayol  = 'Sınav Programı'

Write-Host ''
Write-Host '════════════════════════════════════════════' -ForegroundColor Cyan
Write-Host '  SINAV PROGRAMI — Yayınlanıyor...'          -ForegroundColor Cyan
Write-Host '  (ilk seferde birkaç dakika sürebilir)'      -ForegroundColor DarkGray
Write-Host '════════════════════════════════════════════' -ForegroundColor Cyan
Write-Host ''

# ── .NET SDK var mı? ──────────────────────────────────────────
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host '  [HATA] .NET SDK bulunamadı.' -ForegroundColor Red
    Write-Host '  https://dotnet.microsoft.com/download adresinden kurun.' -ForegroundColor Yellow
    exit 1
}

# ── Uygulama açıksa uyar (klasör silinemez) ───────────────────
$calisan = Get-Process -Name 'SinavProgrami' -ErrorAction SilentlyContinue
if ($calisan) {
    Write-Host '  [HATA] Sınav Programı şu anda AÇIK. Önce kapatın, sonra tekrar deneyin.' -ForegroundColor Red
    exit 1
}

# ── Eski veritabanını yedekle (klasör birazdan silinecek) ─────
$eskiDb = Join-Path $cikis 'SinavDB.db'
if (Test-Path $eskiDb) {
    $yedek = Join-Path $PSScriptRoot ("SinavDB-yedek-{0}.db" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Copy-Item $eskiDb $yedek -Force
    Write-Host "  Eski veritabanı yedeklendi: $yedek" -ForegroundColor Yellow
    Write-Host ''
}

# ── Eski çıktıyı temizle ──────────────────────────────────────
if (Test-Path $cikis) { Remove-Item $cikis -Recurse -Force }

# ── Yayınla ───────────────────────────────────────────────────
& dotnet publish $proje `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -o $cikis

if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host '  [HATA] Yayınlama başarısız oldu. Yukarıdaki mesajı kontrol edin.' -ForegroundColor Red
    exit 1
}

# ── Exe gerçekten üretildi mi? ────────────────────────────────
if (-not (Test-Path (Join-Path $cikis $exeAdi))) {
    Write-Host ''
    Write-Host "  [HATA] $exeAdi üretilemedi. Derleme çıktısını kontrol edin." -ForegroundColor Red
    exit 1
}

# ── Şablonun kopyalandığını garantile ─────────────────────────
# (Uygulama Templates\dagitim_sablonu.xlsx dosyasını exe'nin yanında arar.)
$sablonKlasor = Join-Path $cikis 'Templates'
if (-not (Test-Path $sablonKlasor)) { New-Item -ItemType Directory -Path $sablonKlasor | Out-Null }
Copy-Item (Join-Path $PSScriptRoot 'Templates\dagitim_sablonu.xlsx') $sablonKlasor -Force

# ── Gereksiz dosyaları temizle (boyut küçülsün) ───────────────
Get-ChildItem $cikis -Filter '*.pdb' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

# ── Masaüstüne kısayol oluştur ────────────────────────────────
$exeYolu  = Join-Path $cikis $exeAdi
$masaustu = [Environment]::GetFolderPath('Desktop')
$lnkYolu  = Join-Path $masaustu "$kisayol.lnk"

$ws  = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($lnkYolu)
$lnk.TargetPath       = $exeYolu
$lnk.WorkingDirectory = $cikis
$lnk.IconLocation     = "$exeYolu,0"
$lnk.Description      = 'Sınav Programı Oluşturma Uygulaması'
$lnk.Save()

# ── Taşınabilir ZIP paketi oluştur ────────────────────────────
# Başka bilgisayara götürülecek tek dosya budur.
$zipYolu = Join-Path $PSScriptRoot 'SinavProgrami-Portable.zip'
if (Test-Path $zipYolu) { Remove-Item $zipYolu -Force }

Write-Host ''
Write-Host '  ZIP paketi hazırlanıyor...' -ForegroundColor DarkGray
Compress-Archive -Path (Join-Path $cikis '*') -DestinationPath $zipYolu -CompressionLevel Optimal

# ── Özet ──────────────────────────────────────────────────────
$boyut    = [math]::Round(((Get-ChildItem $cikis -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
$zipBoyut = [math]::Round(((Get-Item $zipYolu).Length / 1MB), 1)

Write-Host ''
Write-Host '════════════════════════════════════════════' -ForegroundColor Green
Write-Host '  TAMAM!'                                      -ForegroundColor Green
Write-Host ''
Write-Host "  Uygulama klasörü : $cikis"
Write-Host "  Çalıştırmak için : $exeYolu"
Write-Host "  Masaüstünde      : `"$kisayol`" kısayolu oluşturuldu"
Write-Host "  Toplam boyut     : $boyut MB"
Write-Host ''
Write-Host "  TAŞINABİLİR PAKET: $zipYolu  ($zipBoyut MB)" -ForegroundColor Yellow
Write-Host ''
Write-Host '  Başka bilgisayara götürmek için SADECE bu ZIP dosyasını'  -ForegroundColor DarkGray
Write-Host '  kopyalayın. Karşı tarafta bir klasöre çıkarıp'            -ForegroundColor DarkGray
Write-Host '  SinavProgrami.exe dosyasına çift tıklamak yeterlidir —'   -ForegroundColor DarkGray
Write-Host '  .NET, SQL Server veya başka hiçbir kurulum gerekmez.'     -ForegroundColor DarkGray
Write-Host '════════════════════════════════════════════' -ForegroundColor Green
Write-Host ''
