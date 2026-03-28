use std::sync::Mutex;
use chrono::Local;
use once_cell::sync::Lazy;

use crate::files;

pub(crate) static BUFFER: Lazy<Mutex<String>> = Lazy::new(|| Mutex::new(String::new()));

#[macro_export]
macro_rules! log {
    ($($arg:tt)*) => {{
        use std::fmt::Write as _;
        use crate::log::BUFFER;

        let s = format!($($arg)*);
        let mut buffer = BUFFER.lock().unwrap();
        write!(&mut buffer, "[INFO] {}\n--------------------------------------\n", s).unwrap();
        println!("{}", s);
    }};
}

#[macro_export]
macro_rules! warn {
    ($($arg:tt)*) => {{
        use std::fmt::Write as _;
        use crate::log::BUFFER;

        let s = format!($($arg)*);
        let mut buffer = BUFFER.lock().unwrap();
        write!(&mut buffer, "[WARN] {}\n--------------------------------------\n", s).unwrap();
        println!("{}", s);
    }};
}

#[macro_export]
macro_rules! error {
    ($($arg:tt)*) => {{
        use std::fmt::Write as _;
        use crate::log::BUFFER;

        let s = format!($($arg)*);
        let mut buffer = BUFFER.lock().unwrap();
        write!(&mut buffer, "[ERROR] {}\n--------------------------------------\n", s).unwrap();
        eprintln!("{}", s);
    }};
}

#[macro_export]
macro_rules! critical {
    ($($arg:tt)*) => {{
        use std::fmt::Write as _;
        use crate::log::BUFFER;

        let s = format!($($arg)*);
        let mut buffer = BUFFER.lock().unwrap();
        let backtrace = std::backtrace::Backtrace::capture();
        write!(&mut buffer, "[CRITICAL] {}\n--------------------------------------\nBacktrace:\n{}", s, backtrace).unwrap();
        eprintln!("{}", s);
        eprintln!("{}", backtrace)
    }};
}

pub(crate) fn write_crashlog() {
    let buffer = BUFFER.lock().unwrap();
    let now = Local::now();
    let formatted = now.format("%d-%m-%Y %H-%M").to_string();
    let path = files::crash_log_base().join(format!("{}.crash.log", formatted));
    std::fs::write(&path, buffer.as_bytes()).unwrap();
}

pub(crate) fn write_debuglog() {
    let args: Vec<String> = std::env::args().collect();

    if args.contains(&"--debug".to_string()) {
        let buffer = BUFFER.lock().unwrap();
        let now = Local::now();
        let formatted = now.format("%d-%m-%Y %H-%M").to_string();
        std::fs::write(&format!("{}.debug.log", formatted), buffer.as_bytes()).unwrap();
    }
}