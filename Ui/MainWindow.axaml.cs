using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Animation;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Media;
using Vice.Ui.Controls;
using Vice.Ui.Pages.Channels;
using Vice.Ui.Pages.Sfxs;
using Vice.Ui.Pages.Effects;
using Vice.Ui.Pages.Performance;
using Vice.Ui.Pages.Settings;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Vice.Ui;

public partial class MainWindow : Window
{
    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(IsPaneOpen), false);
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        PageHost.Content = new ChannelsPage();
        ChannelsItem.IsSelected = true;

        ChannelsItem.PointerPressed += (_, _) => Navigate("channels", ChannelsItem);
        SfxsItem.PointerPressed += (_, _) => Navigate("sfxs", SfxsItem);
        EffectsItem.PointerPressed += (_, _) => Navigate("effects", EffectsItem);
        PerformanceItem.PointerPressed += (_, _) => Navigate("performance", PerformanceItem);
        SettingsItem.PointerPressed += (_, _) => Navigate("settings", SettingsItem);

        SidebarOpen.Click += (_, _) => TriggerPaneCommand();
    }
    
    private void TriggerPaneCommand()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    private void Navigate(string tag, SidebarItem item)
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
                break;
            case "sfxs":
                PageHost.Content = new SfxsPage();
                break;
            case "effects":
                PageHost.Content = new EffectsPage();
                break;
            case "performance":
                PageHost.Content = new PerformancePage();
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