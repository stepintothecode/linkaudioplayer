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
| `LinkAudioPlayer.Diagnostics` | Global exception hooks and a log sink. | nothing |
| `LinkAudioPlayer.Backup` | Export and merge of the whole library and playlists. | Abstractions |
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

**The web view draws its own scrollbars, and CSS cannot reach them.** They are the
bars that appear along the right and bottom edges when a scroll runs out of room, so
`MauiProgram` turns them and the overscroll effect off on the platform view. The page
separately sets `overflow-x: hidden`, because the swipe gesture translates the page
sideways and that overflow would otherwise become a real horizontal scroll area.

**The player bar's height is measured, not guessed.** `player-space.js` watches the
bar with a `ResizeObserver` and writes `--player-height`, which the lists use as bottom
padding. A constant could not track a bar that grows with the queue name and the error
block, and shrinks to nothing when playback stops, so the last row of every list sat
underneath it.

**A tab remembers where you were inside it.** `TabMemory` holds the playlist last
opened, so tapping or swiping onto the playlists tab returns to it rather than the
list. Seeing the list clears it, and so does opening a playlist that no longer exists,
which stops the tab reopening something deleted. Swiping back out of a playlist
deliberately ignores the memory, or it would put you straight back where you came from.

**One icon, generated once.** The app icon and the in app mark both come from
`tools/make-icons.py`. A hand written SVG copy drifted out of step, tilting the other
way because Pillow rotates counter clockwise for a positive angle and SVG rotates
clockwise. The generated PNG is now the only source.

**Icons are inline SVG, not emoji.** Android renders emoji in their own colours, so
the shuffle, loop and stop glyphs came out yellow and white whatever the button did.
An SVG inherits `currentColor`, which lets a button own its icon.

**Removing keeps the audio; startup clears the orphans.** Deleting a track no longer
deletes its downloaded file, so an undo does not force a re-download. Anything still
orphaned at the next start is purged by `PurgeOrphanedDownloadsAsync`.

**Undo restores position, not just existence.** `RemovalService` captures the library
order and the order of every affected playlist before deleting, then reorders after
re-adding. A safety net that puts a track back in the wrong place is only half a net.

**A backup references tracks by source URL, not by id.** Ids are local, so a file
restored onto another install would point at nothing. The URL is also the identity
used to skip a track already in the library.

**Importing merges and never deletes.** A track already present is skipped, a playlist
that already exists gains only the entries it lacks, and the same link may sit in any
number of playlists. That last rule is why playlist membership is deduplicated per
playlist rather than globally.

**Playlist names are unique, compared trimmed and case insensitively.** Two playlists
with the same name were indistinguishable in the list and made import ambiguous. The
check lowercases both sides rather than trusting the SQLite collation, which is case
sensitive by default.

**Support links open in the device browser, never in the web view.** A payment page
inside an app reads as an in app purchase that skipped store billing. Android needs a
`<queries>` block for `https` or the launch silently does nothing.

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

**Pages that show the playing row subscribe to `PlayerController.StateChanged`.**
Without it the highlight stuck to whatever was playing when the page rendered, because
nothing told Blazor the queue had advanced.

**The log viewer was removed, the sink was not.** `InMemoryLogStore` is still the only
logging provider in a Release build, and `GlobalExceptionHandler` still marks unobserved
task exceptions observed so a background failure cannot take the process down. Nothing
reads the buffer any more, so diagnosing a new bug means adding a way to read it back.

**Nothing throws out of a UI handler.** An escaped exception tears down the Blazor
renderer, and every button in the app stops responding until it is force closed.
Handlers route through `UiErrorState.TryAsync`, and `MainLayout` wraps the body in
an `ErrorBoundary` as a second line.

