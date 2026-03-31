use std::{ffi::{CStr, c_char}, sync::{Arc, Mutex}};
use global_hotkey::{GlobalHotKeyEvent, hotkey::HotKey};
use windows::Win32::{
    Foundation::HWND,
    UI::WindowsAndMessaging::{
        DispatchMessageW, GetMessageW, MSG, TranslateMessage
    }
};
use single_instance::SingleInstance;
use tray_icon::menu::MenuEvent;

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

    let tray = ui::SystemTray::new();
    let hotkeys = Arc::new(Mutex::new(ui::Hotkeys::new()));
    ui::IpcServer::create_in_thread(Arc::clone(&hotkeys));

    hotkeys.lock().unwrap().register_keybinds();

    let args: Vec<String> = std::env::args().collect();
    if !args.contains(&"--background".to_string()) {
        ui::run_ui();
    }

    loop {
        unsafe {
            let mut msg = MSG::default();

            while GetMessageW(&mut msg, Some(HWND(std::ptr::null_mut())), 0, 0).into() {
                let _ = TranslateMessage(&msg);
                let _ = DispatchMessageW(&msg);

                if let Ok(menu_event) = MenuEvent::receiver().try_recv() {
                    if menu_event.id() == tray.quit.id() {
                        log::write_debuglog();
                        std::process::exit(0);
                    } else if menu_event.id() == tray.open_ui.id() {
                        ui::run_ui();
                    } else if menu_event.id() == tray.restart.id() {
                        audio::restart();
                        hotkeys.lock().unwrap().register_keybinds();
                    }
                }

                if let Ok(event) = GlobalHotKeyEvent::receiver().try_recv() {
                    let keys: Vec<HotKey> = hotkeys.lock().unwrap().hotkeys.keys().cloned().collect();
                    let mut name: String = String::new();
                    for hotkey in &keys {
                        if hotkey.id == event.id {
                            if let Some(pos) = keys.iter().position(|h| h == hotkey) {
                                let names: Vec<String> = hotkeys.lock().unwrap().hotkeys.values().cloned().collect();
                                if let Some(n) = names.get(pos) {
                                    name = n.to_string();
                                    break;
                                } else {
                                    error!("Failed to get name of hotkey");
                                    break;
                                }
                            } else {
                                error!("Hotkey not found");
                                break;
                            }
                        }
                    }

                    let sfxs = files::get_soundboard();
                    if let Some(pos) = sfxs.iter().position(|s| s.name == name) {
                        if let Some(sfx) = sfxs.get(pos) {
                            funcs::play_sound(name, sfx.lowlatency);
                        }
                    }
                }
            }
        }
    }
}