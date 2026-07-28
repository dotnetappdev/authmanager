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
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px",
            AppbarHeight = "64px"
        }
    };

    /// <summary>
    /// High-contrast accessibility theme — pure black background, yellow text and
    /// borders. Uses the same palette for both light/dark slots since this theme
    /// doesn't have a light/dark variant of its own.
    /// </summary>
    public static MudTheme BuildHighContrast() => new()
    {
        PaletteLight = BuildHighContrastPalette(),
        PaletteDark = BuildHighContrastDarkPalette(),
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

    // High-contrast accessibility palette — pure black with yellow text/borders.
    // Success/Warning/Error keep distinct hues (green/orange/red) since collapsing
    // every status indicator to the same yellow would hurt usability rather than help it.
    private static PaletteLight BuildHighContrastPalette() => new()
    {
        Primary = "#FFFF00",
        PrimaryContrastText = "#000000",
        Secondary = "#FFFF00",
        SecondaryContrastText = "#000000",
        Tertiary = "#FFFF00",
        TertiaryContrastText = "#000000",
        Background = "#000000",
        BackgroundGray = "#000000",
        Surface = "#000000",
        DrawerBackground = "#000000",
        DrawerText = "#FFFF00",
        DrawerIcon = "#FFFF00",
        AppbarBackground = "#000000",
        AppbarText = "#FFFF00",
        TextPrimary = "#FFFF00",
        TextSecondary = "#FFFF00",
        TextDisabled = "#808000",
        ActionDefault = "#FFFF00",
        ActionDisabled = "#808000",
        ActionDisabledBackground = "#000000",
        Divider = "#FFFF00",
        DividerLight = "#FFFF00",
        TableLines = "#FFFF00",
        TableStriped = "#0D0D00",
        TableHover = "#1A1A00",
        LinesDefault = "#FFFF00",
        LinesInputs = "#FFFF00",
        Success = "#00FF00",
        SuccessContrastText = "#000000",
        Warning = "#FFA500",
        WarningContrastText = "#000000",
        Error = "#FF3333",
        ErrorContrastText = "#000000",
        Info = "#00FFFF",
        InfoContrastText = "#000000",
        OverlayDark = "rgba(0, 0, 0, 0.9)",
        OverlayLight = "rgba(0, 0, 0, 0.7)"
    };

    private static PaletteDark BuildHighContrastDarkPalette() => new()
    {
        Primary = "#FFFF00",
        PrimaryContrastText = "#000000",
        Secondary = "#FFFF00",
        SecondaryContrastText = "#000000",
        Tertiary = "#FFFF00",
        TertiaryContrastText = "#000000",
        Background = "#000000",
        BackgroundGray = "#000000",
        Surface = "#000000",
        DrawerBackground = "#000000",
        DrawerText = "#FFFF00",
        DrawerIcon = "#FFFF00",
        AppbarBackground = "#000000",
        AppbarText = "#FFFF00",
        TextPrimary = "#FFFF00",
        TextSecondary = "#FFFF00",
        TextDisabled = "#808000",
        ActionDefault = "#FFFF00",
        ActionDisabled = "#808000",
        ActionDisabledBackground = "#000000",
        Divider = "#FFFF00",
        DividerLight = "#FFFF00",
        TableLines = "#FFFF00",
        TableStriped = "#0D0D00",
        TableHover = "#1A1A00",
        LinesDefault = "#FFFF00",
        LinesInputs = "#FFFF00",
        Success = "#00FF00",
        SuccessContrastText = "#000000",
        Warning = "#FFA500",
        WarningContrastText = "#000000",
        Error = "#FF3333",
        ErrorContrastText = "#000000",
        Info = "#00FFFF",
        InfoContrastText = "#000000",
        OverlayDark = "rgba(0, 0, 0, 0.9)",
        OverlayLight = "rgba(0, 0, 0, 0.7)"
    };

}
