# Sınav Programı — Proje Anlatımı

> Bu doküman, yarıda bırakılmış olan **Sınav Programı** (Teknoloji Fakültesi) projesini
> devralacak kişinin projeyi hızlıca anlaması için yazılmıştır. Kodun ne yaptığını,
> nasıl organize edildiğini, verinin nasıl aktığını ve **nelerin eksik / yarım kaldığını**
> anlatır. Kurulum ve paketleme adımları için ayrıca [NASIL_CALISTIRILIR.md](NASIL_CALISTIRILIR.md)
> dosyasına bakın.

---

## 1. Proje Ne İşe Yarıyor?

Fakültenin **vize / final sınav programını** hazırlayan bir Windows masaüstü uygulamasıdır.
Üç ana işi yapar:

1. **Takvime yerleştirme** — Hangi dersin sınavı hangi gün ve hangi saatte olacak?
2. **Salon ataması** — Her sınavın öğrencileri hangi salonlara, kaçar kişi dağıtılacak?
3. **Gözetmen ataması** — Her salona hangi araştırma görevlisi gözetmen olarak gidecek?

Uygulama "Excel gidiş-dönüş" mantığıyla çalışır: her aşamanın sonunda bir Excel dosyası
üretir, kullanıcı bu dosyayı Excel'de açıp **elle düzeltebilir**, sonra düzeltilmiş dosyayı
geri yükleyip bir sonraki aşamaya geçer. Yani algoritma "öneri" üretir, son söz kullanıcıdadır.

Bölümler (kodda sabit): **BİL** (Bilgisayar), **EEM/ELM** (Elektrik-Elektronik), **INS/İNŞ**
(İnşaat), **MAK** (Makine), **MEK** (Mekatronik), **MEM** (Metalurji ve Malzeme).
Binalar: **T1** ve **T4** (salon kodları `T1-xxx`, `T4-xxx`).

---

## 2. Teknoloji

| Bileşen | Detay |
|---|---|
| Dil / Platform | C#, **.NET 8**, **Windows Forms** (`net8.0-windows`) |
| Excel okuma/yazma | **EPPlus 7.7.0** (NonCommercial lisans modunda) |
| Veritabanı | **SQLite** (`Microsoft.Data.Sqlite`) — exe'nin yanında tek dosya: `SinavDB.db` |
| Dağıtım | Self-contained publish → taşınabilir ZIP (`yayinla.bat` / `yayinla.ps1`) |
| Test | **Yok** (test projesi bulunmuyor) |
| Versiyon kontrolü | Klasörde `.gitignore` var ama **git deposu başlatılmamış** |

> Not: Proje başlangıçta **SQL Server** kullanıyordu, sonra **SQLite**'a taşındı.
> Kökteki `.sql` dosyaları bu geçişten kalmadır.

Proje şu an **hatasız derleniyor** (`dotnet build` → 0 hata, 0 uyarı).

---

## 3. Klasör Yapısı

