# Sınav Programı — Çalıştırma ve Dağıtım

Uygulama **tamamen taşınabilirdir**: hedef bilgisayarda .NET, SQL Server veya
başka hiçbir şeyin kurulu olmasına gerek yoktur. Veritabanı, exe'nin yanındaki
tek bir `SinavDB.db` dosyasıdır.

Arayüz, Windows'taki **Microsoft Edge WebView2** bileşeniyle çizilir. WebView2
Windows 11'de hazır gelir; Windows 10'da Edge ile birlikte kurulur. Yoksa uygulama
açılışta bunu söyler ve indirme sayfasını önerir (bkz. Bölüm 2, Adım 4).

---

## BÖLÜM 1 — Senin bilgisayarında paketi hazırlama

Bunu **her kod değişikliğinden sonra** tekrar yapman gerekir; aksi halde
paket eski sürümü taşır.

### Adım 1 — Uygulama açıksa kapat

Sınav Programı penceresi açıksa kapat. Açıkken paketleme yapılamaz
(betik zaten uyarı verip durur).

### Adım 2 — `yayinla.bat` dosyasına çift tıkla

Proje klasöründe duran `yayinla.bat` dosyasına çift tıkla.
Siyah bir pencere açılır ve şunları sırayla yapar:

1. `.NET SDK` kurulu mu diye bakar
2. `Yayin\` klasöründe eski bir `SinavDB.db` varsa **yedekler**
   (proje klasörüne `SinavDB-yedek-tarih.db` adıyla)
3. Uygulamayı `Yayin\` klasörüne **self-contained** derler
   (yani .NET çalışma zamanı paketin içine gömülür; arayüz dosyaları `wwwroot\`
   ve `_content\` olarak yanına konur, internetten hiçbir şey indirilmez)
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
| `.NET SDK bulunamadı` | https://dotnet.microsoft.com/download adresinden .NET 10 SDK kur |
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

- `SinavDB.db` otomatik oluşturulur, tablolar kurulur
- Pencere tam ekran açılır, **Genel Bakış** sayfası gelir

Program hiçbir derslik listesiyle gelmez. Genel Bakış'taki **"Neler eksik?"**
kutusu ve sol menüdeki adımlar sırada ne olduğunu gösterir; okulunun derslik
Excel'ini yükleyince 2. adım tamamlanır (bkz. Bölüm 4).

### Adım 4 — WebView2 uyarısı çıkarsa

"Bu bilgisayarda Microsoft Edge WebView2 bulunamadı" mesajı çıkarsa **Evet**'e bas;
Microsoft'un sayfası açılır. Oradan **Evergreen Bootstrapper**'ı indirip kur,
sonra programı yeniden aç. (Windows 11'de ve Edge kurulu Windows 10'da bu mesaj çıkmaz.)

### Adım 5 — Windows SmartScreen uyarısı çıkarsa

İmzasız exe olduğu için Windows "Bilgisayarınız korundu" diyebilir.
**Ek bilgi** → **Yine de çalıştır** de. Bu bir kez sorulur.

### Adım 6 — Kısayol oluştur (isteğe bağlı)

`SinavProgrami.exe` → sağ tık → **Kısayol oluştur** → kısayolu masaüstüne taşı.
Görev çubuğuna sabitlemek için kısayola sağ tık → *Görev çubuğuna sabitle*.

---

## BÖLÜM 3 — Program içindeki iş akışı

Sol menü iş akışının kendisidir: her adımın yanında durumu görünür
(✓ tamamlandı, numara = sıradaki / bekliyor, kilit = önceki adım bitmeden açılmaz;
kilidin üzerine gelince nedeni yazar). Adımlar sırayla:

1. **Dönem ve Takvim** — öğretim yılı, dönem (Güz / Bahar), sınav türü, sınav günleri ve saatleri
2. **Derslikler** — okulun derslik Excel'i (bir kez yeterli, bkz. Bölüm 4)
3. **Dersler** — fakülteden gelen ham ders (sınav) listesi Excel'i; sekmelerde ders listesi,
   uygulamalı dersler, hata oranı, ortak dersler ve havuzlar
4. **Şablon** — **Otomatik şablon oluştur** → Excel'de kontrol et / düzelt → **Şablonu geri yükle**;
   yüklenen şablon ızgarada da düzenlenebilir, kural ihlalleri kırmızı gösterilir
5. **Salonlar** — **Salonları ata** → **Salonlu Excel çıkar** → kontrol et → **Salonlu Excel yükle**;
   salon matrisinde elle düzeltme yapılabilir
6. **Gözetmenler** — gözetmen listesini yükle (tabloda düzeltilebilir) → **Gözetmenleri ata**
7. **Final** — **Gözetmenli taslak çıkar** → düzenle → **Taslağı geri yükle** → **Final programını çıkar**

Şablon, Salonlar ve Final sayfalarındaki **Excel kartı** aynı düzendedir: 1) oluştur,
2) son dosyayı Excel'de aç / klasörde göster, 3) geri yükle.

Tablolar Excel gibi kullanılır: hücreye tıkla / oklarla gez, **Enter**, **F2** ya da çift
tıklamayla düzenle, **Esc** ile vazgeç, **Delete** ile sil; **Ctrl + fare tekerleği** ile
yakınlaştır. Ders ve gözetmen tablolarında arama kutusu vardır.

### Sınav dönemi tipi

| Tip | Varsayılan gün | Hafta sonu |
|---|---|---|
| **Vize** | 7 | Sınav günü sayılır (her gün) |
| **Final** | 10 | Boş bırakılır (yalnızca Pazartesi–Cuma) |
| **Bütünleme** | 5 | Sınav günü sayılır (hiçbir gün kısıtı yok) |
| **Tek Ders** | 1 | Sınav günü sayılır (hiçbir gün kısıtı yok) |

Gün sayısında **kısıt yoktur** — vize 10-15 gün de sürebilir. **1 · Dönem ve Takvim**
sayfasında tipi seçince **Gün** kutusuna varsayılan yazılır; istediğin sayıyı (1–60)
girip **Takvimi doldur** dediğinde takvim o kadar gün işaretlenir (önce hafta özetiyle
onay sorulur). Takvimde güne tıklayarak tek tek gün ekleyip çıkarmak ve her günün
saatlerini seçmek serbesttir. Sınav türü gün sayısından tahmin edilmez: 10 günlük bir
vize yine VİZE başlığıyla çıkar.

### Öğretim yılı ve dönem (zorunlu)

**1 · Dönem ve Takvim** sayfasının en üstünde **Öğretim yılı** (ör. 2026/2027; listeden
seçilir ya da yazılır) ve **Dönem** (Güz / Bahar) seçilir. İkisi de boş gelir; seçilmeden
otomatik şablon ve Excel çıktıları çalışmaz. Final / gözetmenli Excel'in başlıkları bu
seçimden oluşur, örn. **"2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI"**
(VİZE / FİNAL / BÜTÜNLEME / TEK DERS kısmı *Sınav türü* seçiminden gelir). Seçim
uygulama kapatılıp açılınca korunur; **Ayarlar → Uygulamayı sıfırla** temizler.

---

## BÖLÜM 4 — Derslik listesi

Derslikler her okulda farklı olduğu için program kendi listesiyle gelmez ve
derslikleri **veritabanında tutmaz**. **2 · Derslikler → Derslik listesi yükle** ile
okulun derslik Excel'i yüklenir. Beklenen düzen:

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
- **Uygulamayı sıfırla** derslik listesini de siler.

Derslik listesi yüklenmeden *Otomatik şablon*, *Salonları ata* ve Excel çıktıları
çalışmaz; program önce listeyi yüklemeni ister.

---

## BÖLÜM 5 — Önemli notlar

**Çalışma verileri.** Ders listesi, takvim, derslikler, ayarlar, atamalar ve
gözetmenler `%AppData%\SinavProgrami\oturum.json` dosyasında saklanır; uygulama
15 saniyede bir ve kapanırken kaydeder, açılınca kaldığın yerden devam eder.
Tema ve tablo yakınlaştırma gibi tercihler aynı klasördeki `ayarlar.json`'dadır
(**Uygulamayı sıfırla** bunları silmez). `SinavDB.db` derslik tutmaz.

**Tek pencere.** Aynı anda yalnızca bir Sınav Programı penceresi açılabilir; ikinciyi
açmaya çalışınca "zaten açık" uyarısı çıkar (iki pencere aynı kayda yazmasın diye).

**Templates klasörü.** `dagitim_sablonu.xlsx` exe'nin yanındaki `Templates\`
klasöründe durmak zorundadır; silinirse şablon üretimi hata verir.

**Excel'de açık dosya.** Yazılacak dosya Excel'de açıksa program "dosya başka bir
programda açık" der; Excel'de kapatıp tekrar dene.

**Pencere boyutu.** Pencere kenarından sürüklenerek büyütülebilir veya büyüt düğmesiyle
tam ekran yapılabilir; dar ekranda sayfalar kendini uydurur. Uygulama kapatıldığındaki
pencere boyutuyla (tam ekransa tam ekran) ve son açık sayfayla açılır.

**Hata günlüğü.** Beklenmeyen hatalar `%LocalAppData%\SinavProgrami\hata.log`
dosyasına yazılır (**Ayarlar → Hata günlüğü → Klasörü aç**). Bir sorun bildirirken bu
dosyayı ekleyebilirsin.

**Eski arayüz (yedek).** Önceki Windows Forms arayüzü geçiş süresince yedek olarak
duruyor: `SinavProgrami.exe --eski-arayuz` ile açılır ve aynı oturum kaydını kullanır
(ikisi aynı anda açılamaz). Kısayolla açmak için kısayolun *Hedef* alanının sonuna
` --eski-arayuz` ekle.

**Deneme kopyası.** `SinavProgrami.exe --deneme` ile açılınca uygulama gerçek
verine dokunmaz: `%AppData%\SinavProgrami\Deneme\` klasöründe, gerçek oturum ve hoca
eşlemesi dosyalarının kopyalarıyla çalışır (üst çubukta **Deneme verisi** rozeti görünür).

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
dotnet run                          # yeni arayüz, gerçek veri
dotnet run -- --deneme              # yeni arayüz, deneme kopyası
dotnet run -- --eski-arayuz         # eski arayüz
cd Testler && dotnet run            # testler (gerçek kayda dokunmaz)
```

Bu durumda veritabanı `bin\Debug\net10.0-windows\SinavDB.db` olarak oluşur.
`yayinla.bat` yalnızca dağıtılabilir sürüm üretmek içindir; geliştirme akışını
değiştirmez.
