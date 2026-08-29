# Contributing

Read [docs/legal-boundaries.md](docs/legal-boundaries.md) first. Some changes
are refused on principle regardless of quality.

## Setup

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) on PATH, needed at runtime but not
  to build or test

```bash
dotnet build
dotnet test
dotnet run --project src/MusicPlayer.Api
```

## Layout

```
src/     one project per concern, see docs/architecture.md
tests/   mirrors src/, one test project per source project
  support/   fakes and helpers, not tests
docs/    the docs listed in the README
```

The test tree mirrors the source tree. `src/MusicPlayer.Data` is tested by
`tests/MusicPlayer.Data.Tests`. Finding the tests for a file should never need
a search.

## Conventions

**Project names are PascalCase.** This is the one place the repo departs from
lower-case-with-dashes naming. .NET project names double as assembly and
namespace names, and the tooling assumes it. Folders that are not projects stay
lower case.

**Central package versions.** All versions live in `Directory.Packages.props`.
A `PackageReference` in a csproj carries no `Version` attribute. Shared build
settings live in `Directory.Build.props`.

**Warnings are errors.** `TreatWarningsAsErrors` is on and every public type
and member needs an XML doc comment. Keep them to one line.

**Comments explain why, not what.** If a comment restates the code, delete it.
The ones worth keeping look like the note on `PlaylistStore.AddTrackAsync`
explaining why the insert is explicit.

**No em dashes or en dashes** in code, comments or docs. Use a plain hyphen or
rewrite the sentence.

## Tests

xunit.v3, run with `dotnet test`. .NET has no built in runner, which is the
only reason there is a test dependency at all.

Store tests run against real in memory SQLite rather than the EF in memory
provider, so provider specific failures surface in tests instead of at runtime.
Use `SqliteTestDatabase` from `tests/support`, and take a fresh context per
operation. Sharing one context across a whole test hides bugs and creates
others, because the server uses a scoped context per request.

When you fix a bug, add the test that would have caught it and note in one line
what used to happen:

```csharp
// Used to throw NotSupportedException: SQLite cannot ORDER BY a DateTimeOffset column.
```

## Adding a source

Implement `IMediaResolver`, register it in `ServiceRegistration`, and give it a
`Priority` below 100 so it is tried before the yt-dlp fallback. Do not add a
resolver for a DRM protected host. `DrmSourcePolicy` blocks those before any
resolver runs, and that is deliberate.
