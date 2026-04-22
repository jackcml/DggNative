using System.Collections.Frozen;
using DggNative.Rendering;
using DggNative.Services;

namespace DggNative.Tests;

public class EmoteCatalogServiceTest
{
    [Fact]
    public async Task InitializeAsync_RetriesUntilTheCatalogLoads()
    {
        var attempts = 0;
        var service = new EmoteCatalogService(
            catalogLoader: () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    return Task.FromResult<FrozenDictionary<string, string>?>(null);
                }

                return Task.FromResult<FrozenDictionary<string, string>?>(BuildCatalog("Aware"));
            },
            catalogRetryDelay: TimeSpan.Zero);

        await service.InitializeAsync();

        Assert.Equal(2, attempts);
        var part = Assert.Single(service.ParseMessage("Aware"));
        Assert.True(part.IsEmote);
        Assert.Equal("https://example.test/Aware.png", part.ImageUrl);
    }

    [Fact]
    public async Task GetImageAsync_RetriesAfterANullLoad()
    {
        var attempts = 0;
        var service = new EmoteCatalogService(
            imageLoader: _ =>
            {
                attempts++;
                return Task.FromResult<Avalonia.Media.Imaging.Bitmap?>(null);
            });

        Assert.Null(await service.GetImageAsync("https://example.test/Aware.png"));
        Assert.Null(await service.GetImageAsync("https://example.test/Aware.png"));

        Assert.Equal(2, attempts);
    }

    private static FrozenDictionary<string, string> BuildCatalog(params string[] names)
    {
        return names
            .ToDictionary(static name => name, static name => $"https://example.test/{name}.png", StringComparer.Ordinal)
            .ToFrozenDictionary(StringComparer.Ordinal);
    }
}
