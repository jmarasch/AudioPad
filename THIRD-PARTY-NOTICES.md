# Third-party notices

AudioPad is distributed with the following components. Each remains under its own licence and
copyright.

## libVLC — LGPL-2.1-or-later

Copyright © VideoLAN and its contributors.

The Windows and Android packages bundle libVLC's native libraries. The Linux package does not:
it depends on the distribution's own `libvlc5`, installed through the package manager.

libVLC is used **as a dynamically linked shared library**, and is not modified. As the LGPL
requires, it can be replaced with a compatible build of the same version by substituting the
`libvlc` shared libraries alongside the application.

- Source: https://code.videolan.org/videolan/vlc
- Licence: https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html

## LibVLCSharp — LGPL-2.1-or-later

Copyright © VideoLAN and its contributors. The .NET bindings for libVLC, used unmodified.

- Source: https://code.videolan.org/videolan/LibVLCSharp

## Avalonia — MIT

Copyright © The Avalonia Project. The cross-platform UI framework.

- Source: https://github.com/AvaloniaUI/Avalonia

## CommunityToolkit.Mvvm — MIT

Copyright © .NET Foundation and Contributors. MVVM source generators.

- Source: https://github.com/CommunityToolkit/dotnet

## Inter typeface — SIL Open Font License 1.1

Copyright © The Inter Project Authors. Bundled via `Avalonia.Fonts.Inter`.

- Source: https://github.com/rsms/inter

## Xamarin.AndroidX libraries — Apache-2.0

Copyright © The Android Open Source Project. Android support libraries, in the Android package
only.

## .NET runtime — MIT

Copyright © .NET Foundation and Contributors. The Windows and Linux packages are self-contained
and include the runtime, so neither requires .NET to be installed.

- Source: https://github.com/dotnet/runtime
