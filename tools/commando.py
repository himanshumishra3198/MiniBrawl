"""Para SF commando sprites, drawn from scratch.

Side view, not front. A front-facing character cannot hold a rifle convincingly —
that is what made an earlier astronaut look wrong the moment a gun was attached.
Side view also gives a silhouette that reads as a soldier: helmet, plate carrier,
rifle, boots.

The weapon is not a prop laid on top. The firing arm, support arm and rifle are
one sprite pivoting at the shoulder, which is how a 2D shooter keeps the muzzle
attached through a full 360 degrees of aim.

Uniform takes the seat colour, kit stays neutral: the largest coloured area on
screen is the one that says which player this is. Six of these have to be told
apart while moving, on a phone.

Drawn at 4x and downsampled — Pillow has no antialiased primitives.
"""
import colorsys
import math
import os
from PIL import Image, ImageDraw

S = 4
W, H = 64, 120           # 120px at 100 ppu = 1.2 world units

# Walk is a four-frame cycle, not two poses. Two frames read as a shuffle no
# matter how far apart the legs are, because a real walk passes through a low
# point and a high point that two frames cannot express.
WALK_FRAMES = 4

GEAR      = (66, 72, 82)
GEAR_LIT  = (92, 100, 112)
STRAP     = (33, 36, 42)
BOOT      = (28, 30, 35)
SKIN      = (206, 158, 118)
SKIN_DARK = (170, 126, 92)
VISOR     = (26, 30, 38)
VISOR_LIT = (130, 205, 225)
METAL     = (98, 106, 120)
METAL_LIT = (155, 165, 182)


def mul(rgb, f):
    return tuple(max(0, min(255, int(c * f))) for c in rgb)


def uniform_from_seat(rgb01):
    """Seat colour pushed towards something a uniform could plausibly be.

    The palette is pastel because it was picked for readable squares. Worn flat
    on a soldier it looks like pyjamas, so saturation rises and value drops — far
    enough to read as field dress, not so far that six collapse into one olive.
    """
    h, s, v = colorsys.rgb_to_hsv(*rgb01)
    return tuple(int(c * 255) for c in
                 colorsys.hsv_to_rgb(h, min(1.0, s * 1.45 + 0.12), v * 0.62))


class Pen:
    def __init__(self, d):
        self.d = d

    def limb(self, a, b, t, fill):
        """A bone as a capsule. Tapered quads came out as wedges."""
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


def head(p, hx, hy):
    """Helmet, goggles, balaclava.

    Drawn as a jaw that juts forward rather than a circle. A round head reads as
    a mascot, which is most of what was wrong with the first attempt — at this
    size the shape of the skull is the only facial detail that survives.
    """
    p.rr((hx - 8, hy - 6, hx + 8, hy + 11), 7, SKIN)          # skull
    p.rr((hx + 2, hy + 1, hx + 12, hy + 10), 3.5, SKIN)       # jaw, forward
    p.rr((hx + 8, hy + 3, hx + 13, hy + 9), 2.5, SKIN_DARK)   # chin shadow

    # Balaclava over nose and mouth, leaving a strip of cheek above it.
    p.rr((hx - 8, hy + 4, hx + 13, hy + 12), 3, STRAP)
    p.rr((hx - 7, hy + 9, hx + 9, hy + 13), 2.5, mul(STRAP, 0.85))

    # Helmet: dome plus a brow that overhangs the goggles.
    p.pie((hx - 10, hy - 13, hx + 11, hy + 8), 180, 360, GEAR)
    p.rr((hx - 10, hy - 4, hx + 11, hy - 0.5), 1.5, mul(GEAR, 0.78))
    p.rr((hx + 6, hy - 4, hx + 12, hy - 0.5), 1.5, mul(GEAR, 0.9))   # brow
    p.rr((hx - 10, hy - 9, hx - 7, hy - 2), 1, GEAR_LIT)             # rear pad

    # Goggles sit under the brow. The glint is what makes them read as glass.
    p.rr((hx - 1, hy - 2, hx + 12, hy + 4), 2, VISOR)
    p.rr((hx + 6, hy - 1, hx + 11, hy + 1.5), 1, VISOR_LIT)
    p.rr((hx - 9, hy - 1, hx + 1, hy + 2), 1, mul(STRAP, 1.1))       # strap