```
├── Program.cs                     Giriş noktası: EPPlus lisansı + DB hazırlama + MainForm
├── MainForm.cs                    ~3800 satır — TÜM arayüz mantığı ve iş akışı burada
├── MainForm.Designer.cs           WinForms tasarımcı kodu
├── SinavProgrami.csproj
│
├── Models/                        Veri modelleri
│   ├── Ders.cs                    Bir dersin bir şubesi (bölüm, sınıf, kod, kontenjan, hoca)
│   ├── Salon.cs                   Salon (kod, kapasite, NORMAL/UYGULAMALI)
│   ├── SinavAtama.cs              Bir sınavın gün/saat/salon(lar) ataması + SalonAtama
│   ├── Gozetmen.cs                Gözetmen (ad, bölüm, ağırlık, atanan sayı, skor)
│   ├── DersHavuzu.cs              Farklı bölüm/şubeleri tek salonda birleştirme tanımı
│   ├── SinavTipi.cs               Vize (7 gün) / Final (10 gün) takvim kuralları
│   ├── SubeEtiketi.cs             Şube normalizasyonu (INT→D, 1→A, 2→B …)
│   └── KayitliDers.cs             Eski "KayitliDersler" tablosu modeli (artık kullanılmıyor)
│
├── Algorithm/                     Algoritmalar
│   ├── SablonDagitimAlgoritmasi.cs   Dersleri gün+saat slotlarına otomatik yerleştirir
│   ├── SablonDogrulayici.cs          Şablondaki kural ihlallerini bulur (uyarı amaçlı)
│   ├── GunlukSalonDagitici.cs        Şablondaki dersleri salonlara dağıtır
│   ├── GozetmenAtamaAlgoritmasi.cs   Salonlara gözetmen atar + yük dengeler
│   └── SinavDagitimAlgoritma.cs      ESKİ tek-adımlı algoritma (KULLANILMIYOR)
│
├── DataAccess/                    Excel ve veritabanı katmanı
│   ├── DatabaseHelper.cs             SQLite şema kurulumu (derslik tutmaz)
│   ├── SinavListesiOkuyucu.cs        Adım 1: fakültenin ham ders listesi Excel'i
│   ├── CekilmisDerslerExportHelper.cs Adım 2: şablon Excel'ini üretir
│   ├── DagitimSablonuOkuyucu.cs      Adım 3: doldurulmuş şablonu geri okur
│   ├── GunSalonMatrisExportHelper.cs Adım 4: salon atanmış gün sayfaları Excel'i
│   ├── GunSalonMatrisOkuyucu.cs      Adım 5: salon atanmış Excel'i geri okur
│   ├── ExcelImportHelper.cs          Gözetmen listesi Excel'i okuma (+ eski ders import)
│   ├── ExcelMatrisExportHelper.cs    Nihai "Genel Dosya" çıktısı (çok sayfalı)
│   └── HazirSinavExcelOkuyucu.cs     ESKİ "BAHAR FİNAL" okuyucu (KULLANILMIYOR)
│
├── Templates/dagitim_sablonu.xlsx Şablon üretiminde temel alınan boş Excel (zorunlu)
├── Resources/app.ico
│
├── yayinla.bat / yayinla.ps1      Taşınabilir ZIP paketi üretir
├── Gozetmen_Tablolari.sql         Eski SQL Server şeması (sadece referans)
├── Salon_Tipi_Guncelleme.sql      Eski SQL Server betiği (sadece referans)
└── NASIL_CALISTIRILIR.md          Kurulum / paketleme / kullanım kılavuzu
```

---

## 4. Uygulamanın İş Akışı (Adım Adım)

Arayüzdeki butonlar adım kilitleriyle sıralı açılır (bir adım bitmeden sonraki aktif olmaz).

```
 [Sınav Listesi Excel]                                [Gözetmen Excel]
        │                                                    │
   1) Sınav Listesi Yükle ──► _dersListesi                    │
        │                                                    │
   2) Şablon Oluştur (boş)  ─┐                               │
   2+) Otomatik Dağıt ───────┴─► Şablon .xlsx  ──(elle düzelt)
        │                                                    │
   3) Salon Ataması İçin Dosya Yükle ──► _sablonSonuclari    │
        │      (uygulama içinde grid'de de düzenlenebilir)   │
   4) Salonları Ata ──► _gunSalonSatirlari / _sonSonuclar    │
        │                                                    │
      Salon Atanmış Dosya Çıkart ──► .xlsx ──(elle düzelt)   │
        │                                                    │
   5) Gözetmenler için Dosya Yükle ──► _sonSonuclar     Gözetmen Yükle
        │                                                    │
   6) Gözetmen Ata ◄─────────────────────────────────────────┘
        │
      Genel Dosya Çıkart ──► Nihai çok sayfalı Excel
```

### Adım 1 — Sınav Listesi Yükle
- **Sınıf:** `SinavListesiOkuyucu`
- Fakültenin ham ders listesini okur (her satır bir şube). Beklenen sütunlar:
  *Bölüm | Ünvanı Adı Soyadı | Öğretim Türü | DERSİN KODU | VERİLEN DERSİN ADI | Dersin Şubesi | Öğrenci Sayısı*.
