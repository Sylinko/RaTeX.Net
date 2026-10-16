# Repository Guidelines

## Project Structure & Module Organization

- `src/RaTeX/`: native C ABI bindings, immutable display-list models, and JSON decoding.
- `src/RaTeX.Avalonia/`: `FormulaControl`, prepared rendering resources, and bundled KaTeX fonts in `Fonts/`.
- `tests/RaTeX.Tests/`: binding, protocol, and deployment tests. `tests/RaTeX.Avalonia.Tests/`: headless rendering and pixel tests, with formulas and reference PNGs under `Fixtures/`.
- `samples/RaTeX.Avalonia.Demo/`: interactive demo; `samples/Shared/` holds example formulas.
- `build/`: native build scripts, pinned upstream configuration, and Cargo lockfile. `licenses/` contains redistribution notices. Treat `artifacts/`, `bin/`, `obj/`, and staged native binaries as generated output.

## Build, Test, and Development Commands

- Run commands from the repository root.
- Use the .NET 10 SDK with .NET 8/10 runtimes for both target frameworks.
- Install the Rust toolchain specified in `build/native.json`, including `rust-docs`; see `README.md` for platform prerequisites.
- Build the native library before running tests, the demo, or packing:
    - Windows: `./build/build-windows-native.ps1` (PowerShell 7).
    - macOS: `bash build/build-mac-native.sh`.
    - Linux: `bash build/build-linux-native.sh` on the target architecture.
- Then use:
    - `dotnet restore RaTeX.slnx --locked-mode`: restore pinned dependencies.
    - `dotnet build -c Release`: compile all projects and target frameworks.
    - `dotnet test -c Release`: run both test suites.
    - `dotnet run --project samples/RaTeX.Avalonia.Demo -c Release -f net10.0`: launch the demo.
    - `dotnet pack src/RaTeX -c Release -o artifacts/packages`: package the bindings; repeat for `src/RaTeX.Avalonia`.

## Coding Style & Naming Conventions

- Follow `.editorconfig`: UTF-8, four-space indentation, CRLF, and a final newline; project/configuration files use two spaces, while shell/Python files use LF.
- Use file-scoped namespaces, `var`, System-first imports, and braces on separate lines.
- Follow existing PascalCase types/members, camelCase locals/parameters, and `_camelCase` private fields.
- Missing public API documentation and nullable warnings fail library builds.
- Keep package versions in `Directory.Packages.props` and update affected lockfiles.

## Comments and Documentation

- Write all code comments and XML documentation comments in English.
- Prefer multiline XML `<summary>` comments.
- Add XML documentation to public APIs. Use `<inheritdoc />` for interface implementations and overrides when the inherited contract is sufficient.
- Preserve useful existing comments during refactoring; rewriting an implementation is not a reason to remove its documentation.
- Explain non-obvious lifetime rules, thread boundaries, state transitions, algorithms, and performance tradeoffs so future maintainers understand why the design exists.

## Testing Guidelines

- Core tests use xUnit v2; Avalonia tests use xUnit v3 with Avalonia.Headless.XUnit and Skia.
- Name tests `Method_Scenario_ExpectedBehavior`; use `[AvaloniaFact]` for UI-thread tests.
- Add regression tests for changed behavior. No numerical code-coverage gate is configured.
- For rendering changes, inspect the demo and reference images; explain intentional baseline changes rather than weakening pixel assertions.

## Commit & Pull Request Guidelines

- History uses short, prefixed subjects such as `feat: `, `refactor: ...`, and `build: ...`; follow that pattern.
- Keep commits focused.
- PRs should describe the change, link relevant issues, report test commands and platforms, and include screenshots for rendering changes.
- Update documentation and third-party notices when affected.
