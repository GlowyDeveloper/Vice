
use std::{
    collections::HashMap, io::{Cursor, Read as _, Write as _}, net::{TcpListener, TcpStream}, sync::{Arc, Mutex, atomic::{AtomicBool, Ordering}}
};
use global_hotkey::{GlobalHotKeyManager, hotkey::{Code, HotKey, Modifiers}};
use serde_json::{Value, json};
use tray_icon::{Icon, TrayIcon, TrayIconBuilder, menu::{Menu, MenuItem}};

use crate::{error, files::{self, DeviceOrApp}, funcs, log, warn};

#[link(name = "open_ui")]
extern "C" {
    fn hollow_process(payload: *const u8, changelog: bool) -> bool;
    fn run_in_job(payload: *const u8, payload_size: usize, changelog: bool);
}

const EMBEDDED_UI: &[u8] = include_bytes!("../../Ui/bin/Release/net10.0/win-x64/publish/Vice.Ui.exe");
const ICON: &[u8] = include_bytes!("../../icons/icon.ico");

static IS_OPEN: AtomicBool = AtomicBool::new(false);

struct Client {
    stream: TcpStream,
    buffer: Vec<u8>,
}

pub struct IpcServer {
    listener: TcpListener,
    clients: Vec<Client>,
    hotkeys: Arc<Mutex<Hotkeys>>
}

impl IpcServer {
    pub fn new(hotkeys: Arc<Mutex<Hotkeys>>) -> Self {
        let listener = TcpListener::bind("127.0.0.1:8423")
            .expect("Failed to bind TCP listener");

        listener
            .set_nonblocking(true)
            .expect("Failed to set nonblocking");

        log!("IPC initialized");

        Self {
            listener,
            clients: Vec::new(),
            hotkeys,
        }
    }

    pub fn create_in_thread(hotkeys: Arc<Mutex<Hotkeys>>) {
        std::thread::Builder::new()
            .name("Ipc Server".to_string())
            .spawn(move || {
                let mut server = IpcServer::new(hotkeys);
                loop {
                    server.poll();
                    std::thread::sleep(std::time::Duration::from_millis(50));
                }
            })
            .expect("Failed to create IPC thread");
    }

    pub fn poll(&mut self) {
        loop {
            match self.listener.accept() {
                Ok((stream, _addr)) => {
                    log!("Client connected");

                    stream
                        .set_nonblocking(true)
                        .expect("Failed to set client nonblocking");

                    self.clients.push(Client {
                        stream,
                        buffer: Vec::new(),
                    });
                }
                Err(ref e) if e.kind() == std::io::ErrorKind::WouldBlock => {
                    break;
                }
                Err(e) => {
                    error!("Failed to accept connection: {}", e);
                    break;
                }
            }
        }

        let mut i = 0;
        while i < self.clients.len() {
            let remove = Self::poll_client(&mut self.clients[i], &mut self.hotkeys.lock().unwrap()).is_err();

            if remove {
                self.clients.swap_remove(i);
                log!("Client removed");
            } else {
                i += 1;
            }
        }
    }

    fn poll_client(client: &mut Client, hotkeys: &mut Hotkeys) -> std::io::Result<()> {
        let mut temp = [0u8; 1024];

        match client.stream.read(&mut temp) {
            Ok(0) => {
                log!("Client disconnected");
                return Err(std::io::Error::new(
                    std::io::ErrorKind::ConnectionReset,
                    "closed",
                ));
            }
            Ok(n) => {
                client.buffer.extend_from_slice(&temp[..n]);
            }
            Err(ref e) if e.kind() == std::io::ErrorKind::WouldBlock => {
                return Ok(());
            }
            Err(e) => return Err(e),
        }

        while let Some(pos) = client.buffer.iter().position(|&b| b == b'\n') {
            let line: Vec<u8> = client.buffer.drain(..=pos).collect();

            let text = match std::str::from_utf8(&line) {
                Ok(t) => t.trim(),
                Err(_) => {
                    error!("Invalid UTF-8");
                    continue;
                }
            };

            if text.is_empty() {
                continue;
            }

            let value: serde_json::Value = match serde_json::from_str(text) {
                Ok(v) => v,
                Err(e) => {
                    error!("Invalid JSON: {}", e);
                    continue;
                }
            };

            if let Some(cmd) = value.get("cmd").and_then(|v| v.as_str()) {
                if let Some(args) = value.get("args") {
                    let response = handle_request(cmd, args.clone(), hotkeys);

                    if value.get("respond").and_then(|v| v.as_bool()) == Some(true) {
                        let formatted = format!("{}\n", response["result"]);

                        if let Err(e) = client.stream.write_all(formatted.as_bytes()) {
                            error!("Write failed: {}", e);
                            return Err(e);
                        }
                    }
                }
            }
        }

        Ok(())
    }
}

