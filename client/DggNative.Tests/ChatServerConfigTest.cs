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
    public void ResolveRejectsInvalidCustomUrlWithoutChangingTrustDomain()
    {
        var settings = new AppSettings(ServerKind.Custom, "not a url");
        Assert.Throws<ChatServerConfigurationException>(() => ChatServerConfig.Resolve(null, settings));
    }

    [Fact]
    public void ResolveRejectsNonWebSocketScheme()
    {
        var settings = new AppSettings(ServerKind.Custom, "https://example.com/ws");
        Assert.Throws<ChatServerConfigurationException>(() => ChatServerConfig.Resolve(null, settings));
    }

    [Fact]
    public void ResolvePrefersEnvUrlOverSettings()
    {
        var settings = new AppSettings(ServerKind.Custom, "ws://localhost:9999");
        var config = ChatServerConfig.Resolve("ws://localhost:8080", settings);

        Assert.Equal(new Uri("ws://localhost:8080"), config.ServerUri);
        Assert.Equal(ChatAuthMode.Hello, config.AuthMode);
    }

    [Fact]
    public void ResolveUsesCookieAuthOnlyForExactApprovedOfficialEndpoint()
    {
        var config = ChatServerConfig.Resolve("wss://chat.destiny.gg/ws", new AppSettings());

        Assert.Equal(ChatAuthMode.Cookie, config.AuthMode);
    }

    [Theory]
    [InlineData("ws://chat.destiny.gg/ws")]
    [InlineData("wss://destiny.gg/ws")]
    [InlineData("wss://evil.destiny.gg/ws")]
    [InlineData("wss://notdestiny.gg/ws")]
    [InlineData("wss://chat.destiny.gg/other")]
    public void ResolveNeverAttachesCookiesToUnapprovedEnvEndpoint(string envUrl)
    {
        Assert.Equal(ChatAuthMode.Hello, ChatServerConfig.Resolve(envUrl, new AppSettings()).AuthMode);
    }

    [Fact]
    public void ResolveRejectsInvalidEnvUrlInsteadOfUsingPersistedOrOfficialServer()
    {
        var settings = new AppSettings(ServerKind.Custom, "ws://localhost:8080");
        Assert.Throws<ChatServerConfigurationException>(() => ChatServerConfig.Resolve("nope", settings));
    }

    [Theory]
    [InlineData("destiny.gg", true)]
    [InlineData("www.destiny.gg", true)]
    [InlineData("chat.destiny.gg", true)]
    [InlineData("notdestiny.gg", false)]
    [InlineData("destiny.gg.example.com", false)]
    public void HostBoundaryCheckRequiresExactHostOrDotSubdomain(string host, bool expected)
    {
        Assert.Equal(expected, EndpointTrust.IsHostOrSubdomain(host, "destiny.gg"));
    }
}
