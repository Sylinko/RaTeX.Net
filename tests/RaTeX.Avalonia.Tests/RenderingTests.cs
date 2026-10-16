using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

namespace RaTeX.Avalonia.Tests;

public class RenderingTests
{
    [AvaloniaFact]
    public void FormulaControl_OnLayoutChanges_ReusesAndRebuildsResources()
    {
        var list = Ratex.ParseAndLayout(@"\frac{x_1}{y^2}");
        var formula = new FormulaControl { UseLayoutRounding = false, DisplayList = list, FontSize = 32, Padding = new Thickness(3, 4, 5, 6) };
        Assert.Null(formula.PreparationError);
        formula.Measure(Size.Infinity);
        Assert.Equal(list.Width * 32 + 8, formula.DesiredSize.Width, 6);
        Assert.Equal(list.TotalHeight * 32 + 10, formula.DesiredSize.Height, 6);
        Assert.Equal(list.Height * 32 + 4, formula.Baseline, 6);
        var count = formula.PreparationCount;
        using var bitmap = new RenderTargetBitmap(new PixelSize(400, 200));
        formula.Arrange(new Rect(formula.DesiredSize));
        bitmap.Render(formula);
        bitmap.Render(formula);
        Assert.Equal(count, formula.PreparationCount);
        formula.Padding = new Thickness(8);
        Assert.Equal(count, formula.PreparationCount);
        formula.FontSize = 48;
        Assert.Equal(count + 1, formula.PreparationCount);
        Assert.Same(list, formula.DisplayList);

        var window = new Window { Content = formula };
        window.Show();
        try
        {
            count = formula.PreparationCount;
            window.Content = null;
            window.Content = formula;
            Assert.Equal(count + 1, formula.PreparationCount);
            Assert.Null(formula.PreparationError);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MathFonts_WithBundledIds_ResolvesExactFacesAndCmap()
    {
        foreach (var id in MathFonts.Ids)
        {
            Assert.NotNull(MathFonts.Current.Get(id));
        }

        Assert.NotSame(MathFonts.Current.Get("Main-Regular"), MathFonts.Current.Get("Main-Italic"));
        Assert.NotSame(MathFonts.Current.Get("Main-Bold"), MathFonts.Current.Get("Main-BoldItalic"));
        Assert.Equal(65u, MathFonts.CmapCodePoint("Main-Bold", 0x1D400));
        Assert.Equal(0x1D400u, MathFonts.CmapCodePoint("Main-Regular", 0x1D400));
        var resolved = MathFonts.Current.ResolveGlyph("Math-Italic", 'x', true);
        Assert.Same(MathFonts.Current.Get("Math-Italic"), resolved.Face);
    }

    [AvaloniaFact]
    public void FormulaControl_AfterFontOrGlyphFailure_Recovers()
    {
        DisplayList Missing(string font, uint cp) => new(1, 1, 1, 0, [new GlyphPathItem(0, 1, 1, font, cp, RatexColor.Black)]);
        var control = new FormulaControl { DisplayList = Missing("Unknown-Font", 0x4E2D) };
        Assert.NotNull(control.PreparationError);
        control.DisplayList = Missing("Main-Regular", 0x10FFFF);
        Assert.Contains("Missing glyph", control.PreparationError!.Message);
        control.DisplayList = Ratex.ParseAndLayout("x");
        Assert.Null(control.PreparationError);
        control.DisplayList = Ratex.ParseAndLayout("");
        Assert.Null(control.PreparationError);
        control.DisplayList = null;
        Assert.Null(control.PreparationError);
        Assert.Throws<ArgumentException>(() => control.FontSize = double.NaN);
        Assert.Throws<ArgumentException>(() => control.Padding = new Thickness(-1));
    }
}
