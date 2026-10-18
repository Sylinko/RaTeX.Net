using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using RaTeX.Samples;

namespace RaTeX.Avalonia.Demo;

/// <summary>
/// Owns the demo's UI-thread state and automatically parses editable examples on a background thread.
/// </summary>
public sealed class DemoViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets the source to parse automatically after each change.
    /// </summary>
    public string Latex
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
            RequestParse();
        }
    } = Examples[0].Latex;

    /// <summary>
    /// Gets the shared examples, including intentional native and rendering failures.
    /// </summary>
    public static ImmutableArray<FormulaExample> Examples => FormulaExamples.All;

    /// <summary>
    /// Gets or sets the example whose source and initial mode are copied into the editor.
    /// </summary>
    public FormulaExample SelectedExample
    {
        get => _selectedExample;
        set
        {
            _selectedExample = value;
            Latex = value.Latex;
            Mode = value.Mode;
            Changed();
        }
    }

    /// <summary>
    /// Gets the mathematical styles offered by the demo.
    /// </summary>
    public AvaloniaList<RatexMode> Modes { get; } = [RatexMode.Inline, RatexMode.Display];

    /// <summary>
    /// Gets or sets the style and automatically updates the layout.
    /// </summary>
    public RatexMode Mode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
            RequestParse();
        }
    } = Examples[0].Mode;

    /// <summary>
    /// Gets the default colors offered by the demo, including a translucent option.
    /// </summary>
    public FormulaColor[] Colors
    {
        get;
    } =
    [
        new("Black", RatexColor.Black),
        new("White", new RatexColor(1, 1, 1, 1)),
        new("Blue (50% alpha)", new RatexColor(0, .3f, 1, .5f)),
        new("Red", new RatexColor(1, 0, 0, 1))
    ];

    /// <summary>
    /// Gets or sets the default color and automatically updates the layout.
    /// </summary>
    public FormulaColor SelectedColor
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
            RequestParse();
        }
    }

    /// <summary>
    /// Gets whether a background parse or a pending input change is being processed.
    /// </summary>
    public bool IsParsing { get; private set; }

    /// <summary>
    /// Gets the immutable layout shared by both preview controls.
    /// </summary>
    public DisplayList? DisplayList { get; private set; }

    /// <summary>
    /// Gets the native parse or layout error from the most recent request.
    /// </summary>
    public string NativeError { get; private set; } = "";

    /// <summary>
    /// Gets font, deployment, invalid-input, or protocol diagnostics from the most recent request.
    /// </summary>
    public string EnvironmentError { get; private set; } = "";

    /// <summary>
    /// Gets a description of the current layout's metrics and successful layout request count.
    /// </summary>
    public string Metrics => DisplayList is { } list
        ? $"Width {list.Width:F4} em | Height {list.Height:F4} | Depth {list.Depth:F4} | {list.Items.Length} items | {_layoutCount} layouts"
        : "No layout";

    /// <summary>
    /// Gets or sets the primary preview's font size without parsing again.
    /// </summary>
    public double FontSize
    {
        get;
        set
        {
            field = value;
            Changed();
        }
    } = 36;

    /// <summary>
    /// Gets or sets uniform preview padding without rebuilding glyph runs.
    /// </summary>
    public double PaddingSize
    {
        get => _padding;
        set
        {
            _padding = value;
            Changed();
            Changed(nameof(FormulaPadding));
        }
    }

    /// <summary>
    /// Gets the primary preview's padding in device-independent pixels.
    /// </summary>
    public Thickness FormulaPadding => new(_padding);

    /// <summary>
    /// Gets or sets whether the previews use a dark background.
    /// </summary>
    public bool IsDark
    {
        get;
        set
        {
            field = value;
            Changed();
            Changed(nameof(PreviewBackground));
        }
    }

    /// <summary>
    /// Gets the preview background independently of the formula's layout colors.
    /// </summary>
    public IBrush PreviewBackground => IsDark ? Brushes.Black : Brushes.White;

    /// <summary>
    /// Gets or sets whether both previews may use configured or system fonts for missing glyphs.
    /// </summary>
    public bool IsFontFallbackEnabled
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Changed();
            RequestParse();
        }
    } = true;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private FormulaExample _selectedExample;
    private int _layoutCount;
    private double _padding = 16;
    private bool _parseRequested;

    /// <summary>
    /// Initializes the demo and starts its first parse.
    /// </summary>
    public DemoViewModel()
    {
        _selectedExample = Examples[0];
        SelectedColor = Colors[0];
    }

    private async void RequestParse()
    {
        _parseRequested = true;
        if (IsParsing)
        {
            return;
        }

        IsParsing = true;
        Changed(nameof(IsParsing));
        try
        {
            while (_parseRequested)
            {
                _parseRequested = false;
                // Capture UI-owned inputs and publish only if no newer change arrived while parsing.
                var input = Latex;
                var options = new RatexOptions { Mode = Mode, DefaultColor = SelectedColor.Color };
                var useFallback = IsFontFallbackEnabled;
                DisplayList? layout = null;
                var nativeError = "";
                var environmentError = "";

                try
                {
                    layout = useFallback
                        ? await FormulaLayout.ParseAndLayoutAsync(input, options)
                        : await Task.Run(() => Ratex.ParseAndLayout(input, options));
                    _layoutCount++;
                }
                catch (RatexException ex)
                {
                    nativeError = $"Native error {ex.Error.NativeCode}: {ex.Error.NativeMessage}";
                }
                catch (Exception ex) when (ex is RatexRenderException or IOException or InvalidOperationException)
                {
                    environmentError = $"Font preparation: {ex.Message}";
                }
                catch (Exception ex) when (ex is RatexProtocolException or DllNotFoundException or BadImageFormatException
                    or EntryPointNotFoundException or ArgumentException or PlatformNotSupportedException)
                {
                    environmentError = $"Environment / protocol: {ex.Message}";
                }

                if (_parseRequested)
                {
                    continue;
                }

                DisplayList = layout;
                NativeError = nativeError;
                EnvironmentError = environmentError;
                Changed(nameof(DisplayList));
                Changed(nameof(Metrics));
                Changed(nameof(NativeError));
                Changed(nameof(EnvironmentError));
            }
        }
        finally
        {
            IsParsing = false;
            Changed(nameof(IsParsing));
        }
    }

    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A named default color offered by the demo.
/// </summary>
/// <param name="Name">The display name.</param>
/// <param name="Color">The normalized RGBA value.</param>
public sealed record FormulaColor(string Name, RatexColor Color)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}
