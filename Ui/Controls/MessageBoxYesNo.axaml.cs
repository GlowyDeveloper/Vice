using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vice.Ui.Controls;

public partial class MessageBoxYesNo : Window
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MessageBoxYesNo, string>(nameof(Text));

    public event EventHandler<RoutedEventArgs>? YesClicked;
    public event EventHandler<RoutedEventArgs>? NoClicked;

    public MessageBoxYesNo()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Yes_Clicked(object sender, RoutedEventArgs e)
    {
        if (YesClicked is not null)
        {
            YesClicked.Invoke(sender, e);
        }
        this.Close();
    }

    private void No_Clicked(object sender, RoutedEventArgs e)
    {
        if (NoClicked is not null)
        {
            NoClicked.Invoke(sender, e);
        }
        this.Close();
    }
}