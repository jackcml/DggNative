using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DggNative.Models;

namespace DggNative.Services;

public class AuthenticationService
{
    private static readonly Uri LoginUri = new("https://www.destiny.gg/login");

    public async Task<AuthCookies?> LoginAsync(Window owner)
    {
        var tcs = new TaskCompletionSource<AuthCookies?>();

        var dialog = new NativeWebDialog
        {
            Title = "Login to Destiny.gg",
            Source = LoginUri,
            CanUserResize = true
        };

        dialog.NavigationCompleted += async (_, args) =>
        {
            if (!args.IsSuccess) return;

            // After a successful login, destiny.gg redirects away from /login.
            // Detect when we've left the login page.
            var currentUrl = dialog.Source;

            var path = currentUrl.AbsolutePath;
            if (path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)) return;

            // We've navigated away from the login page — extract cookies
            var cookieManager = dialog.TryGetCookieManager();
            if (cookieManager == null)
            {
                tcs.TrySetResult(null);
                dialog.Close();
                return;
            }

            var cookies = await cookieManager.GetCookiesAsync();

            var sid = cookies.FirstOrDefault(c =>
                c.Name.Equals("sid", StringComparison.OrdinalIgnoreCase))?.Value;

            // NOTE: rememberme seems not to be set here for some reason, though we know it
            // exists on the web client. The sid alone seems sufficient for authentication;
            // we persist it to disk ourselves to survive app restarts.
            var rememberMe = cookies.FirstOrDefault(c =>
                c.Name.Equals("rememberme", StringComparison.OrdinalIgnoreCase))?.Value;

            var authCookies = new AuthCookies(sid, rememberMe);
            tcs.TrySetResult(authCookies.HasCredentials ? authCookies : null);

            dialog.Close();
        };

        dialog.Closing += (_, _) =>
        {
            // If the user manually closed the dialog without completing login
            tcs.TrySetResult(null);
        };

        dialog.Show(owner);
        return await tcs.Task;
    }
}
