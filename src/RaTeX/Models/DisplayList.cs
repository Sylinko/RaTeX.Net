using System.Collections.Immutable;

namespace RaTeX;

/// <summary>
/// A fully managed, immutable layout snapshot. Geometry is in em, with y increasing downwards.
/// </summary>
/// <param name="ProtocolVersion">The native display list protocol version, currently 1.</param>
/// <param name="Width">The horizontal layout advance in em.</param>
/// <param name="Height">The distance from the layout origin to the baseline in em.</param>
/// <param name="Depth">The distance below the baseline in em.</param>
/// <param name="Items">Drawing instructions in paint order.</param>
public sealed record DisplayList(
    int ProtocolVersion,
    double Width,
    double Height,
    double Depth,
    ImmutableArray<DisplayItem> Items
)
{
    /// <summary>
    /// Gets the full vertical layout extent in em.
    /// </summary>
    public double TotalHeight => Height + Depth;
}

/// <summary>
/// A straight-alpha RGBA color with finite components in the inclusive range [0, 1].
/// </summary>
/// <param name="R">The red component.</param>
/// <param name="G">The green component.</param>
/// <param name="B">The blue component.</param>
/// <param name="A">The opacity, where 0 is transparent and 1 is opaque.</param>
public readonly record struct RatexColor(float R, float G, float B, float A)
{
    /// <summary>
    /// Gets opaque black.
    /// </summary>
    public static RatexColor Black => new(0, 0, 0, 1);
}

/// <summary>
/// A drawing instruction positioned relative to the layout origin.
/// </summary>
/// <param name="X">The horizontal position in em.</param>
/// <param name="Y">The vertical position in em, increasing downwards.</param>
/// <param name="Color">The final color of this instruction.</param>
public abstract record DisplayItem(double X, double Y, RatexColor Color);

/// <summary>
/// Draws one glyph from an exact upstream font at its baseline origin.
/// </summary>
/// <param name="X">The glyph's horizontal baseline position in em.</param>
/// <param name="Y">The glyph's vertical baseline position in em.</param>
/// <param name="Scale">The glyph size relative to the formula's rendering font size.</param>
/// <param name="Font">The upstream font ID, such as Main-Regular.</param>
/// <param name="CodePoint">The Unicode scalar selected by the layout engine.</param>
/// <param name="Color">The glyph's final color.</param>
public sealed record GlyphPathItem(
    double X,
    double Y,
    double Scale,
    string Font,
    uint CodePoint,
    RatexColor Color
) : DisplayItem(X, Y, Color);

/// <summary>
/// Draws a horizontal rule centered on its vertical position.
/// </summary>
/// <param name="X">The start position in em.</param>
/// <param name="Y">The vertical center of the rule in em.</param>
/// <param name="Width">The horizontal extent in em.</param>
/// <param name="Thickness">The stroke thickness in em.</param>
/// <param name="Dashed">Whether to draw alternating dashes and gaps.</param>
/// <param name="Color">The rule's final color.</param>
public sealed record LineItem(
    double X,
    double Y,
    double Width,
    double Thickness,
    bool Dashed,
    RatexColor Color
) : DisplayItem(X, Y, Color);

/// <summary>
/// Draws a filled rectangle, preserving the signed native extents.
/// </summary>
/// <param name="X">The horizontal origin in em.</param>
/// <param name="Y">The vertical origin in em.</param>
/// <param name="Width">The signed horizontal extent in em.</param>
/// <param name="Height">The signed vertical extent in em.</param>
/// <param name="Color">The rectangle's final color.</param>
public sealed record RectItem(
    double X,
    double Y,
    double Width,
    double Height,
    RatexColor Color
) : DisplayItem(X, Y, Color);

/// <summary>
/// Draws vector contours translated by the item's origin.
/// </summary>
/// <param name="X">The horizontal translation in em.</param>
/// <param name="Y">The vertical translation in em.</param>
/// <param name="Commands">Path-local commands in em; a nonempty path starts with MoveTo.</param>
/// <param name="Fill">Whether to fill the contours instead of stroking them.</param>
/// <param name="Color">The path's final color.</param>
public sealed record PathItem(
    double X,
    double Y,
    ImmutableArray<PathCommand> Commands,
    bool Fill,
    RatexColor Color
) : DisplayItem(X, Y, Color);

/// <summary>
/// A vector instruction with coordinates relative to its containing path, in em.
/// </summary>
public abstract record PathCommand;

/// <summary>
/// Begins a contour at the specified position.
/// </summary>
/// <param name="X">The horizontal position.</param>
/// <param name="Y">The vertical position.</param>
public sealed record MoveTo(double X, double Y) : PathCommand;

/// <summary>
/// Adds a straight segment from the current point to the specified position.
/// </summary>
/// <param name="X">The horizontal endpoint.</param>
/// <param name="Y">The vertical endpoint.</param>
public sealed record LineTo(double X, double Y) : PathCommand;

/// <summary>
/// Adds a cubic Bezier segment using two control points and an endpoint.
/// </summary>
/// <param name="X1">The first control point's horizontal position.</param>
/// <param name="Y1">The first control point's vertical position.</param>
/// <param name="X2">The second control point's horizontal position.</param>
/// <param name="Y2">The second control point's vertical position.</param>
/// <param name="X">The horizontal endpoint.</param>
/// <param name="Y">The vertical endpoint.</param>
public sealed record CubicTo(double X1, double Y1, double X2, double Y2, double X, double Y) : PathCommand;

/// <summary>
/// Adds a quadratic Bezier segment using one control point and an endpoint.
/// </summary>
/// <param name="X1">The control point's horizontal position.</param>
/// <param name="Y1">The control point's vertical position.</param>
/// <param name="X">The horizontal endpoint.</param>
/// <param name="Y">The vertical endpoint.</param>
public sealed record QuadTo(double X1, double Y1, double X, double Y) : PathCommand;

/// <summary>
/// Closes the current contour by joining its current point to its start.
/// </summary>
public sealed record Close : PathCommand;
