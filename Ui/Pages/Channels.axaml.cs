using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Newtonsoft.Json;
using Vice.Ui.Utils;

namespace Vice.Ui.Pages;

public partial class ChannelsPage : UserControl
{
    private InvokeRequest _invokeRequest;
    
    public static readonly StyledProperty<bool> IsLoadedProperty =
        AvaloniaProperty.Register<ChannelsPage, bool>(nameof(IsLoaded), false);
    public static readonly StyledProperty<bool> EditingProperty =
        AvaloniaProperty.Register<ChannelsPage, bool>(nameof(Editing), false);
    public static readonly StyledProperty<ChannelItemTemplate?> EditedItemTemplateProperty =
        AvaloniaProperty.Register<ChannelsPage, ChannelItemTemplate?>(nameof(EditedItemTemplate));
    public static readonly StyledProperty<int> ItemsWidthProperty =
        AvaloniaProperty.Register<ChannelsPage, int>(nameof(ItemsWidth), 100);

    public ObservableCollection<ChannelItemTemplate> Items { get; set; } = new();

    public ObservableCollection<string> DeviceAndAppList { get; set; } = new();

    public bool IsLoaded
    {
        get => GetValue(IsLoadedProperty);
        set => SetValue(IsLoadedProperty, value);
    }
    
    public bool Editing
    {
        get => GetValue(EditingProperty);
        set => SetValue(EditingProperty, value);
    }
    
    public ChannelItemTemplate? EditedItemTemplate
    {
        get => GetValue(EditedItemTemplateProperty);
        set => SetValue(EditedItemTemplateProperty, value);
    }
    
    public int ItemsWidth
    {
        get => GetValue(ItemsWidthProperty);
        set => SetValue(ItemsWidthProperty, value);
    }
    
