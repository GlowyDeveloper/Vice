using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Newtonsoft.Json;
using Vice.Ui.Utils;

namespace Vice.Ui.Pages;

public partial class SettingsPage : UserControl, INotifyPropertyChanged
{
    private InvokeRequest _invokeRequest;
    public SettingsClass _settings { get; set; }
    private event EventHandler<SettingsClass>? ReloadWindowSettings;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    public ObservableCollection<string> OutputDeviceList { get; set; } = new();
    
    public string version { get; set; }
    
    public SettingsPage(EventHandler<SettingsClass> func, SettingsClass settings, InvokeRequest invokeRequest)
    {
        _settings = settings;
        ReloadWindowSettings = func;
        _invokeRequest = invokeRequest;

        MakeOutputList();
        Version();
        
        InitializeComponent();
        DataContext = this;
    }

    private async void MakeOutputList()
    {
        OutputDeviceList.Clear();
        
        try
        {
            var result = await _invokeRequest.SendRequestAsync("get_outputs");
            var parsed = JsonConvert.DeserializeObject<List<string>>(result);
            
            foreach (var item in parsed)
            {
                OutputDeviceList.Add(item);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Output parsing error: {ex}");
            throw;
        }
            
        OnPropertyChanged(nameof(OutputDeviceList));
    }
    
    private async void Version()
    {
        try
        {
            var v = await _invokeRequest.SendRequestAsync("get_version");
            version = v.Replace("\"", "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Version parsing error: {ex}");
            throw;
        }
            
        OnPropertyChanged(nameof(version));
    }

    private async void Save(object sender, RoutedEventArgs e)
    {
        try
        {
            await _invokeRequest.SendRequestAsync("save_settings", _settings, false);
            ReloadWindowSettings.Invoke(null, _settings);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Settings parsing error: {ex}");
        }
    }

    private void OutputDeviceSelection(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem btn && btn.DataContext is string option)
        {
            _settings.output = option;
            OutputDevice.Content = option;
        }
    }

    private async void OpenGithub(object sender, RoutedEventArgs e)
    {
        await _invokeRequest.SendRequestAsync(
            "open_link",
            new Dictionary<string, object> { { "url", "https://github.com/GlowyDeveloper/Vice" } },
            false
        );
    }
}