"""Draws the LinkAudioPlayer mark and writes it at every size the project needs.

The mark is a chain link with a beamed pair of notes sitting inside it: a link that
holds music, which is what the app does. Everything is drawn at 4x and downsampled,
which is what keeps the curves clean without an SVG rasteriser.
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

    stroke = max(2, round(0.072 * span * s))

    # The link: a tilted rounded rectangle ring, wide enough to hold the notes.
    ring = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    rd = ImageDraw.Draw(ring)
    rd.rounded_rectangle(
        box(0.10, 0.255, 0.90, 0.745),
        radius=(0.245 * span * s),
        outline=FG,
        width=stroke,
    )
    ring = ring.rotate(-18, resample=Image.BICUBIC, center=(s / 2, s / 2))
    img.alpha_composite(ring)

    # The notes: two stems under a beam, the shape from the first version of the icon.
    beam_top, beam_thick = 0.335, 0.075
    left_stem, right_stem = 0.395, 0.605
    stem_w = 0.052

    d.polygon(
        [
            (x(left_stem), x(beam_top + 0.045)),
            (x(right_stem + stem_w), x(beam_top - 0.045)),
            (x(right_stem + stem_w), x(beam_top + beam_thick - 0.045)),
            (x(left_stem), x(beam_top + beam_thick + 0.045)),
        ],
        fill=FG_DEEP,
    )

    d.rounded_rectangle(
        box(left_stem, beam_top + 0.045, left_stem + stem_w, 0.615),
        radius=stem_w * span * s / 2,
        fill=FG_DEEP,
    )
    d.rounded_rectangle(
        box(right_stem, beam_top - 0.045, right_stem + stem_w, 0.545),
        radius=stem_w * span * s / 2,
        fill=FG_DEEP,
    )

    d.ellipse(box(0.315, 0.565, 0.455, 0.675), fill=FG_DEEP)
    d.ellipse(box(0.525, 0.495, 0.665, 0.605), fill=FG_DEEP)

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
