#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SinavProgrami.DataAccess;

namespace SinavProgrami.Services
{
    /// <summary>Kullanıcı tercihleri (çalışma verisinden ayrı; "Uygulamayı Sıfırla" bunları silmez).</summary>
    public sealed class UygulamaAyarlari
    {
        /// <summary>Koyu tema mı? Varsayılan açık tema.</summary>
        public bool KoyuTema { get; set; }

        /// <summary>Şablon sayfasında gizlenen kural ihlali türleri (IhlalTuru adları).</summary>
        public List<string> GizliIhlalTurleri { get; set; } = new();

        /// <summary>Şablon ızgarasının yakınlaştırma düzeyi (yazı boyutu, pt; eski tablodaki gibi 6–16, varsayılan 9).</summary>
        public double SablonYaziBoyutu { get; set; } = 9;

        /// <summary>Salon matrisinin yakınlaştırma düzeyi (yazı boyutu, pt; 6–16, varsayılan 9).</summary>
        public double MatrisYaziBoyutu { get; set; } = 9;
    }

    /// <summary>
    /// AYARLAR — tema gibi tercihler oturum kaydının yanındaki ayarlar.json dosyasında tutulur.
    /// Dosya okunamazsa varsayılanlar kullanılır; tercih kaybı çalışmayı engellemez.
    /// </summary>
    public class AyarlarDeposu
    {
        public static string DosyaYolu => Path.Combine(OturumKaydi.Klasor, "ayarlar.json");

        public UygulamaAyarlari Ayarlar { get; private set; } = new UygulamaAyarlari();

        public event Action Degisti;

        public void Yukle()
        {
            try
            {
                if (File.Exists(DosyaYolu))
                    Ayarlar = JsonSerializer.Deserialize<UygulamaAyarlari>(File.ReadAllText(DosyaYolu)) ?? new UygulamaAyarlari();
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz("Ayarlar okunamadı (varsayılanlar kullanılıyor)", ex);
                Ayarlar = new UygulamaAyarlari();
            }
        }

        public void TemaAyarla(bool koyu)
        {
            if (Ayarlar.KoyuTema == koyu) return;
            Ayarlar.KoyuTema = koyu;
            Kaydet();
            Degisti?.Invoke();
        }

        public bool IhlalTuruGizliMi(IhlalTuru tur) => Ayarlar.GizliIhlalTurleri?.Contains(tur.ToString()) == true;

        public void IhlalTuruGizle(IhlalTuru tur, bool gizli)
        {
            Ayarlar.GizliIhlalTurleri ??= new List<string>();
            if (gizli == IhlalTuruGizliMi(tur)) return;
            if (gizli) Ayarlar.GizliIhlalTurleri.Add(tur.ToString());
            else Ayarlar.GizliIhlalTurleri.Remove(tur.ToString());
            Kaydet();
            Degisti?.Invoke();
        }

        public void SablonYaziBoyutuAyarla(double boyut)
        {
            boyut = Math.Clamp(Math.Round(boyut), 6, 16);
            if (Math.Abs(Ayarlar.SablonYaziBoyutu - boyut) < 0.01) return;
            Ayarlar.SablonYaziBoyutu = boyut;
            Kaydet();
            Degisti?.Invoke();
        }

        public void MatrisYaziBoyutuAyarla(double boyut)
        {
            boyut = Math.Clamp(Math.Round(boyut), 6, 16);
            if (Math.Abs(Ayarlar.MatrisYaziBoyutu - boyut) < 0.01) return;
            Ayarlar.MatrisYaziBoyutu = boyut;
            Kaydet();
            Degisti?.Invoke();
        }

        private void Kaydet()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu));
                File.WriteAllText(DosyaYolu, JsonSerializer.Serialize(Ayarlar, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz("Ayarlar kaydedilemedi", ex);
            }
        }
    }
}
