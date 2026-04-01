using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Vice.Ui.Utils;

public enum DeviceOrApp
{
    Device,
    App
}

public class ChannelsClass
{
    public ChannelsClass() { }

    public string name { get; set; } = "Unknown";
    public string icon { get; set; } = "Unknown";
    public List<byte> color { get; set; } = new List<byte>();
    public string device { get; set; } = "Unknown";
    public DeviceOrApp deviceOrApp { get; set; } = DeviceOrApp.Device;
    public bool lowlatency { get; set; } = false;
    public double volume { get; set; } = 1.0;
    
    public ChannelsClass(string Nname, string Nicon, List<byte> Ncolor, string Ndevice, DeviceOrApp NdeviceOrApp, bool Nlowlatency, double Nvolume)
    {
        name = Nname;
        icon = Nicon;
        color = Ncolor;
        device = Ndevice;
        deviceOrApp = NdeviceOrApp;
        lowlatency = Nlowlatency;
        volume = Nvolume;
    }
}

public class SFXClass
{
    public SFXClass() { }

    public string name { get; set; } = "Unknown";
    public string icon { get; set; } = "Unknown";
    public List<byte> color { get; set; } = new List<byte>();
    public bool lowlatency { get; set; } = false;
    public List<string> keys { get; set; } = new List<string>();
    public string sound { get; set; } = "Unknown";
    
    public SFXClass(string Nname, string Nicon, List<byte> Ncolor, bool Nlowlatency, List<string> Nkeys, string Nsound)
    {
        name = Nname;
        icon = Nicon;
        color = Ncolor;
        lowlatency = Nlowlatency;
        keys = Nkeys;
        sound = Nsound;
    }
}

public class SettingsClass
{
    public string output { get; set; } = "Please wait.";
    public double scale { get; set; } = 1.0;
    public string version { get; set; } = "Please wait.";
    public bool light { get; set; } = false;
    public bool monitor { get; set; } = false;
    public bool peaks { get; set; } = true;
    public bool startup { get; set; } = false;
    public bool tray { get; set; } = true;

    public SettingsClass()
    {
    }
}

public class RequestPayload
{
    public string cmd { get; set; } = "Unknown";
    public Dictionary<string, object?>? args { get; set; } = null;
    public bool respond { get; set; } = true;

    public RequestPayload()
    {
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false)]
[JsonSerializable(typeof(SettingsClass))]
[JsonSerializable(typeof(SFXClass))]
[JsonSerializable(typeof(ChannelsClass))]
[JsonSerializable(typeof(List<SFXClass>))]
[JsonSerializable(typeof(List<ChannelsClass>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(RequestPayload))]
internal partial class JsonContext : JsonSerializerContext
{
}