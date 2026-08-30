# Contributing

Read [docs/legal-boundaries.md](docs/legal-boundaries.md) first. Some changes
are refused on principle regardless of quality.

## Setup

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- `dotnet workload install maui-android`
- A JDK 17 or later, and the Android SDK

```bash
dotnet build
dotnet test
dotnet build src/MusicPlayer.App -f net10.0-android
```

`dotnet test` needs neither the workload nor a device. Only the app project does.

## Layout

```
src/     one project per concern, see docs/architecture.md
tests/   mirrors src/, one test project per source project
  support/   fakes and helpers, not tests
docs/    the docs listed in the README
```

The test tree mirrors the source tree. `src/MusicPlayer.Playback` is tested by
`tests/MusicPlayer.Playback.Tests`. Finding the tests for a file should never
need a search.

## Conventions

**Project names are PascalCase.** This is the one place the repo departs from
lower-case-with-dashes naming. .NET project names double as assembly and
namespace names, and the tooling assumes it. Folders that are not projects stay
lower case.

**Central package versions.** All versions live in `Directory.Packages.props`. A
`PackageReference` in a csproj carries no `Version` attribute. Shared build
settings live in `Directory.Build.props`.

**Warnings are errors.** `TreatWarningsAsErrors` is on and every public type and
member needs an XML doc comment. Keep them to one line. The app project opts out
of both, since it is app wiring rather than a library.

**Comments explain why, not what.** If a comment restates the code, delete it.
The ones worth keeping look like the note on `PlaylistStore.AddTrackAsync`
explaining why the insert is explicit.

**No em dashes or en dashes** in code, comments or docs. Use a plain hyphen or
rewrite the sentence.

## Tests

xunit.v3, run with `dotnet test`. .NET has no built in runner, which is the only
reason there is a test dependency at all.

Store tests run against real in memory SQLite rather than the EF in memory
provider, so provider specific failures surface in tests instead of at runtime.
Use `SqliteTestDatabase` from `tests/support`, which doubles as the context
factory the stores take.

Playback is tested against `FakeAudioPlayer` and `FakeMediaCache`, also in
`tests/support`. Everything about the queue, repeat behaviour and the lock
screen buttons is covered there, because none of it can be checked on a device
without a person watching. Platform code in `Platforms/Android` is not unit
tested; it has to be exercised on a phone.

When you fix a bug, add the test that would have caught it and note in one line
what used to happen:

```csharp
// Used to throw NotSupportedException: SQLite cannot ORDER BY a DateTimeOffset column.
```

## Adding a source

Implement `IMediaResolver` and `IMediaDownloader`, register both in
`MauiProgram`, and give them a `Priority` below the YouTube pair at 20. Do not
add a resolver for a DRM protected host. `DrmSourcePolicy` blocks those before
any resolver runs, and that is deliberate.

## The server version

An earlier design ran ASP.NET Core with yt-dlp on a home machine, reached over
Tailscale. It works and is preserved on the `main` branch. It supported far more
sites, because yt-dlp does, but needed a machine left running. The Android app
lives on `android-app`.
