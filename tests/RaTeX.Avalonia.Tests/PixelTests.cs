using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace RaTeX.Avalonia.Tests;

public sealed class PixelTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void Render_WithReferenceCorpus_MatchesInkAndDimensions()
    {
        var formulas = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "formulas.txt"));
        Assert.NotEmpty(formulas);
        for (var i = 0; i < formulas.Length; i++)
        {
            var list = Ratex.ParseAndLayout(formulas[i]);
            var formula = new FormulaControl { UseLayoutRounding = false, DisplayList = list, FontSize = 28, Padding = new Thickness(5) };
            Assert.Null(formula.PreparationError);
            var root = new Border { UseLayoutRounding = false, Background = Brushes.White, Child = formula };
            root.Measure(Size.Infinity);
            root.Arrange(new Rect(root.DesiredSize));
            var pixels = new PixelSize((int)Math.Ceiling(root.DesiredSize.Width * 2), (int)Math.Ceiling(root.DesiredSize.Height * 2));
            using var actual = new RenderTargetBitmap(pixels, new Vector(192, 192));
            actual.Render(root);
            using var expected = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", $"{i + 1:0000}.png"));
            Assert.Equal(expected.PixelSize, actual.PixelSize);
            var a = Pixels(actual);
            var b = Pixels(expected);
            foreach (var line in list.Items.OfType<LineItem>().Where(line => line.Dashed))
            {
                // The reference PNG renderer uses 4t dashes; the chosen Cairo semantics use 3t.
                // Compare the remaining formula and verify the dash itself independently below.
                var left = Math.Max(0, (int)Math.Floor((line.X * 28 + 5) * 2) - 2);
                var right = Math.Min(pixels.Width, (int)Math.Ceiling(((line.X + line.Width) * 28 + 5) * 2) + 2);
                var top = Math.Max(0, (int)Math.Floor((line.Y * 28 + 5 - Math.Max(.5, line.Thickness * 28) / 2) * 2) - 2);
                var bottom = Math.Min(pixels.Height, (int)Math.Ceiling((line.Y * 28 + 5 + Math.Max(.5, line.Thickness * 28) / 2) * 2) + 2);
                for (var y = top; y < bottom; y++)
                {
                    for (var x = left; x < right; x++)
                    {
                        for (var channel = 0; channel < 4; channel++)
                        {
                            a[(y * pixels.Width + x) * 4 + channel] = b[(y * pixels.Width + x) * 4 + channel] = 255;
                        }
                    }
                }

                output.WriteLine($"Reference {i + 1:00}: horizontal dash band excluded (upstream Cairo/PNG policy difference); covered by Render_WithDashedRule_UsesThreeLineWidthPeriods.");
            }
            var similarity = InkIntersectionOverUnion(a, b);
            var coverage = Math.Min(Coverage(a, b, pixels), Coverage(b, a, pixels));
            output.WriteLine($"Reference {i + 1:00}: {similarity:F4} ink IoU; {coverage:F4} 1px coverage; {pixels}");
            Assert.True(similarity >= .70, $"Reference {i + 1} ink IoU {similarity:F4} < .70: {formulas[i]}");
            Assert.True(coverage >= .98, $"Reference {i + 1} 1px coverage {coverage:F4} < .98: {formulas[i]}");
            formula.DisplayList = null;
        }
    }

    [AvaloniaFact]
    public void Render_WithDashedRule_UsesThreeLineWidthPeriods()
    {
        using var prepared = PreparedDisplayList.Prepare(new(1, 6, 1, 0, [new LineItem(0, 1, 6, .2, true, RatexColor.Black)]), 10);
        using var bitmap = new RenderTargetBitmap(new PixelSize(60, 20));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 60, 20));
            prepared.Draw(context);
        }
        var pixels = Pixels(bitmap);
        for (var period = 0; period < 5; period++)
        {
            Assert.True(IsInk(pixels, (10 * 60 + period * 12 + 2) * 4));
            Assert.False(IsInk(pixels, (10 * 60 + period * 12 + 8) * 4));
        }
    }

    [AvaloniaFact]
    public void Draw_WithOpacityTransformAndClip_ComposesOnce()
    {
        var list = new DisplayList(1, 2, 1, 0, [new RectItem(0, 0, 2, 1, new(1, 0, 0, .5f))]);
        using var prepared = PreparedDisplayList.Prepare(list, 10);
        using var bitmap = new RenderTargetBitmap(new PixelSize(80, 40));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 80, 40));
            using (context.PushClip(new Rect(10, 10, 15, 20)))
            using (context.PushOpacity(.5))
            using (context.PushTransform(new Matrix(2, 0, 0, 2, 10, 10)))
            {
                prepared.Draw(context);
            }
        }
        var data = Pixels(bitmap);
        var inside = (15 * 80 + 15) * 4;
        Assert.InRange(data[inside], 189, 193);
        Assert.InRange(data[inside + 1], 189, 193);
        Assert.Equal(255, data[inside + 2]);
        var outside = (15 * 80 + 30) * 4;
        Assert.All(data.Skip(outside).Take(4), v => Assert.Equal(255, v));
        var beforeTransform = (5 * 80 + 5) * 4;
        Assert.All(data.Skip(beforeTransform).Take(4), v => Assert.Equal(255, v));
    }

    private static byte[] Pixels(Bitmap bitmap)
    {
        var pixels = new byte[bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), pinned.AddrOfPinnedObject(), pixels.Length, bitmap.PixelSize.Width * 4);
        }
        finally
        {
            pinned.Free();
        }
        return pixels;
    }

    private static double InkIntersectionOverUnion(byte[] a, byte[] b)
    {
        var intersection = 0;
        var union = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            var inkA = IsInk(a, i);
            var inkB = IsInk(b, i);
            if (inkA || inkB)
            {
                union++;
            }

            if (inkA && inkB)
            {
                intersection++;
            }
        }
        Assert.True(union > 0, "Reference cannot be blank.");
        return (double)intersection / union;
    }

    private static double Coverage(byte[] source, byte[] target, PixelSize size)
    {
        var ink = 0;
        var covered = 0;
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var offset = (y * size.Width + x) * 4;
                if (!IsInk(source, offset))
                {
                    continue;
                }

                ink++;
                var found = false;
                for (var dy = -1; dy <= 1 && !found; dy++)
                {
                    for (var dx = -1; dx <= 1 && !found; dx++)
                    {
                        var tx = x + dx;
                        var ty = y + dy;
                        if (tx < 0 || ty < 0 || tx >= size.Width || ty >= size.Height)
                        {
                            continue;
                        }

                        var index = (ty * size.Width + tx) * 4;
                        found = IsInk(target, index);
                    }
                }

                if (found)
                {
                    covered++;
                }
            }
        }

        Assert.True(ink > 0);
        return (double)covered / ink;
    }
    // CopyPixels is premultiplied BGRA. Composite fractional transparent edge pixels onto white.
    private static bool IsInk(byte[] pixels, int offset)
    {
        return Math.Min(pixels[offset], Math.Min(pixels[offset + 1], pixels[offset + 2])) + 255 - pixels[offset + 3] < 160;
    }
}
