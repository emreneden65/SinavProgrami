#nullable disable
using System;
using System.IO;

namespace SinavProgrami.Services
{
    /// <summary>İstisnaları kullanıcının anlayacağı Türkçe açıklamaya çevirir.</summary>
    public static class HataMesaji
    {
        private const int PaylasimIhlali = unchecked((int)0x80070020);   // ERROR_SHARING_VIOLATION
        private const int KilitIhlali = unchecked((int)0x80070021);      // ERROR_LOCK_VIOLATION

        /// <summary>
        /// Dosya başka bir programda (genellikle Excel'de) açık olduğu için okunamıyor / yazılamıyorsa
        /// ne yapılacağını söyler; diğer hatalarda asıl mesajı döner.
        /// </summary>
        public static string Anlasilir(Exception hata)
        {
            var ic = hata is AggregateException ag && ag.InnerException != null ? ag.InnerException : hata;
            if (ic is IOException io && (io.HResult == PaylasimIhlali || io.HResult == KilitIhlali))
                return "Dosya başka bir programda (büyük olasılıkla Excel'de) açık.\n" +
                       "Excel'de kaydedip dosyayı kapatın, sonra tekrar deneyin.\n\n" +
                       $"Ayrıntı: {ic.Message}";
            if (ic is UnauthorizedAccessException)
                return "Bu konuma yazma / okuma izni yok. Belgeler veya Masaüstü gibi bir klasör seçin.\n\n" +
                       $"Ayrıntı: {ic.Message}";
            return ic.Message;
        }
    }
}
