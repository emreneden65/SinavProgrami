# Sınav Programı — Çalıştırma ve Dağıtım

Uygulama **tamamen taşınabilirdir**: hedef bilgisayarda .NET, SQL Server veya
başka hiçbir şeyin kurulu olmasına gerek yoktur. Veritabanı, exe'nin yanındaki
tek bir `SinavDB.db` dosyasıdır.

---

## BÖLÜM 1 — Senin bilgisayarında paketi hazırlama

Bunu **her kod değişikliğinden sonra** tekrar yapman gerekir; aksi halde
paket eski sürümü taşır.

### Adım 1 — Uygulama açıksa kapat

Sınav Programı penceresi açıksa kapat. Açıkken paketleme yapılamaz
(betik zaten uyarı verip durur).

### Adım 2 — `yayinla.bat` dosyasına çift tıkla

Proje klasöründe (`Elfreni`) duran `yayinla.bat` dosyasına çift tıkla.
Siyah bir pencere açılır ve şunları sırayla yapar:

1. `.NET SDK` kurulu mu diye bakar
2. `Yayin\` klasöründe eski bir `SinavDB.db` varsa **yedekler**
   (proje klasörüne `SinavDB-yedek-tarih.db` adıyla)
3. Uygulamayı `Yayin\` klasörüne **self-contained** derler
   (yani .NET çalışma zamanı paketin içine gömülür)
4. `Templates\dagitim_sablonu.xlsx` dosyasını yanına kopyalar
5. Masaüstünde **"Sınav Programı"** kısayolu oluşturur
6. Her şeyi **`SinavProgrami-Portable.zip`** içinde paketler

İlk çalıştırma birkaç dakika sürer (NuGet paketleri indirilir). Sonraki
çalıştırmalar çok daha hızlıdır.

### Adım 3 — "TAMAM!" yazısını gör

Pencerede yeşil **TAMAM!** yazısı ve ZIP dosyasının yolu görünmelidir.
Kırmızı `[HATA]` görürsen mesajı oku — genellikle üç şeyden biridir:

| Hata | Çözüm |
|---|---|
| `.NET SDK bulunamadı` | https://dotnet.microsoft.com/download adresinden .NET 8 SDK kur |
| `Sınav Programı şu anda AÇIK` | Uygulamayı kapat, tekrar dene |
| Derleme hataları | Kod hatası var; `dotnet build` çıktısına bak |

---

## BÖLÜM 2 — Dış bilgisayarda çalıştırma

### Adım 1 — ZIP'i taşı

Proje klasöründeki **`SinavProgrami-Portable.zip`** dosyasını USB, e-posta veya
OneDrive ile hedef bilgisayara kopyala. Taşınacak **tek dosya budur.**

### Adım 2 — Doğru yere çıkar

ZIP'e sağ tıkla → **Tümünü ayıkla**. Hedef klasör olarak şunlardan birini seç:

- `C:\SinavProgrami`
- Masaüstü
- Belgeler

> ⚠️ **`C:\Program Files` altına ÇIKARMA.** Windows oraya yazma izni vermez,
> uygulama veritabanını oluşturamaz ve açılışta hata verir.

> ⚠️ ZIP'in **içinden** çift tıklayarak çalıştırma. Önce mutlaka ayıkla.

### Adım 3 — `SinavProgrami.exe` dosyasına çift tıkla

İlk açılışta:

- `SinavDB.db` otomatik oluşturulur
- Tablolar kurulur

Program hiçbir derslik listesiyle gelmez. Sol altta sarı
**"⚠️ Derslik listesi yüklenmedi"** yazısı görünür; bu normaldir —
okulunun derslik Excel'ini yükleyince yeşile döner (bkz. Bölüm 4).

### Adım 4 — Windows SmartScreen uyarısı çıkarsa

İmzasız exe olduğu için Windows "Bilgisayarınız korundu" diyebilir.
**Ek bilgi** → **Yine de çalıştır** de. Bu bir kez sorulur.

### Adım 5 — Kısayol oluştur (isteğe bağlı)

`SinavProgrami.exe` → sağ tık → **Kısayol oluştur** → kısayolu masaüstüne taşı.
Görev çubuğuna sabitlemek için kısayola sağ tık → *Görev çubuğuna sabitle*.

---

## BÖLÜM 3 — Program içindeki iş akışı

Butonlar yukarıdan aşağıya sırayla kullanılır:

0. **Derslik Listesi Yükle** — okulun derslik Excel'i (bir kez yeterli, bkz. Bölüm 4)
1. **Sınav Listesi Yükle** — fakülteden gelen ham ders listesi Excel'i
2. **Otomatik Şablon** — dersleri günlere/saatlere dağıtan şablon Excel'i üretir
3. Şablon Excel'ini aç, gerekiyorsa elle düzelt, kaydet
4. **Şablonu Geri Yükle** — az önce düzenlediğin şablonu geri yükle
5. **Salonları Ata** → **Salonlu Excel Çıkart**
6. Çıkan dosyayı kontrol et, gerekiyorsa düzelt
7. **Gözetmen Yükle (Excel)** — araştırma görevlisi listesi
8. **Salonlu Excel Yükle** — salon atanmış dosyayı geri yükle
9. **Gözetmen Ata** → **Genel Dosya Çıkart** (nihai çıktı)

### Sınav dönemi tipi

| Tip | Varsayılan gün | Hafta sonu |
|---|---|---|
| **Vize** | 7 | Sınav günü sayılır (her gün) |
| **Final** | 10 | Boş bırakılır (yalnızca Pazartesi–Cuma) |
| **Bütünleme** | 5 | Sınav günü sayılır (hiçbir gün kısıtı yok) |
| **Tek Ders** | 1 | Sınav günü sayılır (hiçbir gün kısıtı yok) |

Gün sayısında **kısıt yoktur** — vize 10-15 gün de sürebilir. *"Sınav Takvimi ve
Saat Ayarları"* bölümünden tipi seçince **Gün** kutusuna varsayılan yazılır; istediğin
sayıyı (1–60) girip **Takvimi Doldur** dediğinde takvim o kadar gün işaretlenir.
Takvimden tek tek gün ekleyip çıkarmak da serbesttir. Sınav türü gün sayısından
tahmin edilmez: 10 günlük bir vize yine VİZE başlığıyla çıkar.

### Öğretim yılı ve dönem (zorunlu)

Aynı bölümün en üst satırında **Öğretim Yılı** (ör. 2026/2027) ve **Dönem**
(Güz / Bahar) seçilir. İkisi de boş gelir; seçilmeden Şablon Oluştur, Otomatik
Dağıt ve Excel çıktıları çalışmaz. Final / gözetmenli Excel'in başlıkları bu
seçimden oluşur, örn. **"2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI"**
(VİZE / FİNAL / BÜTÜNLEME / TEK DERS kısmı *Sınav Türü* seçiminden gelir). Seçim uygulama kapatılıp
açılınca korunur; **Uygulamayı Sıfırla** temizler.

---

## BÖLÜM 4 — Derslik listesi

Derslikler her okulda farklı olduğu için program kendi listesiyle gelmez ve
derslikleri **veritabanında tutmaz**. Sağ sütundaki **0 · DERSLİKLER →
🏫 Derslik Listesi Yükle** ile okulun derslik Excel'i yüklenir. Beklenen düzen:

| DERSLİK | SINAV DÜZEYİNDEKİ ÖĞR. SAYISI | DERS DÜZEYİNDEKİ ÖĞR. SAYISI |
|---|---|---|
| T1-107 | 62(31) | 155 |
| *TEKNİK RESİM SINIFLARI* (birleşik başlık satırı) | | |
| T1-105 | 52 | 52 |

- Kapasite, "SINAV DÜZEYİNDEKİ" sütunundaki ilk sayıdır (`62(31)` → 62).
- "LABORATUVAR", "TEKNİK RESİM", "ÇİZİM" gibi birleşik başlıkların altındaki derslikler
  **uygulamalı** sayılır. Uygulamalı dersler yalnızca bu dersliklere, diğerleri yalnızca
  normal dersliklere atanır.
- Liste bir kez yüklenince uygulama kapatılıp açılsa da korunur (oturum kaydında durur).
  Değiştirmek için dosyayı yeniden yüklemen yeterli; program eklenen / silinen /
  değişen derslikleri gösterip onay ister.
- **Uygulamayı Sıfırla** derslik listesini de siler.

Derslik listesi yüklenmeden *Şablon Oluştur*, *Otomatik Dağıt*, *Salonları Ata* ve
Excel çıktıları çalışmaz; program önce listeyi yüklemeni ister.

---

## BÖLÜM 5 — Önemli notlar

**Çalışma verileri.** Ders listesi, takvim, derslikler, ayarlar, atamalar ve
gözetmenler `%AppData%\SinavProgrami\oturum.json` dosyasında saklanır; uygulama
kapatılıp açılınca kaldığın yerden devam eder. `SinavDB.db` derslik tutmaz.

**Templates klasörü.** `dagitim_sablonu.xlsx` exe'nin yanındaki `Templates\`
klasöründe durmak zorundadır; silinirse şablon üretimi hata verir.

**Pencere boyutu.** Pencere kenarından sürüklenerek büyütülebilir veya sağ üstteki
büyüt düğmesiyle tam ekran yapılabilir. Tam ekranda sonuç paneli (Şablon / Sonuçlar /
Matris tabloları) otomatik açılır ve ekranın geri kalanını doldurur; normal boyuta
dönünce yine kapanır. Uygulama kapatıldığındaki pencere boyutuyla (tam ekransa tam
ekran) açılır. Sol paneldeki bir tabloyu büyük görmek istersen bölüm başlığının
sağındaki **⤢** düğmesine bas — ayrı, boyutlandırılabilir pencerede açılır.

**Kod değiştirdiğinde.** Yeni sürümü almak için `yayinla.bat` dosyasını tekrar
çalıştır. `Yayin` klasörü silinip yeniden oluşturulur; içindeki `SinavDB.db`
otomatik olarak proje klasörüne yedeklenir.

**Eski SQL betikleri.** `Gozetmen_Tablolari.sql` ve `Salon_Tipi_Guncelleme.sql`
artık kullanılmıyor; şema uygulama tarafından otomatik kuruluyor. Yalnızca
referans olarak duruyorlar (salon tablosu artık hiç oluşturulmuyor).

---

## BÖLÜM 6 — Geliştirme sırasında

Kod üzerinde çalışırken:

```
dotnet run
```

Bu durumda veritabanı `bin\Debug\net8.0-windows\SinavDB.db` olarak oluşur.
`yayinla.bat` yalnızca dağıtılabilir sürüm üretmek içindir; geliştirme akışını
değiştirmez.
