using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DggNative.Rendering;

namespace DggNative.Services;

public class EmoteCatalogService
{
    private static readonly Uri EmotesEndpoint = new("https://emotes.click/api/emotes");
    private static readonly TimeSpan DefaultCatalogRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly FrozenDictionary<string, string> EmptyCatalog =
        new Dictionary<string, string>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);

    private readonly object _initializationGate = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> _imageCache = new(StringComparer.Ordinal);
    private readonly Func<Task<FrozenDictionary<string, string>?>> _catalogLoader;
    private readonly Func<string, Task<Bitmap?>> _imageLoader;
    private readonly TimeSpan _catalogRetryDelay;

    private FrozenDictionary<string, string> _emotes = EmptyCatalog;
    private Task? _initializationTask;
    private bool _isCatalogLoaded;

    public event EventHandler? CatalogUpdated;

    public EmoteCatalogService(
        Func<Task<FrozenDictionary<string, string>?>>? catalogLoader = null,
        Func<string, Task<Bitmap?>>? imageLoader = null,
        TimeSpan? catalogRetryDelay = null)
    {
        _catalogLoader = catalogLoader ?? LoadCatalogSnapshotAsync;
        _imageLoader = imageLoader ?? LoadBitmapAsync;
        _catalogRetryDelay = catalogRetryDelay ?? DefaultCatalogRetryDelay;
    }

    public IReadOnlyList<ChatInlinePart> ParseMessage(string? message)
        => EmoteTokenizer.Tokenize(message, _emotes);

    public Task InitializeAsync()
    {
        lock (_initializationGate)
        {
            if (_isCatalogLoaded)
            {
                return Task.CompletedTask;
            }

            return _initializationTask ??= LoadCatalogAsync();
        }
    }

    public async Task<Bitmap?> GetImageAsync(string imageUrl)
    {
        var loader = _imageCache.GetOrAdd(imageUrl, url =>
            new Lazy<Task<Bitmap?>>(
                () => _imageLoader(url),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var bitmap = await loader.Value.ConfigureAwait(false);
            if (bitmap is null)
            {
                _imageCache.TryRemove(imageUrl, out _);
            }

            return bitmap;
        }
        catch
        {
            _imageCache.TryRemove(imageUrl, out _);
            return null;
        }
    }

    private async Task LoadCatalogAsync()
    {
        while (!_isCatalogLoaded)
        {
            if (await TryLoadCatalogAsync().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(_catalogRetryDelay).ConfigureAwait(false);
        }
    }

    private async Task<bool> TryLoadCatalogAsync()
    {
        try
        {
            var snapshot = await _catalogLoader().ConfigureAwait(false);
            if (snapshot is null)
            {
                return false;
            }

            _emotes = snapshot.Count == 0 ? EmptyCatalog : snapshot;
            _isCatalogLoaded = true;
            CatalogUpdated?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            // Keep rendering raw message text if the catalog is unavailable.
            return false;
        }
    }

    private static async Task<FrozenDictionary<string, string>?> LoadCatalogSnapshotAsync()
    {
        await using var stream = await HttpClient.GetStreamAsync(EmotesEndpoint).ConfigureAwait(false);
        var response = await JsonSerializer.DeserializeAsync<EmoteApiResponse>(stream).ConfigureAwait(false);
        if (response?.Data is null)
        {
            return null;
        }

        return response.Data
            .Where(static emote =>
                emote.IsActive
                && !string.IsNullOrWhiteSpace(emote.Prefix)
                && !string.IsNullOrWhiteSpace(emote.ImageUrl))
            .GroupBy(static emote => emote.Prefix, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last().ImageUrl!, StringComparer.Ordinal)
            .ToFrozenDictionary(StringComparer.Ordinal);
    }

    private async Task<Bitmap?> LoadBitmapAsync(string imageUrl)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(imageUrl).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes, writable: false);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private sealed class EmoteApiResponse
    {
        [JsonPropertyName("data")]
        public required List<EmoteApiItem> Data { get; init; }
    }

    private sealed class EmoteApiItem
    {
        [JsonPropertyName("prefix")]
        public required string Prefix { get; init; }

        [JsonPropertyName("image_url")]
        public required string ImageUrl { get; init; }

        [JsonPropertyName("is_active")]
        public required bool IsActive { get; init; }
    }
}
