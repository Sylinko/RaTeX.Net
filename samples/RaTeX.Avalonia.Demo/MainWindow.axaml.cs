using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace RaTeX.Avalonia.Demo;

/// <summary>
/// Hosts the formula editor, example picker, diagnostics, and shared-layout previews.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Loads the view and starts the demo's initial formula parse.
    /// </summary>
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = new DemoViewModel();
    }
}

/// <summary>
/// Draws a visual guide at a formula's measured baseline.
/// </summary>
public sealed class BaselineGuide : Control
{
    /// <summary>
    /// Defines the <see cref="Baseline"/> property.
    /// </summary>
    public static readonly StyledProperty<double> BaselineProperty = AvaloniaProperty.Register<BaselineGuide, double>(nameof(Baseline));

    /// <summary>
    /// Gets or sets the guide's vertical offset in device-independent pixels.
    /// </summary>
    public double Baseline
    {
        get => GetValue(BaselineProperty);
        set => SetValue(BaselineProperty, value);
    }

    static BaselineGuide() => AffectsRender<BaselineGuide>(BaselineProperty);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        context.DrawLine(new Pen(Brushes.CornflowerBlue, .6), new Point(0, Baseline), new Point(Bounds.Width, Baseline));
    }
}
