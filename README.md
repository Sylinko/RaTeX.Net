# RaTeX / RaTeX.Avalonia

**.NET bindings and Avalonia rendering for [RaTeX](https://github.com/erweixin/RaTeX).**

**Credits:** The original [RaTeX](https://github.com/erweixin/RaTeX) engine was created by [@erweixin](https://github.com/erweixin). This is an **unofficial** project builds upon their work to bring RaTeX to .NET and Avalonia.

## Build

Build with .NET SDK 8/10 and Rust 1.98.1, including the `rust-docs` component.

- Windows builds use PowerShell 7 and the Visual C++ build tools.
- macOS builds use Bash, Python 3, and Xcode Command Line Tools.
- Linux builds use Bash, Python 3, GCC/build-essential, curl, and a glibc-based system.

Build the native library on Windows:

```powershell
./build/build-windows-native.ps1
```

On macOS, build for the current architecture or select a RID:

```bash
bash build/build-mac-native.sh
# Or select a target:
bash build/build-mac-native.sh osx-arm64
bash build/build-mac-native.sh osx-x64
```

On Linux, build on a host matching the target architecture:

```bash
bash build/build-linux-native.sh
# Or select the host's RID explicitly:
bash build/build-linux-native.sh linux-x64
bash build/build-linux-native.sh linux-arm64
```

Then build, test, or pack the managed projects:

```sh
dotnet build -c Release
dotnet test -c Release
dotnet pack src/RaTeX.Net -c Release -o artifacts/packages
dotnet pack src/RaTeX.Avalonia -c Release -o artifacts/packages
dotnet run --project samples/RaTeX.Avalonia.Demo -c Release -f net10.0
```

The native scripts read `build/native.json`, install the selected Rust target, and place its library under `src/RaTeX.Net/runtimes/{rid}/native/`:

| RID         | Native library    |
|-------------|-------------------|
| win-x64     | ratex_ffi.dll     |
| osx-arm64   | libratex_ffi.dylib |
| osx-x64     | libratex_ffi.dylib |
| linux-x64   | libratex_ffi.so   |
| linux-arm64 | libratex_ffi.so   |

- Windows needs the x64 Visual C++ runtime (`VCRUNTIME140.dll`), UCRT, and system DLLs.
- Linux CI builds use Ubuntu 22.04 on each architecture. Compatibility with older distributions is not established; musl/Alpine is not supported.
- macOS and Linux runtime support has not been tested.

Each native build also generates `licenses/THIRD_PARTY_NOTICES.{rid}.txt`. Regenerate or check macOS/Linux notices with `bash build/update-mac-notices.sh [rid] [--check]` or `bash build/update-linux-notices.sh [rid] [--check]`.

## Binding

```csharp
using RaTeX;

var formula = Ratex.ParseAndLayout(@"\frac{1}{2}");
if (!Ratex.TryParseAndLayout(@"\frac{", null, out var result, out var error))
{
    Console.WriteLine($"{error.NativeCode}: {error.NativeMessage}");
}
```

`RatexOptions.Mode` defaults to Display; `RatexOptions.DefaultColor` defaults to opaque black.

Pass LaTeX formula content without Markdown delimiters. Input must be non-null, with valid UTF-16 and no embedded NUL characters. Options require a valid mode and finite RGBA channels in `[0, 1]`.

Use `TryParseAndLayout` to receive native parse and layout failures as a `RatexError`, or `ParseAndLayout` to throw `RatexException`. Both APIs throw for invalid arguments and deployment errors, and throw `RatexProtocolException` for malformed or unsupported protocol data.

Use `RatexError.NativeMessage` for diagnostics: native error code 1 covers more than syntax errors. Each error retains a copy of the native message.

```text
string → strict UTF-8 → existing native parse-and-layout
    → owned NUL-terminated JSON pointer
    → borrowed ReadOnlySpan<byte> → source-generated synchronous decode
    → finally: ratex_free_display_list → immutable managed DisplayList
```

The returned `DisplayList` and its nested arrays are immutable managed objects. Share them across controls or threads; they need no `Dispose`. The decoder reads JSON directly from native memory into the managed models, then frees the native buffer.

All geometry uses em units and preserves negative values. `Height` is the ascent and `Depth` is the descent. Coordinates start at the top left and increase downward, with the baseline at `Height`. `GlyphPathItem.CodePoint` holds a Unicode scalar.

## Avalonia

```csharp
using RaTeX.Avalonia;
var snapshot = Ratex.ParseAndLayout(@"\sqrt{x^2+y^2}");
var formula = new FormulaControl
{
    DisplayList = snapshot,
    FontSize = 32,
    Padding = new Avalonia.Thickness(8)
};
```

In AXAML, use `xmlns:ratex="using:RaTeX.Avalonia"` and
`<ratex:FormulaControl DisplayList="{Binding Formula}" FontSize="32" Padding="8" />`.
Parse when the LaTeX source, mode, or default color changes, then assign the result to `DisplayList`. The control handles rendering: font size changes rebuild drawing resources, padding moves the drawing, and scrolling or repainting reuses it. These rendering changes reuse the parsed layout.

`Baseline = Height * FontSize + Padding.Top`. The desired size is the layout's width and total height multiplied by the font size, plus padding, subject to Avalonia's layout rounding. Glyphs may draw beyond those dimensions, and the control leaves them unclipped by default.

The control preserves each item's color and alpha across theme changes. To update the default formula color for a new theme, parse again with new color options.

`PreparationError` reports failures to prepare fonts, glyphs, or geometry. Assign a new display list to retry preparation.

The renderer uses the exact bundled math glyph when available. Font fallback is enabled by default: missing glyphs and upstream Unicode fallback IDs use Avalonia's configured or system fonts. Basic CJK and single-character emoji work with a matching font. Fallback retains native positions and estimated metrics; complex shaping, bidi, multi-character emoji sequences, and color emoji need text rendering support beyond this character-level fallback.

Linux applications also need Avalonia's [Linux platform dependencies](https://docs.avaloniaui.net/docs/platform-specific-guides/linux) and installed or configured fonts for CJK/emoji fallback.

To restrict rendering to bundled fonts, set `FormulaControl.IsFontFallbackEnabled = false` or call `PreparedDisplayList.Prepare(snapshot, fontSize, isFontFallbackEnabled: false)`. Changing this setting rebuilds the control's drawing resources without another parse. Set preferred fallback families through Avalonia's `FontManagerOptions.FontFallbacks`. A glyph that cannot be resolved, or an unknown font ID, causes a rendering error.

For custom drawing, call `PreparedDisplayList.Prepare(snapshot, fontSize)`, reuse `Draw(DrawingContext)`, and dispose the prepared resources on the Avalonia UI thread. `FormulaControl` manages these resources automatically: it releases them when the layout is replaced or the control leaves the visual tree, and rebuilds them when the control rejoins.

Avalonia's FontManager owns the font faces and handles fallback matching and caching. Each prepared drawing owns its glyph runs.

---

[MIT License](LICENSE) & [Third-Party Notices](licenses/)
