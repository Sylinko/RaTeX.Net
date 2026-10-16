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
