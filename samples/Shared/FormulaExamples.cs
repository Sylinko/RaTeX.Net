using System.Collections.Immutable;

namespace RaTeX.Samples;

/// <summary>
/// Describes the expected result when parsing and then preparing an example for rendering.
/// </summary>
public enum ExampleOutcome
{
    /// <summary>
    /// Both native layout and rendering with the bundled fonts should succeed.
    /// </summary>
    Render,

    /// <summary>
    /// The native engine should reject the source.
    /// </summary>
    NativeError,

    /// <summary>
    /// Layout succeeds and drawing requires a matching configured or system fallback font.
    /// </summary>
    FontFallback
}

/// <summary>
/// A runnable formula with an explanation of its expected behavior.
/// </summary>
/// <param name="Name">The short name shown in the example picker.</param>
/// <param name="Latex">The editable LaTeX source.</param>
/// <param name="Description">What to observe after parsing the example.</param>
/// <param name="Outcome">The expected parse and rendering result.</param>
/// <param name="Mode">The initial mathematical layout style.</param>
public sealed record FormulaExample(
    string Name,
    string Latex,
    string Description,
    ExampleOutcome Outcome = ExampleOutcome.Render,
    RatexMode Mode = RatexMode.Display)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// Provides the same examples to the desktop demo and regression checks.
/// </summary>
public static class FormulaExamples
{
    /// <summary>
    /// Gets the examples in presentation order, including intentional failures.
    /// </summary>
    public static ImmutableArray<FormulaExample> All
    {
        get;
    } =
    [
        new(
            "Gaussian integral",
            @"\int_0^\infty e^{-x^2}\,dx = \frac{\sqrt{\pi}}{2}",
            "Display-style limits, a fraction, and a radical. Both previews reuse one layout."),
        new(
            "Text and units",
            @"\text{speed} = \frac{\text{distance}}{\text{time}} = 12\,\text{m/s}",
            "Use \\text for ordinary words and units inside mathematics."),
        new(
            "Text spacing and escaped symbols",
            @"\text{success rate: } 95\% \quad \text{cost: } \$20",
            "Text preserves word spacing; percent and dollar signs are escaped explicitly."),
        new(
            "Piecewise function",
            @"f(x)=\begin{cases}x^2 & \text{if } x\ge 0 \\ -x & \text{otherwise}\end{cases}",
            "A cases environment combines aligned mathematics and text labels."),
        new(
            "Aligned equations",
            @"\begin{aligned}(a+b)^2 &= a^2+2ab+b^2 \\ (a-b)^2 &= a^2-2ab+b^2\end{aligned}",
            "Ampersands align the two equations at their equals signs."),
        new(
            "Matrix and scalable delimiters",
            @"\left[\begin{matrix}1 & \frac{1}{2} \\ \sqrt{3} & 4\end{matrix}\right]",
            "The delimiters grow to fit the matrix and its nested fraction."),
        new(
            "Accents and annotations",
            @"\underbrace{a+\cdots+a}_{n\text{ terms}}=na,\qquad \widehat{ABC},\quad \vec{v}",
            "Stretchy annotations and accents exercise glyphs and vector geometry."),
        new(
            "Mathematical alphabets",
            @"\mathbb{R}\supset\mathbb{Q},\quad\mathcal{F},\quad\mathfrak{g},\quad\mathbf{v}",
            "Each alphabet uses its exact bundled KaTeX font."),
        new(
            "Color inside a formula",
            @"\textcolor{red}{a^2}+\textcolor{blue}{b^2}=c^2",
            "Source colors override the default color. Choose the translucent blue default to compare alpha."),
        new(
            "Inline fraction",
            @"\text{ratio } \frac{a+b}{c+d}",
            "Compare Inline and Display modes; changing only the preview size does not parse again.",
            Mode: RatexMode.Inline),
        new(
            "Combining accent",
            "\\text{e\u0301}",
            "A decomposed Latin acute accent is supported by the pinned engine and bundled fonts."),
        new(
            "CJK · Chinese",
            @"\text{中文：面积}=\pi r^2",
            "Measures configured or system Chinese fonts before final layout. Disable font fallback to compare strict mode.",
            ExampleOutcome.FontFallback),
        new(
            "CJK · Japanese",
            @"\text{日本語：面積}=\pi r^2",
            "Measures configured or system Japanese fonts before final layout. Glyph appearance depends on installed fonts.",
            ExampleOutcome.FontFallback),
        new(
            "CJK · Korean",
            @"\text{한국어}=x+1",
            "Uses configured or system fonts for precomposed Korean syllables. A missing host font is still reported explicitly.",
            ExampleOutcome.FontFallback),
        new(
            "Emoji · character fallback",
            @"\text{😀}+1",
            "Attempts a system glyph for this single emoji. Color rendering and multi-character emoji sequences are not guaranteed.",
            ExampleOutcome.FontFallback),
        new(
            "Error · unfinished fraction",
            @"\frac{1}{",
            "Expected native error. Select a valid example to demonstrate automatic recovery.",
            ExampleOutcome.NativeError),
        new(
            "Error · unknown command",
            @"\definitelyUnknownCommand{x}",
            "Expected native error. TryParseAndLayout returns false; ParseAndLayout throws RatexException.",
            ExampleOutcome.NativeError)
    ];
}
