#!/usr/bin/env python3
"""Draws the mod's cover: a blueprint with an arrow pointing up above it, in
the brush strokes of Timber Empires' icons (Models/Icons/make_icons.py, found
next to this repo), on a blueprint-paper background.

    python3 Tools/make_cover.py [Timber Empires checkout]

Writes Media/cover.png (1920x1080, for the Workshop and mod.io pages) and
thumbnail.png (800x450, the one the game's mod list shows).
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
TE = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT.parent / "TimberEmpires"
sys.path.insert(0, str(TE / "Models" / "Icons"))
import make_icons as mi  # noqa: E402

W, H = 1920, 1080
ART = 760                       # the drawing's square
mi.SIZE = ART
mi.SCALE = ART * mi.SS / 100

PAPER = (28, 62, 104)
PAPER_LIGHT = (52, 102, 156)
LINE = (150, 190, 228)


def background():
    rnd = random.Random(3)
    # Paper lit from the middle
    cx, cy = W * 0.5, H * 0.46
    img = Image.new("RGB", (W, H))
    px = img.load()
    maxd = math.hypot(W / 2, H / 2)
    for y in range(H):
        for x in range(W):
            k = min(1, math.hypot(x - cx, (y - cy) * 1.25) / maxd)
            k = k ** 1.4
            px[x, y] = tuple(int(PAPER_LIGHT[i] + (PAPER[i] - PAPER_LIGHT[i]) * k) for i in range(3))
    img = img.convert("RGBA")

    # The grid: fine every 30px, heavier every 150px, drawn a touch uneven
    grid = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(grid)
    for n, x in enumerate(range(0, W + 1, 30)):
        major = n % 5 == 0
        d.line([(x, 0), (x, H)], fill=LINE + (70 if major else 26,), width=2 if major else 1)
    for n, y in enumerate(range(0, H + 1, 30)):
        major = n % 5 == 0
        d.line([(0, y), (W, y)], fill=LINE + (70 if major else 26,), width=2 if major else 1)
    img.alpha_composite(grid)

    # Drafting doodles off to the sides: a compass arc, a dimension line, a title block
    doo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(doo)
    ink = LINE + (95,)
    d.arc([-260, 620, 340, 1220], 270, 360, fill=ink, width=3)
    d.arc([-200, 680, 280, 1160], 270, 360, fill=ink, width=2)
    d.arc([1560, -300, 2160, 300], 90, 180, fill=ink, width=3)
    d.arc([1620, -240, 2100, 240], 90, 180, fill=ink, width=2)
    for x0, y0, x1 in ((120, 130, 520), (1400, 960, 1800)):
        d.line([(x0, y0), (x1, y0)], fill=ink, width=3)
        for x in (x0, x1):
            d.line([(x, y0 - 14), (x, y0 + 14)], fill=ink, width=3)
            d.line([(x - 8, y0 + 8), (x + 8, y0 - 8)], fill=ink, width=3)
    d.rectangle([1450, 860, 1830, 940], outline=ink, width=3)
    d.line([(1450, 900), (1830, 900)], fill=ink, width=2)
    d.line([(1600, 860), (1600, 940)], fill=ink, width=2)
    img.alpha_composite(doo)
    return img


def drawing():
    """The blueprint sheet and the arrow, 100 grid. Returns the strokes and the sheet's fill."""
    g = mi.Glyph()
    sheet = [(8, 42), (80, 42), (92, 54), (92, 94), (8, 94)]
    g.line(*sheet, (8, 42), taper=False, w=3.6)
    # The folded corner
    g.line((80, 42), (80, 54), (92, 54), w=2.6)
    # A floor plan: outer walls, rooms, a door's swing, windows
    g.line((18, 64), (82, 64), (82, 86), (18, 86), (18, 64), taper=False, w=2.6)
    g.line((46, 64), (46, 86), w=2.2)
    g.line((46, 76), (82, 76), w=2.2)
    g.line((18, 78), (28, 78), w=2.2)
    g.arc(28, 78, 8, 8, 180, 270, w=1.6)
    g.line((56, 64), (72, 64), w=4.2, taper=False)
    # Dimension line above the plan
    g.line((18, 58), (82, 58), w=1.6)
    g.line((18, 55), (18, 61), w=1.6)
    g.line((82, 55), (82, 61), w=1.6)
    # The arrow
    g.line((50, 36), (50, 11), w=5.6)
    g.line((34, 22), (50, 4), (66, 22), w=5.6)
    fill = [(x, y) for x, y in sheet]
    return g, fill


def main():
    img = background()
    g, sheet = drawing()
    SS = mi.SS
    strokes = g.mask.resize((ART, ART), Image.LANCZOS)

    # The sheet's paper, lighter than the board, and the arrow's soft shadow
    paper = Image.new("L", (ART * SS, ART * SS), 0)
    ImageDraw.Draw(paper).polygon([(x * mi.SCALE, y * mi.SCALE) for x, y in sheet], fill=255)
    paper = paper.resize((ART, ART), Image.LANCZOS)

    ox, oy = (W - ART) // 2, (H - ART) // 2 + 20
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    shadow = Image.new("L", (W, H), 0)
    shadow.paste(ImageChops.lighter(paper, strokes), (ox + 10, oy + 22))
    shadow = shadow.filter(ImageFilter.GaussianBlur(22)).point(lambda v: int(v * 0.55))
    black = Image.new("RGBA", (W, H), (6, 14, 30, 0))
    black.putalpha(shadow)
    img.alpha_composite(black)

    sheet_col = Image.new("RGBA", (ART, ART), (74, 128, 184, 0))
    sheet_col.putalpha(paper.point(lambda v: int(v * 0.55)))
    layer.alpha_composite(sheet_col, (ox, oy))

    # A glow behind the arrow
    glow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(glow).ellipse([W / 2 - 150, oy - 40, W / 2 + 150, oy + 260], fill=120)
    glow = glow.filter(ImageFilter.GaussianBlur(70))
    warm = Image.new("RGBA", (W, H), (230, 200, 130, 0))
    warm.putalpha(glow.point(lambda v: int(v * 0.5)))
    layer.alpha_composite(warm)

    gold = Image.new("RGBA", (ART, ART), mi.GOLD + (0,))
    gold.putalpha(strokes)
    layer.alpha_composite(gold, (ox, oy))
    img.alpha_composite(layer)

    # Grain, then the vignette
    rnd = random.Random(11)
    noise = Image.effect_noise((W, H), 38).convert("L")
    grain = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    grain.putalpha(noise.point(lambda v: max(0, v - 128) // 6))
    img.alpha_composite(grain)
    vig = Image.new("L", (W // 4, H // 4), 0)
    vp = vig.load()
    for y in range(vig.height):
        for x in range(vig.width):
            dx, dy = (x / vig.width - 0.5) * 2, (y / vig.height - 0.5) * 2
            vp[x, y] = int(255 * min(1, max(0, (math.hypot(dx, dy * 1.1) - 0.55) / 0.95)) ** 1.6 * 0.7)
    vig = vig.resize((W, H), Image.BICUBIC)
    dark = Image.new("RGBA", (W, H), (4, 10, 22, 0))
    dark.putalpha(vig)
    img.alpha_composite(dark)

    out = img.convert("RGB")
    (ROOT / "Media").mkdir(exist_ok=True)
    out.save(ROOT / "Media" / "cover.png")
    out.resize((800, 450), Image.LANCZOS).save(ROOT / "thumbnail.png")


main()
