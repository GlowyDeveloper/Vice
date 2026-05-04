use std::{
    ffi::{CStr, CString, c_char}, sync::atomic::{AtomicBool, AtomicI16, Ordering}, thread
};

use crate::{critical, error, files::{self, Channel, DeviceOrApp, Effects, EffectsType}, log};

#[link(name = "audio")]
unsafe extern "C" {
    static stop_audio: AtomicBool;
    static audio_threads_running: AtomicI16;

    fn get_outputs(len: *mut usize) -> *const *const c_char;
    fn get_inputs(len: *mut usize) -> *const *const c_char;
    fn get_apps(len: *mut usize) -> *const *const c_char;
    fn get_sfx_peaks(len: *mut usize, file: *const c_char) -> *const *const c_char;
    fn play_sound(file: *const c_char, device_name: *const c_char, low_latency: bool, effects: *const FFIEffects);
    fn device_to_device(input: *const c_char, output: *const c_char, low_latency: bool, channel_name: *const c_char, effects: *const FFIEffects);
    fn app_to_device(input: *const c_char, output: *const c_char, low_latency: bool, channel_name: *const c_char, effects: *const FFIEffects);
    fn insert_volume(key: *const c_char, value: f32);
    fn reset_volume();
    fn get_volume_display(key: *const c_char) -> f32;
}

#[repr(C)]
pub struct FFISlice<T> {
    pub ptr: *const T,
    pub len: usize,
}

#[repr(u32)]
pub enum FFIEffectsType {
    In = 0,
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

#[repr(C)]
pub struct FFIEffectNode {
    pub x: u32,
    pub y: u32,
    pub type_of: FFIEffectsType,
    pub id: *const c_char,
    pub inputs: FFISlice<*const c_char>,
    pub outputs: FFISlice<*const c_char>,
    pub options: FFISlice<*const c_char>,
}

#[repr(C)]
pub struct FFIEffectsConnection {
    pub from_node_id: *const c_char,
    pub from_port_id: *const c_char,
    pub to_node_id: *const c_char,
    pub to_port_id: *const c_char,
}

#[repr(C)]
pub struct FFIEffects {
    pub nodes: FFISlice<FFIEffectNode>,
    pub connections: FFISlice<FFIEffectsConnection>,
}

#[repr(C)]
pub struct EffectsFFIOwned {
    pub ffi: FFIEffects,

