using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RaTeX.Avalonia.Demo;
using RaTeX.Samples;

namespace RaTeX.Avalonia.Tests;

public sealed class ExampleTests
{
    [AvaloniaFact]
    public async Task ExamplePicker_WithFallback_UsesMeasuredLayoutAndStrictMode()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var viewModel = Assert.IsType<DemoViewModel>(window.DataContext);
            await WaitForParseAsync(viewModel);
            var formula = Assert.IsType<FormulaControl>(window.FindControl<FormulaControl>("Primary"));
            foreach (var example in DemoViewModel.Examples.Where(example => example.Outcome == ExampleOutcome.FontFallback))
            {
                viewModel.SelectedExample = example;
                await WaitForParseAsync(viewModel);
                var initial = Ratex.ParseAndLayout(example.Latex);
                FallbackGlyphMetrics[] metrics;
                try
                {
                    metrics = FormulaLayout.MeasureFallbackGlyphs(initial);
                }
                catch (RatexRenderException)
                {
                    Assert.Null(viewModel.DisplayList);
                    Assert.StartsWith("Font preparation:", viewModel.EnvironmentError);
                    continue;
                }

                var expected = Ratex.ParseAndLayoutWithFallbackMetrics(example.Latex, metrics);
                var actual = Assert.IsType<DisplayList>(viewModel.DisplayList);
                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);
                Assert.Equal(expected.Depth, actual.Depth);
                Assert.Empty(viewModel.EnvironmentError);
                Assert.Null(formula.PreparationError);

                viewModel.IsFontFallbackEnabled = false;
                await WaitForParseAsync(viewModel);
                Assert.NotNull(formula.PreparationError);
                Assert.Equal(initial.Width, viewModel.DisplayList!.Width);
                viewModel.IsFontFallbackEnabled = true;
                await WaitForParseAsync(viewModel);
                Assert.Null(formula.PreparationError);
                Assert.Equal(expected.Width, viewModel.DisplayList!.Width);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExamplePicker_AfterNativeAndRenderingFailures_RecoversWithTextExample()
    {
        var window = new MainWindow();
        window.Show();

        try
        {
            var viewModel = Assert.IsType<DemoViewModel>(window.DataContext);
            await WaitForParseAsync(viewModel);
            var formula = Assert.IsType<FormulaControl>(window.FindControl<FormulaControl>("Primary"));
            var picker = Assert.IsType<ComboBox>(window.FindControl<ComboBox>("ExamplePicker"));
            Assert.NotNull(formula.DisplayList);

            var invalid = DemoViewModel.Examples.First(example => example.Outcome == ExampleOutcome.NativeError);
            picker.SelectedItem = invalid;
            Assert.Same(invalid, viewModel.SelectedExample);
            Assert.Equal(invalid.Latex, viewModel.Latex);
            await WaitForParseAsync(viewModel);
            Assert.Null(formula.DisplayList);
            Assert.NotEmpty(viewModel.NativeError);
            Assert.Empty(viewModel.EnvironmentError);
            Assert.Null(formula.PreparationError);

            viewModel.IsFontFallbackEnabled = false;
            picker.SelectedItem = DemoViewModel.Examples.First(example => example.Outcome == ExampleOutcome.FontFallback);
            await WaitForParseAsync(viewModel);
            Assert.NotNull(formula.DisplayList);
            Assert.Empty(viewModel.NativeError);
            Assert.Empty(viewModel.EnvironmentError);
            Assert.NotNull(formula.PreparationError);

            picker.SelectedItem = DemoViewModel.Examples.Single(example => example.Name == "Text and units");
            await WaitForParseAsync(viewModel);
            Assert.NotNull(formula.DisplayList);
            Assert.Null(formula.PreparationError);
            Assert.Empty(viewModel.NativeError);
            Assert.Empty(viewModel.EnvironmentError);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task EditorAndOptions_WhenChanged_AutomaticallyParseLatestInput()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var viewModel = Assert.IsType<DemoViewModel>(window.DataContext);
            await WaitForParseAsync(viewModel);
            var editor = Assert.IsType<TextBox>(window.FindControl<TextBox>("FormulaEditor"));
            var modes = Assert.IsType<ComboBox>(window.FindControl<ComboBox>("ModePicker"));
            var colors = Assert.IsType<ComboBox>(window.FindControl<ComboBox>("ColorPicker"));
            var formula = Assert.IsType<FormulaControl>(window.FindControl<FormulaControl>("Primary"));

            var startupLayout = viewModel.DisplayList;
            Assert.NotNull(startupLayout);
            Assert.Same(startupLayout, formula.DisplayList);
            viewModel.FontSize = 52;
            viewModel.PaddingSize = 24;
            Assert.Same(startupLayout, viewModel.DisplayList);
            Assert.False(viewModel.IsParsing);
            Assert.Equal(52, formula.FontSize);
            Assert.Equal(viewModel.FormulaPadding, formula.Padding);

            editor.Text = @"\frac{1}{2}";
            await WaitForParseAsync(viewModel);
            Assert.NotNull(formula.DisplayList);
            Assert.Empty(viewModel.NativeError);
            var previous = viewModel.DisplayList;

            modes.SelectedItem = viewModel.Mode == RatexMode.Inline ? RatexMode.Display : RatexMode.Inline;
            await WaitForParseAsync(viewModel);
            Assert.NotSame(previous, viewModel.DisplayList);
            var expected = Ratex.ParseAndLayout(viewModel.Latex, new RatexOptions { Mode = viewModel.Mode });
            Assert.Equal(expected.Height, viewModel.DisplayList!.Height);

            previous = viewModel.DisplayList;
            colors.SelectedItem = viewModel.Colors.Single(color => color.Name == "Red");
            await WaitForParseAsync(viewModel);
            Assert.NotSame(previous, viewModel.DisplayList);
            var glyphs = viewModel.DisplayList!.Items.OfType<GlyphPathItem>().ToArray();
            Assert.NotEmpty(glyphs);
            Assert.All(glyphs, glyph =>
                Assert.Equal(viewModel.SelectedColor.Color, glyph.Color));

            editor.Text = @"\frac{";
            editor.Text = "x";
            editor.Text = "x^2 + y^2";
            await WaitForParseAsync(viewModel);
            Assert.Equal("x^2 + y^2", viewModel.Latex);
            Assert.NotNull(formula.DisplayList);
            Assert.Empty(viewModel.NativeError);
            Assert.Empty(viewModel.EnvironmentError);
            expected = Ratex.ParseAndLayout(viewModel.Latex, new RatexOptions
            {
                Mode = viewModel.Mode,
                DefaultColor = viewModel.SelectedColor.Color
            });
            Assert.Equal(expected.Width, viewModel.DisplayList!.Width);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task WaitForParseAsync(DemoViewModel viewModel)
    {
        for (var attempt = 0; attempt < 500 && viewModel.IsParsing; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.False(viewModel.IsParsing, "The demo parse did not complete within five seconds.");
    }
}
