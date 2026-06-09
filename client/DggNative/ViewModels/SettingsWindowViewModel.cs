using CommunityToolkit.Mvvm.ComponentModel;
using DggNative.Models;

namespace DggNative.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    [ObservableProperty] private bool _isOfficial;

    [ObservableProperty] private bool _isCustom;

    [ObservableProperty] private string _customServerUrl;

    [ObservableProperty] private string? _urlError;

    public bool IsEnvOverrideActive { get; }
    public string EnvOverrideText { get; }

    public SettingsWindowViewModel(AppSettings settings, string? envUrl)
    {
        _settings = settings;
        _isOfficial = settings.ServerKind == ServerKind.Official;
        _isCustom = settings.ServerKind == ServerKind.Custom;
        _customServerUrl = settings.CustomServerUrl ?? "ws://localhost:8080";
        IsEnvOverrideActive = !string.IsNullOrWhiteSpace(envUrl);
        EnvOverrideText = $"The wsurl environment variable ({envUrl}) is set and overrides the server chosen here.";
    }

    // Parameterless constructor for the XAML designer only
    public SettingsWindowViewModel() : this(new AppSettings(), null)
    {
    }

    /// <summary>Validates the form and returns the settings to persist, or null when invalid.</summary>
    public AppSettings? BuildResult()
    {
        var url = CustomServerUrl.Trim();

        if (IsCustom && !ChatServerConfig.TryParseWsUri(url, out _))
        {
            UrlError = "Enter an absolute ws:// or wss:// URL.";
            return null;
        }

        UrlError = null;
        return _settings with
        {
            ServerKind = IsCustom ? ServerKind.Custom : ServerKind.Official,
            CustomServerUrl = url.Length > 0 ? url : null,
        };
    }
}