def leg(p, hip, knee, foot, uni, back=False):
    f = 0.70 if back else 1.0
    p.limb(hip, knee, 9.5, mul(uni, f))
    p.limb(knee, foot, 8, mul(uni, f))
    bx, by = foot
    p.rr((bx - 5, by - 3, bx + 8, by + 4), 2, mul(BOOT, 0.8 if back else 1.0))


def walk_leg(phase, hip_x, knee_y, foot_y, reach=11.0, lift=7.0):
    """One leg at a point in the cycle, as (knee, foot).

    Horizontal swing alone is not a walk. Swinging both legs on a plain sine put
    the two passing positions in the same place, so half a four-frame cycle was a
    duplicate of the other half and the whole thing read as a shuffle. The foot
    has to leave the ground on the way forward — that is what separates the two
    passing frames from each other, and a walk from a slide.

    phase 0 is contact with this leg forward; 0.5 is contact with it behind.
    """
    a = 2 * math.pi * phase
    x = math.cos(a)
    up = max(0.0, -math.sin(a))          # peaks mid-swing, zero while planted
    return ((hip_x + x * reach * 0.5, knee_y - up * lift * 0.55),
            (hip_x + x * reach, foot_y - up * lift))


def body(seat_rgb01, pose):
    uni = uniform_from_seat(seat_rgb01)
    img = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
    p = Pen(ImageDraw.Draw(img))

    cx = 29
    hip_y, knee_y, foot_y = 78, 99, 114
    drop = 0
    lean = 0

    if pose.startswith('walk'):
        t = (int(pose[4:]) - 1) / WALK_FRAMES
        # The body is lowest at contact, when the legs are spread and neither is
        # carrying it straight. Without the drop the torso glides.
        drop, lean = abs(math.cos(2 * math.pi * t)) * 2.0, 1
        front = walk_leg(t, cx + 2, knee_y, foot_y)
        back = walk_leg(t + 0.5, cx - 2, knee_y, foot_y)
    elif pose == 'jump':
        drop, lean = -1, 0
        front = (cx + 8, knee_y - 10), (cx + 5, foot_y - 12)
        back = (cx + 3, knee_y - 3), (cx + 12, foot_y - 3)
    elif pose == 'hurt':
        drop, lean = 2, -3
        front = (cx + 6, knee_y), (cx + 10, foot_y)
        back = (cx - 8, knee_y), (cx - 13, foot_y)
    else:  # stand
        front = (cx + 5, knee_y), (cx + 7, foot_y)
        back = (cx - 4, knee_y), (cx - 7, foot_y)

    top = hip_y + drop
    leg(p, (cx - 2, top), back[0], back[1], uni, back=True)
    leg(p, (cx + 2, top), front[0], front[1], uni)

    ty = 34 + drop                                   # torso top
    p.limb((cx - 1 + lean, ty + 8), (cx + 5 + lean, ty + 24), 7, mul(uni, 0.68))

    p.rr((cx - 10, top - 10, cx + 10, top + 4), 4, mul(uni, 0.88))          # hips
    p.rr((cx - 10 + lean, ty, cx + 10 + lean, top - 4), 6, uni)             # torso
    p.rr((cx - 9 + lean, ty + 2, cx + 9 + lean, ty + 16), 5, mul(uni, 1.12))

    p.rr((cx - 7 + lean, ty + 6, cx + 8 + lean, top - 16), 3, GEAR)         # carrier
    p.rr((cx - 5 + lean, ty + 13, cx - 1 + lean, ty + 21), 1.5, GEAR_LIT)
    p.rr((cx + 1 + lean, ty + 13, cx + 5 + lean, ty + 21), 1.5, GEAR_LIT)
    p.rr((cx - 10 + lean, ty + 2, cx + 10 + lean, ty + 6), 1.5, STRAP)
    p.rr((cx - 10, top - 9, cx + 10, top - 4), 1.5, STRAP)                  # belt
    p.rr((cx + 3, top + 2, cx + 9, top + 13), 2, GEAR)                      # thigh rig

    p.rr((cx + 1 + lean, ty - 7, cx + 6 + lean, ty + 2), 2.5, SKIN_DARK)    # neck
    head(p, cx + 1 + lean, ty - 18)

    return img.resize((W, H), Image.LANCZOS)


