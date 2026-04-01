using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vice.Ui.Controls;

public partial class MessageBoxOk : Window
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MessageBoxOk, string>(nameof(Text));
    
    public event EventHandler<RoutedEventArgs>? Clicked;

    public MessageBoxOk()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Ok_Clicked(object sender, RoutedEventArgs e)
    {
        if (Clicked is not null)
        {
            Clicked.Invoke(sender, e);
        }
        this.Close();
    }
}