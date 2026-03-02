using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Vice.Ui.Controls;
using Vice.Ui.Pages;
using Vice.Ui.Utils;

namespace Vice.Ui;

public partial class MainWindow : Window
{
    private InvokeRequest _invokeRequest = new InvokeRequest();
    
    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(IsPaneOpen), false);
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        PageHost.Content = new ChannelsPage(_invokeRequest);
        ChannelsItem.IsSelected = true;

        ChannelsItem.PointerPressed += (_, _) => Navigate(ChannelsItem);
        SfxsItem.PointerPressed += (_, _) => Navigate(SfxsItem);
        EffectsItem.PointerPressed += (_, _) => Navigate(EffectsItem);
        PerformanceItem.PointerPressed += (_, _) => Navigate(PerformanceItem);
        SettingsItem.PointerPressed += (_, _) => Navigate(SettingsItem);
    }
    
    private void TriggerPaneCommand(object? sender, RoutedEventArgs e)
    {
        IsPaneOpen = !IsPaneOpen;
    }

    private void Navigate(SidebarItem item)
    {
        ChannelsItem.IsSelected = false;
        SfxsItem.IsSelected = false;
        EffectsItem.IsSelected = false;
		PerformanceItem.IsSelected = false;
        SettingsItem.IsSelected = false;

        item.IsSelected = true;
        
        switch (item.Name)
        {
            case "ChannelsItem":
                PageHost.Content = new ChannelsPage(_invokeRequest);
                break;
            case "SfxsItem":
                PageHost.Content = new SfxsPage(_invokeRequest);
                break;
            case "EffectsItem":
                PageHost.Content = new EffectsPage();
                break;
            case "PerformanceItem":
                PageHost.Content = new PerformancePage();
                break;
            case "SettingsItem":
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