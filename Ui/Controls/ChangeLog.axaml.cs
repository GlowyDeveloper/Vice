using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vice.Ui.Controls;

public partial class ChangeLog : Window
{
   public ChangeLog()
    {
        InitializeComponent();
    }

    private void Ok_Clicked(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}