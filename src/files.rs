use std::{
    env, fs::{self, OpenOptions}, io::Write,
    path::PathBuf, process::Command
};
use chrono::Local;
use serde::{Deserialize, Serialize};
use serde_json::Value;
use windows::{
    core::{PCWSTR, Interface},
    Win32::{
        UI::Shell::{IShellLinkW, ShellLink, FOLDERID_Startup, SHGetKnownFolderPath},
        System::Com::{IPersistFile, CoInitializeEx, CoCreateInstance, CoUninitialize, CLSCTX_INPROC_SERVER, COINIT_APARTMENTTHREADED},
    },
};

use crate::{error, log, warn};

#[derive(Debug, Deserialize, Serialize, PartialEq, Clone, Default)]
pub(crate) enum EffectsType {
    #[default]
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

#[derive(Debug, Deserialize, Serialize, PartialEq, Clone, Default)]
pub(crate) struct EffectNode {
    pub(crate) x: u32,
    pub(crate) y: u32,
    pub(crate) type_of: EffectsType,
    pub(crate) id: String,
    pub(crate) inputs: Vec<String>,
    pub(crate) outputs: Vec<String>,
    pub(crate) options: Vec<String>
}

#[derive(Debug, Deserialize, Serialize, PartialEq, Default, Clone)]
pub(crate) struct Effects {
    nodes: Vec<EffectNode>,
    connections: Vec<EffectsConnection>
}

#[derive(Debug, Deserialize, Serialize, PartialEq, Default, Clone)]
pub(crate) struct EffectsConnection {
    from_node_id: String,
    from_port_id: String,
    to_node_id: String,
    to_port_id: String
}

#[derive(Debug, Deserialize, Serialize, PartialEq, Default)]
pub(crate) struct SoundboardSFX {
    pub(crate) name: String,
    pub(crate) icon: String,
    pub(crate) color: [u8; 3],
    pub(crate) lowlatency: bool,
    pub(crate) keys: Vec<String>,
    pub(crate) effects: Effects,
}

#[derive(Debug, Deserialize, Serialize, PartialEq, Clone, Default)]
pub(crate) enum DeviceOrApp {
    #[default]
    Device,
    App
}

#[derive(Debug, Deserialize, Serialize, PartialEq, Default, Clone)]
pub(crate) struct Channel {
    pub(crate) name: String,
    pub(crate) icon: String,
    pub(crate) color: [u8; 3],
    pub(crate) device: String,
    pub(crate) deviceorapp: DeviceOrApp,
    pub(crate) lowlatency: bool,
    pub(crate) volume: f32,
    pub(crate) effects: Effects,
}

#[derive(Deserialize, Serialize, PartialEq, Clone)]
pub(crate) struct Settings {
    pub(crate) output: String,
    pub(crate) scale: f32,
    pub(crate) light: bool,
    pub(crate) monitor: bool,
    pub(crate) peaks: bool,
    pub(crate) startup: bool,
    pub(crate) tray: bool
}

#[derive(Deserialize, Serialize)]
pub(crate) struct File {
    pub(crate) soundboard: Vec<SoundboardSFX>,
    pub(crate) channels: Vec<Channel>,
    pub(crate) settings: Settings,
}

impl Default for File {
    fn default() -> Self {
        File { soundboard: vec![], channels: vec![], settings: Settings::default() }
    }
}

impl Default for Settings {
    fn default() -> Self {
        Settings { output: "".to_string(), scale: 1.0, light: false, monitor: true, peaks: true, startup: false, tray: true }
    }
}

impl DeviceOrApp {
    pub(crate) fn from_string(s: &str) -> Self {
        match s {
            "Device" => DeviceOrApp::Device,
            "App" => DeviceOrApp::App,
            _ => DeviceOrApp::Device, 
        }
    }
}

impl EffectsType {
    pub(crate) fn from_string(s: &str) -> Self {
        match s {
            "In" => EffectsType::In,
            "Out" => EffectsType::Out,
            "Split" => EffectsType::Split,
            "Merge" => EffectsType::Merge,
            "Compression" => EffectsType::Compression,
            "Delay" => EffectsType::Delay,
            "Distortion" => EffectsType::Distortion,
            "Gain" => EffectsType::Gain,
            "Gating" => EffectsType::Gating,
            "Reverb" => EffectsType::Reverb,
            _ => EffectsType::In, 
        }
    }
}

const EMBEDDED_UPDATER: &[u8] = include_bytes!("../updater/target/release/updater.exe");

pub(crate) fn app_base() -> PathBuf {
    let base = env::var("APPDATA");

    match base {
        Ok(s) => return PathBuf::from(s).join("Vice"),
        Err(e) => {
            error!("Error occured when getting App Base: {:#?}", e);
            return env::temp_dir().join("Vice");
        }
    }
}

pub(crate) fn sfx_base() -> PathBuf {
    app_base().join("SFXs")
}

pub(crate) fn crash_log_base() -> PathBuf {
    app_base().join("CrashLogs")
}

fn settings_json() -> PathBuf {
    app_base().join("settings.json")
}

pub(crate) fn create_files() {
    let base: PathBuf = app_base();

    if !base.exists() {
        if let Err(e) = fs::create_dir_all(base) {
            error!("Failed to create app base: {}", e);
        }
    }

    let file_path: PathBuf = settings_json();

    if !file_path.exists() {
        let mut default_file: File = File::default();
        default_file.settings.scale = 1.0;

        match serde_json::to_string_pretty(&default_file) {
            Ok(json) => {
                if let Err(e) = fs::write(&file_path, json) {
                    error!("Failed to create settings: {}", e);
                }
            }
            Err(e) => {
                error!("Failed to serialize default settings: {}", e);
            }
        }
    }

    let sfxs_path: PathBuf = sfx_base();

    if !sfxs_path.exists() {
        if let Err(e) = fs::create_dir_all(sfxs_path) {
            error!("Failed to create soundeffect directory: {}", e);
        }
    }

    let crash_log_path: PathBuf = crash_log_base();

    if !crash_log_path.exists() {
        if let Err(e) = fs::create_dir_all(crash_log_path) {
            error!("Failed to create crash log directory: {}", e);
        }
    }

    manage_startup();
}

pub(crate) fn fix_settings(broken: Value) -> Settings {
    let mut settings: Settings = Settings::default();

    if let Some(output) = broken.get("output").and_then(|v| v.as_str()) {
        settings.output = output.to_string();
    }

    if let Some(scale) = broken.get("scale").and_then(|v| v.as_f64()) {
        if scale.is_finite() && scale >= 0.1 && scale <= 2.0 {
            settings.scale = scale as f32;
        } else {
            settings.scale = 1.0;
        }
    }

    if let Some(light) = broken.get("light").and_then(|v| v.as_bool()) {
        settings.light = light;
    }

    if let Some(monitor) = broken.get("monitor").and_then(|v| v.as_bool()) {
        settings.monitor = monitor;
    }

    if let Some(peaks) = broken.get("peaks").and_then(|v| v.as_bool()) {
        settings.peaks = peaks;
    }

    if let Some(startup) = broken.get("startup").and_then(|v| v.as_bool()) {
        settings.startup = startup;
    }

    if let Some(tray) = broken.get("tray").and_then(|v| v.as_bool()) {
        settings.tray = tray;
    }

    settings
}

pub(crate) fn fix_soundeffect(broken: Value) -> SoundboardSFX {
    let mut sfx: SoundboardSFX = SoundboardSFX::default();

    if let Some(name) = broken.get("name").and_then(|v| v.as_str()) {
        sfx.name = name.to_string();
    }

    if let Some(icon) = broken.get("icon").and_then(|v| v.as_str()) {
        sfx.icon = icon.to_string();
    }

    if let Some(broken_color) = broken.get("color").and_then(|v| v.as_array()) {
        let mut color: Vec<u8> = vec![];
        for v in broken_color {
            let int: i64 = v.as_i64().map_or(-1, |i: i64| i);
            if int > 255 {
                color.push(255 as u8);
            } else if int < 0 {
                color.push(0 as u8);
            } else {
                color.push(int as u8);
            }
        }

        if color.len() > 3 {
            color.truncate(3);
        } else if color.len() < 3 {
            color.resize(3, 0); 
        }

        let mut color_array: [u8; 3] = [0, 0, 0];
        for (i, &val) in color.iter().enumerate() {
            color_array[i] = val;
        }

        sfx.color = color_array;
    }

    if let Some(lowlatency) = broken.get("lowlatency").and_then(|v| v.as_bool()) {
        sfx.lowlatency = lowlatency;
    }

    if let Some(broken_keys) = broken.get("keys").and_then(|v| v.as_array()) {
        let mut keys: Vec<String> = vec![];
        for v in broken_keys {
            let str: String = v.as_str().unwrap_or_default().into();
            keys.push(str);
        }

        sfx.keys = keys;
    }

    if let Some(effects) = broken.get("effects") {
        sfx.effects = fix_effects(effects.clone());
    }

    sfx
}

pub(crate) fn fix_channel(broken: Value) -> Channel {
    let mut channel: Channel = Channel::default();

    if let Some(name) = broken.get("name").and_then(|v| v.as_str()) {
        channel.name = name.to_string();
    }

    if let Some(icon) = broken.get("icon").and_then(|v| v.as_str()) {
        channel.icon = icon.to_string();
    }

    if let Some(broken_color) = broken.get("color").and_then(|v| v.as_array()) {
        let mut color: Vec<u8> = vec![];
        for v in broken_color {
            let int: i64 = v.as_i64().map_or(-1, |i: i64| i);
            if int > 255 {
                color.push(255 as u8);
            } else if int < 0 {
                color.push(0 as u8);
            } else {
                color.push(int as u8);
            }
        }

        if color.len() > 3 {
            color.truncate(3);
        } else if color.len() < 3 {
            color.resize(3, 0); 
        }

        let mut color_array: [u8; 3] = [0, 0, 0];
        for (i, &val) in color.iter().enumerate() {
            color_array[i] = val;
        }

        channel.color = color_array;
    }

    if let Some(device) = broken.get("device").and_then(|v| v.as_str()) {
        channel.device = device.to_string();
    }

    if let Some(deviceorapp) = broken.get("deviceorapp").and_then(|v| v.as_bool()) {
        channel.deviceorapp = match deviceorapp {
            true => DeviceOrApp::Device,
            false => DeviceOrApp::App
        };
    }

    if let Some(lowlatency) = broken.get("lowlatency").and_then(|v| v.as_bool()) {
        channel.lowlatency = lowlatency;
    }

    if let Some(volume) = broken.get("volume").and_then(|v| v.as_f64()) {
        channel.volume = volume as f32;
    }

    if let Some(effects) = broken.get("effects") {
        channel.effects = fix_effects(effects.clone());
    }

    channel
}

pub(crate) fn fix_effects(broken: Value) -> Effects {
    let mut effects: Effects = Effects::default();

    if let Some(nodes_val) = broken.get("nodes").and_then(|v| v.as_array()) {
        let mut nodes: Vec<EffectNode> = vec![];
        for broken_node in nodes_val.iter() {
            let mut node: EffectNode = EffectNode::default();

            if let Some(x) = broken_node.get("x").and_then(|v| v.as_u64()) {
                node.x = x as u32;
            }

            if let Some(y) = broken_node.get("y").and_then(|v| v.as_u64()) {
                node.y = y as u32;
            }

            if let Some(type_val) = broken_node.get("type_of").and_then(|v| v.as_str()) {
                node.type_of = EffectsType::from_string(type_val);
            }

            if let Some(id) = broken_node.get("id").and_then(|v| v.as_str()) {
                node.id = id.to_string();
            }

            if let Some(input_val) = broken_node.get("inputs").and_then(|v| v.as_array()) {
                for input in input_val.iter() {
                    if let Some(str) = input.as_str() {
                        node.inputs.push(str.to_string());
                    }
                }
            }

            if let Some(outputs_val) = broken_node.get("outputs").and_then(|v| v.as_array()) {
                for output in outputs_val.iter() {
                    if let Some(str) = output.as_str() {
                        node.outputs.push(str.to_string());
                    }
                }
            }

            if let Some(option_val) = broken_node.get("options").and_then(|v| v.as_array()) {
                for options in option_val.iter() {
                    if let Some(str) = options.as_str() {
                        node.options.push(str.to_string());
                    }
                }
            }

            nodes.push(node);
        }

        effects.nodes = nodes;
    }

    if let Some(connections_val) = broken.get("connections").and_then(|v| v.as_array()) {
        let mut connections: Vec<EffectsConnection> = vec![];
        for broken_connection in connections_val.iter() {
            let mut connection: EffectsConnection = EffectsConnection::default();

            if let Some(from_node_id) = broken_connection.get("from_node_id").and_then(|v| v.as_str()) {
                connection.from_node_id = from_node_id.to_string();
            }

            if let Some(from_port_id) = broken_connection.get("from_port_id").and_then(|v| v.as_str()) {
                connection.from_port_id = from_port_id.to_string();
            }

            if let Some(to_node_id) = broken_connection.get("to_node_id").and_then(|v| v.as_str()) {
                connection.to_node_id = to_node_id.to_string();
            }

            if let Some(to_port_id) = broken_connection.get("to_port_id").and_then(|v| v.as_str()) {
                connection.to_port_id = to_port_id.to_string();
            }

            connections.push(connection);
        }

        effects.connections = connections;
    }

    effects
}

pub(crate) fn fix_file(broken: Value) -> File {
    let mut file: File = File::default();

    if let Some(settings_val) = broken.get("settings") {
        file.settings = fix_settings(settings_val.clone());
    }

    if let Some(sfxs) = broken.get("soundboard").and_then(|v| v.as_array()) {
        let mut soundeffects: Vec<SoundboardSFX> = vec![];
        for sfx in sfxs.iter() {
            soundeffects.push(fix_soundeffect(sfx.clone()));
        }
        file.soundboard = soundeffects;
    }

    if let Some(channels) = broken.get("channels").and_then(|v| v.as_array()) {
        let mut cs: Vec<Channel> = vec![];
        for channel in channels.iter() {
            cs.push(fix_channel(channel.clone()));
        }
        file.channels = cs;
    }

    file
}

pub(crate) fn get_file() -> File {
    let path: PathBuf = settings_json();

    if path.exists() {
        let data = match fs::read_to_string(path) {
            Ok(d) => d,
            Err(e) => {
                error!("Failed to read settings file: {}", e);
                return File::default();
            }
        };

        if data.is_empty() {
            return File::default();
        }

        match serde_json::from_str::<File>(&data) {
            Ok(file) => return file,
            Err(e) => {
                warn!("Failed to parse settings file: {}. Fixing...", e);

                match serde_json::from_str::<Value>(&data) {
                    Ok(value) => {
                        let fixed: File = fix_file(value);

                        log!("Successfully fixed!");

                        let _ = save_file(&fixed);
                        return fixed;
                    }
                    Err(e) => {
                        let fixed: File = File::default();

                        error!("Failed to fix the settings file: {}", e);

                        let _ = save_file(&fixed);
                        return fixed;
                    }
                }
            }
        }
    }

    File::default()
}

pub(crate) fn save_file(file: &File) -> Result<(), String> {
    let path: PathBuf = settings_json();

    let data: String = match serde_json::to_string_pretty(file) {
        Ok(d) => d,
        Err(e) => {
            error!("Failed to serialize settings: {}", e);
            return Err("Serialization".to_string());
        }
    };

    match fs::write(&path, data) {
        Ok(_) => Ok(()),
        Err(e) => {
            error!("Failed to write settings file: {}", e);
            return Err("Saving".to_string());
        }
    }
}

pub(crate) fn get_channels() -> Vec<Channel> {
    get_file().channels
}

pub(crate) fn save_channels(channels: Vec<Channel>) -> Result<(), String> {
    let mut file: File = get_file();
    file.channels = channels;
    return save_file(&file);
}

pub(crate) fn get_soundboard() -> Vec<SoundboardSFX> {
    get_file().soundboard
}

pub(crate) fn save_soundboard(soundboard_sfxs: Vec<SoundboardSFX>) -> Result<(), String> {
    let mut file: File = get_file();
    file.soundboard = soundboard_sfxs;
    return save_file(&file);
}

pub(crate) fn get_settings() -> Settings {
    get_file().settings
}

pub(crate) fn save_settings(settings: Settings) -> Result<(), String> {
    let mut file: File = get_file();
    file.settings = settings;
    return save_file(&file);
}

pub(crate) fn extract_updater(arg: &str, path: PathBuf, debug: &str) -> Result<String, String> {
    let mut temp_path = env::temp_dir();

    let now = Local::now();
    let formatted = now.format("%d-%m-%Y %H-%M-%S-%f").to_string();

    let filename = "Vice-Uninstaller-".to_string()+&formatted+".exe";

    temp_path.push(&filename);

    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&temp_path)
        .map_err(|e| format!("Failed to create updater file: {}", e))?;