pub struct SystemTray {
    _icon: Icon,
    _tray_menu: Menu,
    pub(crate) quit: MenuItem,
    pub(crate) open_ui: MenuItem,
    pub(crate) restart: MenuItem,
    _tray: TrayIcon,
}

impl SystemTray {
    pub fn new() -> Self {
        let tray_icon = Self::create_icon().map(|(data, width, height)| Icon::from_rgba(data, width, height).unwrap())
            .unwrap_or(Icon::from_rgba(vec![0, 0, 0, 0], 1, 1).unwrap());

        let tray_menu = Menu::new();
        let quit = MenuItem::new("Quit", true, None);
        let open_ui = MenuItem::new("Open UI", true, None);
        let restart = MenuItem::new("Restart Audio", true, None);
        tray_menu.append(&quit).unwrap();
        tray_menu.append(&open_ui).unwrap();
        tray_menu.append(&restart).unwrap();

        let _tray = TrayIconBuilder::new()
            .with_icon(tray_icon.clone())
            .with_menu(Box::new(tray_menu.clone()))
            .with_tooltip("Vice")
            .with_title("Vice")
            .build()
            .expect("Failed to build tray icon");

        Self {
            _icon: tray_icon,
            _tray_menu: tray_menu,
            quit: quit,
            open_ui: open_ui,
            restart: restart,
            _tray,
        }
    }

    fn create_icon() -> Option<(Vec<u8>, u32, u32)> {
        let reader = Cursor::new(ICON);
        let icon_dir = match ico::IconDir::read(reader) {
            Ok(i) => i,
            Err(e) => {
                error!("Error occured when getting ico: {:#?}", e);
                return None;
            }
        };

        let entry = icon_dir
            .entries()
            .iter()
            .max_by_key(|e| e.width());
            
        let image = match entry {
            Some(i) => i,
            None => {
                error!("Failed to get an entry in ico");
                return None;
            }
        };

        match image.decode() {
            Ok(i) => {
                return Some((i.rgba_data().to_vec(), i.width(), i.height()));
            }
            Err(e) => {
                error!("Failed to decode icon: {:#?}", e);
                return None;
            }
        };
    }
}

pub(crate) struct Hotkeys {
    pub(crate) manager: Option<GlobalHotKeyManager>,
    pub(crate) hotkeys: HashMap<HotKey, String>
}

unsafe impl Send for Hotkeys {}

impl Hotkeys {
    pub fn new() -> Self {
        Self {
            manager: Some(GlobalHotKeyManager::new().unwrap()),
            hotkeys: HashMap::new()
        }
    }

    pub fn register_keybinds(&mut self) {
        let manager = self.manager.as_ref().unwrap();
        let hotkeys: Vec<HotKey> = self.hotkeys.keys().cloned().collect();

        if let Err(e) = manager.unregister_all(&hotkeys) {
            warn!("Failed to unregister keybinds: {}", e);
            return;
        }
        let _ = self.hotkeys.clear();

        let sfxs = files::get_soundboard();
        for sfx in sfxs {
            let sfxkeys: Vec<String> = sfx.keys;
            let mut codes: Option<Code> = None;
            let mut modifiers: Modifiers = Modifiers::empty();

            for key in sfxkeys {
                let (code, modif) = Hotkeys::string_to_code_or_mod(key);
                if code.is_some() {
                    codes = code;
                } else if modif.is_some() {
                    modifiers.insert(modif.unwrap());
                }
            }

            if codes.is_none() {
                continue;
            }

            let hotkey = HotKey::new(Some(modifiers), codes.unwrap());
            if let Err(e) = manager.register(hotkey) {
                warn!("Failed to register keybinds: {}", e);
                continue;
            }

            let _ = self.hotkeys.insert(hotkey, sfx.name);
        }
    }

