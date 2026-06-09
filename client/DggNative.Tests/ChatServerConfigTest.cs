using DggNative.Models;

namespace DggNative.Tests;

public class ChatServerConfigTest
{
    [Fact]
    public void ResolveDefaultsToOfficialServerWithCookieAuth()
    {
        var config = ChatServerConfig.Resolve(null, new AppSettings());

        Assert.Equal(ChatServerConfig.OfficialUri, config.ServerUri);
        Assert.Equal(ChatAuthMode.Cookie, config.AuthMode);
        Assert.Null(config.HelloNick);
    }

    [Fact]
    public void ResolveUsesCustomServerWithHelloAuth()
    {
        var settings = new AppSettings(ServerKind.Custom, "ws://localhost:8080");
        var config = ChatServerConfig.Resolve(null, settings);

        Assert.Equal(new Uri("ws://localhost:8080"), config.ServerUri);
        Assert.Equal(ChatAuthMode.Hello, config.AuthMode);
    }

    [Fact]
    public void ResolveFallsBackToOfficialOnInvalidCustomUrl()
    {
        var settings = new AppSettings(ServerKind.Custom, "not a url");
        var config = ChatServerConfig.Resolve(null, settings);

        Assert.Equal(ChatServerConfig.OfficialUri, config.ServerUri);
        Assert.Equal(ChatAuthMode.Cookie, config.AuthMode);
    }

    [Fact]
    public void ResolveFallsBackToOfficialOnNonWebSocketScheme()
    {
        var settings = new AppSettings(ServerKind.Custom, "https://example.com/ws");
        var config = ChatServerConfig.Resolve(null, settings);

        Assert.Equal(ChatServerConfig.OfficialUri, config.ServerUri);
    }

    [Fact]
    public void ResolvePrefersEnvUrlOverSettings()
    {
        var settings = new AppSettings(ServerKind.Custom, "ws://localhost:9999");
        var config = ChatServerConfig.Resolve("ws://localhost:8080", settings);

        Assert.Equal(new Uri("ws://localhost:8080"), config.ServerUri);
        Assert.Equal(ChatAuthMode.Hello, config.AuthMode);
    }

    [Theory]
    [InlineData("wss://chat.destiny.gg/ws")]
    [InlineData("wss://destiny.gg/ws")]
    public void ResolveUsesCookieAuthForDestinyGgEnvUrls(string envUrl)
    {
        var config = ChatServerConfig.Resolve(envUrl, new AppSettings());

        Assert.Equal(ChatAuthMode.Cookie, config.AuthMode);
    }

    [Fact]
    public void ResolveIgnoresInvalidEnvUrl()
    {
        var settings = new AppSettings(ServerKind.Custom, "ws://localhost:8080");
        var config = ChatServerConfig.Resolve("nope", settings);

        Assert.Equal(new Uri("ws://localhost:8080"), config.ServerUri);
        Assert.Equal(ChatAuthMode.Hello, config.AuthMode);
    }
}
