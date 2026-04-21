using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services;

public class CookiePersistenceService
{
    private static readonly string CookieFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DggNative",
        "auth.json");

    public async Task SaveAsync(AuthCookies cookies)
    {
        var dir = Path.GetDirectoryName(CookieFilePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(cookies);
        await File.WriteAllTextAsync(CookieFilePath, json);
    }

    public async Task<AuthCookies?> LoadAsync()
    {
        if (!File.Exists(CookieFilePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(CookieFilePath);
            return JsonSerializer.Deserialize<AuthCookies>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(CookieFilePath))
            File.Delete(CookieFilePath);
    }
}
