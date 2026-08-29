# Architecture

## The one idea worth knowing

The server hands the browser a plain audio file.

```
Android PWA  --HTTPS-->  ASP.NET Core on your NAS  -->  yt-dlp  -->  source
  <audio>                  SQLite + cached audio
  MediaSession
```

The phone sees `<audio src="/api/stream/{id}">` and nothing more. It has no
idea where the audio came from. That is what makes lock screen playback work:
there is no embedded player fighting the browser, just a media element the
platform already knows how to keep alive.

Everything else follows from that. Range requests make the scrubber work,
including the one on the lock screen. Caching to disk makes offline work and
doubles as insurance against the source throttling.

## Projects

Under `src/`:

| Project | Holds | Depends on |
| --- | --- | --- |
| `MusicPlayer.Domain` | Entities. No dependencies at all. | nothing |
| `MusicPlayer.Abstractions` | Interfaces and value types. The seams. | Domain |
| `MusicPlayer.Contracts` | DTOs and route names shared by client and server. | nothing |
| `MusicPlayer.Extraction` | Source policy, resolvers, the yt-dlp subprocess. | Abstractions |
| `MusicPlayer.Data` | EF Core, SQLite, stores. | Abstractions |
| `MusicPlayer.Streaming` | On disk media cache, content types. | Abstractions |
| `MusicPlayer.Api` | Endpoints, DI wiring, hosts the client. | all of the above |
| `MusicPlayer.Web` | Blazor WebAssembly client and the audio JS. | Contracts |

Domain and Abstractions know nothing about the web, the filesystem, or any
external service. Everything external sits behind an interface named in one
file, so the seam is findable and tests can supply a fake.

## The seams

- `ISourcePolicy` decides whether a URL may be touched. One implementation,
  `DrmSourcePolicy`, and it is the project's legal guardrail.
- `IMediaResolver` turns a URL into metadata. `DirectUrlResolver` handles plain
  media links, `YtDlpResolver` handles everything else. Lowest `Priority` wins.
- `IMediaDownloader` fetches audio to disk. Keeps the cache from knowing that
  yt-dlp exists.
- `IMediaCache` stores and locates that audio.
- `IPlaylistStore` and `ITrackStore` are persistence.

## Playback flow

1. Client posts a URL to `/api/tracks`.
2. `MediaResolutionService` runs the policy, then picks a resolver.
3. Metadata is stored. No audio is fetched yet.
4. Client sets `<audio src="/api/stream/{id}">`.
5. The stream endpoint serves the cached file, downloading it first if needed.
6. `audio-player.js` mirrors the element's state into the Media Session API,
   which is what draws the lock screen controls.

First play on an uncached track waits for the download. Pre-cache with the
download button to avoid that.

## Decisions worth recording

**yt-dlp runs as a subprocess rather than being reimplemented.** YouTube breaks
extraction constantly and yt-dlp is the only thing that keeps pace. A C# port
would be permanently out of date.

**Timestamps are stored as unix milliseconds.** SQLite cannot `ORDER BY` a
`DateTimeOffset` column. A converter in `LibraryDbContext` handles it.

**Playlist items are added with an explicit `Add`.** Entities carry a
`Guid.CreateVersion7()` key from their property initializer, and EF reads a
pre-set key on a navigation-discovered entity as an existing row, turning the
insert into an update.

**Blazor Server was rejected.** Its SignalR connection drops when the phone
sleeps, which is exactly the case this project is built for. WebAssembly has no
such problem.

**The player service is a singleton.** The queue and the audio element have to
survive navigation between pages.
