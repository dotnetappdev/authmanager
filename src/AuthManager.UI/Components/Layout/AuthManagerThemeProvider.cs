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
        // See BuildDarkPalette's Dark override — MudBlazor's built-in "Dark" swatch backs
        // .mud-layout (the gaps around the page container) regardless of light/dark theme,
        // so it needs pinning to match Background here too, or it paints a stray dark patch.
        Dark = "#F8FAFC",
        DarkContrastText = "#0F172A",
        DarkLighten = "#FFFFFF",
        DarkDarken = "#F1F5F9",
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

    // Monochrome dark palette — neutral charcoal surfaces, white/gray text and interactive
    // chrome throughout. No blue (or any other hue) accent anywhere — buttons, nav highlight,
    // avatars, and icons are all white/gray on dark, matching a genuinely neutral dark theme
    // (the same idea as this very chat UI's own dark mode). Success/Warning/Error are the only
    // colors left, since they carry real semantic meaning (status, not brand/chrome).
    private static PaletteDark BuildDarkPalette() => new()
    {
        Primary = "#FFFFFF",
        PrimaryContrastText = "#1A1A1A",
        // Secondary is used all over the UI for captions/subtitles (MudText Color="Color.Secondary"),
        // not just as a second brand accent — keep it a neutral gray so those reads as muted helper
        // text (like a password manager's dark theme), not as a second, competing splash of color.
        Secondary = "#9A9A9A",
        SecondaryContrastText = "#1A1A1A",
        Tertiary = "#C5C5C5",
        // MudBlazor's own built-in "Dark" swatch (default #27272f, a violet-tinted charcoal)
        // backs .mud-layout — the full-viewport backdrop behind the page container — and isn't
        // covered by any of the fields above, so left alone it shows through as a purple cast
        // behind every grid/table. Pin it to the same neutral charcoal as everything else.
        Dark = "#202020",
        DarkContrastText = "#FFFFFF",
        DarkLighten = "#2C2C2C",
        DarkDarken = "#1A1A1A",
        Background = "#202020",        // Fluent solid background base
        BackgroundGray = "#1A1A1A",
        Surface = "#2C2C2C",           // Fluent card/layer background
        DrawerBackground = "#1F1F1F",  // Nav pane — subtly darker than content
        DrawerText = "#E8E8E8",
        DrawerIcon = "#C5C5C5",
        AppbarBackground = "#1F1F1F",
        AppbarText = "#FFFFFF",
        TextPrimary = "#FFFFFF",
        TextSecondary = "#C5C5C5",
        TextDisabled = "#6E6E6E",
        ActionDefault = "#E8E8E8",
        ActionDisabled = "#4A4A4A",
        ActionDisabledBackground = "#2A2A2A",
        Divider = "#3B3B3B",
        DividerLight = "#2A2A2A",
        TableLines = "#3B3B3B",
        TableStriped = "#262626",
        TableHover = "#333333",
        LinesDefault = "#3B3B3B",
        LinesInputs = "#4A4A4A",
        Success = "#6CCB5F",
        SuccessContrastText = "#0A0A0A",
        Warning = "#FFB900",
        WarningContrastText = "#0A0A0A",
        Error = "#FF6961",
        ErrorContrastText = "#0A0A0A",
        Info = "#C5C5C5",
        InfoContrastText = "#0A0A0A",
        OverlayDark = "rgba(0, 0, 0, 0.7)",
        OverlayLight = "rgba(44, 44, 44, 0.6)"
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
        Dark = "#000000",
        DarkContrastText = "#FFFF00",
        DarkLighten = "#000000",
        DarkDarken = "#000000",
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
        Dark = "#000000",
        DarkContrastText = "#FFFF00",
        DarkLighten = "#000000",
        DarkDarken = "#000000",
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
