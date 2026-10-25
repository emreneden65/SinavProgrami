#nullable disable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;
using SinavProgrami.Services;

namespace SinavProgrami.UI
{
    /// <summary>
    /// Yeni arayüzün servis kayıtları — pencere (Program) ve bileşen testleri aynı kaydı kullanır.
    /// Önceden kaydedilmiş bir servis (ör. testin hazırladığı çalışma durumu, gerçek pencere servisi)
    /// ezilmez; yalnızca eksik olanlar eklenir.
    /// </summary>
    public static class ServisKayitlari
    {
        public static IServiceCollection ArayuzServisleriniEkle(this IServiceCollection servisler)
        {
            servisler.AddMudServices(ayar =>
            {
                var bildirim = ayar.SnackbarConfiguration;
                bildirim.PositionClass = Defaults.Classes.Position.BottomRight;
                bildirim.NewestOnTop = true;
                bildirim.PreventDuplicates = false;
                bildirim.ShowCloseIcon = true;
                bildirim.VisibleStateDuration = 6000;
                bildirim.MaxDisplayedSnackbars = 4;
            });
            servisler.Replace(ServiceDescriptor.Transient<MudLocalizer, TurkceMudYerellestirici>());

            servisler.TryAddSingleton<CalismaDurumu>();
            servisler.TryAddSingleton<IsYurutucu>();
            servisler.TryAddSingleton<OturumYoneticisi>();
            servisler.TryAddSingleton<AyarlarDeposu>();
            servisler.TryAddSingleton<TakvimServisi>();
            servisler.TryAddSingleton<HocaEslemesi>();
            servisler.TryAddSingleton<DerslikServisi>();
            servisler.TryAddSingleton<DersServisi>();
            servisler.TryAddSingleton<OrtakDersServisi>();
            servisler.TryAddSingleton<SablonServisi>();
            servisler.TryAddSingleton<SalonServisi>();
            servisler.TryAddSingleton<GozetmenServisi>();
            servisler.TryAddSingleton<FinalServisi>();
            servisler.TryAddSingleton<TemizlemeServisi>();
            servisler.TryAddSingleton<IPencereServisi, BosPencereServisi>();
            servisler.TryAddSingleton<IDosyaServisi, BosDosyaServisi>();
            return servisler;
        }
    }
}
