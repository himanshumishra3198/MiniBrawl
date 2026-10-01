"""Palm trees for the island map.

Kenney's background pack carries thirty-five trees and every one of them is a
conifer, a cactus or a round broadleaf. None of those say island, and a palm is
simple enough to draw: a trunk that leans, a few fronds thrown off the top, and
a couple of coconuts.

Drawn rather than downloaded for the same reason as the commandos — and because
a palm needs to lean in a chosen direction so a row of them does not look
stamped out of one mould.
"""
import math
import os
import random

from PIL import Image, ImageDraw

S = 4

TRUNK      = (122, 92, 60)
TRUNK_DARK = (94, 70, 46)
FROND      = (62, 126, 68)
FROND_DARK = (44, 96, 54)
FROND_LIT  = (86, 158, 86)
COCONUT    = (86, 66, 44)


def palm(width, height, lean, seed):
    """One palm, leaning by `lean` final pixels over its height."""
    rng = random.Random(seed)
    img = Image.new('RGBA', (width * S, height * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    base_x = width * 0.5 - lean * 0.5
    top_x = base_x + lean
    top_y = height * 0.30

    # Trunk, as a stack of short segments following a curve. One tapered polygon
    # would be straight, and a straight palm looks like a lamp post.
    steps = 16
    for i in range(steps):
        a, b = i / steps, (i + 1) / steps
        ax = base_x + (top_x - base_x) * (a * a)
        bx = base_x + (top_x - base_x) * (b * b)
        ay = height - (height - top_y) * a
        by = height - (height - top_y) * b
        w = (height * 0.055) * (1.0 - 0.45 * a)
        d.line([ax * S, ay * S, bx * S, by * S],
               fill=TRUNK if i % 2 == 0 else TRUNK_DARK, width=int(w * S))

    # Fronds, swept from the crown. Each is a tapered triangle bent downwards so
    # the tip droops, which is most of what makes a palm read as a palm.
    count = rng.randint(6, 7)
    for i in range(count):
        spread = math.pi * (0.08 + 0.84 * (i / max(1, count - 1)))
        angle = math.pi - spread + rng.uniform(-0.09, 0.09)
        length = height * rng.uniform(0.30, 0.42)
        droop = rng.uniform(0.35, 0.65)

        tipx = top_x + math.cos(angle) * length
        tipy = top_y - math.sin(angle) * length + length * droop
        midx = (top_x + tipx) * 0.5
        midy = (top_y + tipy) * 0.5 - length * 0.22

        thick = height * 0.055
        shade = FROND_LIT if i % 3 == 0 else (FROND if i % 3 == 1 else FROND_DARK)
        d.polygon([(top_x * S, top_y * S),
                   ((midx - thick * 0.5) * S, (midy - thick * 0.5) * S),
                   (tipx * S, tipy * S),
                   ((midx + thick * 0.9) * S, (midy + thick * 0.9) * S)], fill=shade)

    r = height * 0.030
    for dx, dy in ((-0.9, 0.9), (0.6, 1.2), (1.5, 0.6)):
        cx, cy = top_x + dx * r * 1.8, top_y + dy * r * 1.8
        d.ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=COCONUT)

    return img.resize((width, height), Image.LANCZOS)


if __name__ == '__main__':
    out = 'gen/Island'
    os.makedirs(out, exist_ok=True)

    # Leans alternate so a row of them reads as a stand of trees, not a repeat.
    for i, (w, h, lean) in enumerate([(150, 210, 16), (130, 180, -13), (165, 240, 7)]):
        palm(w, h, lean, seed=41 + i * 13).save(f'{out}/palm{i}.png')

    print('wrote 3 palms to', out)