- Başlıklar esnek eşleştirilir (Türkçe karakter/boşluk farkları tolere edilir).
- Sınıf seviyesi ders kodunun ilk rakamından türetilir (`BIL214` → 2. sınıf).
- Hoca bilgileri `%AppData%\SinavProgrami\hoca_eslemesi.json` dosyasına **kalıcı** kaydedilir
  (sonraki çıktılarda "Öğretim Elemanı" sütunu buradan doldurulur).

### Adım 2 — Şablon Oluştur / Otomatik Dağıt
- **Sınıflar:** `CekilmisDerslerExportHelper`, `SablonDagitimAlgoritmasi`
- Önce sağdaki **Sınav Takvimi** panelinden günler/saatler seçilmelidir
  (Vize/Final tipine göre "Takvimi Doldur" otomatik işaretler).
- **Şablon Oluştur (boş):** `Templates\dagitim_sablonu.xlsx`'i kopyalayıp
  "Çekilmiş Dersler" sayfasını doldurur; "Sınav Haftası Dağıtım Şablonu" sayfası boş takvimdir,
  kullanıcı dersleri elle yerleştirir.
- **Otomatik Dağıt:** Aynı dosyayı üretir ama dersleri kurallara göre takvime de yerleştirir.

**Otomatik dağıtım kuralları (`SablonDagitimAlgoritmasi`):**
1. Ortak dersler (kodu bölüm önekiyle — BIL/ELM/INS/MAK/MEK/MEM — başlamayanlar; ör. FIZ, MAT, TKN)
   önce yerleştirilir ve tüm bölümlerde **aynı gün+saatte** olur.
2. Ortak ders tek oturumda normal salonlara sığmıyorsa ELM+MEK kısmı ayrı bir slota alınır.
3. Bir slotta bir bölümün en fazla bir sınavı olur.
4. **Saat–sınıf tercihi (esnek):** 09:00→3. sınıf, 11:00→1. sınıf, 13:00→4. ve 0. sınıf,
   15:00→2. sınıf. Yer yoksa 17:00 dahil diğer saatlere taşar.
5. Günlük sınav limiti, slot kapasitesi ve gün dengesi gözetilir.
6. Bölüm bazlı **hata oranı** (% gelmeyecek öğrenci) kontenjandan düşülerek "efektif kontenjan"
   ile çalışılır.

### Adım 3 — Salon Ataması İçin Dosya Yükle
- **Sınıflar:** `DagitimSablonuOkuyucu`, `SablonDogrulayici`
- Doldurulmuş şablonun "Sınav Haftası Dağıtım Şablonu" sayfası okunur → takvim + salonsuz yerleşim.
- Takvimin gün sayısına göre Vize/Final tipi otomatik tahmin edilir.
- Yerleşim uygulama içinde **Şablon** sekmesindeki grid'de gösterilir ve **burada da düzenlenebilir**
  (ders seçme, autocomplete, Delete ile silme, Ctrl+tekerlek zoom).
- `SablonDogrulayici` her düzenlemeden sonra kural ihlallerini (bölüm+sınıf çakışması, ortak ders
  bütünlüğü, sınıf-saat kuralı, dinlenme molası, slot kapasite aşımı) bulup hücreleri boyar.
  İhlaller **engelleyici değildir**, sadece uyarıdır.

### Adım 4 — Salonları Ata + Salon Atanmış Dosya Çıkart
- **Sınıflar:** `GunlukSalonDagitici`, `GunSalonMatrisExportHelper`
- Kurallar:
  1. **Şube izolasyonu:** Farklı şubeler aynı salona karıştırılmaz.
  2. **Havuz istisnası:** Kullanıcının "Havuz / Ortak" penceresinde tanımladığı şube grupları tek
     kontenjan gibi birleştirilip mümkünse tek salona konur.
  3. Ortak dersler önce işlenir.
  4. **ELM + MEK** sınavları önce **T4** binasına, diğer bölümler önce T4 dışına yerleştirilir.
  5. **Uygulamalı** işaretli dersler sadece `UYGULAMALI` salonlara, diğerleri sadece `NORMAL`
     salonlara gider.
  6. Bir slotta bir salon tek gruba verilir.
