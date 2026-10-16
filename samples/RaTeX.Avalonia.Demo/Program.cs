using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace RaTeX.Avalonia.Demo;

/// <summary>
/// Starts the desktop demo on an STA thread.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Configures the desktop platform and runs the application lifetime.
    /// </summary>
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<DemoApp>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
}

/// <summary>
/// Provides the demo's theme and desktop main window.
/// </summary>
public sealed class DemoApp : Application
{
    /// <inheritdoc />
    public override void Initialize() => Styles.Add(new FluentTheme());

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
