#!/usr/bin/env bash
# Builds a distributable copy of AudioPad for each platform into bin/{Linux,Windows,Android}/,
# ready to zip up (Linux/Windows) or install (Android) on another machine.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

rm -rf bin/Linux bin/Windows bin/Android
mkdir -p bin/Android

dotnet publish src/AudioPad.Desktop -c Release -r linux-x64 --self-contained false -o bin/Linux
dotnet publish src/AudioPad.Desktop -c Release -r win-x64 --self-contained false -o bin/Windows

dotnet build src/AudioPad.Android -c Release \
  -p:AndroidSdkDirectory="${ANDROID_SDK_ROOT:-$HOME/android-sdk}" \
  -p:JavaSdkDirectory="${JAVA_SDK_DIRECTORY:-/usr/lib/jvm/java-21-openjdk-amd64}"
cp src/AudioPad.Android/bin/Release/net10.0-android/com.newterrastudios.audiopad-Signed.apk bin/Android/AudioPad.apk

echo
echo "Packaged (framework-dependent: target machines need the .NET 10 runtime installed):"
echo "  bin/Linux/AudioPad.Desktop    (also needs libvlc5 installed on the target machine)"
echo "  bin/Windows/AudioPad.Desktop.exe"
echo "  bin/Android/AudioPad.apk      (adb install bin/Android/AudioPad.apk)"
