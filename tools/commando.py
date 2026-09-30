"""Para SF commando sprites, drawn from scratch.

Side view, not front. A front-facing character cannot hold a rifle convincingly —
that is what made the previous astronaut look wrong the moment a gun was attached
to it. Side view also gives a silhouette that reads as a soldier at the size these
are actually drawn: helmet, plate carrier, rifle, boots.

The weapon is not a separate prop laid on top. The firing arm and rifle are one
sprite that pivots at the shoulder, which is how a 2D shooter keeps the muzzle
attached to the body through a full 360 degrees of aim.

Uniform takes the seat colour and the kit stays neutral, so the largest coloured
area on screen is the one that says which player this is. Six of these have to be
told apart while moving, on a phone.

Drawn at 4x and downsampled: Pillow has no antialiased primitives, and hard pixel
edges look wrong against the smooth UI art.
"""
import colorsys
import os
from PIL import Image, ImageDraw

S = 4                      # supersample
W, H = 48, 92              # body sprite, feet on the bottom edge

# Kit is deliberately near-neutral. Anything that competes with the uniform for
# attention costs player identification.
GEAR      = (48, 52, 58)
GEAR_LIT  = (70, 76, 84)
STRAP     = (34, 37, 42)
BOOT      = (30, 32, 37)
SKIN      = (198, 150, 112)
SKIN_DARK = (168, 124, 90)
VISOR     = (28, 32, 40)
VISOR_LIT = (120, 190, 210)
METAL     = (96, 104, 118)
METAL_LIT = (150, 160, 176)


def mul(rgb, f):
    return tuple(max(0, min(255, int(c * f))) for c in rgb)


def uniform_from_seat(rgb01):
    """Seat colour, pushed towards something a uniform could plausibly be.

    The palette is pastel because it was picked for readable squares. Worn flat
    on a soldier it looks like pyjamas, so saturation comes up and value comes
    down — far enough to read as field dress, not so far that six of them
    collapse into the same olive.
    """
    h, s, v = colorsys.rgb_to_hsv(*rgb01)
    s = min(1.0, s * 1.45 + 0.12)
    v = v * 0.62
    return tuple(int(c * 255) for c in colorsys.hsv_to_rgb(h, s, v))


class Pen:
    def __init__(self, d):
        self.d = d

    def limb(self, a, b, t, fill):
        """A bone as a capsule.

        The first pass drew limbs as tapered quads and they came out as wedges —
        the firing arm ended up a solid triangle wide enough to hide the rifle
        behind it. Constant thickness with round caps is both simpler and what
        a limb actually looks like.
        """
        self.d.line([a[0] * S, a[1] * S, b[0] * S, b[1] * S], fill=fill, width=int(t * S))
        for (x, y) in (a, b):
            self.d.ellipse([(x - t / 2) * S, (y - t / 2) * S,
                            (x + t / 2) * S, (y + t / 2) * S], fill=fill)

    def rr(self, box, r, fill):
        self.d.rounded_rectangle([c * S for c in box], radius=r * S, fill=fill)

    def ell(self, box, fill):
        self.d.ellipse([c * S for c in box], fill=fill)

    def pie(self, box, a, b, fill):
        self.d.pieslice([c * S for c in box], a, b, fill=fill)

    def poly(self, pts, fill):
        self.d.polygon([(x * S, y * S) for x, y in pts], fill=fill)


def leg(p, hip_x, knee_x, foot_x, uni, top=60, knee=76, bottom=87, back=False):
    """Thigh, shin and boot. Back legs darken so the pair reads as depth."""
    f = 0.70 if back else 1.0
    trouser = mul(uni, f)
    boot = mul(BOOT, 0.8 if back else 1.0)

    p.limb((hip_x, top), (knee_x, knee), 7.5, trouser)
    p.limb((knee_x, knee), (foot_x, bottom), 6.5, trouser)
    p.rr((foot_x - 4, bottom - 2, foot_x + 6, bottom + 4), 1.5, boot)


