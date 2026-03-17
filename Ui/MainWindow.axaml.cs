using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Text.Json;
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

    public MainWindow()
    {
        InitializeComponent();
    }
    
    public async void InitilizeAsync()
    {
        try
        {
            _invokeRequest = new InvokeRequest();
            var result = await _invokeRequest.SendRequestAsync("get_settings");
            _settings = JsonSerializer.Deserialize(result, JsonContext.Default.SettingsClass)!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Settings parsing error: {ex}");
            throw;
        }

        DataContext = this;

        var page = new ChannelsPage();
        page.Load(_invokeRequest);

        PageHost.Content = page;
        ChannelsItem.IsSelected = true;

        ChannelsItem.PointerPressed += (_, _) => Navigate(ChannelsItem);
        SfxsItem.PointerPressed += (_, _) => Navigate(SfxsItem);
        EffectsItem.PointerPressed += (_, _) => Navigate(EffectsItem);
        PerformanceItem.PointerPressed += (_, _) => Navigate(PerformanceItem);
        SettingsItem.PointerPressed += (_, _) => Navigate(SettingsItem);
        
        Closing += OnClosing;
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
                var channelsPage = new ChannelsPage();
                channelsPage.Load(_invokeRequest);

                PageHost.Content = channelsPage;
                break;
            case "SfxsItem":
                var sfxsPage = new SfxsPage();
                sfxsPage.Load(_invokeRequest);

                PageHost.Content = sfxsPage;
                break;
            case "EffectsItem":
                PageHost.Content = new EffectsPage();
                break;
            case "PerformanceItem":
                PageHost.Content = new PerformancePage();
                break;
            case "SettingsItem":
                var settingsPage = new SettingsPage();
                settingsPage.Load(Reload, _settings, _invokeRequest);
                
                PageHost.Content = settingsPage;
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
            if (_settings == null) return Colors.Transparent;
            return _settings.light ? Colors.White : Colors.Black;
        }
    }
}