**The app owns the back button.** On a tab, the first press shows "Press back again to
exit" and a second within two seconds sends the app to the background, as Android does
for back on a root screen, so music keeps playing. Anywhere else, back returns to the
page you came from, tracked by `BackNavigation`, and an open confirmation closes first.
`MainActivity` registers its own `OnBackPressedCallback` and re-adds it on resume and
when the layout attaches, because Android asks the newest callback first and the web
view adds one that walks its history. MAUI's `OnBackPressed` lifecycle hook was tried
first and is never reached on Android 16, where back arrives through predictive back.

**Every page keeps its scroll position.** `scroll-memory.js` records how far down each
route was and restores it on return, however you got back. The target is read when the
route changes, before the new page renders, because swapping the page shrinks the
document and the clamped scroll would otherwise overwrite it. It then waits for the
page to grow tall enough, since lists load after the first render. `FocusOnNavigate`
was removed: focusing a heading drew the web view's focus ring round the logo and
scrolled the page to the top.

**Swipes work from anywhere.** `swipe.js` listens on the document, not on `main`,
which covers neither the header nor the space below a short page. The header and main
set `touch-action: pan-y`, so the browser keeps vertical scrolling and no longer cancels
a sideways drag it took for its own pan. A long drag counts however slowly it is made;
only a short flick has to be quick.

**Shares and opened files arrive through their own activities.** `ShareActivity`
takes `text/plain` shares, such as YouTube's Share menu, and `OpenAudioActivity` takes
"Open with" and shares of audio files. Each shows nothing: it hands the link or file to
a singleton inbox and opens `MainActivity`. They are separate because a share launches
into the sender's task, and receiving it in `MainActivity` would start a second copy of
the app inside YouTube. Their `TaskAffinity` is empty, so neither becomes the root of
the app's own task: when the share receiver was, a later share launched as a new task
only brought that task forward and never arrived. `OpenAudioActivity` passes its read
grant on to `MainActivity`, or the sender's permission would lapse when it finished.

**A page is built once per navigation.** The page wrapper is keyed on the live address.
It was keyed on `_path`, which `OnLocationChanged` updates after the router has already
drawn the new page, so every page was built, thrown away and built again. Besides the
wasted loads, the first copy took a shared link out of its inbox and the second found
it empty.

**Only a retry clears an error.** `UiErrorState.TryAsync` used to clear the banner
whenever any work succeeded, and pages reload lists and rebind the reorder handle on
every render, so a failed add or save vanished before it could be read. A success now
clears only the message its own action produced, and moving to another screen clears
the rest.

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
checks `PRAGMA table_info` and adds missing columns and indexes. Its SQL is written
out per table rather than built from a name, because interpolating an identifier into
DDL is how injection gets in even when the value is ours.

**Both lists carry their own order.** Tracks and playlists each have a `Position`
column, dense from zero, with a new row taking zero and pushing the rest down. A
backup therefore imports playlists oldest first, or the file's order would come out
reversed.

**`android:icon` must be declared in the manifest.** The generated mipmap resources
are packaged regardless, but without the attribute the launcher shows the default
Android robot.

**Swipe detection observes, it never intercepts.** `swipe.js` listens passively and
never calls `preventDefault`, so vertical scrolling and every other touch behaviour is
untouched. It uses touch events rather than pointer events because `reorder.js`
captures the pointer on the grip, and sharing that stream invites conflicts. The
routing arithmetic lives in `TabNavigation` so the edge cases are testable without a
device.

**A drilled in page pops back rather than jumping out.** Swiping back inside a
playlist returns to the playlist list, matching the back button on that screen, while
swiping forward continues to the next tab. `TabNavigation` ranks every route on one
line, a tab at a multiple of ten and a page below it just after, so a slide direction
always has an answer.

**The queue follows edits to the list it came from.** `PlayerController.SyncQueue`
re-points the queue at an edited list without interrupting playback, so reordering a
playlist changes what plays next. Without it the queue kept the shape it had when
playback started and the edit looked ignored. A sync naming a different queue is
dropped, and one whose list no longer holds the playing track leaves the queue alone,
which is what happens when the playing track is the one removed.

