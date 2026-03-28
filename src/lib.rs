/*use std::{cell::RefCell, collections::HashMap, io::Cursor, rc::Rc, thread, time::Duration};
use device_query::{DeviceQuery, DeviceState};
use global_hotkey::{GlobalHotKeyEvent, GlobalHotKeyManager, HotKeyState, hotkey::{Code, HotKey, Modifiers}};
use rust_embed::RustEmbed;
use serde_json::json;
use tao::{event::{Event, WindowEvent}, event_loop::{ControlFlow, EventLoopBuilder, EventLoopProxy, EventLoopWindowTarget}, window::{Icon, Window, WindowBuilder}};
use tiny_http::{Header, Response, Server};
use tray_icon::{Icon as TrayIcon, TrayIconBuilder, menu::{Menu, MenuEvent, MenuItem}};
use wry::{WebContext, WebView, WebViewBuilder};

use crate::files::SoundboardSFX;

mod funcs;
mod performance;
mod audio;
mod files;

#[derive(RustEmbed)]
#[folder = "flutter/build/web"]
struct Assets;

#[derive(Default)]
struct Keys {
    manager: Option<GlobalHotKeyManager>,
    hotkeys: HashMap<HotKey, String>
}

#[derive(Default)]
pub struct App {
    window: Option<Window>,
    webview: Option<WebView>,
    web_context: Option<WebContext>,
    keys: Keys
}

#[derive(PartialEq)]
pub enum ServerCommand {
    CreateWindow,
    IpcRequest { id: String, cmd: String, args: serde_json::Value },
    GenerateKeybinds
}

fn handle_ipc(cmd: &str, args: serde_json::Value, app: &Rc<RefCell<App>>) -> serde_json::Value {
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
                        if let Some(device) = args.get("device").and_then(|v| v.as_bool()) {
                            if let Some(low) = args.get("low").and_then(|v| v.as_bool()) {
                                let res = funcs::new_channel(
                                    color,
                                    icon.to_string(),
                                    name.to_string(),
                                    deviceapps.to_string(),
                                    device,
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
                                let mut keys: Vec<String> = vec![];
                                for (i, val) in key.iter().enumerate() {
                                    keys[i] = val.as_str().unwrap_or_default().into();
                                }
                                let res = funcs::new_sound(
                                    color,
                                    icon.to_string(),
                                    name.to_string(),
                                    sound.to_string(),
                                    low,
                                    keys
                                ).map(|_| {register_keybinds(&app)});
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
                        if let Some(device) = args.get("device").and_then(|v| v.as_bool()) {
                            if let Some(oldname) = args.get("oldname").and_then(|v| v.as_str()) {
                                if let Some(low) = args.get("low").and_then(|v| v.as_bool()) {
                                    let res = funcs::edit_channel(
                                        color,
                                        icon.to_string(),
                                        name.to_string(),
                                        deviceapps.to_string(),
                                        device,
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
    } else if cmd == "edit_soundboard" {
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
                                let mut keys: Vec<String> = vec![];
                                for (_, val) in key.iter().enumerate() {
                                    keys.push(val.as_str().unwrap_or_default().into());
                                }
                                let res = funcs::edit_soundboard(
                                    color,
                                    icon.to_string(),
                                    name.to_string(),
                                    oldname.to_string(),
                                    low,
                                    keys,
                                ).map(|_| {register_keybinds(&app)});
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
    } else if cmd == "pick_menu_sound" {
        let sound = funcs::pick_menu_sound();
        return json!({"result": sound});
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
                                let res = funcs::save_settings(
                                    output.to_string(),
                                    scale as f32,
                                    light,
                                    monitor,
                                    peaks,
                                    startup
                                );
                                return json!({"result": res});
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
    } else if cmd == "flutter_print" {
        if let Some(text) = args.get("text").and_then(|v| v.as_str()) {
            println!("{}", text);
        }
    } else if cmd == "get_version" {
        let version = env!("CARGO_PKG_VERSION");
        return json!({ "result": version });
    } else if cmd == "open_link" {
        if let Some(url) = args.get("url").and_then(|v| v.as_str()) {
            return json!({"result": open::that(url).map_err(|e| e.to_string())});
        }
    } else if cmd == "key_bind_select" {
        let device_state = DeviceState::new();
        let mut final_keys: Vec<String> = Vec::new();

        loop {
            if device_state.get_keys().is_empty() { break; }
            thread::sleep(Duration::from_millis(50));
        }

        loop {
            let keys = device_state.get_keys();
            if !keys.is_empty() { break; }
            thread::sleep(Duration::from_millis(20));
        }

        loop {
            let current_keys = device_state.get_keys();

            if current_keys.is_empty() && !final_keys.is_empty() {
                break;
            }

            if current_keys.len() > final_keys.len() {
                final_keys = current_keys.iter().map(|k| format!("{:?}", k)).collect();
            }

            thread::sleep(Duration::from_millis(10));
        }

        return json!({"result": final_keys});
    }

    serde_json::Value::Null
}

fn string_to_code_or_mod(string: String) -> (Option<Code>, Option<Modifiers>) {
    let modifier = match string.as_str() {
        "LControl" => Some(Modifiers::CONTROL),
        "RControl" => Some(Modifiers::CONTROL),
        "LAlt" => Some(Modifiers::ALT),
        "RAlt" => Some(Modifiers::ALT),
        "LShift" => Some(Modifiers::SHIFT),
        "RShift" => Some(Modifiers::SHIFT),
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

        "Minus" => Some(Code::Minus),
        "Equal" => Some(Code::Equal),
        "LeftBracket" => Some(Code::BracketLeft),
        "RightBracket" => Some(Code::BracketRight),
        "Backspace" => Some(Code::Backspace),
        "Enter" => Some(Code::Enter),
        "Slash" => Some(Code::Slash),
        "Dot" => Some(Code::Period),
        "Comma" => Some(Code::Comma),
        "Semicolon" => Some(Code::Semicolon),
        "BackSlash" => Some(Code::Backslash),
        
        _ => None
    };

    if key.is_some() {
        return (key, None);
    }
    
    return (None, None);
}

fn register_keybinds(app: &Rc<RefCell<App>>) {
    let mut app = app.borrow_mut();
    let keys = &mut app.keys;
    let manager = keys.manager.as_ref().unwrap();
    let hotkeys: Vec<HotKey> = keys.hotkeys.keys().cloned().collect();

    if let Err(e) = manager.unregister_all(&hotkeys) {
        eprintln!("Failed to unregister keybinds: {}", e);
        return;
    }
    let _ = &keys.hotkeys.clear();

    let sfxs = files::get_soundboard();
    for sfx in sfxs {
        let sfxkeys: Vec<String> = sfx.keys;
        let mut codes: Option<Code> = None;
        let mut modifiers: Modifiers = Modifiers::empty();

        for key in sfxkeys {
            let (code, modif) = string_to_code_or_mod(key);
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
            eprintln!("Failed to register hotkey: {}", e);
            continue;
        }
        let _ = &keys.hotkeys.insert(hotkey, sfx.name);
    }
}

pub fn run_server(ready_tx: &std::sync::mpsc::Sender<()>, proxy: EventLoopProxy<ServerCommand>) {
    let server = match Server::http("127.0.0.1:5923") {
        Ok(s) => s,
        Err(e) => {
            eprintln!("Failed to bind server to 127.0.0.1:5923: {}", e);
            let _ = ready_tx.send(());
            return;
        }
    };
    println!("Server listening on 127.0.0.1:5923");
    
    ready_tx.send(()).unwrap();

    for request in server.incoming_requests() {
        if request.method() == &tiny_http::Method::Get && request.url() == "/webview" {
            let _ = proxy.send_event(ServerCommand::CreateWindow);
            let mut response = Response::from_string("Success");
            response.add_header(Header::from_bytes(&b"Content-Type"[..], &b"text/plain"[..]).unwrap());
            let _ = request.respond(response);
            continue;
        }

        let path = if request.url() == "/" { "index.html" } else { &request.url()[1..] };

        if let Some(file) = Assets::get(path) {
            let mime = match path.rsplit(".").next() {
                Some("js") => "application/javascript",
                Some("css") => "text/css",
                Some("html") => "text/html",
                Some("json") => "application/json",
                Some("wasm") => "application/wasm",
                Some("woff2") => "font/woff2",
                _ => "application/octet-stream",
            };
            let mut response = Response::from_data(file.data.into_owned());
            response.add_header(Header::from_bytes(&b"Content-Type"[..], mime.as_bytes()).unwrap());
            response.add_header(Header::from_bytes(&b"Cache-Control"[..], &b"no-store"[..]).unwrap());
            request.respond(response).unwrap();
        } else {
            if let Some(file) = Assets::get("index.html") {
                let mut response = Response::from_data(file.data.into_owned());
                response.add_header(Header::from_bytes(&b"Content-Type"[..], &b"text/html"[..]).unwrap());
                response.add_header(Header::from_bytes(&b"Cache-Control"[..], &b"no-store"[..]).unwrap());
                request.respond(response).unwrap();
            } else {
                request.respond(Response::from_string("Not Found").with_status_code(404)).unwrap();
            }
        }
    }
}

pub fn create_window(event_loop: &EventLoopWindowTarget<ServerCommand>, proxy: EventLoopProxy<ServerCommand>, app: &Rc<RefCell<App>>, hide_window: bool) {
    if app.borrow().window.is_none() {
        if !hide_window {
            let icon = create_icon().map(|(data, width, height)| Icon::from_rgba(data, width, height).unwrap());
            let window = WindowBuilder::new()
                .with_title("Vice")
                .with_window_icon(icon)
                .build(event_loop)
                .unwrap();

            app.try_borrow_mut().unwrap().window = Some(window);
        }
    }

    if app.borrow().webview.is_none() || app.borrow().web_context.is_none() {
        if !hide_window {
            let cache_dir = files::app_base().join("Cache");
            let mut web_context = WebContext::new(Some(cache_dir));

            let webview = WebViewBuilder::new_with_web_context(&mut web_context)
                .with_initialization_script(
                    r#"
                    document.addEventListener('contextmenu', event => {
                        event.preventDefault();
                    });
                    "#
                )
                .with_url("http://127.0.0.1:5923")
                .with_ipc_handler({
                    let proxy = proxy.clone();
                    move |req| {
                        if let Ok(v) = serde_json::from_str::<serde_json::Value>(req.body()) {
                            if v["ipc_type"] == "request" {
                                let id = v["id"].as_str().unwrap().to_string();
                                let cmd = v["cmd"].as_str().unwrap().to_string();
                                let args = v["args"].clone();

                                proxy.send_event(ServerCommand::IpcRequest { id, cmd, args }).ok();
                            }
                        }
                    }
                })
                .build(&app.borrow().window.as_ref().unwrap())
                .unwrap();

            app.try_borrow_mut().unwrap().webview = Some(webview);
            app.try_borrow_mut().unwrap().web_context = Some(web_context);
        }
    }
}

pub fn run() {
    if let Ok(client) = reqwest::blocking::Client::builder().timeout(std::time::Duration::from_millis(250)).build() {
        match client.get("http://127.0.0.1:5923").send() {
            Ok(_) => {
                println!("Calling http://127.0.0.1:5923/webview");
                let _ = client.get("http://127.0.0.1:5923/webview").send();
                std::process::exit(0);
            }
            Err(_) => {},
        }
    }

    files::create_files();
    audio::start();
    performance::start();

    let (tx, rx) = std::sync::mpsc::channel();
    let event_loop = EventLoopBuilder::<ServerCommand>::with_user_event().build();
    let proxy = event_loop.create_proxy();
    
    thread::spawn({
        let proxy = proxy.clone();
        move || {
            run_server(&tx, proxy);
        }
    });
    rx.recv().unwrap();

    let args: Vec<String> = std::env::args().collect();
    let hide_window: bool = args.contains(&"--background".to_string());

    let app = Rc::new(RefCell::new(App::default()));
    create_window(&event_loop, proxy.clone(), &app, hide_window);

    let keys = Keys {
        manager: Some(GlobalHotKeyManager::new().unwrap()),
        hotkeys: HashMap::new()
    };
    app.borrow_mut().keys = keys;
    register_keybinds(&app);

    event_loop.run(move |event, event_loop_target, control_flow| {
        *control_flow = ControlFlow::WaitUntil(
            std::time::Instant::now() + Duration::from_millis(16)
        );

        match event {
            Event::WindowEvent {
                event: WindowEvent::CloseRequested,
                ..
            } => {
                app.try_borrow_mut().unwrap().window = None;
                app.try_borrow_mut().unwrap().webview = None;
                app.try_borrow_mut().unwrap().web_context = None;
            },
            Event::UserEvent(e) => match e {
                ServerCommand::CreateWindow => create_window(event_loop_target, proxy.clone(), &app, false),
                ServerCommand::IpcRequest { id, cmd, args } => {
                    let res = handle_ipc(&cmd, args, &app);
                    if let Some(webview) = &app.borrow().webview {
                        let result_value = match res.get("result") {
                            Some(v) => v.clone(),
                            None => res.clone(),
                        };

                        let js = format!(
                            "window.postMessage({{ ipc_type: 'response', id: '{}', result: {} }}, '*');",
                            id,
                            result_value.to_string()
                        );
                        
                        match webview.evaluate_script(&js) {
                            Ok(_ret) => {},
                            Err(e) => eprintln!("Failed to return ipc {:?}", e),
                        }
                    }
                },
                ServerCommand::GenerateKeybinds => register_keybinds(&app),
            },
            _ => (),
        }

        if let Ok(menu_event) = MenuEvent::receiver().try_recv() {
            if menu_event.id() == quit.id() {
                std::process::exit(0);
            } else if menu_event.id() == open_ui.id() {
                create_window(event_loop_target, proxy.clone(), &app, false);
            } else if menu_event.id() == restart.id() {
                audio::restart();
                register_keybinds(&app);
            }
        }

        if let Ok(event) = GlobalHotKeyEvent::receiver().try_recv() {
            if event.state == HotKeyState::Pressed {
                let hotkeys: Vec<HotKey> = app.borrow_mut().keys.hotkeys.keys().cloned().collect();
                let mut name: String = String::new();
                for hotkey in &hotkeys {
                    if hotkey.id == event.id {
                        if let Some(pos) = hotkeys.iter().position(|h| h == hotkey) {
                            let names: Vec<String> = app.borrow_mut().keys.hotkeys.values().cloned().collect();
                            if let Some(n) = names.get(pos) {
                                name = n.to_string();
                                break;
                            } else {
                                eprintln!("Failed to get name of hotkey");
                                break;
                            }
                        } else {
                            eprintln!("Hotkey not found");
                            break;
                        }
                    }
                }

                let sfxs: Vec<SoundboardSFX> = files::get_soundboard();
                if let Some(pos) = sfxs.iter().position(|s| s.name == name) {
                    if let Some(sfx) = sfxs.get(pos) {
                        funcs::play_sound(name, sfx.lowlatency);
                    }
                }
            }
        }
    });
}*/

use std::ffi::{CStr, c_char};

use single_instance::SingleInstance;

mod files;
mod audio;
mod performance;
mod ui;
mod funcs;
mod log;

#[no_mangle]
pub extern "C" fn error(info: *const c_char) {
    unsafe {
        let string = CStr::from_ptr(info).to_string_lossy().into_owned();
        error!("{}", string);
    }
}

#[no_mangle]
pub extern "C" fn warn(info: *const c_char) {
    unsafe {
        let string = CStr::from_ptr(info).to_string_lossy().into_owned();
        warn!("{}", string);
    }
}

pub fn run() {
    std::env::set_var("RUST_BACKTRACE", "1");

    std::panic::set_hook(Box::new(|panic_info| {
        critical!("{}", panic_info);
        log::write_crashlog();
    }));

    let instance = SingleInstance::new("ViceSingleInstance").expect("Failed to create single instance");
    if !instance.is_single() {
        ui::call_instance();
        std::process::exit(0);
    }

    audio::start();
    performance::start();
    files::create_files();
    ui::run_ipc();

    let args: Vec<String> = std::env::args().collect();
    if !args.contains(&"--background".to_string()) {
        ui::run_ui();
    }

    std::thread::park();
}