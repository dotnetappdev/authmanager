using MudBlazor;

namespace AuthManager.UI.Components.Layout;

/// <summary>
/// Builds the MudBlazor theme using BookIt dark palette for dark mode
/// and a clean professional palette for light mode.
/// </summary>
public static class AuthManagerThemeProvider
{
    public static MudTheme Build() => new()
    {
        PaletteLight = BuildLightPalette(),
        PaletteDark = BuildDarkPalette(),
        Typography = BuildTypography(),
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px",
            AppbarHeight = "64px"
        }
    };

    private static PaletteLight BuildLightPalette() => new()
    {
        Primary = "#6D28D9",           // Violet-700
        PrimaryContrastText = "#FFFFFF",
        Secondary = "#0891B2",          // Cyan-600
        SecondaryContrastText = "#FFFFFF",
        Tertiary = "#059669",           // Emerald-600
        Background = "#F8FAFC",
        BackgroundGray = "#F1F5F9",
        Surface = "#FFFFFF",
        DrawerBackground = "#1E1B4B",  // Indigo-950 — dark sidebar in light mode
        DrawerText = "#E0E7FF",
        DrawerIcon = "#A5B4FC",
        AppbarBackground = "#FFFFFF",
        AppbarText = "#0F172A",
        TextPrimary = "#0F172A",
        TextSecondary = "#475569",
        TextDisabled = "#94A3B8",
        ActionDefault = "#6D28D9",
        ActionDisabled = "#CBD5E1",
        ActionDisabledBackground = "#F1F5F9",
        Divider = "#E2E8F0",
        DividerLight = "#F1F5F9",
        TableLines = "#E2E8F0",
        TableStriped = "#F8FAFC",
        TableHover = "#EEF2FF",
        LinesDefault = "#E2E8F0",
        LinesInputs = "#CBD5E1",
        Success = "#059669",
        SuccessContrastText = "#FFFFFF",
        Warning = "#D97706",
        WarningContrastText = "#FFFFFF",
        Error = "#DC2626",
        ErrorContrastText = "#FFFFFF",
        Info = "#0891B2",
        InfoContrastText = "#FFFFFF",
        OverlayDark = "rgba(15, 23, 42, 0.5)",
        OverlayLight = "rgba(248, 250, 252, 0.8)"
    };

    // BookIt dark palette — deep navy/indigo with violet accents
    private static PaletteDark BuildDarkPalette() => new()
    {
        Primary = "#A78BFA",           // Violet-400 — bright on dark bg
        PrimaryContrastText = "#0B0F1A",
        Secondary = "#22D3EE",          // Cyan-400
        SecondaryContrastText = "#0B0F1A",
        Tertiary = "#34D399",           // Emerald-400
        Background = "#0B0F1A",        // BookIt: deepest navy
        BackgroundGray = "#111827",    // Gray-900
        Surface = "#161B2E",           // BookIt: card surface
        DrawerBackground = "#0D1117",  // BookIt: sidebar (darker than surface)
        DrawerText = "#E2E8F0",
        DrawerIcon = "#A78BFA",
        AppbarBackground = "#0D1117",
        AppbarText = "#F1F5F9",
        TextPrimary = "#F1F5F9",
        TextSecondary = "#94A3B8",
        TextDisabled = "#475569",
        ActionDefault = "#A78BFA",
        ActionDisabled = "#334155",
        ActionDisabledBackground = "#1E293B",
        Divider = "#1E293B",
        DividerLight = "#0F172A",
        TableLines = "#1E293B",
        TableStriped = "#0F172A",
        TableHover = "#1E2D4A",
        LinesDefault = "#1E293B",
        LinesInputs = "#334155",
        Success = "#34D399",
        SuccessContrastText = "#022C22",
        Warning = "#FBBF24",
        WarningContrastText = "#1C1917",
        Error = "#F87171",
        ErrorContrastText = "#1C1917",
        Info = "#38BDF8",
        InfoContrastText = "#0C1A2E",
        OverlayDark = "rgba(11, 15, 26, 0.8)",
        OverlayLight = "rgba(22, 27, 46, 0.6)"
    };

    private static Typography BuildTypography() => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = ["Inter", "Roboto", "-apple-system", "BlinkMacSystemFont", "sans-serif"],
            FontSize = "0.875rem",
            FontWeight = "400",
            LineHeight = "1.5",
            LetterSpacing = "normal"
        },
        H1 = new H1Typography { FontSize = "2rem", FontWeight = "700", LineHeight = "1.2" },
        H2 = new H2Typography { FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.3" },
        H3 = new H3Typography { FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.4" },
        H4 = new H4Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.4" },
        H5 = new H5Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.5" },
        H6 = new H6Typography { FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.5" },
        Body1 = new Body1Typography { FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.5" },
        Body2 = new Body2Typography { FontSize = "0.8125rem", FontWeight = "400", LineHeight = "1.5" },
        Caption = new CaptionTypography { FontSize = "0.75rem", FontWeight = "400", LineHeight = "1.4" },
        Button = new ButtonTypography { FontSize = "0.875rem", FontWeight = "500", TextTransform = "none" }
    };
}
