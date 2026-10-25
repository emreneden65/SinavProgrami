#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using SinavProgrami.Services;

namespace SinavProgrami.UI.Components
{
    /// <summary>
    /// Sayfada beklenmeyen bir hata olursa uygulama çökmez: sayfanın yerine hata kartı çıkar,
    /// ayrıntı hata günlüğüne yazılır, kullanıcı Genel Bakış'a dönüp devam edebilir.
    /// </summary>
    public class HataSiniri : ErrorBoundary
    {
        protected override Task OnErrorAsync(Exception hata)
        {
            HataGunlugu.Yaz("Sayfa çizilirken yakalanmamış hata", hata);
            return Task.CompletedTask;
        }
    }
}