- Çıktı Excel'inde her sınav günü ayrı sayfa (sekme adı `GG.AA.YYYY`): satırlar dersler,
  sütunlar salonlar, hücreler öğrenci sayısı.

### Adım 5 — Gözetmenler için Dosya Yükle
- **Sınıf:** `GunSalonMatrisOkuyucu`
- Kullanıcının elle düzeltmiş olabileceği salon atanmış Excel'i geri okur.

### Adım 6 — Gözetmen Yükle + Gözetmen Ata + Genel Dosya Çıkart
- **Sınıflar:** `ExcelImportHelper.GozetmenleriOku`, `GozetmenAtamaAlgoritmasi`, `ExcelMatrisExportHelper`
- Gözetmen listesi iki formatı destekler: fakülte "GÖREVLER" listesi (Bölüm | Kod | Ad Soyad)
  veya basit liste (Ad Soyad | Bölüm). Her gözetmenin **Ağırlık** değeri grid'den düzenlenebilir.

**Gözetmen atama kuralları (`GozetmenAtamaAlgoritmasi`):**
- **Ağırlık (görev payı):** Her gözetmenin hedefi `Toplam Görev × Ağırlık / Toplam Ağırlık`.
  Ağırlığı 1,0 olan 14 sınava giriyorsa 0,5 olan 7 sınava girer. Yük her yerde ağırlıklıdır
  (`Skor = Atanan Sınav / Ağırlık`); ağırlıklar eşitse herkese eşit görev düşer.
- **Kesin kurallar:** Bir salona bir slotta tek gözetmen; bir gözetmen bir slotta tek salonda.
- **Bölüm kodlu dersler:** Önce dersin kendi bölümünden; yoksa ağırlıklı ortalama yükü en düşük
  bölümden "destek". Seçim: en düşük skor.
- **Ortak dersler:** En düşük skorlu hocalar grubundan; grup içinde bölüm eşleşmesi varsa o;
  T4'teki sınavda EEM/MEK hocası öne alınır; eşitlikte kalabalık bölüm tercih edilir.
- **Faz 2 – Yük dengeleme (swap):** Hocaların hedeften sapmaları (`Sınav − Hedef`) arasındaki fark
  ≤ 1 olana kadar görevler devredilir; gerekirse zincirleme aktarım, son çare olarak bölüm dışına
  devir (`SonCareCaprazDevir`). "Gözetmen Ata" sonunda ağırlığa göre dağılım özeti gösterilir.

**Genel Dosya** şu sayfaları içerir: *Sınav Programı Matrisi, Salon Doluluk Özeti, Slot Bazlı Sınav
Planı, Sınav Programı Formu, Sınav Programı Formu (Gözetmenli), Görev Çizelgesi*.

### Yan Paneller (sol taraf, açılır/kapanır "akordeon")
- **Ders Listesi** — yüklenen dersler, inline düzenleme, silme, arama.
- **Havuz / Ortak** — ortak dersleri **manuel** işaretleme (yapılırsa otomatik tespit devre dışı
  kalır) ve şube havuzları tanımlama.