    fn string_to_code_or_mod(string: String) -> (Option<Code>, Option<Modifiers>) {
        let modifier = match string.as_str() {
            "Ctrl" => Some(Modifiers::CONTROL),
            "Alt" => Some(Modifiers::ALT),
            "Shift" => Some(Modifiers::SHIFT),
            "CapsLock" => Some(Modifiers::CAPS_LOCK),
            _ => None
        };
        if modifier.is_some() {
            return (None, modifier);
        }

        let key = match string.as_str() {
            "A" => Some(Code::KeyA),
            "B" => Some(Code::KeyB),
            "C" => Some(Code::KeyC),
            "D" => Some(Code::KeyD),
            "E" => Some(Code::KeyE),
            "F" => Some(Code::KeyF),
            "G" => Some(Code::KeyG),
            "H" => Some(Code::KeyH),
            "I" => Some(Code::KeyI),
            "J" => Some(Code::KeyJ),
            "K" => Some(Code::KeyK),
            "L" => Some(Code::KeyL),
            "M" => Some(Code::KeyM),
            "N" => Some(Code::KeyN),
            "O" => Some(Code::KeyO),
            "P" => Some(Code::KeyP),
            "Q" => Some(Code::KeyQ),
            "R" => Some(Code::KeyR),
            "S" => Some(Code::KeyS),
            "T" => Some(Code::KeyT),
            "U" => Some(Code::KeyU),
            "V" => Some(Code::KeyV),
            "W" => Some(Code::KeyW),
            "X" => Some(Code::KeyX),
            "Y" => Some(Code::KeyY),
            "Z" => Some(Code::KeyZ),

            "F1" => Some(Code::F1),
            "F2" => Some(Code::F2),
            "F3" => Some(Code::F3),
            "F4" => Some(Code::F4),
            "F5" => Some(Code::F5),
            "F6" => Some(Code::F6),
            "F7" => Some(Code::F7),
            "F8" => Some(Code::F8),
            "F9" => Some(Code::F9),
            "F10" => Some(Code::F10),
            "F11" => Some(Code::F11),
            "F12" => Some(Code::F12),

            "1" => Some(Code::Digit1),
            "2" => Some(Code::Digit2),
            "3" => Some(Code::Digit3),
            "4" => Some(Code::Digit4),
            "5" => Some(Code::Digit5),
            "6" => Some(Code::Digit6),
            "7" => Some(Code::Digit7),
            "8" => Some(Code::Digit8),
            "9" => Some(Code::Digit9),
            "0" => Some(Code::Digit0),

            "-" => Some(Code::Minus),
            "=" => Some(Code::Equal),
            "[" => Some(Code::BracketLeft),
            "]" => Some(Code::BracketRight),
            "Enter" => Some(Code::Enter),
            "/" => Some(Code::Slash),
            "." => Some(Code::Period),
            "," => Some(Code::Comma),
            ";" => Some(Code::Semicolon),
            "\\" => Some(Code::Backslash),
            "'" => Some(Code::Quote),

            "Left" => Some(Code::ArrowLeft),
            "Right" => Some(Code::ArrowRight),
            "Up" => Some(Code::ArrowUp),
            "Down" => Some(Code::ArrowDown),
            
            _ => None
        };

        if key.is_some() {
            return (key, None);
        }
        
        return (None, None);
    }
}

