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

## Music on the phone

The **Playlists** tab also lists the music already on the phone under **On this
device**, one playlist per folder. A song in `Songs/Coldplay` appears in a playlist
called Coldplay. You can play and reorder these, and add their songs to your own
playlists. The app only reads the files and never changes, moves or deletes them.

## Saving audio as a file

Under the link field, **M4A** and **MP3** save a single video's audio wherever you
choose on the phone. M4A is YouTube's own audio, saved as it is. MP3 is re-encoded on
the phone, which takes a little longer. Saving does not add the link to your library.

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
Source images, how they are generated, and how to redraw them by hand are all in
[assets/logo/README.md](assets/logo/README.md).

## Author

Built by [StepIntoTheCode](https://github.com/stepintothecode).
Source: [github.com/stepintothecode/linkaudioplayer](https://github.com/stepintothecode/linkaudioplayer).
Free, ad free and tracker free. [Support the project](https://stepintothecode.github.io/support/?from=linkaudioplayer-app).
