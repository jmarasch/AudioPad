# Architecture

## Why this split

`Core` has zero UI or platform dependencies, so the setup/page/pad domain model and its
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
│   ├── AudioPad.Core/          # models, IAudioEngine, persistence — no UI, no platform code
│   │   ├── Models/
│   │   │   ├── PlaybackMode.cs     # enum: Latch, Loop
│   │   │   ├── PadConfig.cs        # one button's saved config: file, mode, volume, label, icon, position
│   │   │   ├── Page.cs             # one titled, themed grid: rows, columns, the pads in them
│   │   │   └── Setup.cs            # the whole saved state: an ordered list of pages
│   │   ├── Playback/
│   │   │   ├── IAudioEngine.cs             # Play/Stop/SetVolume/IsPlaying + PlaybackStateChanged event
│   │   │   ├── PadRuntimeState.cs          # live IsPlaying, separate from saved PadConfig
│   │   │   └── PlaybackStateChangedEventArgs.cs
│   │   └── Persistence/
│   │       ├── AppStorage.cs           # app-private data dirs under LocalApplicationData
│   │       ├── SetupRepository.cs      # load/save the whole Setup as JSON
│   │       └── SetupArchive.cs         # export/import a Page or Setup as a portable zip + media
│   │
│   ├── AudioPad.Audio/         # IAudioEngine implementation backed by LibVLCSharp (libVLC)
│   │   └── LibVlcAudioEngine.cs    # one LibVLC instance, one MediaPlayer per playing pad
│   │
│   ├── AudioPad.UI/            # Shared Avalonia app: Views, ViewModels, Controls (MVVM)
│   │   ├── NullAudioEngine.cs      # no-op IAudioEngine for the XAML previewer/design time
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs  # owns the Setup, exposes PageViewModels, tracks the current
│   │   │   │                           # page, ActiveConfig overlay, and overview selection
│   │   │   ├── PageViewModel.cs        # one page: title, theme color, its PadViewModels
│   │   │   ├── PadViewModel.cs         # one pad: label, icon, lit state driven by IAudioEngine
│   │   │   └── PadConfigViewModel.cs   # working copy of one pad's editable fields (file/mode/volume/…)
│   │   ├── Views/
│   │   │   ├── MainView.axaml          # renders the current page's grid + the config overlay
│   │   │   ├── PadConfigView.axaml     # double-tap config UI, shown as an in-place overlay
│   │   │   └── PageOverviewView.axaml  # all pages as tiles: add/delete/reorder/navigate
│   │   ├── Controls/
│   │   │   ├── PadButton.axaml         # one grid button, styled "lit" when playing, double-tap to configure
│   │   │   └── PageTile.axaml          # one page's tile in the overview, double-tap to open it
│   │   └── Interactions/
│   │       ├── HoldDragReorderBehavior.cs  # hold-then-drag-to-swap, shared by pads and page tiles
│   │       └── VisualTreeHelpers.cs        # ancestor-DataContext lookup used by the above
│   │
│   ├── AudioPad.Desktop/       # Entry point for Windows + Linux — composition root wires LibVlcAudioEngine
│   └── AudioPad.Android/       # Entry point for Android — composition root wires LibVlcAudioEngine
│
└── tests/
    └── AudioPad.Core.Tests/    # xUnit — setup/page serialization, archive round-trips, model logic
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

## `EndReached` threading and the config overlay

`LibVlcAudioEngine`'s `EndReached` event fires on a libVLC-internal thread; calling `Stop()`
synchronously from inside that callback deadlocks (`Stop()` blocks until VLC's internal threads
join). The handler hops off that thread first via `ThreadPool.QueueUserWorkItem`, then does the
Loop-restart-or-Latch-teardown under the engine's lock. `PadViewModel` in turn marshals the
resulting `IsLit` update onto the UI thread with `Dispatcher.UIThread.Post`, since the event can
arrive from that same background thread.

The double-tap config UI (`PadConfigView`) is a `UserControl` shown as an in-place overlay in
`MainView`, not a separate `Window`: Avalonia's `Window.ShowDialog` requires an owner `Window`,
which doesn't meaningfully exist under Android's single-view activity lifetime. An overlay whose
visibility is driven by `MainWindowViewModel.ActiveConfig` being non-null works identically on
Desktop and Android.

## Pages, and why `Setup` wraps them

A `Setup` is an ordered list of `Page`s, each its own titled, themed grid with independent
`Rows`/`Columns`. Pages are navigated as an endless carousel in `MainView`, or managed as a whole
in `PageOverviewView`. Keeping the grid dimensions on `Page` rather than globally means one setup
can mix a 4×4 board with a 6×8 one, and `Page.Resize` preserves every pad whose position is still
in bounds instead of rebuilding the grid from scratch.

`PadConfig` stores its own `Row`/`Column` rather than living in a 2-D array, so reordering is a
swap of two positions and serialization stays a flat list.

## `SetupArchive` is stream-based, not path-based

Export/import bundles copies of every referenced audio and icon file into a zip, so an exported
page opens correctly on a *different machine* — not just a different path on the same one. Media
is de-duplicated per distinct source path, and name collisions get a ` (n)` suffix.

The API takes `Stream` rather than a file path deliberately: on Android both the export
destination and the import source are typically Storage Access Framework `content://` handles, not
plain files, so the UI layer always drives this through `IStorageFile.OpenWriteAsync()` /
`OpenReadAsync()`. Imported media is extracted into app-private storage via `AppStorage`, which is
also where `PadConfigView` copies picked files to — for the same reason, a `content://` URI isn't
something native playback code can open directly.

## Hold-drag reordering instead of `DragDrop`

`HoldDragReorderBehavior<TItem>` powers reordering for both pads and page tiles. It's built on
Avalonia's native long-press gesture rather than `Avalonia.Input.DragDrop` because it needs full
control over the in-place drag visual (a plain `TranslateTransform` follow) instead of an OS-level
drag cursor, and needs to behave identically for a fixed grid and a reflowing wrap panel.

Two non-obvious details are load-bearing: the pointer is captured on *every* press so `PointerMoved`
keeps arriving once the pointer leaves the item's own bounds, and the handlers are registered with
`handledEventsToo: true` because `Button` marks its own pointer events handled, which would
otherwise stop a plain `+=` subscription from ever firing.

## What's next

1. **`PageConfigView`**: a per-page settings surface for title, theme color, and grid size
   (`Page.Resize` already backs the last one). The overview's Settings button is wired to nothing
   until this exists — see the TODO in `PageOverviewView.axaml`.
2. **Replace the temporary "+ Page" button** in `MainView` with the Add Page tile in the overview
   — see the TODO in `MainView.axaml`.
