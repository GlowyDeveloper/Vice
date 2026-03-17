fn main() {
    cc::Build::new()
        .cpp(true)
        .file("src/audio/audio.cpp")
        .include("src/audio")
        .compile("audio");

    cc::Build::new()
        .cpp(true)
        .file("src/performance/performance.cpp")
        .compile("performance");
    
    cc::Build::new()
        .cpp(true)
        .file("src/ui/opener.cpp")
        .compile("open_ui");

    println!("cargo:rustc-link-lib=ole32");
    println!("cargo:rustc-link-lib=mfplat");
    println!("cargo:rustc-link-lib=mfreadwrite");
    println!("cargo:rustc-link-lib=mfuuid");
    println!("cargo:rustc-link-lib=wmcodecdspuuid");
    println!("cargo:rustc-link-lib=user32");

    winres::WindowsResource::new()
        .set_icon("icons/icon.ico")
        .compile()
        .unwrap();
}