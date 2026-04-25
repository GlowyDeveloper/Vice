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
    public ChannelsClass() {}

    public string name { get; set; } = "Unknown";
    public string icon { get; set; } = "Unknown";
    public List<byte> color { get; set; } = new List<byte>();
    public string device { get; set; } = "Unknown";
    public DeviceOrApp deviceOrApp { get; set; } = DeviceOrApp.Device;
    public bool lowlatency { get; set; } = false;
    public double volume { get; set; } = 1.0;
    public EffectsClass effects { get; set; } = new EffectsClass();
    
    public ChannelsClass(string Nname, string Nicon, List<byte> Ncolor, string Ndevice, DeviceOrApp NdeviceOrApp, bool Nlowlatency, double Nvolume, EffectsClass Neffects)
    {
        name = Nname;
        icon = Nicon;
        color = Ncolor;
        device = Ndevice;
        deviceOrApp = NdeviceOrApp;
        lowlatency = Nlowlatency;
        volume = Nvolume;
        effects = Neffects;
    }
}

public class SFXClass
{
    public SFXClass() {}

    public string name { get; set; } = "Unknown";
    public string icon { get; set; } = "Unknown";
    public List<byte> color { get; set; } = new List<byte>();
    public bool lowlatency { get; set; } = false;
    public List<string> keys { get; set; } = new List<string>();
    public string sound { get; set; } = "Unknown";
    public EffectsClass effects { get; set; } = new EffectsClass();
    
    public SFXClass(string Nname, string Nicon, List<byte> Ncolor, bool Nlowlatency, List<string> Nkeys, string Nsound, EffectsClass Neffects)
    {
        name = Nname;
        icon = Nicon;
        color = Ncolor;
        lowlatency = Nlowlatency;
        keys = Nkeys;
        sound = Nsound;
        effects = Neffects;
    }
}

public class SettingsClass
{
    public SettingsClass() {}
    
    public string output { get; set; } = "Please wait.";
    public double scale { get; set; } = 1.0;
    public string version { get; set; } = "Please wait.";
    public bool light { get; set; } = false;
    public bool monitor { get; set; } = false;
    public bool peaks { get; set; } = true;
    public bool startup { get; set; } = false;
    public bool tray { get; set; } = true;
}

public class RequestPayload
{
    public RequestPayload() {}
    
    public string cmd { get; set; } = "Unknown";
    public Dictionary<string, object?>? args { get; set; } = null;
    public bool respond { get; set; } = true;
}

public struct EffectsClass
{
    public EffectsClass() {}

    public List<NodeClass> nodes { get; set; } = new List<NodeClass>();
    public List<ConnectionClass> connections { get; set; } = new List<ConnectionClass>();
}

public class ConnectionClass
{
    public ConnectionClass() {}
    
    public string from_node_id { get; set; } = string.Empty;
    public string from_port_id { get; set; } = string.Empty;
    public string to_node_id { get; set; } = string.Empty;
    public string to_port_id { get; set; } = string.Empty;
}

public class NodeClass
{
    public NodeClass() {}
    
    public int x { get; set; } = 0;
    public int y { get; set; } = 0;
    public string type_of { get; set; } = string.Empty;
    public string id { get; set; } = string.Empty;
    public List<string> inputs { get; set; } = new List<string>();
    public List<string> outputs { get; set; } = new List<string>();
    public List<string> options { get; set; } = new List<string>();
}

public enum NodeType
{
    In,
    Out,
    
    Split,
    Merge,
    
    Compression,
    Delay,
    Distortion,
    Gain,
    Gating,
    Reverb,
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
[JsonSerializable(typeof(NodeClass))]
[JsonSerializable(typeof(List<NodeClass>))]
[JsonSerializable(typeof(ConnectionClass))]
[JsonSerializable(typeof(List<ConnectionClass>))]
internal partial class JsonContext : JsonSerializerContext
{
}