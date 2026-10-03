"""The level tileset.

What was there before was one tile repeated everywhere, tinted flat, with a green
strip laid along the top. That reads as a slab because it is missing the three
things that make 2D ground look like ground:

  a surface   — grass that meets the dirt on an irregular line and hangs over the
                edge, rather than a straight band of a second colour
  ends        — a platform that stops should look rounded off, not sliced; a cut
                edge is the clearest sign a tile was repeated rather than placed
  variation   — two fills alternated, because the eye finds a repeat in about
                three tiles and then sees nothing else

Lit from above throughout, matching the characters, so the level and the people
standing on it agree about where the sun is.

64px tiles at 128 pixels per unit: half a world unit each, a third of a player's
height, which is the scale platformers tend to land on because it lets a ledge be
one tile and still look deliberate.
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

S = 4
T = 64                      # tile edge, final pixels

GRASS_LIT  = (104, 158, 74)
GRASS      = (74, 126, 56)
GRASS_DARK = (52, 94, 44)
DIRT_LIT   = (122, 92, 62)
DIRT       = (96, 71, 48)
DIRT_DARK  = (68, 50, 34)
DIRT_DEEP  = (48, 36, 26)
PEBBLE     = (138, 112, 84)
PEBBLE_D   = (74, 57, 42)


def canvas(w=T, h=T):
    img = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def rr(d, box, r, fill):
    d.rounded_rectangle([c * S for c in box], radius=r * S, fill=fill)


def rect(d, box, fill):
    d.rectangle([c * S for c in box], fill=fill)


def speckle(d, rng, box, count, colours, lo=1.0, hi=2.6):
    """Pebbles and grit. What stops a flat fill reading as plastic."""
    x0, y0, x1, y1 = box
    for _ in range(count):
        x = rng.uniform(x0, x1)
        y = rng.uniform(y0, y1)
        r = rng.uniform(lo, hi)
        d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S],
                  fill=colours[rng.randrange(len(colours))])


def grass_edge(d, rng, y, width=T, overhang=True):
    """An irregular grass-to-dirt boundary, with blades hanging over it.

    A straight line here is the single biggest tell that ground was drawn by a
    program. The boundary wanders, and a few blades drop below it.
    """
    points = [(0, 0), (width, 0)]
    step = 4
    lower = []
    for x in range(width, -1, -step):
        wobble = math.sin(x * 0.21 + rng.random() * 2.0) * 1.6 + rng.uniform(-1.2, 1.2)
        lower.append((x, y + wobble))
    d.polygon([(p[0] * S, p[1] * S) for p in points + lower], fill=GRASS)

    # Lit crown along the very top.
    d.rectangle([0, 0, width * S, 3 * S], fill=GRASS_LIT)

    if not overhang:
        return
    for _ in range(width // 9):
        x = rng.uniform(1, width - 1)
        w = rng.uniform(1.6, 3.4)
        drop = rng.uniform(2.5, 6.5)
        d.rounded_rectangle([(x - w / 2) * S, (y - 1) * S, (x + w / 2) * S, (y + drop) * S],
                            radius=w * 0.5 * S, fill=GRASS_DARK)


def ground_top(seed):
    """Surface tile: grass over dirt."""
    rng = random.Random(seed)
    img, d = canvas()
    rect(d, (0, 0, T, T), DIRT)
    speckle(d, rng, (2, 22, T - 2, T - 2), 14, [PEBBLE, PEBBLE_D, DIRT_LIT])
    rect(d, (0, T - 5, T, T), DIRT_DARK)          # shade where the next tile begins
    grass_edge(d, rng, 20)
    return img.resize((T, T), Image.LANCZOS)


def ground_fill(seed):
    """Body tile: dirt, pebbles, and a hint of strata."""
    rng = random.Random(seed)
    img, d = canvas()
    rect(d, (0, 0, T, T), DIRT)
    for _ in range(3):
        y = rng.uniform(6, T - 10)
        h = rng.uniform(2.5, 5.5)
        rr(d, (-2, y, T + 2, y + h), 2, DIRT_DARK)
    speckle(d, rng, (2, 2, T - 2, T - 2), 22, [PEBBLE, PEBBLE_D, DIRT_LIT, DIRT_DEEP])
    rect(d, (0, T - 4, T, T), DIRT_DARK)
    return img.resize((T, T), Image.LANCZOS)


def edge_cap(seed, right=False):
    """The end of a platform, rounded and darkened so it reads as a corner."""
    rng = random.Random(seed)
    w = T // 2
    img, d = canvas(w, T)

    if right:
        rr(d, (-8, 0, w - 1, T), 7, DIRT)
        rect(d, (w - 6, 0, w, T), DIRT_DARK)
    else:
        rr(d, (1, 0, w + 8, T), 7, DIRT)
        rect(d, (0, 0, 6, T), DIRT_DARK)

    speckle(d, rng, (3, 24, w - 3, T - 4), 7, [PEBBLE_D, DIRT_LIT])

    # Grass follows the curve over the top of the cap.
    if right:
        rr(d, (-8, 0, w - 2, 20), 7, GRASS)
        rect(d, (0, 0, w - 3, 3), GRASS_LIT)
    else:
        rr(d, (2, 0, w + 8, 20), 7, GRASS)
        rect(d, (3, 0, w, 3), GRASS_LIT)

    rect(d, (0, T - 4, w, T), DIRT_DARK)
    return img.resize((w, T), Image.LANCZOS)


def rock(seed, w, h):
    """A boulder for the background, to break long empty stretches."""
    rng = random.Random(seed)
    img, d = canvas(w, h)
    body = (72, 78, 86)
    lit = (98, 106, 116)
    dark = (48, 53, 60)

    rr(d, (2, h * 0.22, w - 2, h), h * 0.22, body)
    rr(d, (w * 0.16, h * 0.10, w * 0.78, h * 0.62), h * 0.20, lit)
    rr(d, (2, h - h * 0.22, w - 2, h), h * 0.14, dark)
    speckle(d, rng, (4, h * 0.3, w - 4, h - 3), 8, [dark, lit])
    return img.resize((w, h), Image.LANCZOS)


def pair(left, right):
    """Two tiles side by side, so the repeating unit is two wide.

    A tiled SpriteRenderer repeats one sprite, and the eye finds a one-tile
    rhythm almost immediately — a long floor turns into visible stripes. Baking
    two different tiles into the sprite doubles the period for nothing, which is
    the cheapest variation available without a second renderer.
    """
    out = Image.new('RGBA', (left.width + right.width, left.height), (0, 0, 0, 0))
    out.paste(left, (0, 0))
    out.paste(right, (left.width, 0))
    return out


def _main():
    out = 'gen/Tiles'
    os.makedirs(out, exist_ok=True)

    pair(ground_top(11), ground_top(29)).save(f'{out}/ground_top.png')
    pair(ground_fill(17), ground_fill(43)).save(f'{out}/ground_fill.png')
    edge_cap(7, right=False).save(f'{out}/edge_left.png')
    edge_cap(7, right=True).save(f'{out}/edge_right.png')
    rock(5, 120, 76).save(f'{out}/rock_a.png')
    rock(19, 80, 54).save(f'{out}/rock_b.png')

    print(f'wrote tileset to {out}')
    _background(out)


def _background(out):
    hills(2, 640, 200, near=False).save(f'{out}/hills_far.png')
    hills(8, 640, 220, near=True).save(f'{out}/hills_near.png')
    sea(640, 160).save(f'{out}/sea.png')
    print('wrote background bands')


# --- background ----------------------------------------------------------------
def hills(seed, w, h, near):
    """A band of silhouetted hills for the parallax background.

    Two of these at different speeds is most of what makes a flat arena read as a
    place.

    The far band is *darker* here, which is the opposite of the usual rule. Haze
    pulls distant things towards the colour of the sky, and this sky is a dark
    dusk blue — so distance means less contrast against a dark background, not
    more light. Lifting the far hills instead would make them advance in front of
    the near ones.
    """
    rng = random.Random(seed)
    img, d = canvas(w, h)

    base = (34, 58, 70) if near else (26, 44, 56)
    crest = (44, 72, 84) if near else (32, 52, 64)

    peaks = 5 if near else 7
    span = w / peaks
    for i in range(peaks + 1):
        cx = i * span + rng.uniform(-span * 0.25, span * 0.25)
        top = h * rng.uniform(0.18 if near else 0.38, 0.55 if near else 0.68)
        half = span * rng.uniform(0.75, 1.3)
        d.polygon([((cx - half) * S, h * S), (cx * S, top * S), ((cx + half) * S, h * S)],
                  fill=base)
        d.polygon([((cx - half * 0.35) * S, (top + h * 0.22) * S), (cx * S, top * S),
                   ((cx + half * 0.35) * S, (top + h * 0.22) * S)], fill=crest)

    d.rectangle([0, h * 0.86 * S, w * S, h * S], fill=base)
    return img.resize((w, h), Image.LANCZOS).filter(
        ImageFilter.GaussianBlur(0.4 if near else 0.9))


def sea(w, h):
    """A band of water under the island, with a few highlight lines."""
    img, d = canvas(w, h)
    rng = random.Random(3)
    d.rectangle([0, 0, w * S, h * S], fill=(22, 48, 66))
    d.rectangle([0, 0, w * S, h * 0.12 * S], fill=(38, 74, 94))
    for _ in range(26):
        y = rng.uniform(h * 0.15, h * 0.95)
        x = rng.uniform(0, w)
        length = rng.uniform(w * 0.03, w * 0.12)
        d.rounded_rectangle([x * S, y * S, (x + length) * S, (y + 1.4) * S],
                            radius=0.7 * S, fill=(46, 86, 108))
    return img.resize((w, h), Image.LANCZOS)


if __name__ == '__main__':
    _main()
