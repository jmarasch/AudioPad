#!/usr/bin/env bash
# Builds, installs and starts the Debug APK on the attached device, then tails the gesture trace
# out of logcat. Gesture behaviour can only be judged on a touch device (see docs/ARCHITECTURE.md),
# so this is the loop for working on it: run, perform one gesture, read what the trace says.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

export PATH="$PATH:$HOME/android-sdk/platform-tools"
PACKAGE=com.newterrastudios.audiopad
APK=src/AudioPad.Android/bin/Debug/net10.0-android/$PACKAGE-Signed.apk

if [ -z "$(adb devices | sed -n '2p')" ]; then
    echo "No device attached. Plug the tablet in, unlock it, and accept the USB-debugging prompt." >&2
    exit 1
fi

dotnet build src/AudioPad.Android -c Debug \
    -p:AndroidSdkDirectory="${ANDROID_SDK_ROOT:-$HOME/android-sdk}" \
    -p:JavaSdkDirectory="${JAVA_SDK_DIRECTORY:-/usr/lib/jvm/java-21-openjdk-amd64}"

# Debug keystores differ per machine, so a build signed on another machine can't be upgraded in
# place — Android rejects it as UPDATE_INCOMPATIBLE. Fall back to a reinstall, which wipes the
# app's saved setup.
if ! adb install -r "$APK"; then
    echo "Install failed; uninstalling first (this wipes the saved setup on the device)."
    adb uninstall "$PACKAGE" || true
    adb install "$APK"
fi

adb logcat -c
adb shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null

echo
echo "Tracing gestures — perform one gesture, then Ctrl-C."
adb logcat -s DOTNET:* | grep --line-buffered -E "\[gesture\]|Unhandled|Exception"
