using HackathonStarter.Config;
using MudBlazor;

namespace HackathonStarter.Components.Layout;

/// <summary>
/// MudBlazor theme built from the colors in <see cref="SiteConfig"/>. Values that MudBlazor doesn't cover
/// (the content panel, landing page) are CSS variables in wwwroot/app.css (--app-*); keep the two in sync.
/// </summary>
public static class AppTheme
{
    private static readonly string[] Fonts = ["IBM Plex Sans", "ui-sans-serif", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        // Blue actions, navy navigation and neutral work surfaces. Info is the text/icon accent;
        // status colors remain separate and are always accompanied by a label.
        PaletteLight = new PaletteLight
        {
            Primary = SiteConfig.PrimaryColor,
            Secondary = SiteConfig.SecondaryColor,
            Info = "#1D5DCE",
            Success = "#167552",
            Warning = "#BD4C10",
            Error = "#B83746",
            Background = "#F3F6FA",
            BackgroundGray = "#E9EFF5",
            Surface = "#FFFFFF",
            DrawerBackground = "#132D50",
            DrawerText = "#F1F5F9",
            DrawerIcon = "#96BDF4",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#172B42",
            LinesDefault = "#DCE4ED",
            LinesInputs = "#7C8FA5",
            TableLines = "#DCE4ED",
            Divider = "#DCE4ED",
            TextPrimary = "#172B42",
            TextSecondary = "#526478",
            TextDisabled = "#718399",
            ActionDefault = "#526478",
        },
        PaletteDark = new PaletteDark
        {
            Primary = SiteConfig.PrimaryColor,
            Secondary = SiteConfig.SecondaryColor,
            Info = "#8BBAFF",
            Success = "#73D2B1",
            Warning = "#FFB36B",
            Error = "#FF929E",
            Background = "#101E30",
            BackgroundGray = "#14253A",
            Surface = "#192C43",
            DrawerBackground = "#132D50",
            DrawerText = "#F1F5F9",
            DrawerIcon = "#96BDF4",
            AppbarBackground = "#192C43",
            AppbarText = "#E7EDF5",
            LinesDefault = "#30465E",
            LinesInputs = "#8295AC",
            TableLines = "#30465E",
            Divider = "#30465E",
            TextPrimary = "#E7EDF5",
            TextSecondary = "#ABBAD0",
            TextDisabled = "#8295AC",
            ActionDefault = "#ABBAD0",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px", DrawerWidthLeft = "240px" },
        Typography = new Typography
        {
            // Compact headings leave space for the dossier and case queue.
            Default = new DefaultTypography { FontFamily = Fonts, FontSize = ".9375rem", LineHeight = "1.4667" },
            Body1 = new Body1Typography { FontFamily = Fonts, FontSize = ".9375rem", LineHeight = "1.4667" },
            Body2 = new Body2Typography { FontFamily = Fonts, FontSize = ".875rem", LineHeight = "1.43" },
            Subtitle1 = new Subtitle1Typography { FontFamily = Fonts, FontSize = ".9375rem", FontWeight = "600" },
            Subtitle2 = new Subtitle2Typography { FontFamily = Fonts, FontSize = ".875rem", FontWeight = "600" },
            Caption = new CaptionTypography { FontFamily = Fonts, FontSize = ".75rem", LineHeight = "1.33" },
            H1 = new H1Typography { FontFamily = Fonts, FontWeight = "600", LetterSpacing = "-.02em" },
            H2 = new H2Typography { FontFamily = Fonts, FontWeight = "600", LetterSpacing = "-.02em" },
            H3 = new H3Typography { FontFamily = Fonts, FontSize = "3rem", FontWeight = "600", LetterSpacing = "-.02em" },
            H4 = new H4Typography { FontFamily = Fonts, FontSize = "1.75rem", LineHeight = "1.25", FontWeight = "600", LetterSpacing = "-.015em" },
            H5 = new H5Typography { FontFamily = Fonts, FontSize = "1.375rem", LineHeight = "1.27", FontWeight = "600", LetterSpacing = "-.015em" },
            H6 = new H6Typography { FontFamily = Fonts, FontSize = "1rem", LineHeight = "1.4", FontWeight = "600", LetterSpacing = "-.01em" },
            Button = new ButtonTypography { FontFamily = Fonts, FontSize = ".875rem", FontWeight = "500", TextTransform = "none", LetterSpacing = "-.01em" },
        },
    };
}
