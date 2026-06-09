using Avalonia.Controls;
using Avalonia.Interactivity;
using DggNative.ViewModels;

namespace DggNative.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsWindowViewModel vm) return;

        var result = vm.BuildResult();
        if (result != null)
        {
            Close(result);
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
