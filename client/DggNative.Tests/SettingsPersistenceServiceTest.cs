using DggNative.Models;
using DggNative.Services;

namespace DggNative.Tests;

public class SettingsPersistenceServiceTest
{
    [Fact]
    public async Task SettingsRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "settings.json");
        try
        {
            var service = new SettingsPersistenceService(path);
            var settings = new AppSettings(ServerKind.Custom, "ws://localhost:8080", "jack_1");

            await service.SaveAsync(settings);
            var loaded = await service.LoadAsync();

            Assert.Equal(settings, loaded);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task LoadReturnsNullWhenFileMissing()
    {
        var service = new SettingsPersistenceService(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        Assert.Null(await service.LoadAsync());
    }

    [Fact]
    public async Task LoadReturnsNullOnCorruptFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(path, "not json {");
        try
        {
            var service = new SettingsPersistenceService(path);

            Assert.Null(await service.LoadAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
