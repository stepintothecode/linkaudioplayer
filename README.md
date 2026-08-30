# LinkAudioPlayer

A personal Android audio player for links. Paste a YouTube link, build playlists,
and keep listening with the screen locked. Tracks download to the device, so
playback works offline.

Everything runs on the phone. No server, no account, no deployment.

## Install

```bash
dotnet build src/LinkAudioPlayer.App -f net10.0-android
```

The APK lands in `src/LinkAudioPlayer.App/bin/Debug/net10.0-android/` as
`com.kartikeyan.linkaudioplayer-Signed.apk`. Copy it to the phone and open it, or
install over USB with `adb install`.

Full steps, including the Release build, are in [docs/install.md](docs/install.md).

## Sources

YouTube links, and direct links to media files such as a podcast mp3.

DRM protected services like Spotify and Apple Music are refused by design. See
[docs/legal-boundaries.md](docs/legal-boundaries.md) for why, and read it before
contributing.

## When something goes wrong

Every failure shows a banner with a **See logs** link, and there is a **Logs** tab
in the header. It keeps the last 500 entries with full stack traces, survives the
error that produced it, and has a copy button. Start there.

## Docs

- [Install](docs/install.md), building and putting it on your phone
- [Legal boundaries](docs/legal-boundaries.md), the rules this project holds to
- [Architecture](docs/architecture.md), how it fits together
- [Contributing](CONTRIBUTING.md), layout, tests and conventions

## Logo

Source images live in [assets/logo/](assets/logo/): square PNGs from 16 to 1024,
rounded variants, a transparent foreground for the Android adaptive icon, and a
multi resolution `icon.ico`. Regenerate them with
[tools/make-icons.py](tools/make-icons.py).
