# Install

## What you need

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- The MAUI Android workload: `dotnet workload install maui-android`
- A JDK, 17 or later
- The Android SDK

Visual Studio or the Android Studio SDK manager installs the last two. If the
build cannot find them, set `JAVA_HOME` and `ANDROID_HOME`.

Nothing is needed on the phone beyond Android 8 or later.

## Build the APK

```bash
dotnet build src/MusicPlayer.App -f net10.0-android
```

Output:

```
src/MusicPlayer.App/bin/Debug/net10.0-android/com.kartikeyan.musicplayer-Signed.apk
```

Around 17 MB. It is signed with the local debug key, which is enough to install
and use. Use the Release build below if you want it smaller and faster.

## Put it on the phone

Over USB, with developer options and USB debugging enabled:

```bash
adb install -r src/MusicPlayer.App/bin/Debug/net10.0-android/com.kartikeyan.musicplayer-Signed.apk
```

Or copy the APK to the phone however you like and tap it. Android will ask you
to allow installing from that source. That prompt is expected for any app not
from the Play Store.

## First run

1. Open the app and allow notifications when asked. Android 13 and later hide
   the lock screen controls without that permission.
2. Paste a YouTube link and press Add.
3. Press the download arrow, or just press play and wait for it to fetch.
4. Press play, then lock the screen.

Audio keeps playing and the transport controls appear on the lock screen. The
notification stays until you press stop.

## Release build

Release trims and ahead of time compiles, giving a smaller and faster app. It
takes considerably longer to build.

```bash
dotnet publish src/MusicPlayer.App -f net10.0-android -c Release
```

By default this is signed with the debug key too. To sign with your own key,
create a keystore once:

```bash
keytool -genkeypair -v -keystore player.keystore -alias player \
  -keyalg RSA -keysize 2048 -validity 10000
```

Then pass it to the publish:

```bash
dotnet publish src/MusicPlayer.App -f net10.0-android -c Release \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=player.keystore \
  -p:AndroidSigningKeyAlias=player \
  -p:AndroidSigningKeyPass=<password> \
  -p:AndroidSigningStorePass=<password>
```

Keep the keystore out of the repo. It is already covered by `.gitignore`.

## Where data lives

Playlists sit in a SQLite file and downloaded audio next to it, both under the
app's private directory on the device. Uninstalling the app deletes both, and
nothing else on the phone can read them.

To clear downloads without losing playlists, remove individual tracks from the
library and add them again.

## When something breaks

**A link fails to resolve.** YouTube changes its site regularly and the
extraction library has to catch up. Update it and rebuild:

```bash
dotnet add src/MusicPlayer.Extraction package YoutubeExplode
```

If a newer version does not exist yet, the fix has to wait upstream. This is the
known weak point of running without a server, and is covered in
[architecture.md](architecture.md).

**No lock screen controls.** Check that notifications are allowed for the app in
Android settings. The foreground service starts either way, but the controls are
part of the notification.

**Playback stops when the screen locks.** Some manufacturers, Xiaomi, Oppo,
OnePlus and Samsung in particular, kill background apps aggressively. Allow the
app to run in the background, or disable battery optimisation for it, in the
system settings.

## iOS

Not supported. The project has no iOS head, and adding one would need an Apple
developer account and a Mac to build on.
