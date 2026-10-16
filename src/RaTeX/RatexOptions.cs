namespace RaTeX;

/// <summary>
/// Selects the mathematical layout style used by the native engine.
/// </summary>
public enum RatexMode
{
    /// <summary>
    /// Uses compact inline spacing and operator limits.
    /// </summary>
    Inline,

    /// <summary>
    /// Uses display-style fractions and operator limits.
    /// </summary>
    Display
}

/// <summary>
/// Configures a single parse and layout operation.
/// </summary>
public sealed record RatexOptions
{
    /// <summary>
    /// Gets the mathematical layout style. The default is display style.
    /// </summary>
    public RatexMode Mode { get; init; } = RatexMode.Display;

    /// <summary>
    /// Gets the color used unless the source specifies another color. The default is opaque black.
    /// </summary>
    public RatexColor DefaultColor { get; init; } = RatexColor.Black;
}
