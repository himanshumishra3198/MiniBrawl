"""Crate icons: a medikit and the two weapons you can find.

The crates were coloured squares with a smaller square inside, which told you a
crate was there and nothing about what was in it. Colour alone is a poor carrier
here — it is already doing the job of saying which player is which, and a player
sprinting past a ledge has no time to decode a second colour code.

Drawn at 4x and downsampled, like the rest of the art.
"""
import os

from PIL import Image, ImageDraw

S = 4
SIZE = 64

CASE        = (242, 242, 236)
CASE_SHADE  = (206, 206, 198)
CASE_EDGE   = (120, 124, 128)
CROSS       = (214, 62, 58)
HANDLE      = (92, 96, 102)

GUN         = (58, 62, 70)
GUN_LIT     = (104, 110, 122)
GUN_METAL   = (150, 158, 172)


class Pen:
    def __init__(self, img):
        self.d = ImageDraw.Draw(img)

    def rr(self, box, r, fill):
        self.d.rounded_rectangle([c * S for c in box], radius=r * S, fill=fill)


def medikit():
    """A white case with a red cross and a carry handle."""
    img = Image.new('RGBA', (SIZE * S, SIZE * S), (0, 0, 0, 0))
    p = Pen(img)

    p.rr((22, 10, 42, 17), 2.5, HANDLE)          # handle, behind the case
    p.rr((23, 12, 41, 16), 1.5, (0, 0, 0, 0))    # punch the grip out

    p.rr((7, 16, 57, 54), 5, CASE_EDGE)          # outline
    p.rr((9, 18, 55, 52), 4, CASE)
    p.rr((9, 40, 55, 52), 4, CASE_SHADE)         # lower half, slightly shaded
    p.rr((9, 34, 55, 37), 0.5, CASE_EDGE)        # seam between the halves

    p.rr((27, 24, 37, 46), 1.5, CROSS)           # cross
    p.rr((21, 30, 43, 40), 1.5, CROSS)
    return img.resize((SIZE, SIZE), Image.LANCZOS)


def shotgun():
    """Side silhouette: thick barrel, pump underneath, heavy stock."""
    img = Image.new('RGBA', (SIZE * S, SIZE * S), (0, 0, 0, 0))
    p = Pen(img)

    p.rr((4, 30, 22, 42), 3.5, GUN)              # stock
    p.rr((18, 28, 40, 40), 2.5, GUN_LIT)         # receiver
    p.rr((38, 28, 60, 34), 2.5, GUN_METAL)       # barrel
    p.rr((36, 36, 54, 43), 2, GUN)               # pump
    p.rr((57, 27, 61, 35), 1.5, GUN_METAL)       # muzzle
    return img.resize((SIZE, SIZE), Image.LANCZOS)


def pistol():
    """Side silhouette: slide over an angled grip, no stock."""
    img = Image.new('RGBA', (SIZE * S, SIZE * S), (0, 0, 0, 0))
    p = Pen(img)

    p.rr((14, 22, 52, 32), 2.5, GUN_LIT)         # slide
    p.rr((18, 19, 44, 23), 1, GUN_METAL)         # sight rib
    p.rr((18, 30, 30, 48), 3, GUN)               # grip
    p.rr((28, 30, 36, 38), 2, GUN)               # trigger guard body
    p.rr((49, 23, 54, 31), 1.5, GUN_METAL)       # muzzle
    return img.resize((SIZE, SIZE), Image.LANCZOS)


if __name__ == '__main__':
    out = 'gen/Items'
    os.makedirs(out, exist_ok=True)
    medikit().save(f'{out}/item_health.png')
    shotgun().save(f'{out}/item_shotgun.png')
    pistol().save(f'{out}/item_pistol.png')
    print(f'wrote 3 crate icons to {out}')
