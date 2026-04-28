using Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using Xilium.CefGlue;
using Xilium.CefGlue.Common;

namespace DggNative;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        DotNetEnv.Env.Load();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .AfterSetup(_ => ConfigureCef())
            .LogToTrace();

    private static void ConfigureCef()
    {
        var cefRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DggNative",
            "cef");

        CefRuntimeLoader.Initialize(
            new CefSettings
            {
                CachePath = Path.Combine(cefRoot, "profile"),
                RootCachePath = cefRoot,
                WindowlessRenderingEnabled = false
            },
            [
                KeyValuePair.Create("disable-gpu", "1"),
                KeyValuePair.Create("disable-gpu-compositing", "1")
            ]);
    }
}