    _strings: Vec<CString>,
    _nodes: Vec<FFIEffectNode>,
    _connections: Vec<FFIEffectsConnection>,
}

fn convert_effects_to_ffi_safe(effects: Effects) -> EffectsFFIOwned {
    fn convert_type(t: EffectsType) -> FFIEffectsType {
        match t {
            EffectsType::In => FFIEffectsType::In,
            EffectsType::Out => FFIEffectsType::Out,
            EffectsType::Split => FFIEffectsType::Split,
            EffectsType::Merge => FFIEffectsType::Merge,
            EffectsType::Compression => FFIEffectsType::Compression,
            EffectsType::Delay => FFIEffectsType::Delay,
            EffectsType::Distortion => FFIEffectsType::Distortion,
            EffectsType::Gain => FFIEffectsType::Gain,
            EffectsType::Gating => FFIEffectsType::Gating,
            EffectsType::Reverb => FFIEffectsType::Reverb,
        }
    }

    fn to_c_ptr(s: String, strings: &mut Vec<CString>) -> *const c_char {
        let c = CString::new(s).unwrap();
        let ptr = c.as_ptr();
        strings.push(c);
        ptr
    }

    fn to_slice(vec: Vec<String>, strings: &mut Vec<CString>) -> FFISlice<*const c_char> {
        let mut ptrs: Vec<*const c_char> = Vec::with_capacity(vec.len());

        for s in vec {
            let c = CString::new(s).unwrap();
            ptrs.push(c.as_ptr());
            strings.push(c);
        }

        let ptr = ptrs.as_ptr();
        let len = ptrs.len();

        std::mem::forget(ptrs);

        FFISlice { ptr, len }
    }

    let mut strings = Vec::new();
    let mut nodes = Vec::new();
    let mut connections = Vec::new();

    for n in effects.nodes {
        nodes.push(FFIEffectNode {
            x: n.x,
            y: n.y,
            type_of: convert_type(n.type_of),
            id: to_c_ptr(n.id, &mut strings),
            inputs: to_slice(n.inputs, &mut strings),
            outputs: to_slice(n.outputs, &mut strings),
            options: to_slice(n.options, &mut strings),
        });
    }

    for c in effects.connections {
        connections.push(FFIEffectsConnection {
            from_node_id: to_c_ptr(c.from_node_id, &mut strings),
            from_port_id: to_c_ptr(c.from_port_id, &mut strings),
            to_node_id: to_c_ptr(c.to_node_id, &mut strings),
            to_port_id: to_c_ptr(c.to_port_id, &mut strings),
        });
    }

    let ffi = FFIEffects {
        nodes: FFISlice {
            ptr: nodes.as_ptr(),
            len: nodes.len(),
        },
        connections: FFISlice {
            ptr: connections.as_ptr(),
            len: connections.len(),
        },
    };

    EffectsFFIOwned {
        ffi,
        _strings: strings,
        _nodes: nodes,
        _connections: connections,
    }
}

pub(crate) fn outputs() -> Vec<String> {
    unsafe {
        let mut len: usize = 0;
        let devices: *const *const c_char = get_outputs(&mut len as *mut usize);

        let slice = std::slice::from_raw_parts(devices, len);

        slice.iter()
            .map(|&cstr_ptr| {
                CStr::from_ptr(cstr_ptr).to_string_lossy().into_owned()
            })
            .collect()
    }
}

pub(crate) fn inputs() -> Vec<String> {
    unsafe {
        let mut len: usize = 0;
        let devices: *const *const c_char = get_inputs(&mut len as *mut usize);

        let slice = std::slice::from_raw_parts(devices, len);

        slice.iter()
            .map(|&cstr_ptr| {
                CStr::from_ptr(cstr_ptr).to_string_lossy().into_owned()
            })
            .collect()
    }
}

pub(crate) fn play_sfx(file_path: String, low_latency: bool, effects: Effects) {
    let thread_name = format!("sfx_{}", file_path);

    if let Err(e) = thread::Builder::new()
        .name(thread_name.clone())
        .spawn(move || {
            let output: String = files::get_settings().output;

            let c_device: Option<CString> = match output.is_empty() {
                true => None,
                false => Some(CString::new(output).unwrap())
            };

            let device: *const c_char = c_device
                .as_ref()
                .map_or(std::ptr::null(), |s| s.as_ptr());
            
            let file: CString = CString::new(file_path).unwrap();
            let ffi = convert_effects_to_ffi_safe(effects);

            unsafe {play_sound(file.as_ptr(), device, low_latency, &ffi.ffi);}
        }) {

        error!("Failed to spawn sound effect thread for \"{}\": {}", thread_name, e);
    }
    
}

pub(crate) fn apps() -> Vec<String> {
    unsafe {
        let mut len: usize = 0;
        let apps: *const *const c_char = get_apps(&mut len as *mut usize);

        let slice = std::slice::from_raw_parts(apps, len);

        slice.iter()
            .map(|&cstr_ptr| {
                CStr::from_ptr(cstr_ptr).to_string_lossy().into_owned()
            })
            .collect()
    }
}

pub(crate) fn get_peaks(file_path: String) -> Vec<String> {
    unsafe {
        let mut len: usize = 0;
        let path_cstr: CString = CString::new(file_path).unwrap();
        let apps: *const *const c_char = get_sfx_peaks(&mut len as *mut usize, path_cstr.as_ptr());

        let slice = std::slice::from_raw_parts(apps, len);

        slice.iter()
            .map(|&cstr_ptr| {
                CStr::from_ptr(cstr_ptr).to_string_lossy().into_owned()
            })
            .collect()
    }
}

fn manage_device(input_device_name: String, output_device_name: String, low_latency: bool, channel_name: String, effects: Effects) {
    let input_cstr: Option<CString> = match input_device_name.is_empty() {
        true => None,
        false => Some(CString::new(input_device_name).unwrap())
    };
    let output_cstr: Option<CString> = match output_device_name.is_empty() {
        true => None,
        false => Some(CString::new(output_device_name).unwrap())
    };
    let name_cstr: CString = CString::new(channel_name.clone()).unwrap();

    let input: *const i8 = input_cstr.as_ref().map_or(std::ptr::null(), |cstr| cstr.as_ptr());
    let output: *const i8 = output_cstr.as_ref().map_or(std::ptr::null(), |cstr| cstr.as_ptr());
    let name: *const i8 = name_cstr.as_ptr();
    let ffi = convert_effects_to_ffi_safe(effects);

    unsafe {device_to_device(input, output, low_latency, name, &ffi.ffi)};
}

fn manage_app(app_name: String, output_device_name: String, low_latency: bool, channel_name: String, effects: Effects) {
    let input_cstr: CString = CString::new(app_name).unwrap();
    let output_cstr: Option<CString> = match output_device_name.is_empty() {
        true => None,
        false => Some(CString::new(output_device_name).unwrap())
    };
    let name_cstr: CString = CString::new(channel_name.clone()).unwrap();

    let input: *const i8 = input_cstr.as_ptr();
    let output: *const i8 = output_cstr.as_ref().map_or(std::ptr::null(), |cstr| cstr.as_ptr());
    let name: *const i8 = name_cstr.as_ptr();
    let ffi = convert_effects_to_ffi_safe(effects);

    unsafe {app_to_device(input, output, low_latency, name, &ffi.ffi)};
}

pub(crate) fn set_volume(channel_name: String, volume: f32) {
    let name_cstr = CString::new(channel_name).unwrap();

    let name: *const i8 = name_cstr.as_ptr();

    unsafe { insert_volume(name, volume); }
}

pub(crate) fn get_volume_parsed(name: String) -> String {
    let name_cstr = CString::new(name).unwrap();

    unsafe {
        let vol = get_volume_display(name_cstr.as_ptr());
        vol.to_string()
    }
}

pub(crate) fn start() {
    if unsafe {stop_audio.load(Ordering::SeqCst) == false} {
        log!("Audio threads already running");
        return;
    }

    unsafe {
        stop_audio.store(false, Ordering::SeqCst);
    }

    let channels: Vec<Channel> = files::get_channels();

    if channels.is_empty() {
        log!("No threads to create");
        return;
    }

    for channel in channels {
        let thread_name: String = channel.name.clone();
        if let Err(e) = thread::Builder::new()
            .name(thread_name.clone())
            .spawn(move || {
                unsafe {
                    let channel_cstr = CString::new(channel.name.clone()).unwrap();

                    let channel_name: *const i8 = channel_cstr.as_ptr();

                    insert_volume(channel_name, channel.volume);
                }

                if channel.deviceorapp == DeviceOrApp::Device {
                    manage_device(channel.device, files::get_settings().output, channel.lowlatency, channel.name, channel.effects);
                } else {
                    manage_app(channel.device, files::get_settings().output, channel.lowlatency, channel.name, channel.effects);
                }
            }) {

            critical!("Failed to spawn audio thread \"{}\": {}", thread_name, e);
            log::write_crashlog();
        }
    }

    log!("Created audio threads");
}

pub(crate) fn restart() {
    thread::Builder::new()
        .name("audio_restart".to_string())
        .spawn(|| {
            log!("Restarting audio threads");

            unsafe {
                reset_volume();

                stop_audio.store(true, Ordering::SeqCst);

                loop {
                    if audio_threads_running.load(Ordering::SeqCst) == 0 {
                        break;
                    }

                    std::thread::sleep(std::time::Duration::from_millis(50));
                }
            }
            
            start();
        })
        .expect("Failed to spawn audio restart thread");
}