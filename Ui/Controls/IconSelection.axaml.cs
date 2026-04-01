using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vice.Ui.Controls;

public partial class IconSelection : Grid
{
    public event EventHandler<RoutedEventArgs>? Clicked;
    
    public IconSelection()
    {
        InitializeComponent();
    }

    private void Callback(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            Clicked?.Invoke(button, e);
        }
    }
}