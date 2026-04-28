using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DggNative.Services;
using DggNative.ViewModels;
using DggNative.Views;
using Xilium.CefGlue;

namespace DggNative;

public partial class App : Application
{
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

            var chatService = new WebSocketChatService(new Uri(wsurl));
            var authenticationService = new AuthenticationService();
            var cookiePersistenceService = new CookiePersistenceService();
            var desktopNotificationService = new DesktopNotificationService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    chatService,
                    authenticationService,
                    cookiePersistenceService,
                    desktopNotificationService),
            };

            desktop.Exit += (_, _) =>
            {
                authenticationService.Dispose();
                desktopNotificationService.Dispose();
                chatService.Dispose();

                if (CefRuntime.IsInitialized)
                {
                    CefRuntime.Shutdown();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
