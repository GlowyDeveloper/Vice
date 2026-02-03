# Contributing

## Requirements

To work on this project, you need to have installed [Flutter](https://docs.flutter.dev/get-started/install?_gl=1*h6bu5u*_ga*MTg5MDAyODE1OS4xNzUzMTgwMzIy*_ga_04YGWK0175*czE3NTMzNTExMjYkbzIkZzAkdDE3NTMzNTExMjYkajYwJGwwJGgw), [Rust](https://www.rust-lang.org/learn/get-started) and a C++ compiler, such as [MSVC](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170#latest-supported-redistributable-version) or [GCC](https://gcc.gnu.org/).

## Info

### Flutter

Everything for Flutter side of things is in the flutter sub-directory. All the files are in the lib/ sub-directory. To build, run `flutter build web` while being in the flutter sub-directory. This will build the app in build/web sub-directory. This will create loads of files to work, this will be put into `build/web`. Tauri is made to use this directory so no need to copy and paste the files somewhere else.

### C++

The files are automatically compilled by `cc-rs` when running `cargo run`. For reasons, Vice tries to not use external libs in C++. To create a C++ file, put the C++ file in an appropriate spot, and use add this template in `build.rs`

```rust
cc::Build::new()
    .cpp() //If the file is a C++ or C file
    .file() //The path to the C/C++ file
    .compile(); //What rust will classify the file as
```
### **NOTE:**
If you are using a C++ file, make sure to wrap all your functions like this:
```cpp
//Includes

//Util functions

extern "C" {
    //Functions called by Rust
}
```

## Help

### Flutter showing an old version

This is most likely for tauri using an outdated cache. You can check by going into `flutter/build/web` and running `python -m http.server`. This will make a local host at `http://localhost:8000`. If this is showing what the code should show, go to `C:/Users/<YourUser>/AppData/Roaming/Vice/Cache` and delete it. If it's still not working check index.html and see if contains `<base href="./">`, if it's not, replace the current `base href` with that. If it **STILL** doesn't work, I have no clue what it can be. If the localhost isn't showing what you expect, check if your code is saved correctly outside of your IDE (in Notepad or a similar text-editor).

### C/C++ code not updating

Majority of the time, `cc-rs` doesn't rebuild the C/C++ code every build. `cc-rs` only rebuilds the C++ code when any file of the rust code has been modified. To force a rebuild, add or remove an empty line into the `build.rs` file. When you build again, and **STILL** hasn't updated, check if your code is correctly saved in a different IDE (in Notepad or a similar text-editor).
