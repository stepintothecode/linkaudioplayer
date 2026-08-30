# Architecture

## The one idea worth knowing

Everything runs on the phone.

```
Blazor UI in a WebView
        |
PlayerController          queue rules, platform agnostic
        |
IAudioPlayer  -->  AndroidAudioPlayer  -->  MediaPlayer + MediaSession
                            |
                   PlaybackService       foreground service, keeps audio alive
        |
IMediaCache   -->  files on device  <--  YoutubeExplode
        |
SQLite                                   playlists and library
```

Audio plays through the native Android `MediaPlayer`, not through the WebView.
That is deliberate: WebView audio is suspended when the app is backgrounded, so
routing playback through it would defeat the whole point. The WebView draws the
UI and nothing else.

`MediaSession` publishes title, artist, artwork and position to the system,
which is what paints the lock screen controls. `PlaybackService` is a foreground
service, which is what stops Android killing the process once the screen is off.

## Projects

Under `src/`:

| Project | Holds | Depends on |
| --- | --- | --- |
| `LinkAudioPlayer.Domain` | Entities and the cache key rule. | nothing |
| `LinkAudioPlayer.Abstractions` | Interfaces and value types. The seams. | Domain |
| `LinkAudioPlayer.Extraction` | Source policy, resolvers, downloaders. | Abstractions |
| `LinkAudioPlayer.Data` | EF Core, SQLite, stores. | Abstractions |
| `LinkAudioPlayer.Storage` | On device media cache and download dispatch. | Abstractions |
| `LinkAudioPlayer.Library` | Adding, downloading and removing tracks. | Abstractions, Extraction |
| `LinkAudioPlayer.Playback` | Queue and transport rules. | Abstractions |
| `LinkAudioPlayer.Diagnostics` | In memory log buffer and global exception hooks. | nothing |
| `LinkAudioPlayer.Ui` | Razor pages and components. | Library, Playback |
| `LinkAudioPlayer.App` | MAUI host, Android audio and service. | all of the above |

Domain, Abstractions and Playback know nothing about Android, the filesystem or
any network. That is what makes the queue rules testable without a device.

## The seams

- `ISourcePolicy` decides whether a URL may be touched. One implementation,
  `DrmSourcePolicy`, and it is the project's legal guardrail.
- `IMediaResolver` turns a URL into metadata. `DirectUrlResolver` handles plain
  media links, `YoutubeResolver` handles YouTube. Lowest `Priority` wins.
- `IMediaDownloader` fetches audio to disk, dispatched the same way.
- `IMediaCache` stores and locates that audio.
- `IAudioPlayer` is platform audio. Android implements it; tests fake it.
- `IPlaylistStore` and `ITrackStore` are persistence.

## Playback flow

1. The user pastes a URL. `MediaResolutionService` runs the policy, then picks a
   resolver. Metadata is stored, no audio is fetched.
2. On play, `PlayerController` asks `IMediaCache` for the file. If it is not
   there, it downloads first and reports progress.
3. The local path goes to `AndroidAudioPlayer`, which starts the foreground
   service and hands the file to `MediaPlayer`.
4. Lock screen buttons arrive back through the session callback as
   `NextRequested` and `PreviousRequested`, which the controller turns into
   queue moves.

## Decisions worth recording

**No server.** An earlier version ran ASP.NET Core with yt-dlp on a home
machine. It worked, and is preserved on the `main` branch. It was dropped
because it needed a machine left running.

**YoutubeExplode rather than yt-dlp.** yt-dlp is Python and cannot ship in an
APK. This is the project's weakest point: YoutubeExplode is in maintenance mode,
while yt-dlp is actively developed by a large team. When YouTube changes its
site, expect a wait. Nothing else in the design depends on it, so swapping in a
different extractor means implementing two interfaces.

**Native audio, not the WebView.** WebView media is suspended on backgrounding.
Lock screen playback is the reason this project exists, so audio had to be
native.

**Timestamps are stored as unix milliseconds.** SQLite cannot `ORDER BY` a
`DateTimeOffset` column. A converter in `LibraryDbContext` handles it.

**Playlist items are added with an explicit `Add`.** Entities carry a
`Guid.CreateVersion7()` key from their property initializer, and EF reads a
pre-set key on a navigation-discovered entity as an existing row, turning the
insert into an update.

**Stores take a `IDbContextFactory`, not a context.** There is no request scope
on a phone. A long lived change tracker caused stale reads, and a per operation
context matches how the app actually behaves.

**`PlayerController` is a singleton.** The queue and the audio session have to
survive navigation between pages.

**Every MediaPlayer and MediaSession call runs on the Android main thread.** Those
types need a Looper, and constructing one on a thread pool thread throws
Java.Lang.RuntimeException. Playback is started from a continuation after a
download, which is a pool thread, so `AndroidAudioPlayer` marshals through
`MainThread` everywhere.

**Android insets are applied as padding on the content view**, not through CSS
`env(safe-area-inset)`. Android 15 forces edge to edge, and the web view does not
reliably report the insets, so `MainActivity` reads them and pads instead.

**Nothing throws out of a UI handler.** An escaped exception tears down the Blazor
renderer, and every button in the app stops responding until it is force closed.
Handlers route through `UiErrorState.TryAsync`, and `MainLayout` wraps the body in
an `ErrorBoundary` as a second line.

**Reordering uses pointer events, not HTML5 drag and drop**, which never fires for
touch in an Android web view. `reorder.js` shifts rows visually and reports the
final move; it never mutates the DOM, because Blazor owns that.

## Not built

- Shuffle. Repeat is there; shuffle is not.
- Importing a whole YouTube playlist in one paste.
- Search. Links are pasted, by design, per the legal boundaries.
