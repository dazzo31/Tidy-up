namespace TidyUp.Styles;

/// <summary>
/// Resource keys for the Win11 colour tokens in Colors.xaml / Colors.Dark.xaml.
/// View-models return these instead of colours, and <see cref="ThemeBrush"/> resolves them live,
/// so every badge follows the active theme. ThemeTokenTests keeps this list and both palettes in step.
/// </summary>
public static class ThemeTokens
{
    // Surfaces & text
    public const string Surface = "Win11SurfaceBrush";
    public const string SubtleSurface = "Win11SubtleSurfaceBrush";
    public const string SubtleBorder = "Win11SubtleBorderBrush";
    public const string TextPrimary = "Win11TextPrimaryBrush";
    public const string TextSecondary = "Win11TextSecondaryBrush";
    public const string TextDisabled = "Win11TextDisabledBrush";
    public const string Accent = "Win11AccentBrush";
    public const string AccentLight = "Win11AccentLightBrush";

    // Semantic text/icon colours, each with a tinted background that keeps AA contrast behind it
    public const string Success = "Win11SuccessBrush";
    public const string SuccessBackground = "Win11SuccessBackgroundBrush";
    public const string Warning = "Win11WarningBrush";
    public const string WarningBackground = "Win11WarningBackgroundBrush";
    public const string Error = "Win11ErrorBrush";
    public const string ErrorBackground = "Win11ErrorBackgroundBrush";
    public const string Info = "Win11InfoBrush";
    public const string InfoBackground = "Win11InfoBackgroundBrush";
    public const string Purple = "Win11PurpleBrush";
    public const string PurpleBackground = "Win11PurpleBackgroundBrush";
    public const string Teal = "Win11TealBrush";
    public const string TealBackground = "Win11TealBackgroundBrush";
    public const string Neutral = "Win11NeutralBrush";
    public const string NeutralBackground = "Win11NeutralBackgroundBrush";

    // Solid fills carrying OnSolid text (identical in both themes)
    public const string SuccessSolid = "Win11SuccessSolidBrush";
    public const string ErrorSolid = "Win11ErrorSolidBrush";
    public const string WarningSolid = "Win11WarningSolidBrush";
    public const string InfoSolid = "Win11InfoSolidBrush";
    public const string PurpleSolid = "Win11PurpleSolidBrush";
    public const string TealSolid = "Win11TealSolidBrush";
    public const string NeutralSolid = "Win11NeutralSolidBrush";
    public const string OnSolid = "Win11OnSolidBrush";

    // Monospace diagnostics panel
    public const string CodeSurface = "Win11CodeSurfaceBrush";
    public const string CodeText = "Win11CodeTextBrush";
}
