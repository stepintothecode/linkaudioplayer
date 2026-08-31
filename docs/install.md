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
dotnet build src/LinkAudioPlayer.App -f net10.0-android
```

Output:

```
src/LinkAudioPlayer.App/bin/Debug/net10.0-android/com.kartikeyan.linkaudioplayer-Signed.apk
```

Around 17 MB. It is signed with the local debug key, which is enough to install
and use. The Release build below runs faster but is a larger download.

## Put it on the phone

Over USB, with developer options and USB debugging enabled:

```bash
adb install -r src/LinkAudioPlayer.App/bin/Debug/net10.0-android/com.kartikeyan.linkaudioplayer-Signed.apk
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

Release trims and ahead of time compiles, so the app starts and runs faster. It
takes considerably longer to build, and the APK is larger, around 40 MB, because
it carries precompiled native code for every supported CPU.

```bash
dotnet publish src/LinkAudioPlayer.App -f net10.0-android -c Release
```

To build for one phone only, name its architecture. Almost every phone since
2019 is `android-arm64`, and this roughly halves the size:

```bash
dotnet publish src/LinkAudioPlayer.App -f net10.0-android -c Release -r android-arm64
```

By default this is signed with the debug key too. To sign with your own key,
create a keystore once:

```bash
keytool -genkeypair -v -keystore player.keystore -alias player \
  -keyalg RSA -keysize 2048 -validity 10000
```

Then pass it to the publish:

```bash
dotnet publish src/LinkAudioPlayer.App -f net10.0-android -c Release \
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

## Playing

- **Loop** cycles off, all tracks, this track. Each press names the mode it landed on.
- **Shuffle** plays the queue in a random order without interrupting the current track.
  Turning it off returns to the queue order from wherever you are.
- **Stop** clears the queue and hides the player.

## Adding links

A single video link adds one track. A playlist link adds every video in it, up to 200.

A **mix** link, the ones with `list=RD...`, is refused. YouTube builds those per
viewer rather than publishing them, so there is nothing to list. Open the mix and add
the videos you want.

## Reordering

Both the Library and a playlist reorder by dragging the six dot handle on the left
of a row. The library keeps its own order, so a track stays where you put it.

## Reading the logs

Anything that fails shows a banner with a **See logs** link, and there is a
**Logs** tab in the header. The buffer keeps the last 500 entries with full stack
traces, and it deliberately survives the failure that produced it, so the detail
is still there afterwards.

Use **Copy all** to lift the whole buffer out as text, and **Problems only** to
filter to warnings and errors.

## When something breaks

**Play does nothing at all.** This was a real bug and is fixed. If it recurs, the
now playing bar shows the reason with a See logs link, and the log holds the full
trace including which audio stream was chosen.


**A link fails to resolve.** YouTube changes its site regularly and the
extraction library has to catch up. Update it and rebuild:

```bash
dotnet add src/LinkAudioPlayer.Extraction package YoutubeExplode
```

If a newer version does not exist yet, the fix has to wait upstream. This is the
known weak point of running without a server, and is covered in
[architecture.md](architecture.md).

**No lock screen controls.** Check that notifications are allowed for the app in
Android settings. The foreground service starts either way, but the controls are
part of the notification.

**Nothing responds after an error.** This should no longer happen. Every screen
sits inside an error boundary and every action routes through one handler, so a
failure shows a banner rather than taking the UI down. If you do hit it, the log
buffer outlives the renderer, so open Logs and copy the trace.

**Playback stops when the screen locks.** Some manufacturers, Xiaomi, Oppo,
OnePlus and Samsung in particular, kill background apps aggressively. Allow the
app to run in the background, or disable battery optimisation for it, in the
system settings.

## iOS

Not supported. The project has no iOS head, and adding one would need an Apple
developer account and a Mac to build on.