# --- weapon arm -------------------------------------------------------------
GW, GH = 102, 40
GRIP = (18, 20)


def weapon_arm(seat_rgb01):
    """Firing arm and rifle as one sprite, pivoting at the shoulder."""
    uni = uniform_from_seat(seat_rgb01)
    img = Image.new('RGBA', (GW * S, GH * S), (0, 0, 0, 0))
    p = Pen(ImageDraw.Draw(img))

    sx, sy = GRIP
    st = sx + 3

    # Rifle first, arms over it, so the hands read as gripping the weapon.
    p.rr((st, sy - 4, st + 18, sy + 4), 2.5, mul(GEAR, 0.86))            # stock
    p.rr((st + 16, sy - 7, st + 42, sy + 4), 2.5, GEAR)                  # receiver
    p.rr((st + 20, sy - 11, st + 33, sy - 6), 1.5, mul(GEAR, 1.25))      # optic
    p.rr((st + 24, sy - 14, st + 29, sy - 10), 1, mul(GEAR, 1.1))
    p.rr((st + 26, sy + 4, st + 36, sy + 17), 2, mul(GEAR, 0.78))        # magazine
    p.rr((st + 41, sy - 3, st + 71, sy + 1.5), 2, METAL)                 # barrel
    p.rr((st + 45, sy - 5, st + 58, sy + 3), 1.5, mul(GEAR, 1.05))       # handguard
    p.rr((st + 68, sy - 5, st + 75, sy + 3), 1.5, METAL_LIT)             # muzzle

    p.limb((sx, sy), (sx + 14, sy + 11), 8, mul(uni, 0.95))              # upper arm
    p.limb((sx + 14, sy + 11), (st + 25, sy + 8), 7, uni)                # forearm
    p.ell((st + 21, sy + 4, st + 30, sy + 13), STRAP)                    # trigger hand

    # Support arm routed under the magazine with a real elbow. Straight from
    # shoulder to handguard it lay across the receiver and optic — the parts
    # that make the shape read as a rifle.
    p.limb((sx + 4, sy + 6), (sx + 24, sy + 15), 6, mul(uni, 1.14))
    p.limb((sx + 24, sy + 15), (st + 50, sy + 5), 6, mul(uni, 1.14))
    p.ell((st + 46, sy, st + 55, sy + 9), STRAP)                         # support hand

    return img.resize((GW, GH), Image.LANCZOS)


SEATS = [
    ('cyan',   (0.35, 0.85, 1.00)),
    ('green',  (0.55, 0.95, 0.45)),
    ('amber',  (1.00, 0.75, 0.30)),
    ('violet', (0.80, 0.55, 1.00)),
    ('pink',   (1.00, 0.55, 0.75)),
    ('white',  (0.95, 0.95, 0.95)),
]
POSES = ['stand', 'jump', 'hurt'] + [f'walk{i + 1}' for i in range(WALK_FRAMES)]

if __name__ == '__main__':
    os.makedirs('gen/Characters', exist_ok=True)
    os.makedirs('gen/Weapons', exist_ok=True)
    for i, (name, rgb) in enumerate(SEATS):
        for pose in POSES:
            body(rgb, pose).save(f'gen/Characters/player{i}_{pose}.png')
        weapon_arm(rgb).save(f'gen/Weapons/arm{i}.png')
    print(f'body {W}x{H}, arm {GW}x{GH} pivot {GRIP}, {WALK_FRAMES}-frame walk')
    print(f'wrote {len(SEATS) * len(POSES)} frames + {len(SEATS)} arms')
