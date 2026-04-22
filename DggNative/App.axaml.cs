using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DggNative.Services;
using DggNative.ViewModels;
using DggNative.Views;

namespace DggNative;

public partial class App : Application
{
    public EmoteCatalogService? EmoteCatalogService { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var wsurl = Environment.GetEnvironmentVariable("wsurl");
            if (string.IsNullOrEmpty(wsurl))
            {
                wsurl = "wss://chat.destiny.gg/ws";
            }

            EmoteCatalogService = new EmoteCatalogService();
            _ = EmoteCatalogService.InitializeAsync();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    new WebSocketChatService(new Uri(wsurl)),
                    new AuthenticationService(),
                    new CookiePersistenceService()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
