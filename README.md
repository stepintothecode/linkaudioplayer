# Music Player

A personal Android music player. Paste a YouTube link, build playlists, and keep
listening with the screen locked. Tracks download to the device, so playback
works offline.

Everything runs on the phone. No server, no account, no deployment.

## Install

Grab the APK from a build and sideload it:

```bash
dotnet build src/MusicPlayer.App -f net10.0-android
```

The APK lands in `src/MusicPlayer.App/bin/Debug/net10.0-android/` as
`com.kartikeyan.musicplayer-Signed.apk`. Copy it to the phone and open it, or
install over USB with `adb install`.

Full steps, including how to build a Release APK, are in
[docs/install.md](docs/install.md).

## Sources

YouTube links, and direct links to media files such as a podcast mp3.

DRM protected services like Spotify and Apple Music are refused by design. See
[docs/legal-boundaries.md](docs/legal-boundaries.md) for why, and read it before
contributing.

## Docs

- [Install](docs/install.md), building and putting it on your phone
- [Legal boundaries](docs/legal-boundaries.md), the rules this project holds to
- [Architecture](docs/architecture.md), how it fits together
- [Contributing](CONTRIBUTING.md), layout, tests and conventions
