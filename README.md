# Music Player

A personal, self hosted player for links you already have. Build playlists,
play them on your phone, and keep listening with the screen locked. Downloads
for offline use.

Runs on your own machine or NAS. There is no public instance and there will not
be one.

## Run it

With Docker, which is the intended way:

```bash
docker compose up -d --build
```

Open http://localhost:5080, paste a link, press Add.

From source instead, needing the [.NET 10 SDK](https://dotnet.microsoft.com/download)
and [yt-dlp](https://github.com/yt-dlp/yt-dlp) on PATH:

```bash
dotnet run --project src/MusicPlayer.Api
```

To use it from your phone with lock screen playback you need it running on an
always on device and reachable over HTTPS. See [docs/hosting.md](docs/hosting.md).

## Sources

Anything [yt-dlp supports](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md),
which is roughly 1800 sites, plus direct links to media files.

DRM protected services such as Spotify and Apple Music are refused by design.
See [docs/legal-boundaries.md](docs/legal-boundaries.md) for why, and read it
before contributing.

## Docs

- [Legal boundaries](docs/legal-boundaries.md), the rules this project holds to
- [Hosting](docs/hosting.md), where to run it and why not the cloud
- [Deployment](docs/deployment.md), how to run and configure it
- [Architecture](docs/architecture.md), how it fits together
- [Contributing](CONTRIBUTING.md), layout, tests and conventions
