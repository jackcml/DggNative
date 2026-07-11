using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DggNative.Models;
using Xilium.CefGlue;
using Xilium.CefGlue.Avalonia;
using Xilium.CefGlue.Common.Events;
using Xilium.CefGlue.Common.Handlers;

namespace DggNative.Services;

public class AuthenticationService : IDisposable
{
    private static readonly Uri LoginUri = new("https://www.destiny.gg/login");
    private readonly object _lock = new();
    private CefLoginWindow? _activeLoginWindow;
    private bool _isDisposed;

    public Task ClearOfficialSessionAsync()
    {
        if (!CefRuntime.IsInitialized) return Task.CompletedTask;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void ClearOnUiThread()
        {
            try
            {
                var manager = CefCookieManager.GetGlobal(null);
                var accepted = manager.DeleteCookies(
                    string.Empty,
                    string.Empty,
                    new DeleteCookiesCallback(() => completion.TrySetResult()));
                if (!accepted)
                    completion.TrySetException(new InvalidOperationException(
                        "The embedded browser refused to clear its session cookies."));
            }
            catch (Exception ex)
            {
                completion.TrySetException(new InvalidOperationException(
                    "The embedded browser session could not be cleared.", ex));
            }
        }

        if (Dispatcher.UIThread.CheckAccess()) ClearOnUiThread();
        else Dispatcher.UIThread.Post(ClearOnUiThread);
        return completion.Task;
    }

    private sealed class DeleteCookiesCallback(Action completed) : CefDeleteCookiesCallback
    {
        protected override void OnComplete(int numDeleted) => completed();
    }

    public async Task<AuthCookies?> LoginAsync(Window owner)
    {
        try
        {
            var tcs = new TaskCompletionSource<AuthCookies?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dialog = new CefLoginWindow(tcs);
            lock (_lock)
            {
                ThrowIfDisposed();
                _activeLoginWindow = dialog;
            }

            dialog.Closed += (_, _) => tcs.TrySetResult(null);
            dialog.Show(owner);
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthenticationService] Failed to open login window: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        CefLoginWindow? activeLoginWindow;
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            activeLoginWindow = _activeLoginWindow;
            _activeLoginWindow = null;
        }

        if (activeLoginWindow is null)
        {
            return;
        }

