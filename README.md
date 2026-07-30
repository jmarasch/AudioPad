# AudioPad

A cross-platform soundboard: a user-configurable grid of buttons, each mapped to an audio clip.

- **Latch** mode: press to play through once; press again while playing to interrupt/stop.
- **Loop** mode: press to loop continuously until pressed again.
- Multiple pads can play at once, each with its own volume.
- A pad shows lit while its clip is playing; double-tap a pad to configure its file, mode,
  volume, icon, and label.
- Grid size (rows/columns) is user-configurable.
- Targets Linux, Windows, and Android (15+) from one shared codebase.

This repository is currently a **project scaffold**: the domain model, project structure, and a
placeholder UI are in place and building; the real audio engine and per-pad config dialog are the
next milestones — see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full breakdown and
what's next.

## Stack

- **C# / .NET 10** — the current LTS.
- **[Avalonia UI](https://avaloniaui.net/)** (MIT) — cross-platform XAML/MVVM UI framework;
  unlike .NET MAUI, it targets Linux desktop as a first-class platform alongside Windows and
  Android.
- **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)** (MIT, Microsoft) —
  `ObservableObject`/`RelayCommand` source generators, to keep ViewModels boilerplate-free.
- **[LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp)** (LGPL-2.1, VideoLAN — the VLC
  team) — the audio engine (planned; not yet implemented). Chosen because it can run many
  independent, concurrently-playing clips with per-clip volume, cross-platform, which plain
  platform media APIs don't do uniformly across Windows/Linux/Android.

See the plan's library vetting notes for adoption/maintenance signals checked before adopting
each of these.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Avalonia templates: `dotnet new install Avalonia.Templates`
- For Android builds: `dotnet workload install android`, a JDK (11+), and the Android SDK
  (`dotnet build -t:InstallAndroidDependencies -p:AcceptAndroidSDKLicenses=True
  -p:AndroidSdkPath=<path> -p:JavaSdkDirectory=<path>` will provision it)
- **Linux only, at runtime:** once the audio engine lands, playing clips will require libVLC to
  be installed system-wide (`sudo apt install libvlc5` or a full VLC install) — unlike Windows
  and Android, there's no NuGet package bundling libVLC's native binaries for Linux.

## Building & running

```bash
# Whole solution
dotnet build AudioPad.slnx

# Run the desktop app (Windows/Linux)
dotnet run --project src/AudioPad.Desktop

# Unit tests (Core domain logic — no UI/audio dependencies)
dotnet test tests/AudioPad.Core.Tests

# Android (after the Android SDK/workload prerequisites above)
dotnet build src/AudioPad.Android -f net10.0-android
```

## Project layout

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full directory breakdown, the reasoning
behind the module split, and coding conventions used throughout the codebase.
