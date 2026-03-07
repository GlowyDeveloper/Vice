using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Newtonsoft.Json;
using Vice.Ui.Controls;
using Vice.Ui.Pages;
using Vice.Ui.Utils;

namespace Vice.Ui;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private InvokeRequest _invokeRequest;
    private SettingsClass _settings;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(IsPaneOpen));
    
    private MainWindow(SettingsClass settings, InvokeRequest invokeRequest)
    {
        _invokeRequest = invokeRequest;
        _settings = settings;
        
        InitializeComponent();
        DataContext = this;

        PageHost.Content = new ChannelsPage(_invokeRequest);
        ChannelsItem.IsSelected = true;

        ChannelsItem.PointerPressed += (_, _) => Navigate(ChannelsItem);
        SfxsItem.PointerPressed += (_, _) => Navigate(SfxsItem);
        EffectsItem.PointerPressed += (_, _) => Navigate(EffectsItem);
        PerformanceItem.PointerPressed += (_, _) => Navigate(PerformanceItem);
        SettingsItem.PointerPressed += (_, _) => Navigate(SettingsItem);
        
        Closing += OnClosing;
    }
    
    public static async Task<MainWindow> CreateAsync()
    {
        try
        {
            var invokeRequest = new InvokeRequest();
            var result = await invokeRequest.SendRequestAsync("get_settings");
            var parsed = JsonConvert.DeserializeObject<SettingsClass>(result);

            return new MainWindow(parsed!, invokeRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Settings parsing error: {ex}");
            throw;
        }
    }
    
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        await _invokeRequest.SendRequestAsync("quit", null, false);
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
                PageHost.Content = new SettingsPage(Reload, _settings, _invokeRequest);
                break;
        }
    }

    public void Reload(object? _, SettingsClass settings)
    {
        _settings = settings;
        OnPropertyChanged(nameof(Scale));
        OnPropertyChanged(nameof(Color));
    }

    public bool IsPaneOpen
    {
        get => GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }
    
    public double Scale
    {
        get => Math.Clamp(_settings.scale, 0.1, 2.0);
    }
    
    public Color Color
    {
        get
        {
            if (_settings.light)
            {
                return Colors.White;
            }
            else
            {
                return Colors.Black;
            }
        }
    }
}