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

YouTube video links, YouTube playlist links, and direct links to media files such
as a podcast mp3. Mix links are refused, because YouTube builds those per viewer
rather than publishing them.

DRM protected services like Spotify and Apple Music are refused by design. See
[docs/legal-boundaries.md](docs/legal-boundaries.md) for why, and read it before
contributing.

## Backup

The **About** tab exports your whole library and every playlist as a JSON file, and
imports one back. Importing merges: nothing is deleted, a link already in the library
is not added twice, and a playlist that already exists only gains what it is missing.

## When something goes wrong

Failures show a banner naming what went wrong. Playback failures appear in the player
itself.

## Docs

- [Install](docs/install.md), building and putting it on your phone
- [Legal boundaries](docs/legal-boundaries.md), the rules this project holds to
- [Architecture](docs/architecture.md), how it fits together
- [Contributing](CONTRIBUTING.md), layout, tests and conventions

## Logo

The mark is a chain link holding a beamed pair of notes: a link that holds music.
Source images live in [assets/logo/](assets/logo/): square PNGs from 16 to 1024,
rounded variants, a transparent foreground for the Android adaptive icon, and a
multi resolution `icon.ico`. Regenerate them with
[tools/make-icons.py](tools/make-icons.py).

## Author

Built by [StepIntoTheCode](https://github.com/stepintothecode).
Source: [github.com/stepintothecode/linkaudioplayer](https://github.com/stepintothecode/linkaudioplayer).
Free, ad free and tracker free. [Support the project](https://stepintothecode.github.io/support/?from=linkaudioplayer-app).
