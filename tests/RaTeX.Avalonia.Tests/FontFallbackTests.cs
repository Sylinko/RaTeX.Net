using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace RaTeX.Avalonia.Tests;

public sealed class FontFallbackTests
{
    [AvaloniaTheory]
    [InlineData("中")]
    [InlineData("😀")]
    public async Task FormulaLayout_WithHostFont_UsesMeasuredAdvanceAndInk(string character)
    {
        var source = $@"\text{{{character}}}";
        var initial = Ratex.ParseAndLayout(source);
        var glyph = Assert.IsType<GlyphPathItem>(Assert.Single(initial.Items));
        if (!FontManager.Current.TryMatchCharacter((int)glyph.CodePoint,
            FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, null, null, out _))
        {
            await Assert.ThrowsAsync<RatexRenderException>(() => FormulaLayout.ParseAndLayoutAsync(source));
            return;
        }

        var metric = Assert.Single(FormulaLayout.MeasureFallbackGlyphs(initial));
        var resolved = MathFonts.Current.ResolveGlyph(glyph.Font, glyph.CodePoint, true);
        Assert.True(resolved.Face.TryGetHorizontalGlyphAdvance(resolved.Index, out var advance));
        Assert.Equal((double)advance / resolved.Face.Metrics.DesignEmHeight, metric.Width, 8);
        Assert.True(metric.Height + metric.Depth > 0);
        var measured = await FormulaLayout.ParseAndLayoutAsync(source);
        Assert.Equal(metric.Width, measured.Width, 8);
        Assert.Equal(metric.Height, measured.Height, 8);
        Assert.Equal(metric.Depth, measured.Depth, 8);

        var pair = Ratex.ParseAndLayout($@"\text{{{character}{character}}}");
        Assert.Single(FormulaLayout.MeasureFallbackGlyphs(pair));
    }

    [AvaloniaFact]
    public async Task FormulaLayout_WithBundledGlyphs_PreservesOriginalLayout()
    {
        var initial = Ratex.ParseAndLayout(@"\frac{x}{\sqrt{y}} + \mathbb{R}");
        Assert.Empty(FormulaLayout.MeasureFallbackGlyphs(initial));
        var measured = await FormulaLayout.ParseAndLayoutAsync(@"\frac{x}{\sqrt{y}} + \mathbb{R}");
        Assert.Equal(initial.Width, measured.Width);
        Assert.Equal(initial.Items, measured.Items);
        await Assert.ThrowsAsync<RatexException>(() => FormulaLayout.ParseAndLayoutAsync(@"\frac{"));
    }

    [AvaloniaFact]
    public void MeasureFallbackGlyphs_WithScale_IgnoresNativeStyleScaling()
    {
        var initial = new DisplayList(1, 1, 1, 0,
            [new GlyphPathItem(0, 1, .7, "CJK-Regular", 'A', RatexColor.Black)]);
        var metric = Assert.Single(FormulaLayout.MeasureFallbackGlyphs(initial));
        var fullSize = initial with { Items = [new GlyphPathItem(0, 1, 1, "CJK-Regular", 'A', RatexColor.Black)] };
        Assert.Equal(metric, Assert.Single(FormulaLayout.MeasureFallbackGlyphs(fullSize)));
        Assert.Throws<RatexRenderException>(() => FormulaLayout.MeasureFallbackGlyphs(initial with
        {
            Items = [new GlyphPathItem(0, 1, 1, "CJK-Regular", 0x10FFFF, RatexColor.Black)]
        }));
    }

    [AvaloniaTheory]
    [InlineData("中")]
    [InlineData("한")]
    [InlineData("😀")]
    public void Prepare_WithFallbackCharacter_DrawsMatchedHostGlyph(string character)
    {
        var list = Ratex.ParseAndLayout($"\\text{{{character}}}");
        var glyph = Assert.IsType<GlyphPathItem>(Assert.Single(list.Items));
        var manager = FontManager.Current;
        var hasFont = manager.TryMatchCharacter(
            (int)glyph.CodePoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, null, null, out var typeface);

        if (!hasFont)
        {
            var error = Assert.Throws<RatexRenderException>(() => PreparedDisplayList.Prepare(list, 48));
            Assert.Contains("no configured or system fallback font", error.Message);
            return;
        }

        var resolved = MathFonts.Current.ResolveGlyph(glyph.Font, glyph.CodePoint, true);
        Assert.True(manager.TryGetGlyphTypeface(typeface, out var matchedFace));
        Assert.Same(matchedFace, resolved.Face);
        Assert.NotEqual((ushort)0, resolved.Index);
        Assert.Equal(glyph.CodePoint, resolved.CodePoint);

        using (var drawing = PreparedDisplayList.Prepare(list, 48))
        using (var bitmap = new RenderTargetBitmap(new PixelSize(128, 128)))
        {
            using (var context = bitmap.CreateDrawingContext())
            {
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 128, 128));
                using (context.PushTransform(Matrix.CreateTranslation(20, 20)))
                {
                    drawing.Draw(context);
                }
            }

            var pixels = new byte[128 * 128 * 4];
            var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, 128, 128), pinned.AddrOfPinnedObject(), pixels.Length, 128 * 4);
            }
            finally
            {
                pinned.Free();
            }

            Assert.True(
                Enumerable.Range(0, 128 * 128).Any(index =>
                    pixels[index * 4] < 250 || pixels[index * 4 + 1] < 250 || pixels[index * 4 + 2] < 250),
                "The fallback glyph drew no ink.");
        }

        // Disposing a prepared drawing must not dispose a face owned by FontManager.
        using var next = PreparedDisplayList.Prepare(list, 24);
    }

    [AvaloniaFact]
    public void FormulaControl_WhenFallbackIsToggled_RepreparesWithoutRelayout()
    {
        // An ASCII scalar in a fallback font slot makes this lifecycle check independent of optional CJK fonts.
        var list = new DisplayList(1, 1, 1, 0, [new GlyphPathItem(0, 1, 1, "CJK-Regular", 'A', RatexColor.Black)]);
        var control = new FormulaControl { DisplayList = list };
        Assert.Null(control.PreparationError);
        var count = control.PreparationCount;

        control.IsFontFallbackEnabled = false;
        Assert.NotNull(control.PreparationError);
        Assert.Contains("disabled", control.PreparationError.Message);
        control.IsFontFallbackEnabled = true;
        Assert.Null(control.PreparationError);
        Assert.Equal(count + 1, control.PreparationCount);
        Assert.Same(list, control.DisplayList);
        control.DisplayList = null;
    }
}
