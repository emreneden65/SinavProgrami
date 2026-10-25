#nullable disable
using System;
using System.Diagnostics;

namespace SinavProgrami.Services
{
    /// <summary>
    /// Arayüzün pencereden (WinForms) istediği işler. Razor bileşenleri pencereyi doğrudan
    /// tanımaz; testlerde <see cref="BosPencereServisi"/> kullanılır.
    /// </summary>
    public interface IPencereServisi
    {
        /// <summary>Deneme kopyalarını gerçek verinin güncel haliyle yeniler ve uygulamayı yeniden başlatır.</summary>
        void DenemeVerisiyleYenidenBaslat();

        /// <summary>Bir klasörü Windows Gezgini'nde açar.</summary>
        void KlasoruAc(string klasor);
    }

    /// <summary>Pencere olmayan ortam (testler) için etkisiz sürüm.</summary>
    public sealed class BosPencereServisi : IPencereServisi
    {
        public void DenemeVerisiyleYenidenBaslat() { }
        public void KlasoruAc(string klasor) { }
    }

    /// <summary>Gerçek pencereye yönlendiren sürüm.</summary>
    public sealed class WinFormsPencereServisi : IPencereServisi
    {
        public void DenemeVerisiyleYenidenBaslat() => AnaPencere.Ornek?.DenemeVerisiyleYenidenBaslat();

        public void KlasoruAc(string klasor)
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{klasor}\"") { UseShellExecute = true }); }
            catch (Exception ex) { HataGunlugu.Yaz($"Klasör açılamadı: {klasor}", ex); }
        }
    }
}
