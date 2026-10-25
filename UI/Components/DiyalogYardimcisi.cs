#nullable disable
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using SinavProgrami.Services;

namespace SinavProgrami.UI.Components
{
    /// <summary>Eski MessageBox.Show çağrılarının karşılıkları (Evet/Hayır onayı ve tek düğmeli bilgi).</summary>
    public static class DiyalogYardimcisi
    {
        private static readonly DialogOptions Secenekler = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true,
            BackdropClick = false,
            CloseButton = true
        };

        /// <summary>Kullanıcı onaylarsa true; vazgeçer ya da kapatırsa false.</summary>
        public static async Task<bool> OnaylaAsync(this IDialogService diyalog, string baslik, string mesaj,
            string evetMetni = "Evet", string hayirMetni = "Hayır", bool tehlikeli = false,
            RenderFragment icerik = null, DurumTuru tur = DurumTuru.Bilgi)
        {
            var parametreler = new DialogParameters<OnayDialog>
            {
                { x => x.Baslik, baslik },
                { x => x.Mesaj, mesaj },
                { x => x.Icerik, icerik },
                { x => x.EvetMetni, evetMetni },
                { x => x.HayirMetni, hayirMetni },
                { x => x.Tehlikeli, tehlikeli },
                { x => x.Tur, tehlikeli && tur == DurumTuru.Bilgi ? DurumTuru.Uyari : tur }
            };
            var referans = await diyalog.ShowAsync<OnayDialog>(null, parametreler, Secenekler);
            var sonuc = await referans.Result;
            return sonuc is { Canceled: false };
        }

        /// <summary>Okuma uyarılarını / atlanan satırları aranabilir, kopyalanabilir bir listede gösterir.</summary>
        public static async Task UyarilariGosterAsync(this IDialogService diyalog, string baslik, List<string> uyarilar, string aciklama = null)
        {
            if (uyarilar == null || uyarilar.Count == 0) return;
            var parametreler = new DialogParameters<UyariListesiDialog>
            {
                { x => x.Baslik, baslik },
                { x => x.Aciklama, aciklama },
                { x => x.Uyarilar, uyarilar }
            };
            var secenekler = new DialogOptions
            {
                MaxWidth = MaxWidth.Medium,
                FullWidth = true,
                CloseOnEscapeKey = true,
                BackdropClick = true,
                CloseButton = true
            };
            var referans = await diyalog.ShowAsync<UyariListesiDialog>(null, parametreler, secenekler);
            await referans.Result;
        }

        /// <summary>Tek "Tamam" düğmeli bilgi / uyarı / hata diyaloğu.</summary>
        public static async Task BilgiAsync(this IDialogService diyalog, string baslik, string mesaj,
            DurumTuru tur = DurumTuru.Bilgi, RenderFragment icerik = null)
        {
            var parametreler = new DialogParameters<OnayDialog>
            {
                { x => x.Baslik, baslik },
                { x => x.Mesaj, mesaj },
                { x => x.Icerik, icerik },
                { x => x.EvetMetni, "Tamam" },
                { x => x.HayirMetni, null },
                { x => x.Tur, tur }
            };
            var referans = await diyalog.ShowAsync<OnayDialog>(null, parametreler, Secenekler);
            await referans.Result;
        }
    }
}
