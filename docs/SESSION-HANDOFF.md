# Session handoff — 2026-07-31 (Windows laptop → Linux desktop)

Written at the end of a long session on the Windows laptop. Claude Code transcripts live in
`~/.claude/projects/` and do **not** sync, so this file is the record. Everything below is in the
working tree that Syncthing carries over.

## Git state

Last commit is `bc634a5` ("Normalize line endings to LF"). **Everything since is uncommitted** —
a substantial body of work. It builds clean (Desktop + Android Release, 0 warnings) and the test
suite passes 13/13. No rollback point exists for any of it, so committing early on the Linux side
is worth doing.

Uncommitted files:

```
 M src/AudioPad.Core/Models/Page.cs
 M src/AudioPad.UI/App.axaml
 M src/AudioPad.UI/Controls/PadButton.axaml.cs
 M src/AudioPad.UI/Controls/PageTile.axaml
 M src/AudioPad.UI/Controls/PageTile.axaml.cs
 M src/AudioPad.UI/Interactions/HoldDragReorderBehavior.cs
 M src/AudioPad.UI/ViewModels/MainWindowViewModel.cs
 M src/AudioPad.UI/ViewModels/PadViewModel.cs
 M src/AudioPad.UI/ViewModels/PageViewModel.cs
 M src/AudioPad.UI/Views/MainView.axaml
 M src/AudioPad.UI/Views/MainView.axaml.cs
 M src/AudioPad.UI/Views/PadConfigView.axaml
 M src/AudioPad.UI/Views/PageOverviewView.axaml
 M tests/AudioPad.Core.Tests/PageTests.cs
?? src/AudioPad.UI/ViewModels/CarouselSlot.cs
?? src/AudioPad.UI/ViewModels/PageConfigViewModel.cs
?? src/AudioPad.UI/Views/PageConfigView.axaml
?? src/AudioPad.UI/Views/PageConfigView.axaml.cs
```

## What was completed and verified

- **`PageConfigView`** — per-page settings (title, header colour, grid size). Reachable from the
  overview's Settings button. Verified working on Desktop and on the tablet.
- **Page overview is reachable at all** — it existed but was never mounted and no command opened
  it. Now mounted in `MainView` behind a **Pages** button, which replaced the temporary `+ Page`.
- **Theme-aware overlays** — `App.axaml` gained per-variant resources (`OverlayScrimBrush`,
  `OverlayPanelBrush`, `OverviewBackgroundBrush`, `OverlayWarningBrush`). Previously the overlays
  hardcoded dark colours while inheriting the theme's foreground, so on a light-themed Android
  device the config dialog was black-on-black. `RequestedThemeVariant="Default"` is deliberate —
  the app follows the system theme; do not pin it.
- **`Page.Resize` rebuilds row-major** — it used to append new cells at the end, which scattered
  pads across wrong grid positions on grow. Two tests cover this.
- **Reordering is insert-and-shift, not swap** — `MovePad`/`MovePage` remove and re-insert so
  items slide along ("rearranging notes in a notebook"), replacing the old `SwapPads`/`SwapPages`.
- **Drag-reorder works on Android** (pads). Took four attempts; see the war story below.
- **Page overview is a vertical list**, not a wrap grid. `PageTile` is now a full-width row.
- **`CarouselSlot`** — each carousel slide is its own object. The sentinel slots used to reuse the
  same `PageViewModel` instance at three indices; `ItemsControl` maps containers *by item*, so the
  duplicates corrupted container reuse a little more with each page reorder until pages rendered
  on top of each other. Restarting cleared it because containers were rebuilt.
- **`MovePage` no longer rebuilds the carousel collection** — `RebuildCarouselItems` does
  `Clear()` + re-add, whose Reset made the bound Carousel drop its `SelectedIndex` (pages stopped
  responding to navigation after a rearrange). It now mirrors the reorder as a `Move`.

## Known broken / open

1. **Page-tile dragging in the overview is unreliable** — bounces, skips, thrashes. The reorder
   fires on target *change*, but when two rows alternate (A→B, B→A) the target changes every
   event, so the existing guard doesn't damp it. A midpoint-hysteresis attempt **made it worse**
   (blocked all reorders) and was reverted — during a drag the pointer sits over the dragged row,
   so requiring it to reach the target's centre almost never passes. Needs a different damping
   rule, most likely based on direction of travel rather than absolute position.
2. **Pinch-to-open the page manager does not work.** `Avalonia.Input.Gestures` is *internal* in
   the Avalonia version in use, so `PinchEvent` is unreachable from app code. A manual two-pointer
   implementation exists in `MainView.axaml.cs` but its handlers are **deliberately not wired** —
   they tunnel on the same pointer events as the drag gesture and appeared to interfere. Diagnose
   on-device before re-enabling.
3. **Animated reflow** — items jump between slots during a reorder. Not started.
4. **Export/import UI** — `SetupArchive` is fully implemented and tested in Core, but nothing in
   the UI calls it.
5. **`README.md` is stale** — still describes the repo as "a project scaffold" with the audio
   engine "planned; not yet implemented", though `LibVlcAudioEngine` is committed and per-pad
   config is done.

## The lesson that actually mattered

Three separate gesture bugs were misdiagnosed by reproducing on Desktop and extrapolating.
**Desktop uses mouse input with no competing pan/swipe recogniser, so it cannot reproduce the
Android touch behaviour** — it showed muted or entirely different symptoms every time.

What worked: adding `Console.WriteLine` diagnostics (they reach logcat via the `DOTNET` tag),
deploying, having Jakob perform one gesture, then reading `adb logcat`. That found the real cause
in one pass each time, after several wrong guesses:

- Reorder fired on *every* pointer move (~60/sec), so items ping-ponged between two slots.
- `PadButton.OnMoveRequested` resolved the owning page by walking the visual tree; reordering
  detaches that control, so from the second move on the lookup returned `null` and `?.` silently
  discarded the call. The owner is now captured once at hold-start and passed in.

Instrument the device first. Don't ship two speculative gesture changes in one build — when both
break, neither can be isolated.

## Environment notes (Windows laptop, for when you come back)

- `.NET SDK 10.0.100` pinned via `global.json`; JDK 21 at
  `C:\Program Files\Microsoft\jdk-21.0.12.8-hotspot`; Android SDK at `C:\Users\jakob\android-sdk`.
- dotnet is in **`--update-mode manifests`** (loose manifests, not workload-set mode) because
  workload set 10.0.110 shipped missing `tvos` manifests that broke resolution for *every*
  project. Do not re-enable workload-set mode there.
- **Debug keystores differ per machine**, so sideloading the same app from the laptop and from the
  Linux box requires an uninstall (which wipes app data) — `INSTALL_FAILED_UPDATE_INCOMPATIBLE`.
  Copying `~/.local/share/dotnet/android/debug.keystore` from Linux to
  `%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore` on the laptop would make `install -r`
  work in place from either machine. Worth doing.
- Tablet used for testing: `9491G`, Android 15 / API 35 / arm64-v8a, serial `987800390CE1344`.
- Emulators `AudioPad_API35` and `AudioPad_API36` exist on the laptop but were found too slow to
  work with; physical device is preferred.
