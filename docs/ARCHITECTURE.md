# Architecture

## Why this split

`Core` has zero UI or platform dependencies, so the grid/pad domain model and profile
persistence are trivially unit-testable and reusable everywhere. `Audio` isolates the one
component with a messy native-binary story (libVLC) behind `IAudioEngine`, so it could be
swapped out later without touching `UI` or `Core`. `UI` is one shared Avalonia project (views,
view models, controls); `Desktop` and `Android` are thin heads that wire up the platform entry
point and pull in the right native libVLC package.

```
AudioPad/
├── AudioPad.slnx
├── Directory.Packages.props   # central NuGet package versions for every project
├── src/
│   ├── AudioPad.Core/          # models, IAudioEngine, profile persistence — no UI, no platform code
│   │   ├── Models/
│   │   │   ├── PlaybackMode.cs     # enum: Latch, Loop
│   │   │   ├── PadConfig.cs        # one button's saved config: file, mode, volume, label, icon, position
│   │   │   └── GridProfile.cs      # rows, columns, the pads in them
│   │   ├── Playback/
│   │   │   ├── IAudioEngine.cs             # Play/Stop/SetVolume/IsPlaying + PlaybackStateChanged event
│   │   │   ├── PadRuntimeState.cs          # live IsPlaying, separate from saved PadConfig
│   │   │   └── PlaybackStateChangedEventArgs.cs
│   │   └── Persistence/
│   │       └── ProfileRepository.cs    # load/save GridProfile as JSON
│   │
│   ├── AudioPad.Audio/         # IAudioEngine implementation backed by LibVLCSharp (libVLC)
│   │                           # NOT YET IMPLEMENTED — next milestone, see "What's next" below
│   │
│   ├── AudioPad.UI/            # Shared Avalonia app: Views, ViewModels, Controls (MVVM)
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs  # owns the current GridProfile, exposes it as PadViewModels
│   │   │   └── PadViewModel.cs         # one pad: label, lit state, press command
│   │   ├── Views/
│   │   │   └── MainView.axaml          # renders the UniformGrid of pads (shared by Desktop + Android)
│   │   └── Controls/
│   │       └── PadButton.axaml         # one grid button, styled "lit" when playing
│   │
│   ├── AudioPad.Desktop/       # Entry point for Windows + Linux
│   └── AudioPad.Android/       # Entry point for Android
│
└── tests/
    └── AudioPad.Core.Tests/    # xUnit — profile serialization, grid/pad model logic
```

## Coding conventions

These apply to all future work, not just the initial scaffold:

- Nullable reference types + implicit usings enabled solution-wide.
- One type per file; file-scoped namespaces.
- Every public type/method gets an XML doc `///` comment stating *purpose*, not restating the
  signature.
- Methods stay short and single-purpose — prefer several small, named private helper methods
  over one long procedure, so the call sequence itself documents the flow.
- Strict MVVM: `Views` contain layout + bindings only; all logic lives in `ViewModels`/`Core`/
  `Audio` so it's testable without a UI.
- `CommunityToolkit.Mvvm` for `ObservableObject`/`RelayCommand` source generators, to keep
  ViewModels free of boilerplate.
- NuGet package versions are pinned centrally in `Directory.Packages.props` (Central Package
  Management) — individual `.csproj` files reference packages by name only, no `Version=`.

## Known platform note: libVLC on Linux

`VideoLAN.LibVLC.Windows` and `VideoLAN.LibVLC.Android` bundle libVLC's native binaries via
NuGet, so Windows and Android builds are self-contained. There is no equivalent NuGet package for
Linux — on Linux, libVLC must already be installed on the machine (`sudo apt install libvlc5` or
a full VLC install). This is a runtime dependency for end users, not just a dev-machine
requirement; it'll need to be called out wherever AudioPad is distributed for Linux.

## What's next (not in this scaffold)

1. **`AudioPad.Audio` — the real audio engine.** Implement `IAudioEngine` on top of
   `LibVLCSharp`: one `LibVLC` instance, one `MediaPlayer` per currently-playing pad (so multiple
   clips can run at once), Latch (press-to-interrupt) vs. Loop (restart on `EndReached`) behavior,
   and per-pad volume. LibVLC's `EndReached` event fires on a libVLC-internal thread, so care is
   needed there (don't block/call `Stop()` synchronously from inside the callback).
2. **Wire `MainWindowViewModel`/`PadViewModel` to `IAudioEngine`** instead of the current local
   `IsLit` toggle placeholder, and to `ProfileRepository` for loading/saving the active profile.
3. **Double-tap-to-configure**: a `PadConfigWindow` + `PadConfigViewModel` for setting a pad's
   audio file (file picker), mode, volume, label, and icon.
4. **User-configurable grid size**: a settings surface for changing `GridProfile.Rows`/`Columns`
   at runtime, preserving existing pad configs where positions still exist.
