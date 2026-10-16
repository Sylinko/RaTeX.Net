using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(RaTeX.Avalonia.Tests.TestApp))]

namespace RaTeX.Avalonia.Tests;

public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new global::Avalonia.Themes.Fluent.FluentTheme());
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
