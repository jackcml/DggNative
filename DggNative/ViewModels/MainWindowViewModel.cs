using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DggNative.Models;
using DggNative.Services;

namespace DggNative.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<ChatMessage> MessageList { get; } = [];

    public MainWindowViewModel(IChatService chatService)
    {
        chatService.MessageStream.
            OfType<ChatMessage>().
            ObserveOn(new AvaloniaSynchronizationContext()).
            Subscribe(item => MessageList.Add(item));
        Task.Run(chatService.ConnectAsync);
    }
}