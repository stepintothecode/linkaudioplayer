"""Draws the LinkAudioPlayer mark and writes it at every size the project needs.

The mark is a beamed pair of notes over a link chain, on the app's dark ground.
Everything is drawn at 4x and downsampled, which is what keeps the curves clean.
"""

import os
import sys
from PIL import Image, ImageDraw

BG = (16, 16, 20, 255)
FG = (108, 140, 255, 255)
SS = 4  # supersample factor


def draw_mark(size, transparent=False, inset=0.0):
    """Returns an RGBA image of the mark at the given size."""
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0) if transparent else BG)
    d = ImageDraw.Draw(img)

    # Work in a 0..1 space, then scale, so the geometry reads the same at every size.
    pad = inset
    span = 1.0 - 2 * pad

    def px(x, y):
        return (int((pad + x * span) * s), int((pad + y * span) * s))

    def rect(x0, y0, x1, y1, r=None):
        a, b = px(x0, y0), px(x1, y1)
        if r:
            d.rounded_rectangle([a, b], radius=int(r * span * s), fill=FG)
        else:
            d.rectangle([a, b], fill=FG)

    def ellipse(cx, cy, rx, ry):
        d.ellipse([px(cx - rx, cy - ry), px(cx + rx, cy + ry)], fill=FG)

    # Beam joining the two stems.
    d.polygon(
        [px(0.34, 0.22), px(0.76, 0.14), px(0.76, 0.28), px(0.34, 0.36)],
        fill=FG,
    )

    # Stems.
    rect(0.34, 0.22, 0.395, 0.72)
    rect(0.705, 0.14, 0.76, 0.60)

    # Note heads, tilted the way engraved notes are.
    ellipse(0.275, 0.725, 0.105, 0.082)
    ellipse(0.645, 0.605, 0.105, 0.082)

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
        draw_mark(size, transparent=True, inset=0.22).save(path)
        written.append(path)

    # Multi resolution ICO for Windows and favicons.
    ico = os.path.join(out_dir, "icon.ico")
    draw_mark(256).save(ico, sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    written.append(ico)

    for path in written:
        print(f"{os.path.getsize(path):>8,}  {os.path.basename(path)}")


if __name__ == "__main__":
    main(sys.argv[1])
