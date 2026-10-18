using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace RaTeX.Avalonia;

/// <summary>
/// Reports unsupported fonts, missing glyphs, or invalid drawing geometry during preparation.
/// </summary>
/// <param name="message">A description of the rendering failure.</param>
/// <param name="inner">The underlying preparation failure, if any.</param>
public sealed class RatexRenderException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Owns drawing resources for one layout and font size. Prepare, draw, and dispose on the UI thread.
/// </summary>
public sealed class PreparedDisplayList : IDisposable
{
    private readonly List<Operation> _operations = [];
    private bool _isDisposed;

    private PreparedDisplayList()
    {
    }

    /// <summary>
    /// Prepares a layout with optional character-level fallback through Avalonia's font manager.
    /// </summary>
    /// <remarks>
    /// Preserves supplied positions and metrics; use <see cref="FormulaLayout"/> for host-measured layout.
    /// Does not reshape or reflow text, or guarantee color emoji and multi-codepoint sequence rendering.
    /// Applications can configure preferred fallback fonts through FontManagerOptions.FontFallbacks.
    /// </remarks>
    /// <param name="displayList">The managed layout to prepare without modifying it.</param>
    /// <param name="fontSize">The positive, finite size of one em in device-independent pixels.</param>
    /// <param name="isFontFallbackEnabled">Whether missing glyphs may use configured or system fonts.</param>
    /// <returns>Drawing resources owned by the caller and requiring disposal on the UI thread.</returns>
    public static PreparedDisplayList Prepare(DisplayList displayList, double fontSize, bool isFontFallbackEnabled = true)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(displayList);
        if (!double.IsFinite(fontSize) || fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        }

        var prepared = new PreparedDisplayList();
        try
        {
            if (displayList.ProtocolVersion != 1 || displayList.Items.IsDefault)
            {
                throw new RatexRenderException("Invalid display list version or item array.");
            }

            Check(displayList.Width, displayList.Height, displayList.Depth, displayList.TotalHeight);
            Check(displayList.Width * fontSize, displayList.Height * fontSize, displayList.TotalHeight * fontSize);
            foreach (var item in displayList.Items)
            {
                if (item is null)
                {
                    throw new RatexRenderException("Null display item.");
                }

                Check(item.X * fontSize, item.Y * fontSize);
                var brush = ToBrush(item.Color);
                var x = item.X * fontSize;
                var y = item.Y * fontSize;
                switch (item)
                {
                    case GlyphPathItem glyph:
                        Check(glyph.Scale * fontSize);
                        if (glyph.Scale <= 0 || !Rune.IsValid(glyph.CodePoint))
                        {
                            throw new RatexRenderException("Invalid glyph scale or Unicode scalar.");
                        }

                        var (face, index, cp, _) = MathFonts.Current.ResolveGlyph(glyph.Font, glyph.CodePoint, isFontFallbackEnabled);

                        var run = new GlyphRun(
                            face,
                            glyph.Scale * fontSize,
                            char.ConvertFromUtf32((int)cp).AsMemory(),
                            [index],
                            new Point(x, y));
                        prepared._operations.Add(new Operation(brush, Run: run));
                        break;
                    case LineItem line:
                        Check(line.Width * fontSize, line.Thickness * fontSize);
                        var thickness = Math.Max(.5, line.Thickness * fontSize);
                        prepared._operations.Add(
                            new Operation(
                                brush,
                                Pen: new Pen(brush, thickness, line.Dashed ? new DashStyle([3, 3], 0) : null),
                                Start: new Point(x, y),
                                End: new Point(x + line.Width * fontSize, y)));
                        break;
                    case RectItem rect:
                        Check(rect.Width * fontSize, rect.Height * fontSize);
                        var right = x + rect.Width * fontSize;
                        var bottom = y + rect.Height * fontSize;
                        Check(right, bottom);
                        prepared._operations.Add(
                            new Operation(
                                brush,
                                Rectangle: new Rect(
                                    Math.Min(x, right),
                                    Math.Min(y, bottom),
                                    Math.Abs(right - x),
                                    Math.Abs(bottom - y))));
                        break;
                    case PathItem path:
                        prepared.AddPath(path, fontSize, brush);
                        break;
                    default:
                        throw new RatexRenderException($"Unsupported display item '{item.GetType().Name}'.");
                }
            }
            return prepared;
        }
        catch
        {
            prepared.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Draws the prepared resources at the context origin without parsing or rebuilding geometry.
    /// </summary>
    /// <param name="context">The destination context, including any desired transform, opacity, or clip.</param>
    /// <exception cref="ObjectDisposedException">The prepared resources have been disposed.</exception>
    public void Draw(DrawingContext context)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        foreach (var op in _operations)
        {
            if (op.Run is { } run)
            {
                context.DrawGlyphRun(op.Brush, run);
            }
            else if (op.Geometry is { } geometry)
            {
                context.DrawGeometry(op.Pen is null ? op.Brush : null, op.Pen, geometry);
            }
            else if (op.Rectangle is { } rect)
            {
                context.DrawRectangle(op.Brush, null, rect);
            }
            else
            {
                context.DrawLine(op.Pen!, op.Start, op.End);
            }
        }
    }

