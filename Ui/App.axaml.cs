using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Vice.Ui.Controls;

namespace Vice.Ui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;

            var window = new MainWindow();
            window.InitilizeAsync();
            desktop.MainWindow = window;
            window.Show();

            if (args!.Contains("--changelog"))
            {
                var changelogWindow = new ChangeLog();
                await changelogWindow.ShowDialog(window);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}