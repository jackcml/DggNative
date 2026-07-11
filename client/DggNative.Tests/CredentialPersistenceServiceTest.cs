using DggNative.Models;
using DggNative.Services;

namespace DggNative.Tests;

public class CredentialPersistenceServiceTest
{
    [Fact]
    public async Task SavesAndLoadsThroughSecureStore()
    {
        var credentials = new AuthCookies("sid-value", "remember-value");
        var store = new MemoryCredentialStore();
        var persistence = new CredentialPersistenceService(store);

        var save = await persistence.SaveAsync(credentials);
        var load = await persistence.LoadAsync();

        Assert.True(save.Succeeded);
        Assert.Equal(credentials, load.Credentials);
        Assert.Equal(credentials, store.Credentials);
    }

    [Fact]
    public async Task UnavailableStoreUsesSessionOnlyBehavior()
    {
        var persistence = new CredentialPersistenceService(
            new UnavailableCredentialStore("Secret Service unavailable; login is session-only."));

        var result = await persistence.LoadAsync();

        Assert.Null(result.Credentials);
        Assert.Contains("session-only", result.Error);
    }

    [Fact]
    public async Task ClearRemovesSecureCredentials()
    {
        var store = new MemoryCredentialStore { Credentials = new AuthCookies("secret", null) };
        var persistence = new CredentialPersistenceService(store);

        var result = await persistence.ClearAsync();

        Assert.True(result.Succeeded);
        Assert.Null(store.Credentials);
    }

    [Fact]
    public void WebSocketFactoryRejectsCookieAuthForInsecureOrUnapprovedEndpoints()
    {
        var factory = new ChatWebSocketFactory();
        foreach (var uri in new[]
                 {
                     new Uri("ws://chat.destiny.gg/ws"),
                     new Uri("wss://evil.destiny.gg/ws"),
                     new Uri("wss://chat.destiny.gg/other"),
                 })
        {
            Assert.Throws<InvalidOperationException>(() =>
                factory.Create(new ChatServerConfig(uri, ChatAuthMode.Cookie), new AuthCookies("secret", null)));
        }
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        public AuthCookies? Credentials { get; set; }
        public Task<AuthCookies?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Credentials);
        public Task SaveAsync(AuthCookies credentials, CancellationToken cancellationToken = default)
        {
            Credentials = credentials;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Credentials = null;
            return Task.CompletedTask;
        }
    }
}
