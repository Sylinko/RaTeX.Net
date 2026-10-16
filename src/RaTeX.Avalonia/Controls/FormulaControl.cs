using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RaTeX.Avalonia;

/// <summary>
/// Displays an existing managed layout and prepares drawing resources on the UI thread.
/// </summary>
/// <remarks>
/// This control does not call the native parser. Font size and display list changes rebuild drawing
/// resources; padding changes only affect layout. Resources are released when the control detaches.
/// </remarks>
public sealed class FormulaControl : Control
{
    /// <summary>
    /// Defines the <see cref="DisplayList"/> property.
    /// </summary>
    public static readonly StyledProperty<DisplayList?> DisplayListProperty =
        AvaloniaProperty.Register<FormulaControl, DisplayList?>(nameof(DisplayList));

    /// <summary>
    /// Defines the <see cref="FontSize"/> property.
    /// </summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<FormulaControl, double>(nameof(FontSize), 24, validate: v => double.IsFinite(v) && v > 0);

    /// <summary>
    /// Defines the <see cref="Padding"/> property.
    /// </summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<FormulaControl, Thickness>(
            nameof(Padding),
            validate: padding =>
                double.IsFinite(padding.Left) && padding.Left >= 0 &&
                double.IsFinite(padding.Top) && padding.Top >= 0 &&
                double.IsFinite(padding.Right) && padding.Right >= 0 &&
                double.IsFinite(padding.Bottom) && padding.Bottom >= 0);

    /// <summary>
    /// Defines the <see cref="IsFontFallbackEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsFontFallbackEnabledProperty =
        AvaloniaProperty.Register<FormulaControl, bool>(nameof(IsFontFallbackEnabled), true);

    /// <summary>
    /// Defines the read-only <see cref="PreparationError"/> property.
    /// </summary>
    public static readonly DirectProperty<FormulaControl, RatexRenderException?> PreparationErrorProperty =
        AvaloniaProperty.RegisterDirect<FormulaControl, RatexRenderException?>(nameof(PreparationError), c => c.PreparationError);

    /// <summary>
    /// Defines the read-only <see cref="Baseline"/> property.
    /// </summary>
    public static readonly DirectProperty<FormulaControl, double> BaselineProperty =
        AvaloniaProperty.RegisterDirect<FormulaControl, double>(nameof(Baseline), c => c.Baseline);

    private PreparedDisplayList? _prepared;

    /// <summary>
    /// Gets the number of successful resource preparations during this control's lifetime.
    /// </summary>
    internal int PreparationCount { get; private set; }

    static FormulaControl() => AffectsMeasure<FormulaControl>(DisplayListProperty, FontSizeProperty, PaddingProperty);

    /// <summary>
    /// Gets or sets the immutable layout to display. Null clears the formula.
    /// </summary>
    public DisplayList? DisplayList
    {
        get => GetValue(DisplayListProperty);
        set => SetValue(DisplayListProperty, value);
    }

    /// <summary>
    /// Gets or sets the positive, finite size of one em in device-independent pixels. The default is 24.
    /// </summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite, nonnegative space around the formula in device-independent pixels.
    /// </summary>
    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether missing glyphs may use Avalonia's configured or system fallback fonts. The default is true.
    /// </summary>
    /// <remarks>
    /// Native positions and metrics are retained. This permits approximate character rendering,
    /// not full text shaping. Changing this property rebuilds drawing resources without parsing again.
    /// </remarks>
    public bool IsFontFallbackEnabled
    {
        get => GetValue(IsFontFallbackEnabledProperty);
        set => SetValue(IsFontFallbackEnabledProperty, value);
    }

    /// <summary>
    /// Gets the most recent rendering preparation error, or null after a successful preparation or clear.
    /// </summary>
    public RatexRenderException? PreparationError
    {
        get;
        private set => SetAndRaise(PreparationErrorProperty, ref field, value);
    }

    /// <summary>
    /// Gets the baseline offset from the control's top edge, including padding, in device-independent pixels.
    /// </summary>
    public double Baseline
    {
        get;
        private set => SetAndRaise(BaselineProperty, ref field, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayListProperty || change.Property == FontSizeProperty ||
            change.Property == IsFontFallbackEnabledProperty)
        {
            Prepare();
            InvalidateVisual();
        }

        if (change.Property == DisplayListProperty || change.Property == FontSizeProperty || change.Property == PaddingProperty)
        {
            var baseline = (DisplayList?.Height ?? 0) * FontSize + Padding.Top;
            Baseline = double.IsFinite(baseline) ? baseline : 0;
            InvalidateVisual();
        }
    }

    private void Prepare()
    {
        _prepared?.Dispose();
        _prepared = null;
        PreparationError = null;
        if (DisplayList is not { } list)
        {
            return;
        }

        try
        {
            _prepared = PreparedDisplayList.Prepare(list, FontSize, IsFontFallbackEnabled);
            PreparationCount++;
        }
        catch (Exception ex) when (ex is RatexRenderException or ArgumentException or InvalidOperationException or IOException)
        {
            PreparationError = ex as RatexRenderException ?? new RatexRenderException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = (DisplayList?.Width ?? 0) * FontSize + Padding.Left + Padding.Right;
        var height = (DisplayList?.TotalHeight ?? 0) * FontSize + Padding.Top + Padding.Bottom;
        return new Size(SafeSize(width), SafeSize(height));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        using (context.PushTransform(Matrix.CreateTranslation(Padding.Left, Padding.Top)))
        {
            _prepared?.Draw(context);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _prepared?.Dispose();
        _prepared = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_prepared is null)
        {
            Prepare();
        }
    }

    private static double SafeSize(double v) => double.IsFinite(v) ? Math.Max(0, v) : 0;
}
