#nullable disable
using MudBlazor;

namespace SinavProgrami.UI
{
    /// <summary>İş akışı adımlarının simgeleri (Material Outlined — paketle gelir, internetten indirilmez).</summary>
    public static class AdimIkonlari
    {
        public const string GenelBakis = Icons.Material.Outlined.Dashboard;
        public const string Ayarlar = Icons.Material.Outlined.Settings;

        public static string Adim(int no) => no switch
        {
            1 => Icons.Material.Outlined.CalendarMonth,
            2 => Icons.Material.Outlined.MeetingRoom,
            3 => Icons.Material.Outlined.MenuBook,
            4 => Icons.Material.Outlined.GridOn,
            5 => Icons.Material.Outlined.Apartment,
            6 => Icons.Material.Outlined.Badge,
            7 => Icons.Material.Outlined.TaskAlt,
            _ => Icons.Material.Outlined.Circle
        };
    }
}
