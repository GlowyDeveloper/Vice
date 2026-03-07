using System;
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

public class SFXClass {
    public string name { get; set; }
    public string icon { get; set; }
    public List<byte> color { get; set; }
    public bool lowlatency { get; set; }
    public List<string> keys { get; set; }
    public string sound { get; set; }
    
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
    public string output { get; set; }
    public double scale { get; set; }
    public string version { get; set; }
    public bool light { get; set; }
    public bool monitor { get; set; }
    public bool peaks { get; set; }
    public bool startup { get; set; }
    public bool tray { get; set; }

    public SettingsClass(string NoutputDevice, double Nscale, string Nversion, bool Nstartup, bool NstayInTray, bool Npeaks, bool Nmonitor, bool NlightMode)
    {
        output = NoutputDevice;
        scale = Nscale;
        version = Nversion;
        startup = Nstartup;
        tray = NstayInTray;
        peaks = Npeaks;
        monitor = Nmonitor;
        light = NlightMode;
    }
}