# AudioPad

A cross-platform soundboard: a grid of buttons, each mapped to an audio clip, built for firing
sound cues live — at a table, on stage, or wherever a clip needs to land on time.

Runs on **Windows**, **Linux**, and **Android** from one shared codebase.

## What it does

- **Latch** pads play once; press again to cut them off. **Loop** pads play until stopped.
- Many pads can play at once, each at its own volume.
- A playing pad lights up and shows **restart / stop / pause** as large targets, with elapsed time
  for loops and a countdown for one-shots.
- **Pages** hold separate grids, navigated as an endless carousel — swipe, use the arrows, or
  pinch to zoom out to the page manager.
- Grid size is per page, from 1×1 to 8×8.
- Pads take four colours — idle and playing, each with its own hover shade — set per page and
  overridable per pad.
- **Edit mode** separates arranging from performing: with it on, a tap opens a pad's settings and
  a drag rearranges the board; with it off, taps only play. Nothing can be reconfigured by
  accident mid-performance.
- Clips and icons are **imported into the app's own library**, so a board doesn't break when you
  reorganise your folders, and behaves the same on every platform.
- **Export and import** a page or a whole setup as a single `.audiopad` file, media included —
  build a board on the desktop and carry it to the tablet.

## Install

Grab the latest build from [Releases](https://github.com/jmarasch/AudioPad/releases).

### Windows

Download `AudioPad-1.0.0-windows-x64.zip`, unzip it anywhere, run `AudioPad.exe`.

Needs the [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). If it isn't
installed, Windows says so and links to the download when you first run the app. libVLC is
included in the package.

Windows SmartScreen will warn about an unrecognised publisher, since the build isn't
code-signed. Choose *More info* → *Run anyway*.

### Linux (Debian / Ubuntu)

```sh
sudo apt install ./audiopad_1.0.0_amd64.deb
```

The package depends on your distribution's `libvlc5`, which apt will pull in. The .NET runtime is
bundled — .NET 10 isn't in the Debian or Ubuntu archives, so requiring it would mean adding
Microsoft's package feed first. AudioPad then appears in your application menu.

### Android (sideload)

Download `AudioPad-1.0.0.apk` and open it on the device. You'll need to allow installation from
unknown sources when prompted. Requires **Android 15 (API 35)** or newer.

## Building from source

```sh
dotnet build                        # everything
dotnet run --project src/AudioPad.Desktop   # run the desktop app
dotnet test                         # unit tests
./package.sh                        # distributable packages for all three platforms
```

**Prerequisites:** the [.NET 10 SDK](https://dotnet.microsoft.com/download). Android builds also
need the `android` workload, a JDK 21, and the Android SDK. Running the desktop app from source on
Linux needs libVLC present (`sudo apt install libvlc5`); the packaged builds handle this
themselves.

## How it's put together

`Core` holds the domain model and persistence with no UI or platform dependencies. `Audio`
isolates libVLC behind an interface. `UI` is one shared Avalonia project, and `Desktop` and
`Android` are thin heads that wire up the platform entry point.

[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) covers the structure and the reasoning behind the
decisions that aren't obvious from the code — particularly around gesture handling and audio,
where the intuitive approach turned out to be wrong.

## Licence

MIT — see [LICENSE](LICENSE).

AudioPad links against **libVLC** and **LibVLCSharp** (LGPL-2.1-or-later, © VideoLAN), used
unmodified as dynamically linked libraries. Full attribution for every bundled component is in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
