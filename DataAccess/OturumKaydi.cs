#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SinavProgrami.Algorithm;
using SinavProgrami.Models;

namespace SinavProgrami.DataAccess
{
    /// <summary>
    /// Uygulama kapatılıp açıldığında kaldığı yerden devam edebilmek için
    /// bellekteki tüm çalışma durumunun anlık görüntüsü.
    /// </summary>
    public class OturumDurumu
    {
        /// <summary>Dosya biçimi sürümü — ileride alan eklenirse eski kayıtlar ayırt edilebilsin.</summary>
        public int Surum { get; set; } = 1;

        /// <summary>Kullanıcının yüklediği derslik listesi (veritabanında tutulmaz).</summary>
        public List<Salon> Derslikler { get; set; } = new List<Salon>();

        public List<Ders> Dersler { get; set; } = new List<Ders>();
        public List<TakvimGunu> Takvim { get; set; } = new List<TakvimGunu>();
        public SinavDonemTipi SinavTipi { get; set; } = SinavDonemTipi.Vize;

        /// <summary>Öğretim yılının başladığı yıl (2026 → 2026/2027); null = seçilmedi.</summary>
        public int? OgretimYili { get; set; }

        /// <summary>Güz / Bahar; null = seçilmedi.</summary>
        public Yariyil? Yariyil { get; set; }
        public DateTime? BaslangicTarihi { get; set; }

        public List<DersHavuzu> Havuzlar { get; set; } = new List<DersHavuzu>();
        public bool OrtakSecimYapildi { get; set; }
        public List<string> ManuelOrtakKodlar { get; set; } = new List<string>();
        public List<string> ManuelOrtakDersAdlari { get; set; } = new List<string>();
        public List<string> UygulamaliDersler { get; set; } = new List<string>();
        public Dictionary<string, double> HataOranlari { get; set; } = new Dictionary<string, double>();

        /// <summary>Salonsuz şablon yerleşimi (null = şablon henüz yüklenmedi).</summary>
        public List<SinavAtama> SablonSonuclari { get; set; }
        public Dictionary<string, string> SablonIntNotlari { get; set; } = new Dictionary<string, string>();

        /// <summary>'Salonları Ata' çıktısı (null = salon ataması yapılmadı).</summary>
        public List<GunlukSalonDagitici.GunSatir> GunSalonSatirlari { get; set; }

        /// <summary>Salon (ve varsa gözetmen) atanmış sınavlar (null = henüz yok).</summary>
        public List<SinavAtama> SonSonuclar { get; set; }

        public List<Gozetmen> Gozetmenler { get; set; } = new List<Gozetmen>();

        /// <summary>Adım butonu adı → etkin mi (adım kilitleri).</summary>
        public Dictionary<string, bool> Butonlar { get; set; } = new Dictionary<string, bool>();

        public int SeciliSekme { get; set; }
        public bool SonucPaneliAcik { get; set; }

        /// <summary>Pencere tam ekran mıydı; değilse son normal boyutu (0 = eski kayıt, bilinmiyor).</summary>
        public bool PencereTamEkran { get; set; }
        public int PencereGenislik { get; set; }
        public int PencereYukseklik { get; set; }
    }

    /// <summary>Takvimdeki bir sınav günü ve o günün seçili saatleri.</summary>
    public class TakvimGunu
    {
        public DateTime Gun { get; set; }
        public List<string> Saatler { get; set; } = new List<string>();
    }

    /// <summary>
    /// OTURUM KAYDI — <see cref="OturumDurumu"/>'nu %AppData%\SinavProgrami\oturum.json
    /// dosyasına yazar / okur. Program klasörü dışında durduğu için yeni sürüm kurulunca
    /// (yayinla.bat Yayin klasörünü silse de) kayıt korunur.
    /// </summary>
    public static class OturumKaydi
    {
        /// <summary>Kayıt klasörü (testlerde geçici bir klasöre yönlendirilir).</summary>
        public static string Klasor { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SinavProgrami");

        public static string DosyaYolu => Path.Combine(Klasor, "oturum.json");

        // GunSatir alanlar (field) kullandığından IncludeFields gerekir
        private static readonly JsonSerializerOptions Ayarlar = new JsonSerializerOptions
        {
            IncludeFields = true,
            Converters = { new SaatDilimsizTarih() }
        };

        /// <summary>
        /// Takvim tarihleri saat dilimi olmadan yazılır/okunur. Varsayılan biçim yerel
        /// tarihe "+03:00" ekler; kayıt başka saat dilimindeki bir bilgisayarda açılırsa
        /// sınav günü bir gün kayabilirdi.
        /// </summary>
        private sealed class SaatDilimsizTarih : System.Text.Json.Serialization.JsonConverter<DateTime>
        {
            private const string Bicim = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";

            // Eski kayıtlardaki "+03:00" eki yok sayılır: yazılan saat/tarih aynen alınır
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => DateTimeOffset.Parse(reader.GetString(), System.Globalization.CultureInfo.InvariantCulture).DateTime;

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString(Bicim, System.Globalization.CultureInfo.InvariantCulture));
        }

        public static string Serilestir(OturumDurumu durum) => JsonSerializer.Serialize(durum, Ayarlar);

        /// <summary>
        /// JSON'u diske yazar. Önce geçici dosyaya yazılıp sonra yerine taşınır;
        /// yazma sırasında elektrik kesilse bile eski kayıt bozulmaz.
        /// </summary>
        public static void Yaz(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu));
            string gecici = DosyaYolu + ".tmp";
            File.WriteAllText(gecici, json);
            File.Move(gecici, DosyaYolu, overwrite: true);
        }

        /// <summary>
        /// Kayıtlı oturumu okur; kayıt yoksa null döner. Dosya okunamıyorsa
        /// üzerine yazılıp kaybolmasın diye "oturum.bozuk.json" adıyla kenara alınır.
        /// </summary>
        public static OturumDurumu Oku(out string hata)
        {
            hata = null;
            if (!File.Exists(DosyaYolu)) return null;
            try
            {
                var durum = JsonSerializer.Deserialize<OturumDurumu>(File.ReadAllText(DosyaYolu), Ayarlar);
                if (durum != null) KarsilastiricilariDuzelt(durum);
                return durum;
            }
            catch (Exception ex)
            {
                hata = ex.Message;
                try { File.Move(DosyaYolu, BozukYedekYolu, overwrite: true); } catch { }
                return null;
            }
        }

        public static string BozukYedekYolu => Path.Combine(Klasor, "oturum.bozuk.json");

        public static DateTime? SonKayitZamani =>
            File.Exists(DosyaYolu) ? File.GetLastWriteTime(DosyaYolu) : (DateTime?)null;

        public static void Sil()
        {
            try { if (File.Exists(DosyaYolu)) File.Delete(DosyaYolu); } catch { }
        }

        /// <summary>
        /// JSON sözlükleri varsayılan (büyük/küçük harf duyarlı) karşılaştırıcıyla açılır;
        /// koddaki büyük/küçük harf duyarsız sözlükler eski haline getirilir.
        /// </summary>
        private static void KarsilastiricilariDuzelt(OturumDurumu d)
        {
            d.HataOranlari = new Dictionary<string, double>(
                d.HataOranlari ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase);
            foreach (var s in d.GunSalonSatirlari ?? new List<GunlukSalonDagitici.GunSatir>())
                s.Subeler = new Dictionary<string, int>(
                    s.Subeler ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
