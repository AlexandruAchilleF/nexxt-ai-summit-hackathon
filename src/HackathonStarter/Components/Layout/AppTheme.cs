using HackathonStarter.Config;
using MudBlazor;

namespace HackathonStarter.Components.Layout;

/// <summary>
/// MudBlazor theme built from the colors in <see cref="SiteConfig"/>. Values that MudBlazor doesn't cover
/// (the content panel, landing page) are CSS variables in wwwroot/app.css (--app-*); keep the two in sync.
/// </summary>
public static class AppTheme
{
    private static readonly string[] Fonts = ["Inter", "ui-sans-serif", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        // Apple-leaning palette (design system "Hackathon Starter"). Primary is the fill behind white text (#0071E3 in both
        // themes, 4.7:1); Info carries the accent as text and icons (#0066CC light / #2997FF dark), see --app-accent in app.css.
        // Success is teal so pass/fail never rely on red vs green alone; every status also has an icon and a word.
        PaletteLight = new PaletteLight
        {
            Primary = SiteConfig.PrimaryColor,
            Secondary = SiteConfig.SecondaryColor,
            Info = "#0066CC",
            Success = "#00796B",
            Warning = "#A14800",
            Error = "#D70015",
            Background = "#F5F5F7",
            BackgroundGray = "#F5F5F7",
            Surface = "#FFFFFF",
            DrawerBackground = "rgba(255,255,255,0.72)",
            DrawerText = "#1D1D1F",
            DrawerIcon = "#0066CC",
            AppbarBackground = "rgba(245,245,247,0.72)",
            AppbarText = "#1D1D1F",
            LinesDefault = "rgba(0,0,0,0.1)",
            LinesInputs = "#86868B",
            TableLines = "rgba(0,0,0,0.1)",
            Divider = "rgba(0,0,0,0.1)",
            TextPrimary = "#1D1D1F",
            TextSecondary = "#6E6E73",
            TextDisabled = "#86868B",
            ActionDefault = "#6E6E73",
        },
        // True black page, cards lifted by lighter greys instead of shadows.
        PaletteDark = new PaletteDark
        {
            Primary = SiteConfig.PrimaryColor,
            Secondary = SiteConfig.SecondaryColor,
            Info = "#2997FF",
            Success = "#4FD6C0",
            Warning = "#FFB340",
            Error = "#FF6961",
            Background = "#000000",
            BackgroundGray = "#111112",
            Surface = "#1C1C1E",
            DrawerBackground = "rgba(28,28,30,0.72)",
            DrawerText = "#F5F5F7",
            DrawerIcon = "#2997FF",
            AppbarBackground = "rgba(0,0,0,0.72)",
            AppbarText = "#F5F5F7",
            LinesDefault = "rgba(255,255,255,0.12)",
            LinesInputs = "#8E8E93",
            TableLines = "rgba(255,255,255,0.12)",
            Divider = "rgba(255,255,255,0.12)",
            TextPrimary = "#F5F5F7",
            TextSecondary = "#A1A1A6",
            TextDisabled = "#86868B",
            ActionDefault = "#A1A1A6",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px", DrawerWidthLeft = "240px" },
        Typography = new Typography
        {
            // 15px body, 34px/700 page titles (H4), tight display tracking.
            Default = new DefaultTypography { FontFamily = Fonts, FontSize = ".9375rem", LineHeight = "1.4667" },
            Body1 = new Body1Typography { FontSize = ".9375rem", LineHeight = "1.4667" },
            Body2 = new Body2Typography { FontSize = ".875rem", LineHeight = "1.43" },
            Subtitle1 = new Subtitle1Typography { FontSize = ".9375rem", FontWeight = "600" },
            Subtitle2 = new Subtitle2Typography { FontSize = ".875rem", FontWeight = "600" },
            Caption = new CaptionTypography { FontSize = ".75rem", LineHeight = "1.33" },
            H1 = new H1Typography { FontWeight = "700", LetterSpacing = "-.035em" },
            H2 = new H2Typography { FontWeight = "700", LetterSpacing = "-.03em" },
            H3 = new H3Typography { FontSize = "3rem", FontWeight = "700", LetterSpacing = "-.03em" },
            H4 = new H4Typography { FontSize = "2.125rem", LineHeight = "1.18", FontWeight = "700", LetterSpacing = "-.025em" },
            H5 = new H5Typography { FontSize = "1.375rem", LineHeight = "1.27", FontWeight = "600", LetterSpacing = "-.015em" },
            H6 = new H6Typography { FontSize = "1.1875rem", LineHeight = "1.26", FontWeight = "600", LetterSpacing = "-.01em" },
            Button = new ButtonTypography { FontSize = ".875rem", FontWeight = "500", TextTransform = "none", LetterSpacing = "-.01em" },
        },
    };
}
