using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services;

public class SettingsPersistenceService(string? filePath = null)
{
    private static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DggNative",
        "settings.json");

    private readonly string _filePath = filePath ?? DefaultFilePath;

    public async Task SaveAsync(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task<AppSettings?> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch
        {
            return null;
        }
    }
}