def body(seat_rgb01, pose):
    """One body frame, facing right, feet on the bottom edge of the canvas."""
    uni = uniform_from_seat(seat_rgb01)
    img = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
    p = Pen(ImageDraw.Draw(img))

    cx = 22
    lean = 0
    head_y = 0
    torso_top = 34

    if pose == 'walk1':
        legs = ((cx - 1, cx - 7, cx - 11, 87), (cx + 1, cx + 8, cx + 12, 87))
        lean, head_y = 1, 1
    elif pose == 'walk2':
        # Opposite stride, with the trailing boot lifted: without a height change
        # the two frames only differ in x and the walk reads as a shuffle.
        legs = ((cx - 1, cx + 6, cx + 10, 87), (cx + 1, cx - 6, cx - 9, 83))
        lean, head_y = 1, 0
    elif pose == 'jump':
        legs = ((cx - 2, cx + 6, cx + 3, 76), (cx + 2, cx + 11, cx + 15, 82))
        head_y = -1
    elif pose == 'hurt':
        legs = ((cx - 3, cx - 9, cx - 13, 87), (cx + 2, cx + 6, cx + 9, 87))
        lean, head_y = -2, 1
    else:  # stand
        legs = ((cx - 2, cx - 5, cx - 7, 87), (cx + 2, cx + 5, cx + 8, 87))

    (bhx, bkx, bfx, bby), (fhx, fkx, ffx, fby) = legs
    knee_y = 70 if pose == 'jump' else 76

    leg(p, bhx, bkx, bfx, uni, knee=knee_y, bottom=bby, back=True)
    leg(p, fhx, fkx, ffx, uni, knee=knee_y, bottom=fby)

    # Support arm behind the torso, tucked in to the body. It used to reach
    # forward, which fought the firing arm for the same space.
    p.limb((cx - 1 + lean, 41), (cx + 4 + lean, 53), 5.5, mul(uni, 0.68))

    # Hips, then torso.
    p.rr((cx - 8, 56, cx + 8, 66), 3, mul(uni, 0.88))
    p.rr((cx - 8 + lean, torso_top, cx + 8 + lean, 60), 4.5, uni)
    p.rr((cx - 7 + lean, torso_top + 2, cx + 7 + lean, 44), 4, mul(uni, 1.12))

    # Plate carrier, magazine pouches, straps, belt.
    p.rr((cx - 7 + lean, 38, cx + 7 + lean, 56), 2.5, GEAR)
    p.rr((cx - 5 + lean, 44, cx - 1 + lean, 51), 1, GEAR_LIT)
    p.rr((cx + 1 + lean, 44, cx + 5 + lean, 51), 1, GEAR_LIT)
    p.rr((cx - 8 + lean, 36, cx + 8 + lean, 39), 1, STRAP)
    p.rr((cx - 8, 55, cx + 8, 59), 1, STRAP)
    p.rr((cx + 2, 62, cx + 7, 70), 1.5, GEAR)      # thigh rig

    # Neck and head. The head sits forward of centre: he is looking downrange.
    p.rr((cx + 1 + lean, 30 + head_y, cx + 5 + lean, 37 + head_y), 2, SKIN_DARK)

    hx, hy = cx + 1 + lean, 21 + head_y
    p.ell((hx - 7, hy - 8, hx + 8, hy + 8), SKIN)
    p.rr((hx + 4, hy - 1, hx + 10, hy + 5), 2, SKIN)           # jaw forward

    # Helmet, balaclava and goggles — the three shapes that say special forces
    # rather than infantry once the sprite is too small for anything else.
    p.pie((hx - 8, hy - 11, hx + 9, hy + 7), 180, 360, GEAR)
    p.rr((hx - 8, hy - 3, hx + 9, hy - 0.5), 1, mul(GEAR, 0.78))
    p.rr((hx - 7, hy + 3, hx + 9, hy + 8), 2, STRAP)
    p.rr((hx, hy - 1.5, hx + 9, hy + 3.5), 1.5, VISOR)
    p.rr((hx + 5, hy - 0.5, hx + 8, hy + 1), 0.5, VISOR_LIT)

    return img.resize((W, H), Image.LANCZOS)