        void DisposeOnUiThread()
        {
            activeLoginWindow.DisposeBrowser();
            activeLoginWindow.Close();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            DisposeOnUiThread();
        }
        else
        {
            Dispatcher.UIThread.Post(DisposeOnUiThread);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AuthenticationService));
        }
    }

    private sealed class CefLoginWindow : Window
    {
        private readonly TaskCompletionSource<AuthCookies?> _completion;
        private readonly AvaloniaCefBrowser _browser;
        private readonly AuthCookieStore _cookies = new();
        private bool _isCompleting;
        private bool _isBrowserDisposed;

        public CefLoginWindow(TaskCompletionSource<AuthCookies?> completion)
        {
            _completion = completion;

            Title = "Login to Destiny.gg";
            Width = 900;
            Height = 700;
            MinWidth = 640;
            MinHeight = 480;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _browser = new AvaloniaCefBrowser();
            _browser.LifeSpanHandler = new SameWindowPopupHandler(url => _browser.Address = url);
            _browser.RequestHandler = new AuthRequestHandler(_cookies);
            _browser.LoadEnd += Browser_OnLoadEnd;
            _browser.Address = LoginUri.ToString();

            Content = _browser;
        }

        public void DisposeBrowser()
        {
            if (_isBrowserDisposed)
            {
                return;
            }

            _isBrowserDisposed = true;
            _browser.LoadEnd -= Browser_OnLoadEnd;
            Content = null;
            _browser.Dispose();
        }

        private void Browser_OnLoadEnd(object sender, LoadEndEventArgs args)
        {
            if (!args.Frame.IsMain || _isCompleting)
            {
                return;
            }

            if (!IsCompletedLoginUrl(args.Frame.Url))
            {
                return;
            }

            Dispatcher.UIThread.Post(CompleteLogin);
        }

        private void CompleteLogin()
        {
            if (_isCompleting)
            {
                return;
            }

            _isCompleting = true;

            try
            {
                var cookies = _cookies.ToAuthCookies();
                _completion.TrySetResult(cookies);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthenticationService] Failed to read CEF auth cookies: {ex.Message}");
                _completion.TrySetResult(null);
            }
            finally
            {
                _browser.LoadEnd -= Browser_OnLoadEnd;
                Hide();
                DispatcherTimer.RunOnce(() =>
                {
                    DisposeBrowser();
                    Close();
                }, TimeSpan.FromSeconds(2));
            }
        }

        private static bool IsCompletedLoginUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var currentUrl))
            {
                return false;
            }

            if (!currentUrl.Host.Equals(LoginUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !currentUrl.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class AuthRequestHandler(AuthCookieStore cookies) : RequestHandler
    {
        private readonly AuthResourceRequestHandler _resourceRequestHandler = new(cookies);

        protected override CefResourceRequestHandler GetResourceRequestHandler(
            CefBrowser browser,
            CefFrame frame,
            CefRequest request,
            bool isNavigation,
            bool isDownload,
            string requestInitiator,
            ref bool disableDefaultHandling)
        {
            return _resourceRequestHandler;
        }
    }

    private sealed class AuthResourceRequestHandler(AuthCookieStore cookies) : CefResourceRequestHandler
    {
        protected override CefCookieAccessFilter? GetCookieAccessFilter(
            CefBrowser browser,
            CefFrame frame,
            CefRequest request)
        {
            return null;
        }

        protected override bool OnResourceResponse(
            CefBrowser browser,
            CefFrame frame,
            CefRequest request,
            CefResponse response)
        {
            CaptureSetCookieHeaders(request, response);
            return false;
        }

        protected override void OnResourceRedirect(
            CefBrowser browser,
            CefFrame frame,
            CefRequest request,
            CefResponse response,
            ref string newUrl)
        {
            CaptureSetCookieHeaders(request, response);
        }

        private void CaptureSetCookieHeaders(CefRequest request, CefResponse response)
        {
            if (!IsDestinyHost(request.Url))
            {
                return;
            }

            var headers = response.GetHeaderMap();
            foreach (var setCookie in GetHeaderValues(headers, "Set-Cookie"))
            {
                cookies.CaptureSetCookie(setCookie);
            }
        }

        private static bool IsDestinyHost(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                   && EndpointTrust.IsHostOrSubdomain(uri.Host, "destiny.gg");
        }

        private static string[] GetHeaderValues(NameValueCollection headers, string name)
        {
            return headers.GetValues(name) ?? [];
        }
    }

    private sealed class SameWindowPopupHandler(Action<string> navigate) : LifeSpanHandler
    {
        protected override bool OnBeforePopup(
            CefBrowser browser,
            CefFrame frame,
            string targetUrl,
            string targetFrameName,
            CefWindowOpenDisposition targetDisposition,
            bool userGesture,
            CefPopupFeatures popupFeatures,
            CefWindowInfo windowInfo,
            ref CefClient client,
            CefBrowserSettings settings,
            ref CefDictionaryValue extraInfo,
            ref bool noJavascriptAccess)
        {
            Dispatcher.UIThread.Post(() => navigate(targetUrl));
            return true;
        }
    }

    private sealed class AuthCookieStore
    {
        private readonly object _lock = new();
        private string? _sid;
        private string? _rememberMe;

        public void CaptureSetCookie(string setCookie)
        {
            var equalsIndex = setCookie.IndexOf('=');
            if (equalsIndex <= 0)
                return;

            var name = setCookie[..equalsIndex].Trim();
            var valueEnd = setCookie.IndexOf(';', equalsIndex + 1);
            var value = valueEnd >= 0
                ? setCookie[(equalsIndex + 1)..valueEnd]
                : setCookie[(equalsIndex + 1)..];

            lock (_lock)
            {
                if (name.Equals("sid", StringComparison.OrdinalIgnoreCase))
                {
                    _sid = value;
                }
                else if (name.Equals("rememberme", StringComparison.OrdinalIgnoreCase))
                {
                    _rememberMe = value;
                }
            }
        }

        public AuthCookies? ToAuthCookies()
        {
            lock (_lock)
            {
                var cookies = new AuthCookies(_sid, _rememberMe);
                return cookies.HasCredentials ? cookies : null;
            }
        }
    }
}
