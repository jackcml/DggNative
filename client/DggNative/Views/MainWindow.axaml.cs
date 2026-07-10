using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DggNative.ViewModels;

namespace DggNative.Views;

public partial class MainWindow : Window
{
    private bool _isScrolledToBottom = true;
    private MainWindowViewModel? _notificationActivationViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Activated += (_, _) => SetWindowFocus(true);
        Deactivated += (_, _) => SetWindowFocus(false);
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.IsWindowFocused = IsActive;
        vm.MessageList.CollectionChanged += MessageList_OnCollectionChanged;
        SubscribeToNotificationActivation(vm);
    }

    private void SubscribeToNotificationActivation(MainWindowViewModel vm)
    {
        if (ReferenceEquals(_notificationActivationViewModel, vm))
        {
            return;
        }

        if (_notificationActivationViewModel is not null)
        {
            _notificationActivationViewModel.MentionNotificationActivated -= Notification_OnActivated;
        }

        _notificationActivationViewModel = vm;
        vm.MentionNotificationActivated += Notification_OnActivated;
    }

    private void Notification_OnActivated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(FocusFromNotification);
    }

    private void FocusFromNotification()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Focus();
    }

    private void SetWindowFocus(bool isFocused)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsWindowFocused = isFocused;
        }
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        const int threshold = 50;
        _isScrolledToBottom = scrollViewer.Offset.Y + scrollViewer.Viewport.Height >=
                              scrollViewer.Extent.Height - threshold;
        UpdateMoreMessagesButton();
    }

    private void MessageList_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _isScrolledToBottom)
        {
            MessageScrollViewer.ScrollToEnd();
        }

        UpdateMoreMessagesButton();
    }

    private void UpdateMoreMessagesButton()
    {
        MoreMessagesButton.IsVisible = !_isScrolledToBottom;
    }

    private void MoreMessagesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        MessageScrollViewer.ScrollToEnd();
        _isScrolledToBottom = true;
        UpdateMoreMessagesButton();
    }

    private async void MessageInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox
            || DataContext is not MainWindowViewModel vm
            || textBox.Text is null) return;

        if (e.Key != Key.Enter) return;

        var message = textBox.Text;
        if (await vm.SendChatMessageAsync(message)) textBox.Clear();
        e.Handled = true;
    }

    private async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.LoginCommand.ExecuteAsync(this);
    }

    private async void LogoutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.LogoutCommand.ExecuteAsync(null);
    }

    private async void JoinButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.JoinCommand.ExecuteAsync(null);
    }

    private async void NickInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not MainWindowViewModel vm) return;

        await vm.JoinCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    private async void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.OpenSettingsCommand.ExecuteAsync(this);
    }
}
