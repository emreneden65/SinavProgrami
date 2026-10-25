#nullable disable
using MudBlazor;

namespace SinavProgrami.UI
{
    /// <summary>
    /// TEMA — açık tema Catppuccin "Latte", koyu tema Catppuccin "Mocha" renkleridir.
    /// Eski arayüzün koyu renkleri (lacivert zemin, pastel mavi / yeşil / sarı) Mocha'dan
    /// geldiği için koyu tema tanıdık görünür. Yazı tipi Windows'un kendi yazı tipidir;
    /// internetten font indirilmez.
    /// </summary>
    public static class Tema
    {
        private static readonly string[] YaziTipi = { "Segoe UI Variable Text", "Segoe UI", "system-ui", "sans-serif" };

        public static MudTheme Varsayilan { get; } = Olustur();

        private static MudTheme Olustur()
        {
            var tema = new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    Primary = "#1E66F5",
                    PrimaryContrastText = "#FFFFFF",
                    Secondary = "#8839EF",
                    SecondaryContrastText = "#FFFFFF",
                    Tertiary = "#179299",
                    TertiaryContrastText = "#FFFFFF",
                    Info = "#04A5E5",
                    InfoContrastText = "#FFFFFF",
                    Success = "#40A02B",
                    SuccessContrastText = "#FFFFFF",
                    Warning = "#DF8E1D",
                    WarningContrastText = "#FFFFFF",
                    Error = "#D20F39",
                    ErrorContrastText = "#FFFFFF",
                    Dark = "#4C4F69",
                    Background = "#EFF1F5",
                    BackgroundGray = "#E6E9EF",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#FFFFFF",
                    AppbarText = "#4C4F69",
                    DrawerBackground = "#FFFFFF",
                    DrawerText = "#4C4F69",
                    DrawerIcon = "#6C6F85",
                    TextPrimary = "#4C4F69",
                    TextSecondary = "#6C6F85",
                    TextDisabled = "#9CA0B0",
                    ActionDefault = "#6C6F85",
                    ActionDisabled = "#9CA0B0",
                    ActionDisabledBackground = "#E6E9EF",
                    LinesDefault = "#DCE0E8",
                    LinesInputs = "#BCC0CC",
                    TableLines = "#DCE0E8",
                    TableHover = "#E6E9EF",
                    TableStriped = "#F5F6F9",
                    Divider = "#DCE0E8",
                    DividerLight = "#E6E9EF",
                    Skeleton = "#E6E9EF"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#89B4FA",
                    PrimaryContrastText = "#11111B",
                    Secondary = "#CBA6F7",
                    SecondaryContrastText = "#11111B",
                    Tertiary = "#94E2D5",
                    TertiaryContrastText = "#11111B",
                    Info = "#89DCEB",
                    InfoContrastText = "#11111B",
                    Success = "#A6E3A1",
                    SuccessContrastText = "#11111B",
                    Warning = "#F9E2AF",
                    WarningContrastText = "#11111B",
                    Error = "#F38BA8",
                    ErrorContrastText = "#11111B",
                    Dark = "#11111B",
                    Background = "#181825",
                    BackgroundGray = "#11111B",
                    Surface = "#1E1E2E",
                    AppbarBackground = "#1E1E2E",
                    AppbarText = "#CDD6F4",
                    DrawerBackground = "#1E1E2E",
                    DrawerText = "#CDD6F4",
                    DrawerIcon = "#A6ADC8",
                    TextPrimary = "#CDD6F4",
                    TextSecondary = "#A6ADC8",
                    TextDisabled = "#6C7086",
                    ActionDefault = "#A6ADC8",
                    ActionDisabled = "#6C7086",
                    ActionDisabledBackground = "#313244",
                    LinesDefault = "#313244",
                    LinesInputs = "#45475A",
                    TableLines = "#313244",
                    TableHover = "#313244",
                    TableStriped = "#232334",
                    Divider = "#313244",
                    DividerLight = "#2A2B3C",
                    Skeleton = "#313244"
                },
                LayoutProperties = new LayoutProperties
                {
                    DefaultBorderRadius = "8px",
                    AppbarHeight = "52px"
                }
            };

            var y = tema.Typography;
            foreach (var t in new[] { y.Default, y.H1, y.H2, y.H3, y.H4, y.H5, y.H6, y.Subtitle1, y.Subtitle2,
                                      y.Body1, y.Body2, y.Button, y.Caption, y.Overline })
                t.FontFamily = YaziTipi;

            y.Default.FontSize = "0.875rem";
            y.Body1.FontSize = "0.875rem";
            y.Body2.FontSize = "0.8125rem";
            y.Button.TextTransform = "none";      // düğme yazıları BÜYÜK HARF olmasın
            y.Button.FontWeight = "600";
            y.H5.FontSize = "1.375rem";
            y.H5.FontWeight = "600";
            y.H6.FontSize = "1.0625rem";
            y.H6.FontWeight = "600";
            y.Subtitle1.FontWeight = "600";
            return tema;
        }
    }
}