# --- weapon arm -------------------------------------------------------------
GW, GH = 78, 30
GRIP = (14, 15)       # shoulder pivot, in final pixels


def weapon_arm(seat_rgb01):
    """Firing arm and rifle as one sprite, pivoting at the shoulder.

    Everything is placed relative to GRIP, because that point becomes the
    transform's origin in Unity: the sprite swings around it through a full
    circle and the muzzle has to stay on the far end the whole way.
    """
    uni = uniform_from_seat(seat_rgb01)
    img = Image.new('RGBA', (GW * S, GH * S), (0, 0, 0, 0))
    p = Pen(ImageDraw.Draw(img))

    sx, sy = GRIP
    stock = sx + 2

    # Rifle first, arms over it, so the hands read as gripping rather than
    # floating beside the weapon.
    p.rr((stock, sy - 3, stock + 14, sy + 3), 2, mul(GEAR, 0.86))          # stock
    p.rr((stock + 12, sy - 5, stock + 32, sy + 3), 2, GEAR)                # receiver
    p.rr((stock + 15, sy - 8, stock + 25, sy - 4), 1, mul(GEAR, 1.25))     # optic
    p.poly([(stock + 20, sy + 3), (stock + 27, sy + 3),
            (stock + 28, sy + 13), (stock + 22, sy + 13)], mul(GEAR, 0.78))  # magazine
    p.rr((stock + 31, sy - 2.5, stock + 54, sy + 1), 1.5, METAL)           # barrel
    p.rr((stock + 34, sy - 4, stock + 44, sy + 2), 1, mul(GEAR, 1.05))     # handguard
    p.rr((stock + 52, sy - 4, stock + 57, sy + 2), 1, METAL_LIT)           # muzzle

    # Firing arm: shoulder to elbow to the pistol grip, constant thickness.
    p.limb((sx, sy), (sx + 11, sy + 8), 6, mul(uni, 0.95))
    p.limb((sx + 11, sy + 8), (stock + 19, sy + 6), 5.5, uni)
    p.ell((stock + 16, sy + 3, stock + 23, sy + 10), STRAP)                # trigger hand

    # Support arm, routed under the magazine with a real elbow. Drawn straight
    # from shoulder to handguard it was a bar across the middle of the weapon,
    # hiding the receiver and optic — the parts that make it read as a rifle.
    p.limb((sx + 3, sy + 4), (sx + 16, sy + 14), 4.5, mul(uni, 1.14))
    p.limb((sx + 16, sy + 14), (stock + 38, sy + 4), 4.5, mul(uni, 1.14))
    p.ell((stock + 35, sy, stock + 42, sy + 7), STRAP)                     # support hand

    return img.resize((GW, GH), Image.LANCZOS)


SEATS = [
    ('cyan',   (0.35, 0.85, 1.00)),
    ('green',  (0.55, 0.95, 0.45)),
    ('amber',  (1.00, 0.75, 0.30)),
    ('violet', (0.80, 0.55, 1.00)),
    ('pink',   (1.00, 0.55, 0.75)),
    ('white',  (0.95, 0.95, 0.95)),
]
POSES = ['stand', 'walk1', 'walk2', 'jump', 'hurt']

if __name__ == '__main__':
    out = 'gen'
    os.makedirs(f'{out}/Characters', exist_ok=True)
    os.makedirs(f'{out}/Weapons', exist_ok=True)

    for i, (name, rgb) in enumerate(SEATS):
        for pose in POSES:
            body(rgb, pose).save(f'{out}/Characters/player{i}_{pose}.png')
        weapon_arm(rgb).save(f'{out}/Weapons/arm{i}.png')

    print(f'body {W}x{H}, arm {GW}x{GH} pivot {GRIP}')
    print(f'wrote {len(SEATS)*len(POSES)} frames + {len(SEATS)} arms')
