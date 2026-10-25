#nullable disable
using System;
using System.IO;
using SinavProgrami.DataAccess;

namespace SinavProgrami.Services
{
    /// <summary>
    /// VERİ KLASÖRÜ — oturum kaydı, hoca eşlemesi ve ayarların bulunduğu klasör.
    /// ─────────────────────────────────────────────────────────────
    /// Varsayılan: gerçek klasör (%AppData%\SinavProgrami\, eski arayüzle aynı). "--deneme" ile
    /// açılınca gerçek veriye dokunulmaz: %AppData%\SinavProgrami\Deneme\ klasöründe, gerçek
    /// oturum.json ve hoca_eslemesi.json dosyalarının KOPYALARIYLA çalışılır.
    /// </summary>
    public static class VeriKlasoru
    {
        /// <summary>Kopyalanan çalışma dosyaları.</summary>
        private static readonly string[] KopyalananDosyalar = { "oturum.json", "hoca_eslemesi.json" };

        public static string GercekKlasor { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SinavProgrami");

        public static string DenemeKlasoru => Path.Combine(GercekKlasor, "Deneme");

        /// <summary>Yeni arayüz deneme klasöründe mi çalışıyor?</summary>
        public static bool DenemeModu { get; private set; }

        /// <summary>
        /// Veri klasörünü seçer. Gerçek klasörde bir şey yapılmaz (oturum kaydı zaten oradadır).
        /// Deneme kipinde ilk açılışta gerçek dosyaların kopyası alınır (sonraki açılışlarda denemede
        /// yapılan değişiklikler korunur) ve oturum kaydı deneme klasörüne yönlendirilir.
        /// </summary>
        public static void Kur(bool deneme)
        {
#if DEBUG
            // Yalnızca Debug — uçtan uca otomasyon kendi boş klasöründe çalışır (deneme kopyasına da dokunmaz)
            string otomasyonKlasoru = Environment.GetEnvironmentVariable("SINAVPROGRAMI_OTOMASYON_VERI");
            if (!string.IsNullOrWhiteSpace(otomasyonKlasoru))
            {
                Directory.CreateDirectory(otomasyonKlasoru);
                OturumKaydi.Klasor = otomasyonKlasoru;
                DenemeModu = true;
                return;
            }
#endif
            DenemeModu = deneme;
            if (!deneme) return;

            Directory.CreateDirectory(DenemeKlasoru);
            foreach (var ad in KopyalananDosyalar)
            {
                string kaynak = Path.Combine(GercekKlasor, ad);
                string hedef = Path.Combine(DenemeKlasoru, ad);
                if (!File.Exists(hedef) && File.Exists(kaynak))
                    File.Copy(kaynak, hedef);
            }
            OturumKaydi.Klasor = DenemeKlasoru;
        }

        /// <summary>
        /// Deneme kopyalarını gerçek dosyaların güncel haliyle değiştirir (gerçek dosyalar okunur,
        /// değiştirilmez). Uygulama ardından yeniden başlatılmalıdır.
        /// </summary>
        public static void DenemeVerisiniYenile()
        {
            Directory.CreateDirectory(DenemeKlasoru);
            foreach (var ad in KopyalananDosyalar)
            {
                string kaynak = Path.Combine(GercekKlasor, ad);
                string hedef = Path.Combine(DenemeKlasoru, ad);
                if (File.Exists(kaynak)) File.Copy(kaynak, hedef, overwrite: true);
                else if (File.Exists(hedef)) File.Delete(hedef);
            }
        }
    }
}