fn handle_request(cmd: &str, args: Value, hotkeys: &mut Hotkeys) -> Value {
    if cmd == "get_soundboard" {
        let soundboard = files::get_soundboard();
        return json!({"result": soundboard});
    } else if cmd == "get_channels" {
        let channels = files::get_channels();
        return json!({"result": channels});
    } else if cmd == "new_channel" {
        if let Some(col) = args.get("color").and_then(|v| v.as_array()) {
            let mut color: [u8; 3] = [0, 0, 0];
            for (i, val) in col.iter().enumerate() {
                color[i] = val.as_i64().unwrap_or_default() as u8;
            }
            if let Some(icon) = args.get("icon").and_then(|v| v.as_str()) {
                if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
                    if let Some(deviceapps) = args.get("deviceapps").and_then(|v| v.as_str()) {
                        if let Some(device_str) = args.get("device").and_then(|v| v.as_str()) {
                            let deviceorapp: DeviceOrApp = DeviceOrApp::from_string(device_str);
                            if let Some(low) = args.get("low").and_then(|v| v.as_bool()) {
                                let res = funcs::new_channel(
                                    color,
                                    icon.to_string(),
                                    name.to_string(),
                                    deviceapps.to_string(),
                                    deviceorapp,
                                    low,
                                );
                                return json!({"result": res});
                            }
                        }
                    }
                }
            }
        }
    } else if cmd == "new_sound" {
        if let Some(col) = args.get("color").and_then(|v| v.as_array()) {
            let mut color: [u8; 3] = [0, 0, 0];
            for (i, val) in col.iter().enumerate() {
                color[i] = val.as_i64().unwrap_or_default() as u8;
            }
            if let Some(icon) = args.get("icon").and_then(|v| v.as_str()) {
                if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
                    if let Some(sound) = args.get("sound").and_then(|v| v.as_str()) {
                        if let Some(low) = args.get("low").and_then(|v| v.as_bool()) {
                            if let Some(key) = args.get("keys").and_then(|v| v.as_array()) {
                                let mut keys = Vec::new();
                                for val in key.iter() {
                                    keys.push(val.as_str().unwrap_or_default().into());
                                }
                                let res = funcs::new_sound(
                                    color,
                                    icon.to_string(),
                                    name.to_string(),
                                    sound.to_string(),
                                    low,
                                    keys
                                ).map(|_| {hotkeys.register_keybinds()});
                                return json!({"result": res});
                            }
                        }
                    }
                }
            }
        }
    } else if cmd == "edit_channel" {
        if let Some(col) = args.get("color").and_then(|v| v.as_array()) {
            let mut color: [u8; 3] = [0, 0, 0];
            for (i, val) in col.iter().enumerate() {
                color[i] = val.as_i64().unwrap_or_default() as u8;
            }
            if let Some(icon) = args.get("icon").and_then(|v| v.as_str()) {
                if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
                    if let Some(deviceapps) = args.get("deviceapps").and_then(|v| v.as_str()) {
                        if let Some(device_str) = args.get("device").and_then(|v| v.as_str()) {
                            let deviceorapp: DeviceOrApp = DeviceOrApp::from_string(device_str);
                            if let Some(oldname) = args.get("oldname").and_then(|v| v.as_str()) {
                                if let Some(low) = args.get("low").and_then(|v| v.as_bool()) {
                                    let res = funcs::edit_channel(
                                        color,
                                        icon.to_string(),
                                        name.to_string(),
                                        deviceapps.to_string(),
                                        deviceorapp,
                                        oldname.to_string(),
                                        low,
                                    );
                                    return json!({"result": res});
                                }
                            }
                        }
                    }
                }
            }
        }
    } else if cmd == "edit_sound" {
        if let Some(col) = args.get("color").and_then(|v| v.as_array()) {
            let mut color: [u8; 3] = [0, 0, 0];
            for (i, val) in col.iter().enumerate() {
                color[i] = val.as_i64().unwrap_or_default() as u8;
            }
            if let Some(icon) = args.get("icon").and_then(|v| v.as_str()) {
                if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
                    if let Some(oldname) = args.get("oldname").and_then(|v| v.as_str()) {
                        if let Some(low) = args.get("low").and_then(|v| v.as_bool()) {
                            if let Some(key) = args.get("keys").and_then(|v| v.as_array()) {
                                let mut keys = Vec::new();
                                for val in key.iter() {
                                    keys.push(val.as_str().unwrap_or_default().into());
                                }
                                let res = funcs::edit_soundboard(
                                    color,
                                    icon.to_string(),
                                    name.to_string(),
                                    oldname.to_string(),
                                    low,
                                    keys,
                                ).map(|_| {hotkeys.register_keybinds()});
                                return json!({"result": res});
                            }
                        }
                    }
                }
            }
        }
    } else if cmd == "delete_channel" {
        if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
            let res = funcs::delete_channel(name.to_string());
            return json!({"result": res});
        }
    } else if cmd == "delete_sound" {
        if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
            let res = funcs::delete_sound(name.to_string());
            return json!({"result": res});
        }
    } else if cmd == "get_devices" {
        let devices = funcs::get_devices();
        return json!({"result": devices});
    } else if cmd == "get_apps" {
        let apps = funcs::get_apps();
        return json!({"result": apps});
    } else if cmd == "save_settings" {
        if let Some(output) = args.get("output").and_then(|v| v.as_str()) {
            if let Some(scale) = args.get("scale").and_then(|v| v.as_f64()) {
                if let Some(light) = args.get("light").and_then(|v| v.as_bool()) {
                    if let Some(monitor) = args.get("monitor").and_then(|v| v.as_bool()) {
                        if let Some(peaks) = args.get("peaks").and_then(|v| v.as_bool()) {
                            if let Some(startup) = args.get("startup").and_then(|v| v.as_bool()) {
                                if let Some(tray) = args.get("tray").and_then(|v| v.as_bool()) {
                                    let res = funcs::save_settings(
                                        output.to_string(),
                                        scale as f32,
                                        light,
                                        monitor,
                                        peaks,
                                        startup,
                                        tray
                                    );
                                    return json!({"result": res});
                                }
                            }
                        }
                    }
                }
            }
        }
    } else if cmd == "get_performance" {
        let performance = funcs::get_performance();
        return json!({"result": performance});
    } else if cmd == "clear_performance" {
        funcs::clear_performance();
    } else if cmd == "get_settings" {
        let settings = funcs::get_settings();
        return json!({"result": settings});
    } else if cmd == "set_volume" {
        if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
            if let Some(volume) = args.get("volume").and_then(|v| v.as_f64()) {
                funcs::set_volume(name.to_string(), volume as f32);
            }
        }
    } else if cmd == "get_outputs" {
        let outputs = funcs::get_outputs();
        return json!({"result": outputs});
    } else if cmd == "play_sound" {
        if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
            if let Some(low) = args.get("low").and_then(|v| v.as_bool()) {
                funcs::play_sound(name.to_string(), low);
            }
        }
    } else if cmd == "get_volume" {
        if let Some(name) = args.get("name").and_then(|v| v.as_str()) {
            let volume = funcs::get_volume(name.to_string());
            return json!({"result": volume});
        }
    } else if cmd == "uninstall" {
        let res = funcs::uninstall();
        return json!({"result": res});
    } else if cmd == "update" {
        let res = funcs::update();
        if let Ok((code, version)) = res {
            return json!({"result": {"code": code, "version": version}});
        }
        return json!({"result": res});
    } else if cmd == "confirm_update" {
        let res = funcs::confirm_update();
        return json!({"result": res});
    } else if cmd == "save_blocks" {
        if let Some(item) = args.get("item").and_then(|v| v.as_str()) {
            if let Some(blocks) = args.get("blocks").and_then(|v| v.as_str()) {
                funcs::save_blocks(item.to_string(), blocks.to_string());
            }
        }
    } else if cmd == "load_blocks" {
        if let Some(item) = args.get("item").and_then(|v| v.as_str()) {
            let res = funcs::load_blocks(item.to_string());
            return json!({"result": res});
        }
    } else if cmd == "get_version" {
        let version = env!("CARGO_PKG_VERSION");
        return json!({ "result": version });
    } else if cmd == "open_link" {
        if let Some(url) = args.get("url").and_then(|v| v.as_str()) {
            return json!({"result": open::that(url).map_err(|e| e.to_string())});
        }
    } else if cmd == "reopen_ui" {
        log!("Reopening UI");

        run_ui();
    } else if cmd == "quit" {
        IS_OPEN.store(false, Ordering::SeqCst);

        let settings = files::get_settings();
        if settings.tray == false {
            log::write_debuglog();
            std::process::exit(0);
        }
    } else if cmd == "get_settings_folder" {
        let folder = files::app_base();
        return json!({"result": folder.to_str().unwrap_or_default()});
    }

    json!({"result": "null"})
}

pub(crate) fn call_instance() {
    if let Ok(mut stream) = TcpStream::connect("127.0.0.1:8423") {
        let request = json!({"cmd": "reopen_ui", "args": {}, "respond": false});
        stream.write_all(request.to_string().as_bytes()).unwrap();
        stream.write_all(b"\n").unwrap();
        log!("Sent reopen command to instance");
    } else {
        error!("Failed to connect to instance");
    }
}

pub(crate) fn run_ui() {
    unsafe {
        if IS_OPEN.load(Ordering::SeqCst) == false {
            IS_OPEN.store(true, Ordering::SeqCst);

            let args: Vec<String> = std::env::args().collect();
            let changelog = args.contains(&"--changelog".to_string());

            log!("Opening UI with hollowing");
            if !hollow_process(EMBEDDED_UI.as_ptr(), changelog) {
                log!("Resorting with no hollowing");
                run_in_job(EMBEDDED_UI.as_ptr(), EMBEDDED_UI.len(), changelog);
            }
        }
    }
}