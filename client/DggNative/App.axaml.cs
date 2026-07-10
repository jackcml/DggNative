using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DggNative.Models;
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
            // The effective server config (wsurl env override / persisted settings) is
            // resolved by MainWindowViewModel before it starts the connection.
            var chatService = new WebSocketChatService(
                new ChatServerConfig(ChatServerConfig.OfficialUri, ChatAuthMode.Cookie));
            var authenticationService = new AuthenticationService();
            var cookiePersistenceService = new CookiePersistenceService();
            var settingsPersistenceService = new SettingsPersistenceService();
            var desktopNotificationService = new DesktopNotificationService();

            var mainViewModel = new MainWindowViewModel(
                chatService,
                authenticationService,
                cookiePersistenceService,
                settingsPersistenceService,
                desktopNotificationService);
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };

            desktop.Exit += (_, _) =>
            {
                authenticationService.Dispose();
                desktopNotificationService.Dispose();
                mainViewModel.Dispose();
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
