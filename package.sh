#!/usr/bin/env bash
# Builds the distributable packages for a release into dist/:
#
#   AudioPad-<version>-windows-x64.zip   needs the .NET 10 desktop runtime installed
#   audiopad_<version>_amd64.deb         self-contained; apt pulls in libvlc5
#   AudioPad-<version>.apk               release-signed, sideloadable
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

VERSION="${VERSION:-1.0.0}"
JAVA_SDK="${JAVA_SDK_DIRECTORY:-/usr/lib/jvm/java-21-openjdk-amd64}"
ANDROID_SDK="${ANDROID_SDK_ROOT:-$HOME/android-sdk}"

# Kept outside the repository: this key signs every future update, and losing it means the app can
# never be updated in place again.
KEYSTORE="${AUDIOPAD_KEYSTORE:-$HOME/.androidkeys/audiopad-release.keystore}"
KEYSTORE_PASSWORD_FILE="${AUDIOPAD_KEYSTORE_PASSWORD_FILE:-$HOME/.androidkeys/audiopad-release.password}"

rm -rf dist build
mkdir -p dist

# SkiaSharp and HarfBuzz ship native debug symbols in their NuGet packages, and publish copies them
# into the output — 100 MB of .pdb in a release build, more than the .NET runtime itself. They are
# only useful for debugging those libraries' own native code.
strip_debug_symbols() {
    find "$1" -name '*.pdb' -delete
}

echo "==> Windows (framework-dependent x64)"
# Not self-contained: Windows users can install the .NET runtime, and the generated apphost tells
# them where to get it if it's missing. That keeps ~72 MB of runtime out of the download.
dotnet publish src/AudioPad.Desktop -c Release -r win-x64 --self-contained false \
    -p:Version="$VERSION" -o build/windows >/dev/null

# VideoLAN.LibVLC.Windows ships every Windows architecture and the build copies all of them, so a
# win-x64 publish arrives carrying arm64 and x86 copies of libVLC it can never load — together
# more than twice the size of the rest of the package. Drop them.
find build/windows/libvlc -mindepth 1 -maxdepth 1 -type d ! -name win-x64 -exec rm -rf {} +

strip_debug_symbols build/windows

cp LICENSE THIRD-PARTY-NOTICES.md build/windows/
(cd build/windows && zip -qr "../../dist/AudioPad-$VERSION-windows-x64.zip" .)

echo "==> Linux (.deb, self-contained amd64)"
# Self-contained deliberately: .NET 10 is not in the Debian or Ubuntu archives, so a
# framework-dependent package would make people add Microsoft's feed before it would install.
dotnet publish src/AudioPad.Desktop -c Release -r linux-x64 --self-contained true \
    -p:Version="$VERSION" -o build/linux >/dev/null
strip_debug_symbols build/linux

DEB=build/deb
install -d "$DEB/DEBIAN" "$DEB/opt/audiopad" "$DEB/usr/bin" \
           "$DEB/usr/share/applications" "$DEB/usr/share/icons/hicolor/256x256/apps" \
           "$DEB/usr/share/doc/audiopad"
cp -r build/linux/. "$DEB/opt/audiopad/"
chmod 755 "$DEB/opt/audiopad/AudioPad.Desktop"
cp LICENSE "$DEB/usr/share/doc/audiopad/copyright"
cp THIRD-PARTY-NOTICES.md "$DEB/usr/share/doc/audiopad/"
cp src/AudioPad.UI/Assets/Icon.png "$DEB/usr/share/icons/hicolor/256x256/apps/audiopad.png"

# The app is installed under /opt, so put something on PATH that points at it.
cat > "$DEB/usr/bin/audiopad" <<'LAUNCHER'
#!/bin/sh
exec /opt/audiopad/AudioPad.Desktop "$@"
LAUNCHER
chmod 755 "$DEB/usr/bin/audiopad"

cat > "$DEB/usr/share/applications/audiopad.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=AudioPad
Comment=Cross-platform soundboard
Exec=/opt/audiopad/AudioPad.Desktop
Icon=audiopad
Terminal=false
Categories=AudioVideo;Audio;
DESKTOP

# libvlc is the one thing not bundled on Linux: there is no NuGet package carrying its native
# libraries for Linux, and the distribution's own copy is the supported way to get it.
cat > "$DEB/DEBIAN/control" <<CONTROL
Package: audiopad
Version: $VERSION
Section: sound
Priority: optional
Architecture: amd64
Depends: libvlc5 | vlc, libc6, libicu72 | libicu74 | libicu76 | libicu-dev
Maintainer: New Terra Studios <jakob@newterrastudios.com>
Homepage: https://github.com/jmarasch/AudioPad
Description: Cross-platform soundboard
 A grid of buttons, each mapped to an audio clip, for firing sound cues live.
 Supports one-shot and looping pads, multiple pages, per-pad volume and colours,
 and importing or exporting boards as a single portable file.
CONTROL

fakeroot dpkg-deb --build "$DEB" "dist/audiopad_${VERSION}_amd64.deb" >/dev/null

echo "==> Android (release-signed APK)"
if [ ! -f "$KEYSTORE" ]; then
    echo "No release keystore at $KEYSTORE — see docs/RELEASING.md" >&2
    exit 1
fi
KEYSTORE_PASSWORD="$(cat "$KEYSTORE_PASSWORD_FILE")"

dotnet publish src/AudioPad.Android -c Release \
    -p:Version="$VERSION" \
    -p:ApplicationDisplayVersion="$VERSION" \
    -p:AndroidSdkDirectory="$ANDROID_SDK" \
    -p:JavaSdkDirectory="$JAVA_SDK" \
    -p:AndroidKeyStore=true \
    -p:AndroidSigningKeyStore="$KEYSTORE" \
    -p:AndroidSigningStorePass="$KEYSTORE_PASSWORD" \
    -p:AndroidSigningKeyAlias=audiopad \
    -p:AndroidSigningKeyPass="$KEYSTORE_PASSWORD" \
    -o build/android >/dev/null
cp build/android/com.newterrastudios.audiopad-Signed.apk "dist/AudioPad-$VERSION.apk"

echo
echo "Packages in dist/:"
ls -1sh dist/
