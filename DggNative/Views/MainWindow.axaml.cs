using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DggNative.ViewModels;

namespace DggNative.Views;

public partial class MainWindow : Window
{
    private bool _isScrolledToBottom = true;

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
    }

    private void MessageList_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _isScrolledToBottom)
            MessageScrollViewer.ScrollToEnd();
    }

    private async void MessageInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox
            || DataContext is not MainWindowViewModel vm
            || textBox.Text is null) return;

        if (e.Key != Key.Enter) return;

        var message = textBox.Text;
        textBox.Clear();
        await vm.SendChatMessageAsync(message);
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
}