    public ChannelsPage(InvokeRequest request)
    {
        InitializeComponent();
        DataContext = this;

        _invokeRequest = request;
        
        Refresh();
        
        //TODO: Volume bars
    }
    
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        var width = e.NewSize.Width;
        ItemsWidth = Math.Max(1, (int)(width / 10));
        if (width < 150)
        {
            ItemsWidth = Math.Max(1, (int)width);
        }
        else if (width < 500)
        {
            ItemsWidth = Math.Max(1, (int)(width / 2));
        }
        else if (width < 750)
        {
            ItemsWidth = Math.Max(1, (int)(width / 5));
        } 
        else if (width < 1000)
        {
            ItemsWidth = Math.Max(1, (int)(width / 7));
        }
    }

    public async void Refresh()
    {
        Items.Clear();
        IsLoaded = false;

        try
        {
            var result = await _invokeRequest.SendRequestAsync("get_channels");
            var parsed = JsonConvert.DeserializeObject<List<ChannelsClass>>(result);

            foreach (var item in parsed)
            {
                var itemTemplate = new ChannelItemTemplate(
                    item.name,
                    item.icon,
                    item.color,
                    item.device,
                    item.deviceOrApp,
                    item.lowlatency,
                    item.volume,
                    false
                );
                Items.Add(itemTemplate);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Channel parsing error: {ex}");
            throw;
        }
        finally
        {
            IsLoaded = true;
        }
    }

    public async void GetNewList()
    {
        DeviceAndAppList.Clear();
        
        if (EditedItemTemplate.deviceOrApp == DeviceOrApp.App)
        {
            try
            {
                var result = await _invokeRequest.SendRequestAsync("get_apps");
                var parsed = JsonConvert.DeserializeObject<List<string>>(result);
                
                foreach (var item in parsed)
                {
                    DeviceAndAppList.Add(item);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"App parsing error: {ex}");
                throw;
            }
        }
        else
        {
            try
            {
                var result = await _invokeRequest.SendRequestAsync("get_devices");
                var parsed = JsonConvert.DeserializeObject<List<string>>(result);
                
                foreach (var item in parsed)
                {
                    DeviceAndAppList.Add(item);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"App parsing error: {ex}");
                throw;
            }
        }
        EditedItemTemplate.OnPropertyChanged(nameof(DeviceAndAppList));
    }
    
    private async void DeleteItem(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem btn && btn.Parent.Parent.Parent.Parent.Parent is Border border)
        {
            await _invokeRequest.SendRequestAsync(
                "delete_channel",
                new Dictionary<string, object> { { "name", (border.Tag as string)! } },
                false
            );
            
            Refresh();
        }
    }
    
    private void EditItem(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem btn)
        {
            Editing = true;
            EditedItemTemplate = btn.DataContext as ChannelItemTemplate;
            GetNewList();
        }
    }
    
    private void NewItem(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Editing = true;
        EditedItemTemplate = new ChannelItemTemplate(
            "",
            "question_regular",
            [255,0,0],
            "Select Audio Device",
            DeviceOrApp.Device,
            false,
            1,
            true
        );
        GetNewList();
    }
    
    private void MoreButton(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Open();
        }
    }
    
    private async void SliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (sender is Slider slider && slider.Parent.Parent is Border border)
        {
            await _invokeRequest.SendRequestAsync(
                "set_volume",
                new Dictionary<string, object> { { "name", (border.Tag as string)! }, { "volume", slider.Value } },
                false
            );
        }
    }
    
    private void BackButtonClick(object? sender, RoutedEventArgs e)
    {
        Editing = false;
        EditedItemTemplate = null;
    }

    private async void SaveButtonClick(object? sender, RoutedEventArgs e)
    {
        List<byte> parsedColor = new List<byte>();
        parsedColor.Add(EditedItemTemplate.Color.R);
        parsedColor.Add(EditedItemTemplate.Color.G);
        parsedColor.Add(EditedItemTemplate.Color.B);

        if (EditedItemTemplate.CreatingNew)
        {
            await _invokeRequest.SendRequestAsync(
                "new_channel",
                new Dictionary<string, object> { { "name", EditedItemTemplate.name }, { "icon", EditedItemTemplate.icon },  { "color", parsedColor }, { "deviceapps", EditedItemTemplate.device }, { "device", EditedItemTemplate.deviceOrApp.ToString() }, { "low", EditedItemTemplate.lowlatency } },
                false
            );
        }
        else
        {
            await _invokeRequest.SendRequestAsync(
                "edit_channel",
                new Dictionary<string, object> { { "name", EditedItemTemplate.name }, { "icon", EditedItemTemplate.icon },  { "color", parsedColor }, { "deviceapps", EditedItemTemplate.device }, { "device", EditedItemTemplate.deviceOrApp.ToString() }, { "low", EditedItemTemplate.lowlatency }, { "oldname", EditedItemTemplate.OldName } },
                false
            );
        }
        
        Editing = false;
        EditedItemTemplate = null;
        Refresh();
    }

    private void CaptureDeviceItem(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem)
        {
            EditedItemTemplate!.deviceOrApp = DeviceOrApp.Device;
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.deviceOrApp));
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.DeviceOrAppParsed));
        }

        GetNewList();
    }
    
    private void CaptureAppItem(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem)
        {
            EditedItemTemplate!.deviceOrApp = DeviceOrApp.App;
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.deviceOrApp));
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.DeviceOrAppParsed));
        }

        GetNewList();
    }

    public void DeviceAppSelection(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem btn && btn.DataContext is string option)
        {
            EditedItemTemplate!.device = option;
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.device));
        }
    }
    
    private void IconSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var tag = btn.Tag as string;
            EditedItemTemplate!.icon = tag!;
                
            if (!(Application.Current?.FindResource(tag!) as StreamGeometry is null))
            {
                EditedItemTemplate.Icon = (Application.Current?.FindResource(tag!) as StreamGeometry)!;
            }
            else
            {
                EditedItemTemplate.Icon = (Application.Current?.FindResource("question_regular") as StreamGeometry)!;
            }
                
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.Icon));

            if (btn.Parent!.Parent!.Parent!.Parent! is Popup popup)
            {
                popup.IsOpen = false;
            }
        }
    }
}

public class ChannelItemTemplate : ChannelsClass, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    private Color _color;
    private float _volumeFloat;
    private double _barHeight;
    
    public ChannelItemTemplate(string Nname, string Nicon, List<byte> Ncolor, string Ndevice, DeviceOrApp NdeviceOrApp, bool Nlowlatency, double Nvolume, bool NCreatingNew)
    : base(Nname, Nicon, Ncolor, Ndevice, NdeviceOrApp, Nlowlatency, Nvolume)
    {
        if (Nicon == null || Nicon.Trim() == "" || !(Application.Current?.FindResource(Nicon!) as StreamGeometry is null))
        {
            Icon = (Application.Current?.FindResource(Nicon!) as StreamGeometry)!;
        }
        else
        {
            Icon = (Application.Current?.FindResource("question_regular") as StreamGeometry)!;
        }
        
        Color = new Color(255, Ncolor[0], Ncolor[1], Ncolor[2]);
        OldName = Nname;
        CreatingNew = NCreatingNew;
    }
    
    public Brush Brush => new SolidColorBrush(Color);
    public Geometry Icon { get; set; }
    public string DeviceOrAppParsed
    {
        get
        {
            if (deviceOrApp == DeviceOrApp.App)
            {
                return "Capture App";
            }
            else
            {
                return "Capture Device";
            }
        }
    }
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Brush));
        }
    }
    public string OldName { get; set; }
    public bool CreatingNew { get; set; }
}