using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DggNative.Models;
using Tmds.DBus.Protocol;
using Notifications.DBus;
using NotifProxy = Notifications.DBus.Notifications;
#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace DggNative.Services;

public sealed class DesktopNotificationService : IDesktopNotificationService, IDisposable
{
    private const int MaxBodyLength = 180;
    private const string ActionDefault = "default";

    private uint _lastNotificationId;
    private DBusConnection? _connection;
    private NotifProxy? _notificationsProxy;
    private volatile bool _isInitialized;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);

    public event EventHandler? NotificationActivated;

    public void ShowMentionNotification(ChatMessage message)
    {
        var title = $"{message.User.Nick} mentioned you";
        var body = TrimNotificationBody(message.Data);

#if WINDOWS
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show(toast =>
                {
                    toast.Group = "mentions";
                    toast.Tag = message.Timestamp > 0 ? message.Timestamp.ToString() : Guid.NewGuid().ToString();
                    toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(10);
                    toast.Activated += (_, _) => NotificationActivated?.Invoke(this, EventArgs.Empty);
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to show desktop notification: {ex.Message}");
        }
#else
        _ = ShowMentionNotificationAsync(title, body);
#endif
    }

#if !WINDOWS
    private async Task ShowMentionNotificationAsync(string title, string body)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var notificationsProxy = _notificationsProxy;
            if (notificationsProxy is null)
            {
                return;
            }

            _lastNotificationId = await notificationsProxy.NotifyAsync(
                appName: "DggNative",
                replacesId: _lastNotificationId,
                appIcon: string.Empty,
                summary: title,
                body: body,
                actions: new[] { ActionDefault, "Focus" },
                hints: new Dictionary<string, VariantValue>(),
                expireTimeout: 10_000
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DesktopNotificationService] {ex.Message}");
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            var connection = new DBusConnection(DBusAddress.Session!);
            try
            {
                await connection.ConnectAsync().ConfigureAwait(false);

                var service = new DBusService(connection, "org.freedesktop.Notifications");
                var notificationsProxy = ObjectFactory.CreateNotifications(service, "/org/freedesktop/Notifications");

                await notificationsProxy.WatchActionInvokedAsync(OnActionInvoked).ConfigureAwait(false);

                _connection = connection;
                _notificationsProxy = notificationsProxy;
                _isInitialized = true;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    private void OnActionInvoked(Exception? exception, (uint Id, string ActionKey) args)
    {
        if (exception is not null)
            return;

        if (args.ActionKey == ActionDefault)
        {
            NotificationActivated?.Invoke(this, EventArgs.Empty);
        }
    }
#endif

    private static string TrimNotificationBody(string body)
    {
        if (body.Length <= MaxBodyLength)
        {
            return body;
        }

        return body[..(MaxBodyLength - 3)] + "...";
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _initializeLock.Dispose();
    }
}
