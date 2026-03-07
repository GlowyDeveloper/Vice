use std::{
    fs, io::{BufRead as _, BufReader, Write as _}, os::windows::io::AsRawHandle as _, path::PathBuf, process::{Command, Stdio}, thread, time::Duration
};
use interprocess::{TryClone as _, local_socket::{GenericNamespaced, ListenerOptions, prelude::*}};
use windows::Win32::{
    Foundation::HANDLE,
    System::JobObjects::{
        AssignProcessToJobObject, SetInformationJobObject, CreateJobObjectW,
        JobObjectExtendedLimitInformation, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
    },
};

use device_query::{DeviceQuery as _, DeviceState};
use serde_json::{json};

use crate::{files::{self, DeviceOrApp}, funcs};

const EMBEDDED_UI: &[u8] = include_bytes!("../Ui/bin/Release/net10.0/win-x64/publish/Vice.Ui.exe");

fn handle_request(cmd: &str, args: serde_json::Value) -> serde_json::Value {
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
                            let deviceorapp: DeviceOrApp = device_str.parse().unwrap();
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
                                )/* .map(|_| {register_keybinds(&app)})*/;
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
                            let deviceorapp: DeviceOrApp = device_str.parse().unwrap();
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
                                )/* .map(|_| {register_keybinds(&app)})*/;
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
    } else if cmd == "reopen_ui" {
        println!("Reopening UI");

        run_ui();
    } else if cmd == "quit" {
        let settings = files::get_settings();
        if settings.tray == false {
            std::process::exit(0);
        }
    }

    json!({"result": "null"})
}

pub(crate) fn call_instance() {
    let name = "ViceUiPipe".to_ns_name::<GenericNamespaced>().unwrap();

    if let Ok(mut stream) = LocalSocketStream::connect(name) {
        let request = json!({"cmd": "reopen_ui", "args": {}, "respond": false});
        stream.write(request.to_string().as_bytes()).unwrap();
        stream.write(b"\n").unwrap();
        stream.flush().unwrap();
        println!("Sent reopen command to instance");
    } else {
        eprintln!("Failed to connect to instance");
    }
}

pub(crate) fn check_if_ui_is_installed() {
    let path = files::bins_base().join(format!("Vice.Ui-v{}.exe", env!("CARGO_PKG_VERSION")));

    if path.exists() {
        return;
    }

    for entry in fs::read_dir(files::bins_base()).unwrap_or_else(|_| panic!("Failed to read bins directory")) {
        if let Ok(entry) = entry {
            let file_name = entry.file_name();
            let file_name_str = file_name.to_string_lossy();

            if file_name_str.starts_with("Vice.Ui") && file_name_str.ends_with(".exe") {
                let _ = fs::remove_file(entry.path());
            }
        }
    }

    let mut file = fs::File::create(&path).unwrap();
    let _ = file.write_all(EMBEDDED_UI);
}

fn spawn_in_job(path: &PathBuf) -> std::process::Child {
    unsafe {
        let job = CreateJobObjectW(None, None).unwrap();

        let mut info = JOBOBJECT_EXTENDED_LIMIT_INFORMATION::default();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        SetInformationJobObject(
            job,
            JobObjectExtendedLimitInformation,
            &info as *const _ as *const _,
            std::mem::size_of::<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>() as u32,
        )
        .unwrap();

        let child = Command::new(path)
            .stdout(Stdio::inherit())
            .stderr(Stdio::inherit()) 
            .spawn()
            .expect("Failed to spawn");

        AssignProcessToJobObject(job, HANDLE(child.as_raw_handle())).unwrap();

        child
    }
}

fn handle_client(mut stream: LocalSocketStream) -> std::io::Result<()> {
    let mut reader = BufReader::new(stream.try_clone()?);
    let mut buffer_bytes: Vec<u8> = Vec::new();

    loop {
        buffer_bytes.clear();
        let n = reader.read_until(b'\n', &mut buffer_bytes)?;
        if n == 0 {
            println!("Client disconnected");
            break;
        }

        let buffer_str = String::from_utf8_lossy(&buffer_bytes);

        let buffer_trimmed = buffer_str.trim();
        if buffer_trimmed.is_empty() { continue; }

        let value: serde_json::Value = match serde_json::from_str(buffer_trimmed) {
            Ok(v) => v,
            Err(e) => { eprintln!("Invalid JSON: {}", e); continue; }
        };

        if let Some(cmd) = value.get("cmd").and_then(|v| v.as_str()) {
            if let Some(args) = value.get("args") {
                let response = handle_request(cmd, args.clone());

                if let Some(respond) = value.get("respond").and_then(|v| v.as_bool()) {
                    if respond == true {
                        let formatted = format!("{}\n", response["result"]);
                        stream.write_all(formatted.as_bytes())?;
                        stream.flush()?;
                    }
                }
            }
        }
    }

    Ok(())
}

pub(crate) fn run_ipc() {
    let name = "ViceUiPipe".to_ns_name::<GenericNamespaced>().unwrap();

    let listener = ListenerOptions::new()
        .name(name)
        .create_sync()
        .unwrap();

    println!("Listening for connections…");

    std::thread::Builder::new()
        .name("IPC Listener".into())
        .spawn(move || {
            for connection in listener.incoming() {
                match connection {
                    Ok(stream) => {
                        println!("Client connected");

                        std::thread::spawn(move || {
                            if let Err(e) = handle_client(stream) {
                                eprintln!("Client error: {}", e);
                            }
                        });
                    }
                    Err(e) => eprintln!("Failed to accept connection: {}", e),
                }
            }
        })
        .expect("Failed to spawn IPC listener thread");
}

pub(crate) fn run_ui() {
    let path: PathBuf = files::bins_base().join(format!("Vice.Ui-v{}.exe", env!("CARGO_PKG_VERSION")));

    spawn_in_job(&path);
}