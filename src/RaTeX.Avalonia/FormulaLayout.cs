using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;

namespace RaTeX.Avalonia;

/// <summary>
/// Combines native mathematical layout with measurements from Avalonia's fallback fonts.
/// </summary>
public static class FormulaLayout
{
    /// <summary>
    /// Parses on a worker thread, measures fallback glyphs on the UI thread, then lays out again when needed.
    /// </summary>
    /// <remarks>
    /// Call on the UI thread. Request a new layout after changing configured fonts, but not font size.
    /// Only formulas with fallback glyphs require a second native call and the fallback metrics C ABI.
    /// </remarks>
    /// <param name="latex">LaTeX source to parse.</param>
    /// <param name="options">Layout options, or null to use defaults.</param>
    /// <returns>An immutable layout using the same fonts as <see cref="PreparedDisplayList"/>.</returns>
    /// <exception cref="RatexRenderException">A fallback glyph cannot be resolved or measured consistently.</exception>
    public static async Task<DisplayList> ParseAndLayoutAsync(string latex, RatexOptions? options = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        var initial = await Task.Run(() => Ratex.ParseAndLayout(latex, options));
        var metrics = MeasureFallbackGlyphs(initial);
        return metrics.Length == 0
            ? initial
            : await Task.Run(() => Ratex.ParseAndLayoutWithFallbackMetrics(latex, metrics, options));
    }

    /// <summary>
    /// Measures the fallback glyphs of an initial layout in base-size em on the UI thread.
    /// </summary>
    /// <remarks>
    /// Pass the result and original source/options to <see cref="Ratex.ParseAndLayoutWithFallbackMetrics"/>.
    /// Native layout applies TeX scaling. Multiple fallback faces for one scalar cannot share a metrics entry.
    /// </remarks>
    /// <param name="displayList">An initial native layout whose fallback characters should be measured.</param>
    /// <returns>One measurement per distinct fallback scalar; an empty array for bundled-font-only layouts.</returns>
    public static FallbackGlyphMetrics[] MeasureFallbackGlyphs(DisplayList displayList)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(displayList);
        var measured = new Dictionary<uint, (GlyphTypeface Face, ushort Index, FallbackGlyphMetrics Metrics)>();
        var visited = new HashSet<(string Font, uint CodePoint)>();
        foreach (var item in displayList.Items)
        {
            if (item is not GlyphPathItem glyph || !visited.Add((glyph.Font, glyph.CodePoint)))
            {
                continue;
            }

            if (!Rune.IsValid(glyph.CodePoint))
            {
                throw new RatexRenderException("Invalid Unicode scalar in layout.");
            }

            var resolved = MathFonts.Current.ResolveGlyph(glyph.Font, glyph.CodePoint, true);
            if (!resolved.IsFallback)
            {
                continue;
            }

            if (measured.TryGetValue(glyph.CodePoint, out var previous))
            {
                if (!ReferenceEquals(previous.Face, resolved.Face) || previous.Index != resolved.Index)
                {
                    throw new RatexRenderException($"U+{glyph.CodePoint:X} requires multiple fallback fonts; the native metrics table supports only one per scalar.");
                }

                continue;
            }

            // At design-em size, use advance for width and actual ink bounds for height/depth.
            var em = resolved.Face.Metrics.DesignEmHeight;
            using var run = new GlyphRun(resolved.Face, em,
                char.ConvertFromUtf32((int)resolved.CodePoint).AsMemory(), [resolved.Index], new Point(0, 0));
            var ink = run.InkBounds;
            var metric = new FallbackGlyphMetrics(glyph.CodePoint,
                run.Metrics.WidthIncludingTrailingWhitespace / em,
                Math.Max(0, -ink.Top) / em,
                Math.Max(0, ink.Bottom) / em);
            if (!double.IsFinite(metric.Width) || !double.IsFinite(metric.Height) || !double.IsFinite(metric.Depth))
            {
                throw new RatexRenderException($"Non-finite host metrics for U+{glyph.CodePoint:X}.");
            }

            measured.Add(glyph.CodePoint, (resolved.Face, resolved.Index, metric));
        }

        return measured.Values.Select(value => value.Metrics).ToArray();
    }
}
