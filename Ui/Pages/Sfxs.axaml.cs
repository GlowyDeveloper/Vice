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
using System.Text.Json;
using Avalonia.Layout;
using Avalonia.Threading;
using Vice.Ui.Utils;

namespace Vice.Ui.Pages;

public partial class SfxsPage : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    private InvokeRequest? _invokeRequest;
    
    public static readonly StyledProperty<bool> IsLoadedProperty =
        AvaloniaProperty.Register<SfxsPage, bool>(nameof(IsLoaded), false);
    public static readonly StyledProperty<bool> EditingProperty =
        AvaloniaProperty.Register<SfxsPage, bool>(nameof(Editing), false);
    public static readonly StyledProperty<SfxItemTemplate?> EditedItemTemplateProperty =
        AvaloniaProperty.Register<SfxsPage, SfxItemTemplate?>(nameof(EditedItemTemplate));
    public static readonly StyledProperty<int> ColumnCountProperty =
        AvaloniaProperty.Register<SfxsPage, int>(nameof(ColumnCount), 1);

    public ObservableCollection<SfxItemTemplate> Items { get; set; } = new();

    public new bool IsLoaded
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

    public void Load(InvokeRequest request)
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
            var result = await _invokeRequest!.SendRequestAsync("get_soundboard");
            var parsed = JsonSerializer.Deserialize(result, JsonContext.Default.ListSFXClass);

            foreach (var item in parsed!)
            {
                var itemTemplate = new SfxItemTemplate(
                    item.name,
                    item.icon,
                    item.color,
                    item.lowlatency,
                    item.keys,
                    item.sound,
                    item.effects,
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

    public async void GenerateVolume()
    {
        if (EditedItemTemplate == null)
        {
            return;
        }

        AudioFileText.Text = "Loading";
        EditedItemTemplate!.IsVolumeVisible = false;
        VolumeIndicatorFile.Children.Clear();
        VolumeIndicatorFile.ColumnDefinitions.Clear();

        try
        {
            var result = await _invokeRequest!.SendRequestAsync(
                "get_volume_sfx",
                new Dictionary<string, object>
                    { { "name", EditedItemTemplate!.OldName }, { "file", EditedItemTemplate!.sound } }
            );
            var parsed = JsonSerializer.Deserialize(result, JsonContext.Default.ListString);

            var i = 0;
            foreach (var str in parsed)
            {
                var trimmed = str?.Trim().Trim('"');
                if (float.TryParse(trimmed, out float height))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Console.WriteLine($"{height}");
                        height = Math.Clamp(height, 0f, 1f);

                        var bar = new Border
                        {
                            Background = Brushes.LightBlue,
                            Width = 4,
                            CornerRadius = new CornerRadius(2),
                            Margin = new Thickness(1, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            Height = 32 * height
                        };

                        Grid.SetColumn(bar, i++);
                        VolumeIndicatorFile.ColumnDefinitions.Add(new ColumnDefinition());
                        VolumeIndicatorFile.Children.Add(bar);
                    });
                }
            }

            EditedItemTemplate!.IsVolumeVisible = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Volume parsing error: {ex}");
        }
        finally
        {
            AudioFileText.Text = "Click to select an audio file";
        }
    }
    
    private async void DeleteItem(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem btn && btn.Parent!.Parent!.Parent!.Parent!.Parent is Border border)
        {
            await _invokeRequest!.SendRequestAsync(
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
            EffectsUi.Reset();
            EffectsUi.ConvertJson(EditedItemTemplate!.effects);
            GenerateVolume();
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
            new EffectsClass(),
            true
        );
        EffectsUi.Reset();
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
        List<byte> parsedColor =
        [
            EditedItemTemplate!.Color.R,
            EditedItemTemplate.Color.G,
            EditedItemTemplate.Color.B,
        ];

        List<string> parsedKeys = [];
        Keybinds.Text.Split(" + ").ToList().ForEach(x => parsedKeys.Add(x.Trim()));

        if (EditedItemTemplate.CreatingNew)
        {
            await _invokeRequest!.SendRequestAsync(
                "new_sound",
                new Dictionary<string, object> { { "name", EditedItemTemplate.name }, { "icon", EditedItemTemplate.icon }, { "color", parsedColor }, { "low", EditedItemTemplate.lowlatency }, { "keys", parsedKeys }, { "sound", EditedItemTemplate.sound }, { "effects", EffectsUi.GetCurrentJson() } },
                false
            );
        }
        else
        {
            await _invokeRequest!.SendRequestAsync(
                "edit_sound",
                new Dictionary<string, object> { { "name", EditedItemTemplate.name }, { "icon", EditedItemTemplate.icon },  { "color", parsedColor }, { "low", EditedItemTemplate.lowlatency }, { "oldname", EditedItemTemplate.OldName }, { "keys", parsedKeys }, { "effects", EffectsUi.GetCurrentJson() } },
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

            if (btn.Parent!.Parent!.Parent!.Parent is Popup popup)
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
            EditedItemTemplate!.sound = path;
            EditedItemTemplate.OnPropertyChanged(nameof(EditedItemTemplate.sound));
            GenerateVolume();
        }
    }
}

public class SfxItemTemplate : SFXClass, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    private Color _color;
    private bool _isVolumeVisible;
    
    public SfxItemTemplate(string Nname, string Nicon, List<byte> Ncolor, bool Nlowlatency, List<string> Nkeys, string Nsound, EffectsClass Neffects, bool NCreatingNew)
    : base(Nname, Nicon, Ncolor, Nlowlatency, Nkeys, Nsound, Neffects)
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
        IsVolumeVisible = false;
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
        get
        {
            if (keys.Count == 0)
            {
                return "Click to add a keybind";
            }
            else
            {
                return string.Join(" + ", keys);
            }
        }
        set
        {
            keys.Clear();
            value.Split(" + ").ToList().ForEach(x => keys.Add(x.Trim()));
        }
    }
    public bool IsVolumeVisible
    {
        get => _isVolumeVisible;
        set
        {
            _isVolumeVisible = value;
            OnPropertyChanged();
        }
    }
    public Brush LightOrDarkIconColor
    {
        get
        {
            double luminance = 0.299 * _color.R + 0.587 * _color.G + 0.114 * _color.B;

            return luminance > 145 
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);
        }
    }
}