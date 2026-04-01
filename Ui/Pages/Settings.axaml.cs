using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Vice.Ui.Controls;
using Vice.Ui.Utils;

namespace Vice.Ui.Pages;

public partial class SettingsPage : UserControl, INotifyPropertyChanged
{
    private InvokeRequest? _invokeRequest;
    public SettingsClass? _settings { get; set; }
    private event EventHandler<SettingsClass>? ReloadWindowSettings;

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propname = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    
    public ObservableCollection<string> OutputDeviceList { get; set; } = new();
    
    public string? version { get; set; }

    public SettingsPage()
    {
        InitializeComponent();
    }

    public async void Load(EventHandler<SettingsClass> func, SettingsClass settings, InvokeRequest invokeRequest)
    {
        _settings = settings;
        ReloadWindowSettings = func;
        _invokeRequest = invokeRequest;

        MakeOutputList();
        Version();

        DataContext = this;
    }

    private async void MakeOutputList()
    {
        OutputDeviceList.Clear();
        
        try
        {
            var result = await _invokeRequest!.SendRequestAsync("get_outputs");
            var parsed = JsonSerializer.Deserialize(result, JsonContext.Default.ListString);
            
            foreach (var item in parsed!)
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
            var v = await _invokeRequest!.SendRequestAsync("get_version");
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
            await _invokeRequest!.SendRequestAsync("save_settings", _settings, false);
            ReloadWindowSettings!.Invoke(null, _settings!);
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
            _settings!.output = option;
            OutputDevice.Content = option;
        }
    }

    private async void OpenGithub(object sender, RoutedEventArgs e)
    {
        await _invokeRequest!.SendRequestAsync(
            "open_link",
            new Dictionary<string, object> { { "url", "https://github.com/GlowyDeveloper/Vice" } },
            false
        );
    }

    private async void Open(object sender, RoutedEventArgs e)
    {
        try
        {
            var res = await _invokeRequest!.SendRequestAsync("get_settings_folder");
            var trimmed = res.Trim().Trim('"');
            Process.Start(new ProcessStartInfo
            {
                FileName = trimmed,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Folder opening error: {ex}");
            throw;
        }
    }

    private async void Update(object sender, RoutedEventArgs e)
    {
        try
        {
            var res = await _invokeRequest!.SendRequestAsync("update");
            var parsed = JsonSerializer.Deserialize(res, JsonContext.Default.DictionaryStringObject);
            if (int.TryParse(parsed!["code"].ToString()!.Trim().Trim('"'), out int code))
            {
                var mainWindow = (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                
                if (code == 0)
                {
                    var newVersion = parsed!["version"].ToString()!.Trim().Trim('"');
                    var changelogWindow = new MessageBoxYesNo() {Text = $"Are you sure you want to update from {version} to {newVersion}?" };

                    changelogWindow.YesClicked += async (o, args) =>
                    {
                        await _invokeRequest!.SendRequestAsync("confirm_update", null, false);
                    };
                    
                    await changelogWindow.ShowDialog(mainWindow!);
                }
                else if (code == 1)
                {
                    var changelogWindow = new MessageBoxOk() {Text = "Check your internet connection and try again later."};
                    await changelogWindow.ShowDialog(mainWindow!);
                }
                else if (code == 2)
                {
                    var changelogWindow = new MessageBoxOk() {Text = "It appears there is no update available."};
                    await changelogWindow.ShowDialog(mainWindow!);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Updating error: {ex}");
        }
    }

    private async void Uninstall(object sender, RoutedEventArgs e)
    {
        try
        {
            var mainWindow = (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var changelogWindow = new MessageBoxYesNo() {Text = "Are you sure you want to uninstall?" };

            changelogWindow.YesClicked += async (o, args) =>
            {
                await _invokeRequest!.SendRequestAsync("uninstall", null, false);
            };

            await changelogWindow.ShowDialog(mainWindow!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Uninstalling error: {ex}");
        }
    }
}