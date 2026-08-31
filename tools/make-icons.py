"""Draws the LinkAudioPlayer mark and writes it at every size the project needs.

The mark is a chain link whose right half opens into a rising equaliser, which is the
whole app in one shape: a link turning into sound. Everything is drawn at 4x and
downsampled, which is what keeps the curves clean without an SVG rasteriser.
"""

import os
import sys
from PIL import Image, ImageDraw

BG = (13, 11, 18, 255)
FG = (192, 132, 252, 255)      # accent-soft
FG_DEEP = (124, 58, 237, 255)  # accent-deep
SS = 4                          # supersample factor


def _lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def draw_mark(size, transparent=False, inset=0.0):
    """Returns an RGBA image of the mark at the given size."""
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0) if transparent else BG)
    d = ImageDraw.Draw(img)

    pad = inset
    span = 1.0 - 2 * pad

    def x(v):
        return (pad + v * span) * s

    def box(x0, y0, x1, y1):
        return [x(x0), x(y0), x(x1), x(y1)]

    stroke = max(2, round(0.08 * span * s))

    # A closed chain link on the left, kept clear of the bars so both read at 48px.
    d.rounded_rectangle(
        box(0.07, 0.335, 0.45, 0.665),
        radius=(0.165 * span * s),
        outline=FG,
        width=stroke,
    )

    # Rising then falling equaliser bars, tinted across the accent ramp.
    bars = [
        (0.545, 0.26),
        (0.675, 0.52),
        (0.805, 0.74),
        (0.935, 0.40),
    ]

    for index, (centre, height) in enumerate(bars):
        half = 0.038
        top = 0.5 - height / 2
        colour = _lerp(FG, FG_DEEP, index / max(len(bars) - 1, 1))
        d.rounded_rectangle(
            box(centre - half, top, centre + half, top + height),
            radius=half * span * s,
            fill=colour,
        )

    if SS > 1:
        img = img.resize((size, size), Image.LANCZOS)

    return img


def rounded(img, radius_ratio=0.22):
    """Applies a rounded mask, for icons that are not masked by the platform."""
    size = img.size[0]
    mask = Image.new("L", (size * SS, size * SS), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, size * SS - 1, size * SS - 1],
        radius=int(size * SS * radius_ratio),
        fill=255,
    )
    mask = mask.resize((size, size), Image.LANCZOS)

    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(img, (0, 0), mask)
    return out


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    written = []

    # Square full bleed, for stores and docs.
    for size in (16, 32, 48, 64, 72, 96, 128, 144, 192, 256, 512, 1024):
        path = os.path.join(out_dir, f"icon-{size}.png")
        draw_mark(size).save(path)
        written.append(path)

    # Rounded, for places that do not mask.
    for size in (192, 512, 1024):
        path = os.path.join(out_dir, f"icon-rounded-{size}.png")
        rounded(draw_mark(size)).save(path)
        written.append(path)

    # Transparent foreground, inset for the Android adaptive icon safe zone.
    for size in (432, 1024):
        path = os.path.join(out_dir, f"icon-foreground-{size}.png")
        draw_mark(size, transparent=True, inset=0.24).save(path)
        written.append(path)

    # Multi resolution ICO for Windows and favicons.
    ico = os.path.join(out_dir, "icon.ico")
    draw_mark(256).save(ico, sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    written.append(ico)

    for path in written:
        print(f"{os.path.getsize(path):>8,}  {os.path.basename(path)}")


if __name__ == "__main__":
    main(sys.argv[1])