**The swipe ignore list is short on purpose.** It covers only what owns a horizontal
drag: inputs, the reorder grip, the scrubber and the player bar. Buttons and links
were on it at first, which made most of a list row dead to the gesture. The playlists
page felt stuck because its titles are links, while the library's are plain divs. The
browser cancels a tap once the finger moves, so a swipe may safely start on either.

**The page follows the finger, damped.** Tracking a fraction of the travel is what
makes the gesture read as direct without exposing empty space beside the page. At the
ends of the strip the damping tightens instead of blocking, so the edge feels like a
wall rather than a dead control.

**Reordering uses pointer events, not HTML5 drag and drop**, which never fires for
touch in an Android web view. `reorder.js` shifts rows visually and reports the
final move; it never mutates the DOM, because Blazor owns that.

**Phone songs are tracks with a folder.** A song found on the phone is a `Track` whose
`FolderPath` is set and whose `SourceUrl` is its MediaStore content URI. That lets it
sit in a user's playlist through the normal `PlaylistItem` row, while every library
query filters on `FolderPath == null`, so it never shows in the library, a backup or
the orphan purge. `TrackStore.DeleteAsync` refuses one outright: it leaves only when
its file does.

**The phone is read through MediaStore, not the file system.** Android already indexes
every audio file, and one cursor over that table stays quick with thousands of songs,
where walking folders takes seconds. `is_music` leaves out ringtones, alarms and
notification sounds, and `is_recording` leaves out voice recordings from Android 12.
The only permission is `READ_MEDIA_AUDIO`, which is read only. Those flags are not
enough on their own: older Samsung recorders saved to `VoiceRecorder/` and
`Music/Voice Recorder/`, which Android marks as music, so `DeviceFolder.IsNotMusic`
also skips folders named like recordings, voice notes or ringtones.

**A scan never makes a screen wait.** Pages render what is stored at once and redraw
if a scan changes it. `DeviceMusicService` runs one scan at a time, shares it with any
caller that arrives meanwhile, and skips a scan within 30 seconds of the last. The diff
in `DeviceFolderStore.SyncAsync` saves nothing when nothing changed, so a rescan of an
unchanged phone costs one query.

**A rescan keeps what the user arranged.** Songs are matched by content URI. New ones
join the end of their folder, a moved one joins the end of its new folder, and a
vanished one is removed with its playlist entries. Folders are matched by path, so two
folders called Music in different places stay separate and show their paths.

**Saving uses the system save dialog.** `ACTION_CREATE_DOCUMENT` lets the user pick the
folder, and the app writes only the one file chosen, so no write permission is needed.
The dialog opens before the download, so the user can choose and walk away. A failure
deletes the picked file rather than leaving an empty one behind.

**MP3 is encoded on the phone with LAME.** Android decodes AAC and Opus but has no MP3
encoder, so `libmp3lame.so` ships for each ABI, about 240 KB each and roughly 110 KB
added to the APK once compressed. `AndroidMp3Encoder` decodes with `MediaCodec` and
encodes at 192 kbps constant bitrate, which needs no Xing header and so no seek back
into a save stream that may not allow one. The ID3 tag is written in C# by `Id3Tag`
as version 2.3 with UTF-16 text, which is what both Android and Windows read and what
keeps a Hindi or Kannada title intact. `tools/build-lame.ps1` rebuilds the libraries.

**M4A is never re-encoded.** It is YouTube's AAC stream copied as it is. A source that
is not AAC, such as a direct mp3 link, is refused for M4A with a pointer to MP3, rather
than saved under the wrong extension.

**The header gives way before the tabs.** The name and three tabs need about 400px and
many phones are 360px wide, so the header pushed the page sideways and About fell off
the right edge. The name now ellipsises and, below 26rem, gives way to the logo.

## Not built


- Proper EF migrations. The patch step in `DatabaseInitializer` covers the one
  column added so far; a third or fourth would justify migrations instead.
- YouTube mixes. They are per viewer and not listable, so they are refused.
- Search. Links are pasted, by design, per the legal boundaries.
