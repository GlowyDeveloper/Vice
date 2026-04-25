use std::path::Path;
use std::{net::TcpStream, time::Duration, fs};
use serde::Deserialize;

use crate::files::{self, Channel, DeviceOrApp, Effects, Settings, SoundboardSFX};
use crate::audio::{self};
use crate::error;

static SFX_EXTENTIONS: [&str; 6] = ["wav", "mp3", "wma", "aac", "m4a", "flac"];

pub(crate) fn new_channel(color: [u8; 3], icon: String, name: String, deviceapps: String, device: DeviceOrApp, low: bool, effects: Effects) -> Result<(), String> {
    let channel: Channel = Channel{name, icon, color, device: deviceapps, deviceorapp: device, lowlatency: low, volume: 1.0, effects};
    let mut channels: Vec<Channel> = files::get_channels();

    channels.push(channel);
    return files::save_channels(channels).map(|_| audio::restart());
}

pub(crate) fn new_sound(color: [u8; 3], icon: String, name: String, sound: String, low: bool, keys: Vec<String>, effects: Effects) -> Result<(), String> {
    let mut sfxs: Vec<SoundboardSFX> = files::get_soundboard();
    
    let ext = Path::new(&sound)
        .extension();

    match ext {
        None => {
            error!("Failed to get extension for soundeffect \"{}\"", name);
            return Err(format!("Failed to get extension for soundeffect \"{}\"", name));
        }
        Some(o) => {
            if let Err(e) = fs::copy(&sound, files::sfx_base().join(format!("{}.{}", name, o.to_str().unwrap_or("wav")).to_string())) {
                error!("Failed to copy soundeffect for soundeffect \"{}\": {:#?}", name, e);
                return Err(format!("Failed to copy soundeffect for soundeffect \"{}\"", name));
            }
        }
    }

    let sfx: SoundboardSFX = SoundboardSFX{name, icon, color, lowlatency: low, keys, effects};

    sfxs.push(sfx);
    return files::save_soundboard(sfxs);
}

pub(crate) fn edit_channel(color: [u8; 3], icon: String, name: String, deviceapps: String, device: DeviceOrApp, oldname: String, low: bool, effects: Effects) -> Result<(), String> { //
    let mut channels: Vec<Channel> = files::get_channels();

    if let Some(pos) = channels.iter().position(|c: &Channel| c.name == oldname) {
        let volume  = channels[pos].volume;
        channels[pos] = Channel{name, icon, color, device: deviceapps, deviceorapp: device, lowlatency: low, volume, effects};
    } else {
        error!("Channel \"{}\" not found", name);
        return Err(format!("Channel \"{}\" not found", oldname));
    }
    
    return files::save_channels(channels).map(|_| audio::restart());
}

pub(crate) fn edit_soundboard(color: [u8; 3], icon: String, name: String, oldname: String, low: bool, keys: Vec<String>, effects: Effects) -> Result<(), String> { //
    let mut sfxs: Vec<SoundboardSFX> = files::get_soundboard();

    if let Some(pos) = sfxs.iter().position(|c: &SoundboardSFX| c.name == oldname) {
        sfxs[pos] = SoundboardSFX{name, icon, color, lowlatency: low, keys, effects};
    } else {
        error!("Soundeffect \"{}\" not found", name);
        return Err(format!("Soundeffect \"{}\" not found", oldname));
    }
    
    return files::save_soundboard(sfxs);
}

pub(crate) fn delete_channel(name: String) -> Result<(), String> {
    let mut channels: Vec<Channel> = files::get_channels();

    if let Some(pos) = channels.iter().position(|c| c.name == name) {
        channels.remove(pos);
    } else {
        error!("Channel \"{}\" not found", name);
        return Err(format!("Channel \"{}\" not found", name));
    }
    
    return files::save_channels(channels).map(|_| audio::restart());
}

