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

**Shuffle is a separate play order, not a shuffled queue.** `_queue` keeps the order
tracks were given and `_order` holds indices in play order. Toggling shuffle never
interrupts what is playing, and turning it off restores the original sequence from
wherever the current track sits in it.

**A resolver returns a list, not one track.** One link can name a whole playlist, so
`IMediaResolver` yields `IReadOnlyList<MediaResolution>` and `YoutubePlaylistResolver`
expands a listing in a single request rather than one per video. A link carrying both
a video and a list is treated as the video, because that is what sharing one song from
inside a playlist produces. A `list=RD...` mix is refused with an explanation: YouTube
generates those per viewer, so they are not real playlists and cannot be listed.

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

**Network work is pushed off the caller's thread with `Task.Run`.** A Blazor event
handler runs on the renderer's dispatcher, which on Android is the main thread, so
every await inside a click handler resumes there. Android's StrictMode then throws
`NetworkOnMainThreadException` when an HTTP response is disposed, because closing
the socket drains it with a blocking read. `MediaResolutionService` and
`MediaDownloadService` both hop to the pool, and tests assert they leave the
caller's synchronization context.

Note the two directions: audio has to be marshalled **onto** the main thread for
`MediaPlayer`, and network has to be pushed **off** it. Getting one right does not
give you the other.

**Playback failures are logged, not just stored.** `PlayerController` catches into an
`Error` property. When nothing logged or rendered that property, a failed play was
completely silent: no sound, no message, no log line. It now logs with the stack
trace and the now playing bar renders it.

**Prefer an mp4 audio stream over the highest bitrate one.** YouTube's best audio is
normally WebM Opus, which Android's `MediaPlayer` decodes inconsistently across
versions. A slightly smaller AAC stream that reliably plays is the better trade.

**The cache ignores `.part` files.** A download writes to `{key}.{ext}.part` and
renames on success. The lookup glob `{key}.*` matched the partial too, so an
interrupted download looked cached and handed a truncated file to the player on
every retry.

**Schema changes need a patch step.** `EnsureCreated` only builds a schema from
nothing, so an app updated in place keeps its old table. `DatabaseInitializer`
checks `PRAGMA table_info` and adds missing columns and indexes.

**`android:icon` must be declared in the manifest.** The generated mipmap resources
are packaged regardless, but without the attribute the launcher shows the default
Android robot.

**Reordering uses pointer events, not HTML5 drag and drop**, which never fires for
touch in an Android web view. `reorder.js` shifts rows visually and reports the
final move; it never mutates the DOM, because Blazor owns that.

## Not built

- Proper EF migrations. The patch step in `DatabaseInitializer` covers the one
  column added so far; a third or fourth would justify migrations instead.
- YouTube mixes. They are per viewer and not listable, so they are refused.
- Search. Links are pasted, by design, per the legal boundaries.
