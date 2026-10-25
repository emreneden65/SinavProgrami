#nullable disable
using System;
using Microsoft.AspNetCore.Components;
using SinavProgrami.Services;

namespace SinavProgrami.UI.Components
{
    /// <summary>
    /// Çalışma durumu her değiştiğinde kendini yeniden çizen bileşenlerin tabanı.
    /// (Parametresi olmayan bir alt bileşen, üstü yeniden çizilse bile kendiliğinden
    /// yenilenmez; menü, üst çubuk ve sayfalar bu yüzden olaya kendileri abone olur.)
    /// </summary>
    public abstract class DurumaBagliBilesen : ComponentBase, IDisposable
    {
        [Inject] protected CalismaDurumu Durum { get; set; }

        protected override void OnInitialized()
        {
            Durum.Degisti += DurumDegisti;
            base.OnInitialized();
        }

        private void DurumDegisti() => InvokeAsync(StateHasChanged);

        public virtual void Dispose() => Durum.Degisti -= DurumDegisti;
    }
}
