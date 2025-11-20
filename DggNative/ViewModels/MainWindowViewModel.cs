using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DggNative.Models;
using DggNative.Services;

namespace DggNative.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";
    
    public ObservableCollection<ChatMessage> MessageList { get; } = [];

    public MainWindowViewModel(IChatService chatService)
    {
        chatService.MessageStream.
            OfType<ChatMessage>().
            ObserveOn(new AvaloniaSynchronizationContext()).
            Subscribe(item => MessageList.Add(item));
        
        chatService.IsConnected.
            ObserveOn(new AvaloniaSynchronizationContext()).
            Subscribe(status =>
            {
                IsConnected = status is ConnectionStatusConnected;
                ConnectionStatusText = status switch
                {
                    ConnectionStatusConnected => "Connected",
                    ConnectionStatusDisconnected => "Disconnected",
                    ConnectionStatusConnecting => "Connecting...",
                    ConnectionStatusRetrying r => $"Retrying in {Math.Ceiling(r.MillisecondsUntilRetry / 1000.0)}s...",
                    _ => "Unknown"
                };
            });
        
        Task.Run(chatService.ConnectAsync);
    }
}