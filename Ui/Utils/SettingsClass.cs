using System.Collections.Generic;

namespace Vice.Ui.Utils;

public enum DeviceOrApp
{
    Device,
    App
}

public class ChannelsClass {
    public string name { get; set; }
    public string icon { get; set; }
    public List<byte> color { get; set; }
    public string device { get; set; }
    public DeviceOrApp deviceOrApp { get; set; }
    public bool lowlatency { get; set; }
    public double volume { get; set; }
    
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

class SFXClass {
    public string name;
    public string icon;
    public List<byte> color;
    public bool lowlatency;
    public List<string> keys;
    
    public SFXClass(string Nname, string Nicon, List<byte> Ncolor, bool Nlowlatency, List<string> Nkeys)
    {
        name = Nname;
        icon = Nicon;
        color = Ncolor;
        lowlatency = Nlowlatency;
        keys = Nkeys;
    }
}

public class SettingsClass
{
    public string outputDevice;
    public double scale = 2147483647.0;
    public string version = "Please wait";
    public bool lightMode = false;
    public bool monitor = false;
    public bool peaks = true;
    public bool startup = false;
    public bool stayInTray = true;

    public SettingsClass(string NoutputDevice, double Nscale, string Nversion, bool Nstartup, bool NstayInTray, bool Npeaks, bool Nmonitor, bool NlightMode)
    {
        outputDevice = NoutputDevice;
        scale = Nscale;
        version = Nversion;
        startup = Nstartup;
        stayInTray = NstayInTray;
        peaks = Npeaks;
        monitor = Nmonitor;
        lightMode = NlightMode;
    }
}