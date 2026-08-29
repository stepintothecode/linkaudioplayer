# Running it

For where to host it and why, see [hosting.md](hosting.md). This page covers
how to run it once you have decided.

## GitHub Pages does not work for this

GitHub Pages serves static files only. It cannot run .NET, cannot run yt-dlp,
cannot hold a database, and cannot proxy a stream. Every feature that makes
lock screen playback possible needs a server.

A Blazor WebAssembly app can be hosted there, but with no backend it has
nowhere to hide anything, no way to reach a source past browser CORS, and no
way to produce an audio URL for the player. There is no version of this project
that runs on GitHub Pages. The same applies to Firebase Hosting.

## Docker, which is how to run it for real

```bash
docker compose up -d --build
curl http://localhost:5080/health
```

Playlists and cached audio live in the `player-data` volume and survive
rebuilds. The port binds to `127.0.0.1` only, so remote access goes through
Tailscale rather than an open port. See [hosting.md](hosting.md) for that.

Update yt-dlp when YouTube changes break it:

```bash
docker compose exec player pip install --break-system-packages -U yt-dlp
docker compose restart player
```

## From source, for development

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[yt-dlp](https://github.com/yt-dlp/yt-dlp) on PATH.

```powershell
winget install yt-dlp.yt-dlp
dotnet run --project src/MusicPlayer.Api
```

Open http://localhost:5080. The database and cached audio land in
`src/MusicPlayer.Api/data/`.

## Publish without Docker

```bash
dotnet publish src/MusicPlayer.Api -c Release -o ./publish
./publish/MusicPlayer.Api
```

Kestrel listens on 5080, configurable in `appsettings.json` or with
`ASPNETCORE_HTTP_PORTS`. Add `-r linux-arm64 --self-contained` for a device
with no .NET runtime installed.

## Configuration

Every setting can be overridden with an environment variable, using a double
underscore for nesting:

| Variable | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Library` | `Data Source=data/library.db` | SQLite location |
| `MediaCache__Directory` | `data/media` | Where cached audio is written |
| `YtDlp__ExecutablePath` | `yt-dlp` | Full path if not on PATH |
| `YtDlp__AudioFormat` | `bestaudio/best` | yt-dlp format selector |
| `YtDlp__DownloadTimeoutSeconds` | `600` | Abandons a slow download |
| `ASPNETCORE_HTTP_PORTS` | `5080` | Listening port |

Relative paths resolve against the content root, not the working directory.

## Why iOS is not supported

Background audio in an iOS web app stops when the screen locks, and there is a
further bug where audio dies permanently after 30 seconds paused. This is a
Safari limitation with no workaround from the web side. An iPhone needs a
native client, which this project does not have.
