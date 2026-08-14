# Releasing

`./package.sh` builds everything a release needs into `dist/`:

| File | Platform | Notes |
| --- | --- | --- |
| `AudioPad-<version>-windows-x64.zip` | Windows | Needs the .NET 10 Runtime installed |
| `audiopad_<version>_amd64.deb` | Debian/Ubuntu | Self-contained; apt pulls in `libvlc5` |
| `AudioPad-<version>.apk` | Android | Release-signed, sideloadable |

The version comes from `VERSION` in the environment, defaulting to the `<Version>` in
`Directory.Build.props`. Keep the two in step and bump both for a release.

## The Android signing key

The release keystore lives **outside the repository**:

```
~/.androidkeys/audiopad-release.keystore
~/.androidkeys/audiopad-release.password
```

**This key must sign every future update.** Android identifies an app by its signature, so an APK
signed with a different key will not install over an existing one — users would have to uninstall
first, losing their board. If the key is lost, that is permanent: there is no way to publish an
update to an existing install, and on Google Play the listing cannot be updated at all.

Back both files up somewhere durable and private. They are deliberately not in the repository, so
a fresh clone on another machine cannot build a release until they are copied across. Point
`AUDIOPAD_KEYSTORE` and `AUDIOPAD_KEYSTORE_PASSWORD_FILE` elsewhere if they live somewhere else.

To recreate one (only for a *new* app that has never shipped):

```sh
keytool -genkeypair -keystore audiopad-release.keystore -alias audiopad \
        -keyalg RSA -keysize 4096 -validity 10950 \
        -dname "CN=New Terra Studios, O=New Terra Studios, C=CA"
```

## What each desktop package carries

**Windows is framework-dependent.** The .NET runtime is roughly 72 MB uncompressed and Windows
users can install it themselves; the generated apphost tells them where to get it if it's missing.

**Linux is self-contained.** .NET 10 is not in the Debian or Ubuntu archives, so a
framework-dependent `.deb` would require adding Microsoft's package feed before it would install —
a worse first run than a larger download.

**Neither ships debug symbols.** SkiaSharp and HarfBuzz include native `.pdb` files in their NuGet
packages and publish copies them into the output: 100 MB of symbols nobody will use. `package.sh`
deletes them.

Two more details worth knowing before changing this:

- `VideoLAN.LibVLC.Windows` ships every Windows architecture and the build copies all of them, so
  a win-x64 publish arrives carrying arm64 and x86 copies of libVLC it can never load. `package.sh`
  deletes them; without that step the zip is far larger.
- Linux does **not** bundle libVLC. There is no NuGet package carrying its Linux natives, so the
  `.deb` depends on the distribution's `libvlc5` instead.

`InvariantGlobalization` is on for the desktop builds so they don't need the target machine's ICU
libraries present in order to start. AudioPad has nothing culture-sensitive to lose by it.

## Cutting a release

```sh
./package.sh
git tag -a vX.Y.Z -m "AudioPad vX.Y.Z"
git push origin vX.Y.Z
gh release create vX.Y.Z dist/* --title "AudioPad vX.Y.Z" --notes-file <notes>
```

## Not yet set up

- **No code signing on Windows**, so SmartScreen warns about an unrecognised publisher. Fixing it
  means buying a certificate.
- **No CI.** Releases are built from a working machine, which means the build depends on that
  machine's Android SDK and JDK being present.
- **APK, not AAB.** Google Play requires an Android App Bundle; the sideload APK here is not
  sufficient for a store listing.