- **Uygulamalı Dersler** — lab/çizim salonu gerektiren dersleri işaretleme
  (grid'in kod adı tarihsel olarak `dgvOverbooking`).
- **Hata Oranı** — bölüm başına % gelmeyecek öğrenci.
- **Sınav Takvimi** — Vize/Final tipi, başlangıç tarihi, gün ve saat seçimi
  (varsayılan saatler 09:00, 11:00, 13:00, 15:00, 17:00).
- **Gözetmenler** — gözetmen listesi ve ağırlıklar.

---

## 5. Veritabanı

`SinavDB.db` (SQLite), exe'nin yanında. İlk açılışta `DatabaseHelper.VeritabaniniHazirla()`
tabloları oluşturur.

| Tablo | Durum |
|---|---|
| `Salonlar` | **Yok.** Derslikler veritabanında tutulmaz; eski sürümlerden kalan tablo açılışta silinir |
| `Gozetmenler` | Oluşturuluyor ama **hiç yazılmıyor / okunmuyor** |
| `GozetmenGorevGecmisi` | Oluşturuluyor ama **hiç yazılmıyor / okunmuyor** |
| `KayitliDersler` | Sadece "Sıfırla" butonu siliyor; dersler artık Excel'den geliyor |
| `Dersler` | Oluşturuluyor, **hiç kullanılmıyor** |

**Derslikler:** Her okulun derslikleri farklı olduğu için program salon listesiyle gelmez.
Kullanıcı "🏫 Derslik Listesi Yükle" ile Excel'den yükler (`DerslikListesiOkuyucu`);
liste `MainForm._derslikler`'de tutulur ve oturum kaydında (`%AppData%\SinavProgrami\oturum.json`)
saklanır. Şablonun gün sayfalarındaki salon sütunları da bu listeden kurulur.

---

## 6. Eksikler ve Yarım Kalan İşler

Kod incelemesinden çıkan, projeyi devralan kişinin bilmesi gereken noktalar:

### 6.1 Özellik olarak yarım kalanlar

1. **Gözetmen görev geçmişi kaydedilmiyor.**
   `DatabaseHelper.GozetmenGorevleriniKaydet()` ve `GozetmenleriGetir()` yazılmış ama hiçbir yerden
   çağrılmıyor. Eski SQL tasarımında (`Gozetmen_Tablolari.sql`) hedef "dönem boyu adil dağılım"dı:
   vize + final boyunca kim kaç görev aldı, bir sonraki atamada dikkate alınacaktı. Şu an her
   "Gözetmen Ata" sıfırdan başlıyor; önceki sınav dönemleri hesaba katılmıyor.

2. **Gözetmen listesi kalıcı değil.** Gözetmenler ve elle girilen ağırlıklar sadece bellekte;
   uygulama kapanınca kayboluyor, her seferinde Excel'den yeniden yüklenmesi gerekiyor.

3. **Ayarlar kalıcı değil.** Havuz tanımları, manuel ortak ders seçimleri, uygulamalı ders işaretleri
   ve hata oranları bellekte tutuluyor; uygulama kapanınca kayboluyor. (Kalıcı olan tek şey
   `hoca_eslemesi.json`.)

4. **Salon düzenleme ekranı yok.** Derslik eklemek/kapasite değiştirmek için derslik Excel'i
   düzenlenip yeniden yüklenir.

5. **Hata oranı manuel şablon yolunda salon atamasına etki etmiyor gibi görünüyor.**
   Hata oranı yalnızca `SablonDagitimAlgoritmasi` (Otomatik Dağıt) ve `SablonDogrulayici`'ya
   veriliyor; `GunlukSalonDagitici.Dagit(...)` bu bilgiyi almıyor. Ancak Adım 3 sonrası durum
   mesajı "Hata payını girip Salonları Ata'ya basın" diyor. Boş şablonla ilerleyen kullanıcı için
   bu beklenti karşılanmıyor olabilir — doğrulanması gerekir.

6. **"Dönem" bilgisi boş gidiyor.** `ExcelMatrisExportHelper.MatrisExcelOlustur(..., donem: "")`
   her zaman boş string ile çağrılıyor; çıktı başlıklarında Güz/Bahar ya da Vize/Final yazmıyor
   olabilir.

### 6.2 Ölü / artık kod

| Öğe | Açıklama |
|---|---|
| `Algorithm/SinavDagitimAlgoritma.cs` (~77 KB) | Eski "V8" tek adımlı algoritma. Hiçbir yerden çağrılmıyor. |
| `DataAccess/HazirSinavExcelOkuyucu.cs` | Eski "BAHAR FİNAL" okuyucu. Kullanılmıyor. |
| `ExcelImportHelper` içindeki ders import kodu | Sadece `GozetmenleriOku` kullanılıyor. |
| `DatabaseHelper` içindeki `KayitliDersler` metotları | `TumKayitliDersleriSilAsync` hariç kullanılmıyor. |
| `Models/KayitliDers.cs`, `Dersler` tablosu | Kullanılmıyor. |
| Formdaki manuel ders ekleme kontrolleri | `clbBolum`, `cmbDersAdi`, `numSinifSeviyesi`, `txtSube` vb. gizlenmiş ama hâlâ formda. |
| `cmbSablonMod` | Gizlenmiş, işlevi yok. |
| Kökteki `.sql` dosyaları | SQL Server döneminden kalma, sadece referans. |

### 6.3 Kod kalitesi / bakım

- **`MainForm.cs` çok büyük (~3800 satır, ~195 KB).** Arayüz, iş akışı, şablon grid düzenleme,
  takvim, gözetmen işlemleri hepsi tek sınıfta. Bölünmesi (ör. `TakvimPaneli`, `SablonGrid`,
  `GozetmenPaneli` gibi ayrı sınıflara) bakımı çok kolaylaştırır.
- **Test yok.** Algoritmalar (özellikle gözetmen dengeleme ve salon dağıtımı) karmaşık ve kural
  ağırlıklı; birim testler olmadan değişiklik yapmak riskli.
- **Git yok.** Önce `git init` yapılıp mevcut hâl commit'lenmeli.
- **Sabit (hard-coded) değerler:** Bölüm listesi, ders kodu önekleri, T4 binası tercihi, saat-sınıf
  eşlemesi, varsayılan saatler kodun içinde. Fakülte değişirse kod değişmeli. Ayrıca bu listeler
  birden fazla dosyada tekrarlanıyor (`SablonDagitimAlgoritmasi`, `CekilmisDerslerExportHelper`,
  `GunSalonMatrisExportHelper`, `MainForm.BolumKisalt`…).
- **İsimlendirme tutarsızlıkları:** Bölüm kısaltması bazı yerlerde `EEM`, bazı yerlerde `ELM`;
  `INS` / `İNŞ`. Uygulamalı ders grid'inin adı `dgvOverbooking`.
- **Uzun işlemler UI thread'inde** çalışıyor (`Application.DoEvents()` ile); büyük dosyalarda
  pencere donabilir.
- **Doküman tutarsızlıkları:**
  - `Models/SinavTipi.cs` içindeki enum yorumu Final için "12 sınav günü" diyor, kod 10 gün üretiyor.
  - Buton/mesaj numaralandırması tutarlı değil (mesajlarda "3) Şablonu Yükle", "4) Salonları Ata"
    gibi eski numaralar geçiyor).
  - `NASIL_CALISTIRILIR.md` proje klasörünü `Elfreni` olarak anıyor; mevcut klasör adı farklı.

---

## 7. Geliştirmeye Başlarken

```powershell
dotnet build          # derle
dotnet run            # çalıştır (DB: bin\Debug\net8.0-windows\SinavDB.db)
.\yayinla.bat         # taşınabilir ZIP üret
```

Gereksinim: **.NET 8 SDK**, Windows.

**Önerilen öncelik sırası:**
1. `git init` + ilk commit (mevcut çalışan hâli kayıt altına al).
2. Ölü kodu temizle (`SinavDagitimAlgoritma`, `HazirSinavExcelOkuyucu`, kullanılmayan DB metotları,
   gizli kontroller) — kod tabanı belirgin şekilde küçülür.
3. Gözetmen görev geçmişini DB'ye bağla (6.1-1 ve 6.1-2) — projenin tasarımda olup bitmemiş
   en önemli özelliği.
4. Ayarların (havuz, ortak, uygulamalı, hata oranı) kalıcı saklanması.
5. Hata oranının salon atamasına etkisini netleştir (6.1-5).
6. `MainForm.cs`'yi parçalara ayır, algoritmalar için birim test ekle.
