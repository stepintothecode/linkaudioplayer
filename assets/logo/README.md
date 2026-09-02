# Logo

The mark is a chain link holding a beamed pair of notes: a link that holds music.

## How these were made

Drawn programmatically by [tools/make-icons.py](../../tools/make-icons.py) using
Python and [Pillow](https://python-pillow.org). Nothing here is hand drawn, and no
file in this folder should be edited directly. Change the script and re-run it.

```bash
python tools/make-icons.py assets/logo
```

The script draws plain shapes, a rounded rectangle ring for the link plus a beam,
two stems and two ellipses for the notes, at four times the target size and then
downsamples with LANCZOS. That supersampling is the only reason the curves come out
clean without an SVG rasteriser, which this machine does not have.

## How you would do it by hand

Draw it once as vectors in Figma or Inkscape, then export:

- a 1024px **transparent** PNG of the mark, inset to about 76 percent, as the Android
  adaptive icon foreground
- a 1024px solid `#0d0b12` square as the background
- square PNGs at 48, 72, 96, 144, 192 and 512 for stores and docs

That is the better route if you have the tools. The script exists because this
machine had Pillow and no vector editor.

## One trap worth knowing

**Pillow and SVG rotate in opposite directions for a positive angle.** Pillow turns
counter clockwise, SVG turns clockwise. An earlier hand written SVG copy of this mark
used the same angle as the script and came out mirrored. That is why there is now a
single generated source and no SVG twin.

## What is in here

| File | Used for |
| --- | --- |
| `icon-16` to `icon-1024.png` | square, full bleed, for stores and docs |
| `icon-rounded-{192,512,1024}.png` | where the platform does not mask the corners |
| `icon-foreground-{432,1024}.png` | transparent, inset for the Android adaptive icon |
| `icon.ico` | multi resolution, for Windows and favicons |

Where they end up in the app:

| Copied to | From |
| --- | --- |
| `src/LinkAudioPlayer.App/Resources/AppIcon/appiconfg.png` | `icon-foreground-1024.png` |
| `src/LinkAudioPlayer.App/Resources/Splash/splash.png` | `icon-foreground-1024.png` |
| `src/LinkAudioPlayer.Ui/wwwroot/icon.png` | `icon-192.png` |

`appicon.png` beside the foreground is a plain `#0d0b12` square, written by the same
script run. MAUI composes the two into the adaptive icon.
