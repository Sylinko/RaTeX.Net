namespace RaTeX;

/// <summary>
/// Describes a fallback glyph in base-size em using the font the host will draw.
/// </summary>
/// <remarks>
/// Layout applies TeX scaling. Metrics are keyed by Unicode scalar, not font style or shaped text run.
/// </remarks>
/// <param name="CodePoint">The Unicode scalar identifying the glyph.</param>
/// <param name="Width">The finite horizontal advance in em, not the ink bounding-box width.</param>
/// <param name="Height">The finite height above the baseline in em.</param>
/// <param name="Depth">The finite depth below the baseline in em.</param>
public readonly record struct FallbackGlyphMetrics(uint CodePoint, double Width, double Height, double Depth);