    file.write_all(EMBEDDED_UPDATER)
        .map_err(|e| format!("Failed to write updater file: {}", e))?;

    Command::new("cmd")
        .args(["/C", "start", "", &temp_path.to_string_lossy().to_string(), arg, &path.to_string_lossy().to_string(), debug])
        .spawn()
        .map_err(|e| format!("Failed to run updater file: {}", e))?;

    Ok(filename)
}

pub(crate) fn manage_startup() {
    let lnk_path: PathBuf = unsafe {
        let path = SHGetKnownFolderPath(&FOLDERID_Startup, windows::Win32::UI::Shell::KNOWN_FOLDER_FLAG(0), None).unwrap();
        let folder_str = path.to_string().unwrap();
        PathBuf::from(folder_str).join("Vice.lnk")
    };

    if get_settings().startup == true {
        if lnk_path.exists() {
            return;
        }

        let app = std::env::current_exe().unwrap().to_string_lossy().to_string();

        unsafe {
            let _ = CoInitializeEx(Some(std::ptr::null_mut()), COINIT_APARTMENTTHREADED)
                .ok()
                .map_err(|e| format!("CoInitializeEx failed: {e}"));

            let shell = CoCreateInstance(&ShellLink, None, CLSCTX_INPROC_SERVER)
                .map_err(|e| format!("CoCreateInstance failed: {e}"));

            let shell_link: IShellLinkW = match shell {
                Ok(i) => {i},
                Err(e) => {
                    CoUninitialize();
                    error!("Failed to create startup shortcut: {}", e);
                    return;
                }
            };

            let app_w: Vec<u16> = app.encode_utf16().chain(Some(0)).collect();
            let _ = shell_link.SetPath(PCWSTR::from_raw(app_w.as_ptr()))
                .map_err(|e| format!("SetPath failed: {e}"));

            let args_w: Vec<u16> = "--background".encode_utf16().chain(Some(0)).collect();
            let _ = shell_link.SetArguments(PCWSTR::from_raw(args_w.as_ptr()))
                .map_err(|e| format!("SetArguments failed: {e}"));

            let persist = shell_link.cast().map_err(|e| format!("cast to IPersistFile failed: {e}"));

            let persist_file: IPersistFile = match persist {
                Ok(i) => {i},
                Err(e) => {
                    CoUninitialize();
                    error!("Failed to create startup shortcut: {}", e);
                    return;
                }
            };

            let path_w: Vec<u16> = lnk_path.to_string_lossy().to_string().encode_utf16().chain(Some(0)).collect();
            let save = persist_file.Save(PCWSTR::from_raw(path_w.as_ptr()), true)
                .map_err(|e| format!("Save failed: {e}"));

            match save {
                Ok(_) => {
                    log!("Successfully created startup shortcut");
                },
                Err(e) => {
                    error!("Failed to create startup shortcut: {}", e);
                }
            }

            CoUninitialize();
        }
    } else {
        if !lnk_path.exists() {
            return;
        }

        match fs::remove_file(&lnk_path) {
            Ok(_) => {}
            Err(e) => {
                error!("Failed to delete startup shortcut: {}", e);
            }
        }
    }
}