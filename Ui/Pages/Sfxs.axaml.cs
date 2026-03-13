using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using Vice.Ui.Utils;

namespace Vice.Ui.Pages;

public partial class SfxsPage : UserControl
{
    private InvokeRequest _invokeRequest;
    
    public static readonly StyledProperty<bool> IsLoadedProperty =
        AvaloniaProperty.Register<SfxsPage, bool>(nameof(IsLoaded), false);
    public static readonly StyledProperty<bool> EditingProperty =
        AvaloniaProperty.Register<SfxsPage, bool>(nameof(Editing), false);
    public static readonly StyledProperty<SfxItemTemplate?> EditedItemTemplateProperty =
        AvaloniaProperty.Register<SfxsPage, SfxItemTemplate?>(nameof(EditedItemTemplate));
    public static readonly StyledProperty<int> ColumnCountProperty =
        AvaloniaProperty.Register<SfxsPage, int>(nameof(ColumnCount), 1);

    public ObservableCollection<SfxItemTemplate> Items { get; set; } = new();

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
    
    public SfxItemTemplate? EditedItemTemplate
    {
        get => GetValue(EditedItemTemplateProperty);
        set => SetValue(EditedItemTemplateProperty, value);
    }
    
    public int ColumnCount
    {
        get => GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    public SfxsPage()
    {
        InitializeComponent();
    }

    public async void Load(InvokeRequest request)
    {
        DataContext = this;

        _invokeRequest = request;

        Refresh();
    }
    
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        var width = e.NewSize.Width;
        ColumnCount = Math.Max(1, (int)(width / 250));
    }

    public async void Refresh()
    {
        Items.Clear();
        IsLoaded = false;

        try
        {
            var result = await _invokeRequest.SendRequestAsync("get_soundboard");
            var parsed = JsonConvert.DeserializeObject<List<SFXClass>>(result);

            foreach (var item in parsed)
            {
                var itemTemplate = new SfxItemTemplate(
                    item.name,
                    item.icon,
                    item.color,
                    item.lowlatency,
                    item.keys,
                    item.sound,
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
    
    private async void DeleteItem(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem btn && btn.Parent.Parent.Parent.Parent.Parent is Border border)
        {
            await _invokeRequest.SendRequestAsync(
                "delete_sound",
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
            EditedItemTemplate = btn.DataContext as SfxItemTemplate;
        }
    }
    
    private void NewItem(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Editing = true;
        EditedItemTemplate = new SfxItemTemplate(
            "",
            "question_regular",
            [255,0,0],
            false,
            [],
            "",
            true
        );
    }
    
    private void MoreButton(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Open();
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
                "new_sound",
                new Dictionary<string, object> { { "name", EditedItemTemplate.name }, { "icon", EditedItemTemplate.icon }, { "color", parsedColor }, { "low", EditedItemTemplate.lowlatency }, { "keys", EditedItemTemplate.keys }, { "sound", EditedItemTemplate.sound } },
                false
            );
        }
        else
        {
            await _invokeRequest.SendRequestAsync(
                "edit_sound",
                new Dictionary<string, object> { { "name", EditedItemTemplate.name }, { "icon", EditedItemTemplate.icon },  { "color", parsedColor }, { "low", EditedItemTemplate.lowlatency }, { "oldname", EditedItemTemplate.OldName }, { "keys", EditedItemTemplate.keys } },
                false
            );
        }
        
        Editing = false;
        EditedItemTemplate = null;
        Refresh();
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

    private async void SoundFile(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select a file",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.wav", "*.mp3", "*.wma", "*.aac", "*.m4a", "*.flac" }
                    }
                }
            });

        if (files.Count > 0)
        {
            var file = files[0];
            var path = file.Path.LocalPath;
            EditedItemTemplate.sound = path;
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.sound));
        }
    }
}

public class SfxItemTemplate : SFXClass, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    private Color _color;
    
    public SfxItemTemplate(string Nname, string Nicon, List<byte> Ncolor, bool Nlowlatency, List<string> Nkeys, string Nsound, bool NCreatingNew)
    : base(Nname, Nicon, Ncolor, Nlowlatency, Nkeys, Nsound)
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

    public string KeysText
    {
        get => string.Join("+", keys);
        set
        {
            keys.Clear();
            value.Split('+').ToList().ForEach(x => keys.Add(x.Trim()));
        }
    }
}