using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;
using Avalonia.Threading;

namespace RaTeX.Avalonia;

/// <summary>
/// Loads exact upstream font files on the UI thread and transfers their lifetime to the FontManager.
/// </summary>
internal sealed class MathFonts : FontCollectionBase
{
    private static readonly ConditionalWeakTable<FontManager, MathFonts> Collections = new();
    private readonly Dictionary<string, GlyphTypeface> _faces = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the bounded set of upstream font IDs bundled with this renderer.
    /// </summary>
    internal static IReadOnlyList<string> Ids { get; } =
    [
        "AMS-Regular", "Caligraphic-Regular", "Fraktur-Regular", "Fraktur-Bold",
        "Main-Regular", "Main-Bold", "Main-Italic", "Main-BoldItalic", "Math-Italic", "Math-BoldItalic",
        "SansSerif-Regular", "SansSerif-Bold", "SansSerif-Italic", "Script-Regular",
        "Size1-Regular", "Size2-Regular", "Size3-Regular", "Size4-Regular", "Typewriter-Regular"
    ];

    /// <inheritdoc />
    public override Uri Key { get; } = new("fonts:RaTeX.KaTeX.7d9f9a3");

    /// <summary>
    /// Gets the collection registered with the current application's font manager.
    /// </summary>
    internal static MathFonts Current
    {
        get
        {
            Dispatcher.UIThread.VerifyAccess();
            return Collections.GetValue(
                FontManager.Current,
                manager =>
                {
                    var fonts = new MathFonts();
                    manager.AddFontCollection(fonts);
                    return fonts;
                });
        }
    }

    /// <summary>
    /// Gets or loads a bundled face by exact upstream ID without host font substitution.
    /// </summary>
    internal GlyphTypeface Get(string id)
    {
        if (!Ids.Contains(id, StringComparer.Ordinal))
        {
            throw new RatexRenderException($"Unsupported font '{id}'. It is not a bundled math font.");
        }

        if (_faces.TryGetValue(id, out var face))
        {
            return face;
        }

        using var stream = AssetLoader.Open(new Uri($"avares://RaTeX.Avalonia/Fonts/KaTeX_{id}.ttf"));
        _ = TryAddGlyphTypeface(stream, out face);
        if (face is null)
        {
            throw new RatexRenderException($"Could not load bundled font '{id}'.");
        }
        // Some KaTeX italic files declare Normal style. Key by upstream file ID to avoid family/style collisions.
        if (!TryAddGlyphTypeface(id, new FontCollectionKey { Style = face.Style, Weight = face.Weight, Stretch = face.Stretch }, face))
        {
            face.Dispose();
            throw new RatexRenderException($"Could not register bundled font '{id}'.");
        }
        _faces.Add(id, face);
        return face;
    }

    /// <summary>
    /// Resolves an exact math glyph first, then optionally borrows a character-matched face from FontManager.
    /// </summary>
    /// <remarks>
    /// The font manager owns fallback faces. Only prepared glyph runs are owned by the renderer.
    /// Unknown font IDs remain errors so malformed display lists cannot silently change font families.
    /// </remarks>
    internal (GlyphTypeface Face, ushort Index, uint CodePoint) ResolveGlyph(
        string font,
        uint codePoint,
        bool isFontFallbackEnabled)
    {
        GlyphTypeface? mathFace = null;
        var mappedCodePoint = CmapCodePoint(font, codePoint);
        if (font is not ("CJK-Regular" or "CJK-Fallback" or "Emoji-Fallback"))
        {
            mathFace = Get(font);
            if (mathFace.CharacterToGlyphMap.TryGetGlyph((int)mappedCodePoint, out var index) && index != 0)
            {
                return (mathFace, index, mappedCodePoint);
            }
        }

        if (!isFontFallbackEnabled)
        {
            throw new RatexRenderException($"Missing glyph U+{codePoint:X} in '{font}'; font fallback is disabled.");
        }

        // shortcut: keep native positions and estimated metrics; feed host metrics to layout when the C ABI exposes them.
        var manager = FontManager.Current;
        if (manager.TryMatchCharacter(
                (int)codePoint,
                mathFace?.Style ?? FontStyle.Normal,
                mathFace?.Weight ?? FontWeight.Normal,
                mathFace?.Stretch ?? FontStretch.Normal,
                null,
                null,
                out var typeface) &&
            manager.TryGetGlyphTypeface(typeface, out var fallbackFace) &&
            fallbackFace.CharacterToGlyphMap.TryGetGlyph((int)codePoint, out var fallbackIndex) &&
            fallbackIndex != 0)
        {
            return (fallbackFace, fallbackIndex, codePoint);
        }

        throw new RatexRenderException(
            $"Missing glyph U+{codePoint:X} in '{font}'; no configured or system fallback font contains it.");
    }

    /// <summary>
    /// Applies the exact cmap remapping from pinned ratex-font/src/math_alpha.rs without layout or shaping.
    /// </summary>
    internal static uint CmapCodePoint(string font, uint cp)
    {
        foreach (var (start, name) in LetterRanges)
        {
            if (font == name && cp >= start && cp < start + 52)
            {
                var offset = cp - start;
                return offset < 26 ? 65 + offset : 97 + offset - 26;
            }
        }

        if (font == "AMS-Regular" && cp is >= 0x1D538 and < 0x1D552)
        {
            return 65 + cp - 0x1D538;
        }

        if (font == "Script-Regular" && cp is >= 0x1D49C and < 0x1D4B6)
        {
            return 65 + cp - 0x1D49C;
        }

        if (font == "AMS-Regular" && cp == 0x1D55C)
        {
            return 107;
        }

        foreach (var (start, name) in DigitRanges)
        {
            if (font == name && cp >= start && cp < start + 10)
            {
                return 48 + cp - start;
            }
        }

        return cp;
    }

    private static readonly (uint, string)[] LetterRanges =
    [
        (0x1D400, "Main-Bold"), (0x1D434, "Math-Italic"), (0x1D468, "Math-BoldItalic"),
        (0x1D504, "Fraktur-Regular"), (0x1D56C, "Fraktur-Bold"), (0x1D5A0, "SansSerif-Regular"),
        (0x1D5D4, "SansSerif-Bold"), (0x1D608, "SansSerif-Italic"), (0x1D670, "Typewriter-Regular")
    ];
    private static readonly (uint, string)[] DigitRanges =
    [
        (0x1D7CE, "Main-Bold"), (0x1D7E2, "SansSerif-Regular"), (0x1D7EC, "SansSerif-Bold"), (0x1D7F6, "Typewriter-Regular")
    ];
}
