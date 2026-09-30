"""The weapon sprite, drawn rather than downloaded.

Kenney's raygun is bright yellow and purple, which fights all six player colours
at once. A gun is also the one sprite whose pivot matters: it rotates a full 360
with the aim stick, so the grip has to sit at a known pixel or the barrel wobbles
around the body. Drawing it means both are exactly right.

Drawn at 4x and downsampled — Pillow has no antialiased primitives, and a hard
pixel edge looks wrong next to Kenney's smooth vector art.
"""
from PIL import Image, ImageDraw
import os

S = 4                       # supersample factor
W, H = 56, 22               # final pixel size; grip pivot at (11, 15)

BODY   = (58, 64, 78, 255)
BARREL = (108, 118, 135, 255)
ACCENT = (232, 236, 244, 255)
DARK   = (38, 42, 52, 255)

def rr(d, box, r, fill):
    d.rounded_rectangle([c * S for c in box], radius=r * S, fill=fill)

img = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

rr(d, (9, 13, 19, 21), 2, DARK)      # grip, under and behind the receiver
rr(d, (5, 6, 36, 14), 3, BODY)       # receiver
rr(d, (34, 8, 52, 12), 2, BARREL)    # barrel
rr(d, (50, 6, 55, 14), 2, ACCENT)    # muzzle ring, the bit that reads at distance
rr(d, (11, 8, 28, 10), 1, BARREL)    # top rail highlight

img = img.resize((W, H), Image.LANCZOS)
os.makedirs('gen/Weapons', exist_ok=True)
img.save('gen/Weapons/weapon_blaster.png')
print('weapon_blaster.png', img.size, '- grip pivot (11,15) -> normalized',
      round(11 / W, 4), round(1 - 15 / H, 4))