pub(crate) fn delete_sound(name: String) -> Result<(), String> {
    let mut sfxs: Vec<SoundboardSFX> = files::get_soundboard();

    if let Some(pos) = sfxs.iter().position(|c: &SoundboardSFX| c.name == name) {
        sfxs.remove(pos);
    } else {
        error!("Soundeffect \"{}\" not found", name);
        return Err(format!("Soundeffect \"{}\" not found", name));
    }

    let mut deleted = false;
    for ext in SFX_EXTENTIONS {
        let filename = format!("{}.{}", files::sfx_base().join(&name).to_str().unwrap_or(&name), ext);
        if fs::metadata(&filename).is_ok() {
            fs::remove_file(&filename)
                .map_err(|e| format!("Failed to delete file: {}", e))?;
            deleted = true;
            break;
        }
    }

    if !deleted {
        error!("No file found for base name \"{}\"", name);
    }
    
    return files::save_soundboard(sfxs);
}

pub(crate) fn get_devices() -> Vec<String> {
    audio::inputs()
}

pub(crate) fn get_apps() -> Vec<String> {
    audio::apps()
}

pub(crate) fn save_settings(output: String, scale: f32, light: bool, monitor: bool, peaks: bool, startup: bool, tray: bool) -> Result<(), String> {
    let mut settings: Settings = files::get_settings();
    settings.output = output;
    settings.scale = scale;
    settings.light = light;
    settings.monitor = monitor;
    settings.peaks = peaks;
    settings.startup = startup;
    settings.tray = tray;

    files::save_settings(settings).map(|_| {audio::restart(); files::manage_startup()})
}

pub(crate) fn get_settings() -> Settings {
    files::get_settings()
}

pub(crate) fn set_volume(name: String, volume: f32) {
    let mut channels: Vec<Channel> = files::get_channels();

    if let Some(pos) = channels.iter().position(|c: &Channel| c.name == name) {
        let mut channel: Channel = channels[pos].clone();
        channel.volume = volume;
        channels[pos] = channel.clone();

        audio::set_volume(channel.name, volume);
    } else {
        error!("Channel \"{}\" not found", name);
        return;
    }

    files::save_channels(channels).unwrap_or_else(|e| error!("Error saving channels: {}", e));
}

pub(crate) fn get_outputs() -> Vec<String> {
    audio::outputs()
}

pub(crate) fn play_sound(name: String, low: bool) {
    let mut path: String = "".to_owned();
    for ext in SFX_EXTENTIONS {
        let filename = format!("{}.{}", files::sfx_base().join(&name).to_str().unwrap_or(&name), ext);
        if fs::metadata(&filename).is_ok() {
            path = filename;
            break;
        }
    }

    if path == "" {
        error!("Failed to get soundeffect file for soundeffect \"{}\"", name);
        return;
    }

    audio::play_sfx(&path, low, name);
}

pub(crate) fn get_volume(name: String) -> String {
    audio::get_volume_parsed(name)
}

pub(crate) fn uninstall() -> Result<String, String> {
    let mut debug = "false";
    let args: Vec<String> = std::env::args().collect();
    if args.contains(&"--debug".to_string()) {
        debug = "true";
    }

    return files::extract_updater("uninstall", std::env::current_exe().unwrap(), debug);
}

pub(crate) fn update() -> Result<(i32, String), String> {
    #[derive(Deserialize)]
    struct Release {
        name: String
    }

    let connected = TcpStream::connect_timeout(
        &("1.1.1.1:80".parse().unwrap()),
        Duration::from_secs(2)
    ).is_ok();

    if connected == false {
        return Ok((1, "".to_string()));
    }

    let url = "https://api.github.com/repos/GlowyDeveloper/Vice/releases/latest";

    let client = reqwest::blocking::Client::new();
    let mut res = client
        .get(url)
        .header("User-Agent", "Vice-app")
        .send()
        .map_err(|e| e.to_string())?
        .json::<Release>()
        .map_err(|e| e.to_string())?;

    res.name.remove(0);

    if res.name == env!("CARGO_PKG_VERSION") {
        return Ok((2, "".to_string()));
    }

    Ok((0, res.name))
}

pub(crate) fn confirm_update() -> Result<String, String> {
    let mut debug = "false";
    let args: Vec<String> = std::env::args().collect();
    if args.contains(&"--debug".to_string()) {
        debug = "true";
    }

    return files::extract_updater("update", std::env::current_exe().unwrap(), debug)
}