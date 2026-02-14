using Avalonia;
using Avalonia.Controls;
using Vice.Ui.Controls;
using Vice.Ui.Pages.Channels;
using Vice.Ui.Pages.Sfxs;
using Vice.Ui.Pages.Effects;
using Vice.Ui.Pages.Performance;
using Vice.Ui.Pages.Settings;
using Vice.Ui.Utils;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Vice.Ui;

public partial class MainWindow : Window
{
    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(IsPaneOpen), false);

    private readonly InvokeRequest _invokeRequest = new();
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _ = _invokeRequest.ConnectAsync();

        PageHost.Content = new ChannelsPage();
        ChannelsItem.IsSelected = true;

        ChannelsItem.PointerPressed += async (_, _) => await Navigate("channels", ChannelsItem);
        SfxsItem.PointerPressed += async (_, _) => await Navigate("sfxs", SfxsItem);
        EffectsItem.PointerPressed += async (_, _) => await Navigate("effects", EffectsItem);
        PerformanceItem.PointerPressed += async (_, _) => await Navigate("performance", PerformanceItem);
        SettingsItem.PointerPressed += async (_, _) => await Navigate("settings", SettingsItem);

        SidebarOpen.Click += (_, _) => TriggerPaneCommand();
    }
    
    private void TriggerPaneCommand()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    private async Task Navigate(string tag, SidebarItem item)
    {
        ChannelsItem.IsSelected = false;
        SfxsItem.IsSelected = false;
        EffectsItem.IsSelected = false;
		PerformanceItem.IsSelected = false;
        SettingsItem.IsSelected = false;

        item.IsSelected = true;
        
        switch (tag)
        {
            case "channels":
                PageHost.Content = new ChannelsPage();

                await _invokeRequest.SendRequestAsync(
                    "open_link",
                    new Dictionary<string, object> { { "url", "https://github.com/GlowyDeveloper/Vice" } },
                    false
                );
                break;
            case "sfxs":
                PageHost.Content = new SfxsPage();
                
                Console.WriteLine("This is a command message from page navigation");
                break;
            case "effects":
                PageHost.Content = new EffectsPage();
                break;
            case "performance":
                PageHost.Content = new PerformancePage();

                var res = await _invokeRequest.SendRequestAsync("get_performance");
                Console.WriteLine($"Received performance data: {res}");
                break;
            case "settings":
                PageHost.Content = new SettingsPage();
                break;
        }
    }

    public bool IsPaneOpen
    {
        get => GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }
}