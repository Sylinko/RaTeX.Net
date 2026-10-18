using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using RaTeX.Interop;
using RaTeX.Serialization;

namespace RaTeX;

/// <summary>
/// Parses LaTeX synchronously through the native engine and returns immutable managed layouts.
/// </summary>
public static class Ratex
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Action<nint> ReleaseDisplayList = NativeMethods.FreeDisplayList;

    /// <summary>
    /// Parses a formula and lays it out in em units.
    /// </summary>
    /// <param name="latex">LaTeX source without embedded NUL characters or unpaired UTF-16 surrogates.</param>
    /// <param name="options">Layout options, or <see langword="null"/> to use the defaults.</param>
    /// <returns>A snapshot that owns no native memory and can be shared across threads.</returns>
    /// <exception cref="RatexException">The native parser or layout engine rejected the formula.</exception>
    /// <exception cref="RatexProtocolException">The native response violated the display list protocol.</exception>
    /// <exception cref="ArgumentException">The source or options are invalid.</exception>
    public static DisplayList ParseAndLayout(string latex, RatexOptions? options = null) =>
        TryParseAndLayout(latex, options, out var displayList, out var error) ? displayList : throw new RatexException(error);

    /// <summary>
    /// Attempts to parse and lay out a formula, returning native failures as a copied error snapshot.
    /// </summary>
    /// <remarks>
    /// Only native parse and layout failures return false. Invalid arguments, deployment problems,
    /// and protocol violations still throw. The native error is copied on the calling thread
    /// before another call can overwrite it.
    /// </remarks>
    /// <param name="latex">LaTeX source to parse.</param>
    /// <param name="options">Layout options, or <see langword="null"/> to use the defaults.</param>
    /// <param name="displayList">The immutable layout on success; otherwise null.</param>
    /// <param name="error">The copied native error on failure; otherwise null.</param>
    /// <returns>Whether parsing and layout succeeded.</returns>
    public static bool TryParseAndLayout(
        string latex,
        RatexOptions? options,
        [NotNullWhen(true)] out DisplayList? displayList,
        [NotNullWhen(false)] out RatexError? error)
        => TryParseAndLayoutWithFallbackMetrics(latex, [], options, out displayList, out error);

    /// <summary>
    /// Parses a formula using host-measured metrics for glyphs missing from the bundled math fonts.
    /// </summary>
    /// <remarks>
    /// Each entry requires a valid Unicode scalar and finite measurements; signed values are allowed. The last duplicate wins.
    /// Known math glyphs and unlisted characters keep their normal metrics. Empty tables use the original
    /// native entry point; nonempty tables require the fallback metrics C ABI.
    /// </remarks>
    /// <param name="latex">LaTeX source without embedded NUL or unpaired UTF-16 surrogates.</param>
    /// <param name="metrics">Base-size em measurements from the same fonts used for drawing.</param>
    /// <param name="options">Layout options, or null to use defaults.</param>
    /// <returns>An immutable managed layout that retains no native memory or input table.</returns>
    public static DisplayList ParseAndLayoutWithFallbackMetrics(
        string latex, ReadOnlySpan<FallbackGlyphMetrics> metrics, RatexOptions? options = null)
        => TryParseAndLayoutWithFallbackMetrics(latex, metrics, options, out var displayList, out var error)
            ? displayList : throw new RatexException(error);

    /// <summary>
    /// Attempts layout with host measurements, returning native failures as a copied error snapshot.
    /// </summary>
    /// <remarks>
    /// Uses the same table contract as <see cref="ParseAndLayoutWithFallbackMetrics"/>.
    /// Invalid arguments, deployment errors and protocol violations throw, as with <see cref="TryParseAndLayout"/>.
    /// </remarks>
    /// <param name="latex">LaTeX source to parse.</param>
    /// <param name="metrics">Base-size em measurements, borrowed until this synchronous call returns.</param>
    /// <param name="options">Layout options, or null to use defaults.</param>
    /// <param name="displayList">The immutable layout on success; otherwise null.</param>
    /// <param name="error">The copied native error on failure; otherwise null.</param>
    /// <returns>Whether parsing and layout succeeded.</returns>
    public static unsafe bool TryParseAndLayoutWithFallbackMetrics(
        string latex,
        ReadOnlySpan<FallbackGlyphMetrics> metrics,
        RatexOptions? options,
        [NotNullWhen(true)] out DisplayList? displayList,
        [NotNullWhen(false)] out RatexError? error)
    {
        ArgumentNullException.ThrowIfNull(latex);
        if (latex.Contains('\0'))
        {
            throw new ArgumentException("LaTeX must not contain NUL.", nameof(latex));
        }

        options ??= new RatexOptions();
        if (!Enum.IsDefined(options.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid mode.");
        }

        var color = options.DefaultColor;
        if (!IsValidColor(color))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RGBA must be finite and in [0, 1].");
        }

        var nativeMetrics = metrics.IsEmpty ? [] : new NativeFallbackGlyphMetrics[metrics.Length];
        for (var i = 0; i < metrics.Length; i++)
        {
            var metric = metrics[i];
            if (!Rune.IsValid(metric.CodePoint) || !double.IsFinite(metric.Width) ||
                !double.IsFinite(metric.Height) || !double.IsFinite(metric.Depth))
            {
                throw new ArgumentException("Fallback metrics require valid Unicode scalars and finite measurements.", nameof(metrics));
            }

            nativeMetrics[i] = new NativeFallbackGlyphMetrics
            {
                CodePoint = metric.CodePoint,
                Width = metric.Width,
                Height = metric.Height,
                Depth = metric.Depth
            };
        }

        byte[] utf8;
        try
        {
            utf8 = new byte[checked(StrictUtf8.GetByteCount(latex) + 1)];
            StrictUtf8.GetBytes(latex, utf8);
        }
        catch (EncoderFallbackException ex)
        {
            throw new ArgumentException("LaTeX contains an unpaired UTF-16 surrogate.", nameof(latex), ex);
        }
        var nativeColor = new NativeColor
        {
            R = color.R,
            G = color.G,
            B = color.B,
            A = color.A
        };
        var nativeOptions = new NativeOptions
        {
            StructSize = (nuint)sizeof(NativeOptions),
            DisplayMode = (int)options.Mode,
            Color = &nativeColor
        };
        NativeResult result;
        fixed (byte* input = utf8)
        fixed (NativeFallbackGlyphMetrics* table = nativeMetrics)
        {
            // Empty tables keep existing callers compatible with older native builds.
            result = metrics.IsEmpty
                ? NativeMethods.ParseAndLayout(input, &nativeOptions)
                : NativeMethods.ParseAndLayoutWithFallbackMetrics(input, &nativeOptions, table, (nuint)nativeMetrics.Length);
        }

        if (result.ErrorCode != 0)
        {
            // Copy the borrowed TLS error immediately, on the calling thread, before callbacks or reentry.
            var message = Marshal.PtrToStringUTF8(NativeMethods.GetLastError());
            if (result.Data != 0)
            {
                throw new RatexProtocolException("Native failure returned a non-null data pointer; ownership is undefined.");
            }

            displayList = null;
            error = new RatexError(result.ErrorCode, message);
            return false;
        }
        if (result.Data == 0)
        {
            throw new RatexProtocolException("Native success returned a null data pointer.");
        }

        displayList = DecodeOwned(result.Data, ReleaseDisplayList);
        error = null;
        return true;
    }

    /// <summary>
    /// Checks the normalized, finite RGBA contract shared by input options and native JSON.
    /// </summary>
    internal static bool IsValidColor(RatexColor color)
    {
        return float.IsFinite(color.R) && color.R is >= 0 and <= 1 &&
            float.IsFinite(color.G) && color.G is >= 0 and <= 1 &&
            float.IsFinite(color.B) && color.B is >= 0 and <= 1 &&
            float.IsFinite(color.A) && color.A is >= 0 and <= 1;
    }

    /// <summary>
    /// Takes sole ownership of a successful native result and releases it even when decoding fails.
    /// </summary>
    /// <remarks>
    /// The release callback must match the allocator that produced the NUL-terminated UTF-8 buffer.
    /// Decoding reads that buffer directly; no native pointer escapes into the returned snapshot.
    /// </remarks>
    internal static unsafe DisplayList DecodeOwned(nint data, Action<nint> release)
    {
        try
        {
            var json = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)data);
            return DisplayListDecoder.Decode(json);
        }
        finally
        {
            release(data);
        }
    }
}