    /// <summary>
    /// Releases owned glyph runs on the UI thread. Repeated disposal is harmless.
    /// </summary>
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        // Avalonia 12.1.4's render data clones the platform run reference when recording DrawGlyphRun.
        foreach (var op in _operations)
        {
            op.Run?.Dispose();
        }

        _operations.Clear();
    }

    private void AddPath(PathItem path, double size, IBrush brush)
    {
        if (path.Commands.IsDefault)
        {
            throw new RatexRenderException("Missing path commands.");
        }
        // Upstream Cairo fills each MoveTo contour separately, using EvenOdd within that contour.
        var start = 0;
        for (var i = 1; path.Fill && i < path.Commands.Length; i++)
        {
            if (path.Commands[i] is MoveTo)
            {
                AddSegment(start, i);
                start = i;
            }
        }

        AddSegment(start, path.Commands.Length);

        void AddSegment(int from, int to)
        {
            if (from == to)
            {
                return;
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.SetFillRule(FillRule.EvenOdd);
                var isOpen = false;
                var hasStart = false;
                var startPoint = default(Point);
                foreach (var command in path.Commands.AsSpan(from, to - from))
                {
                    if (command is MoveTo move)
                    {
                        if (isOpen)
                        {
                            context.EndFigure(false);
                        }

                        startPoint = Point(move.X, move.Y);
                        context.BeginFigure(startPoint, path.Fill);
                        isOpen = true;
                        hasStart = true;
                        continue;
                    }
                    if (!hasStart)
                    {
                        throw new RatexRenderException("Path must start with MoveTo.");
                    }

                    if (!isOpen)
                    {
                        context.BeginFigure(startPoint, path.Fill);
                        isOpen = true;
                    }
                    switch (command)
                    {
                        case LineTo line:
                            context.LineTo(Point(line.X, line.Y));
                            break;
                        case QuadTo quad:
                            context.QuadraticBezierTo(Point(quad.X1, quad.Y1), Point(quad.X, quad.Y));
                            break;
                        case CubicTo cubic:
                            context.CubicBezierTo(Point(cubic.X1, cubic.Y1), Point(cubic.X2, cubic.Y2), Point(cubic.X, cubic.Y));
                            break;
                        case Close:
                            context.EndFigure(true);
                            isOpen = false;
                            break;
                        default:
                            throw new RatexRenderException("Unsupported path command.");
                    }
                }
                if (isOpen)
                {
                    context.EndFigure(false);
                }
            }
            _operations.Add(new Operation(brush, Geometry: geometry, Pen: path.Fill ? null : new Pen(brush, 1.5)));
        }

        Point Point(double x, double y)
        {
            x = (path.X + x) * size;
            y = (path.Y + y) * size;
            Check(x, y);
            return new Point(x, y);
        }
    }

    private static ImmutableSolidColorBrush ToBrush(RatexColor color)
    {
        static byte Channel(float value) =>
            float.IsFinite(value)
            && value is >= 0 and <= 1 ?
                (byte)Math.Round(value * 255) :
                throw new RatexRenderException("Invalid RGBA.");

        return new ImmutableSolidColorBrush(Color.FromArgb(Channel(color.A), Channel(color.R), Channel(color.G), Channel(color.B)));
    }

    private static void Check(params ReadOnlySpan<double> values)
    {
        foreach (var v in values)
        {
            if (!double.IsFinite(v))
            {
                throw new RatexRenderException("Non-finite drawing geometry.");
            }
        }
    }

    private sealed record Operation(
        IBrush Brush,
        GlyphRun? Run = null,
        StreamGeometry? Geometry = null,
        IPen? Pen = null,
        Rect? Rectangle = null,
        Point Start = default,
        Point End = default
    );
}
