[![NuGet](https://img.shields.io/nuget/v/RaTeX.svg?style=flat-square)](https://www.nuget.org/packages/RaTeX)
[![.NET 8 and .NET 10](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![Avalonia 12](https://img.shields.io/badge/Avalonia-12-blue.svg?style=flat-square)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

# RaTeX / RaTeX.Avalonia

**.NET bindings and Avalonia rendering for [RaTeX](https://github.com/erweixin/RaTeX).**

**Credits:** The original [RaTeX](https://github.com/erweixin/RaTeX) engine was created by [@erweixin](https://github.com/erweixin). This is an **unofficial** project builds upon their work to bring RaTeX to .NET and Avalonia.

## ⭐ Features

- 🧮 **LaTeX layout**: Parse formulas into immutable managed display lists through the RaTeX native engine.
- 🎨 **Avalonia rendering**: Display layouts with `FormulaControl`, or reuse prepared resources in your own drawing code.
- 🔤 **Math fonts and fallback**: Use bundled math glyphs and Avalonia's configured or system fonts for missing characters.
- 🖥️ **Native builds**: Build libraries for Windows, macOS, and Linux.

## 🚀 Getting Started

Use the `RaTeX` package for parsing and layout, or `RaTeX.Avalonia` to display the results in an Avalonia application.

### 1. Parse a formula

Pass LaTeX formula content without Markdown delimiters:

```csharp
using RaTeX;

var formula = Ratex.ParseAndLayout(@"\frac{1}{2}");
if (!Ratex.TryParseAndLayout(@"\frac{", null, out var result, out var error))
{
    Console.WriteLine($"{error.NativeCode}: {error.NativeMessage}");
}
```

### 2. Display it in Avalonia

Assign a parsed layout to `FormulaControl.DisplayList`:

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

## 🧮 Working with Layouts

### Handle parse and layout errors

Use `TryParseAndLayout` to receive native failures as a `RatexError`, or `ParseAndLayout` to throw `RatexException`.
Both APIs still throw for invalid arguments and deployment errors. Malformed or unsupported protocol data throws `RatexProtocolException`.

Read `RatexError.NativeMessage` for diagnostics: native error code 1 covers more than syntax errors.
Each error keeps its own copy of the native message, so a later parse does not overwrite it.

### Share immutable results

The returned `DisplayList` and its nested arrays are immutable managed objects. You can share them across controls or threads without calling `Dispose`.
The decoder reads JSON directly from native memory into the managed models, then frees the native buffer:

```text
string → strict UTF-8 → existing native parse-and-layout
    → owned NUL-terminated JSON pointer
    → borrowed ReadOnlySpan<byte> → source-generated synchronous decode
    → finally: ratex_free_display_list → immutable managed DisplayList
```

### Work in em units

All geometry uses em units and preserves negative values. `Height` is the ascent, and `Depth` is the descent.
Coordinates start at the top left and increase downward, with the baseline at `Height`. `GlyphPathItem.CodePoint` holds a Unicode scalar.

## 🎨 Rendering in Avalonia

### Reuse layouts when the appearance changes

Parse again when the LaTeX source, mode, or default color changes, then assign the new result to `DisplayList`.
For rendering changes, keep the existing layout and let the control manage its drawing resources:

- Changing the font size rebuilds drawing resources without parsing again.
- Changing the padding moves the drawing without rebuilding it.
- Scrolling or repainting reuses the prepared drawing.

`Baseline = Height * FontSize + Padding.Top`. The desired size is the layout's width and total height multiplied by the font size, plus padding, subject to Avalonia's layout rounding.
Glyphs may draw beyond those dimensions; the control leaves them unclipped by default.

The control preserves each item's color and alpha across theme changes. To change the default formula color for a new theme, parse again with new color options.

### Use font fallback for missing characters

The renderer uses the exact bundled math glyph when available. Font fallback is enabled by default: missing glyphs and upstream Unicode fallback IDs use Avalonia's configured or system fonts.
Basic CJK and single-character emoji work when a matching font is available.

For layout measured with those fonts, call `FormulaLayout.ParseAndLayoutAsync` on the Avalonia UI thread:

```csharp
var snapshot = await FormulaLayout.ParseAndLayoutAsync(@"\frac{\text{中文}}{x}");
formula.DisplayList = snapshot;
```

Generate a new layout after changing the configured fonts.

> [!NOTE]
> Ordinary `Ratex.ParseAndLayout` still uses estimated fallback metrics. Complex shaping, bidi, multi-character emoji sequences, and color emoji need text rendering support beyond this character-level fallback.

To use only bundled fonts, set `FormulaControl.IsFontFallbackEnabled = false` or call `PreparedDisplayList.Prepare(snapshot, fontSize, isFontFallbackEnabled: false)`.
Changing this setting rebuilds drawing resources without another parse. Set preferred fallback families through Avalonia's `FontManagerOptions.FontFallbacks`.
A glyph that cannot be resolved, or an unknown font ID, causes a rendering error.

Linux applications also need Avalonia's [Linux platform dependencies](https://docs.avaloniaui.net/docs/platform-specific-guides/linux) and installed or configured fonts for CJK/emoji fallback.

### Manage drawing resources

`PreparationError` reports failures to prepare fonts, glyphs, or geometry. Assign a new display list to retry preparation.

For custom drawing, call `PreparedDisplayList.Prepare(snapshot, fontSize)`, reuse `Draw(DrawingContext)`, and dispose the prepared resources on the Avalonia UI thread.
`FormulaControl` handles this lifetime for you: it releases resources when the layout is replaced or the control leaves the visual tree, and rebuilds them when the control rejoins.

Avalonia's FontManager owns the font faces and handles fallback matching and caching. Each prepared drawing owns its glyph runs.

## 🛠️ Building from Source

### Prerequisites

Build with .NET SDK 8/10 and Rust 1.98.1, including the `rust-docs` component.

- **Windows**: PowerShell 7 and the Visual C++ build tools.
- **macOS**: Bash, Python 3, and Xcode Command Line Tools.
- **Linux**: Bash, Python 3, GCC/build-essential, curl, and a glibc-based system.

### 1. Build the native library

#### Windows

```powershell
./build/build-windows-native.ps1
```

#### macOS

Build for the current architecture, or select a RID:

```bash
bash build/build-mac-native.sh
# Or select a target:
bash build/build-mac-native.sh osx-arm64
bash build/build-mac-native.sh osx-x64
```

#### Linux

Build on a host matching the target architecture:

```bash
bash build/build-linux-native.sh
# Or select the host's RID explicitly:
bash build/build-linux-native.sh linux-x64
bash build/build-linux-native.sh linux-arm64
```

### 2. Build the managed projects and run the demo

```sh
dotnet build -c Release
dotnet test -c Release
dotnet pack src/RaTeX -c Release -o artifacts/packages
dotnet pack src/RaTeX.Avalonia -c Release -o artifacts/packages
dotnet run --project samples/RaTeX.Avalonia.Demo -c Release -f net10.0
```

### Supported native targets

| RID         | Native library    |
|-------------|-------------------|
| win-x64     | ratex_ffi.dll     |
| osx-arm64   | libratex_ffi.dylib |
| osx-x64     | libratex_ffi.dylib |
| linux-x64   | libratex_ffi.so   |
| linux-arm64 | libratex_ffi.so   |

- **Windows** needs the x64 Visual C++ runtime (`VCRUNTIME140.dll`), UCRT, and system DLLs.
- **Linux** CI builds use Ubuntu 22.04 on each architecture. Compatibility with older distributions is not established; musl/Alpine is not supported.

> [!NOTE]
> macOS and Linux runtime support has not been tested.

---

[MIT License](LICENSE) & [Third-Party Notices](licenses/)
