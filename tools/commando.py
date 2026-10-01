"""Indian Para SF commandos, drawn from scratch.

Side view. A front-facing character cannot hold a rifle convincingly, and side
view gives a silhouette that reads as a soldier at the size this is played at.

The markers taken from reference photographs, in the order they survive being
shrunk: the boonie hat's wide brim, a shemagh at the neck, the beard, wraparound
sunglasses rather than goggles, a tan chest rig over camouflage, desert boots.
The brim and the shemagh do most of the work — they change the outline, and the
outline is all that is left at a distance.

Camouflage is generated from the seat colour rather than painted on top of it.
Six players have to be told apart while moving, so the disruptive pattern is
three tones of one hue: it reads as camo up close and as a single colour across
the arena, which is what identification needs.

The weapon is not a prop laid over the body. The firing arm, support arm and
rifle are one sprite pivoting at the shoulder, so the muzzle stays attached
through a full circle of aim.

Drawn at 4x and downsampled — Pillow has no antialiased primitives.
"""
import colorsys
import math
import os
import random
from PIL import Image, ImageChops, ImageDraw

S = 4
W, H = 96, 160          # 160px at 100 ppu = 1.6 world units
# Eight frames, not four. Four is enough to describe a walk but not enough to make
# one look continuous, and the gap between frames is visible as a snap at the speed
# these characters move.
WALK_FRAMES = 8

RIG       = (138, 118, 82)      # coyote-tan chest rig
RIG_DARK  = (104, 88, 60)
RIG_LIT   = (166, 146, 108)
STRAP     = (58, 52, 40)
BOOT      = (122, 96, 62)       # desert boots, not black
BOOT_DARK = (92, 72, 46)
SKIN      = (198, 150, 110)
SKIN_DARK = (158, 116, 82)
BEARD     = (54, 42, 34)
SHADES    = (24, 26, 32)
SHADES_LIT= (96, 122, 140)
SHEMAGH   = (150, 146, 112)     # olive-sand scarf
SHEMAGH_D = (112, 108, 80)
HAT       = (120, 116, 84)
HAT_DARK  = (92, 88, 62)
METAL     = (92, 98, 110)
METAL_LIT = (146, 155, 170)
GUNMETAL  = (58, 62, 70)


def mul(rgb, f):
    return tuple(max(0, min(255, int(c * f))) for c in rgb)


def uniform_tones(rgb01):
    """Three tones of the seat hue: base, shadow and a sun-bleached highlight.

    Real multicam mixes unrelated hues. Doing that here would cost the one thing
    the colour is for, so the pattern varies value and saturation instead and
    keeps the hue fixed.
    """
    h, s, v = colorsys.rgb_to_hsv(*rgb01)
    s = min(1.0, s * 1.4 + 0.10)
    base = colorsys.hsv_to_rgb(h, s, v * 0.60)
    dark = colorsys.hsv_to_rgb(h, min(1.0, s * 1.15), v * 0.40)
    lite = colorsys.hsv_to_rgb(h, s * 0.62, v * 0.78)
    return tuple(tuple(int(c * 255) for c in t) for t in (base, dark, lite))


class Pen:
    def __init__(self, img):
        self.img = img
        self.d = ImageDraw.Draw(img)

    def limb(self, a, b, t, fill):
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


def camouflage(size, tones, seed):
    """Blotches, to be clipped to whatever the uniform covers.

    Seeded per seat so a given player's pattern is identical in every frame —
    a pattern that crawled between walk frames would read as television static.
    """
    layer = Image.new('RGBA', size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    rng = random.Random(seed)

    for _ in range(110):
        tone = tones[rng.randint(1, 2)]
        x = rng.uniform(0, size[0])
        y = rng.uniform(0, size[1])
        w = rng.uniform(5, 13) * S
        h = rng.uniform(4, 10) * S

        # Overlapping ellipses per blob: a lone ellipse reads as a polka dot.
        for k in range(3):
            ox = x + rng.uniform(-w * 0.35, w * 0.35)
            oy = y + rng.uniform(-h * 0.35, h * 0.35)
            d.ellipse([ox - w / 2, oy - h / 2, ox + w / 2, oy + h / 2], fill=tone)

    return layer


def head(p, hx, hy, tones):
    """Boonie hat, sunglasses, beard, shemagh.

    The hat brim is the single most recognisable thing here: it breaks the round
    skull that made an earlier attempt look like a mascot, and it survives being
    shrunk when nothing else on the face does.
    """
    p.rr((hx - 9, hy - 7, hx + 10, hy + 15), 8, SKIN)            # skull
    p.rr((hx + 3, hy + 2, hx + 15, hy + 14), 4, SKIN)            # jaw forward

    # Beard along the jaw. Worn by most of the operators in the reference and,
    # usefully, a dark shape that separates the head from the neck.
    p.rr((hx - 2, hy + 9, hx + 16, hy + 18), 3.5, BEARD)
    p.rr((hx + 9, hy + 6, hx + 16, hy + 16), 3, BEARD)
    p.rr((hx + 1, hy + 3, hx + 14, hy + 9), 2.5, mul(SKIN, 1.05))  # cheek left visible

    # Wraparound sunglasses, not goggles: thinner, darker, no strap.
    p.rr((hx + 1, hy - 1, hx + 15, hy + 4), 2, SHADES)
    p.rr((hx + 9, hy, hx + 14, hy + 1.8), 0.8, SHADES_LIT)
    p.rr((hx - 6, hy - 0.5, hx + 2, hy + 2), 1, mul(SHADES, 1.2))

    # Boonie hat: crown then a brim wider than the head on both sides.
    p.pie((hx - 11, hy - 18, hx + 12, hy + 4), 180, 360, HAT)
    p.rr((hx - 16, hy - 5, hx + 19, hy - 1), 2, HAT)
    p.rr((hx - 16, hy - 3, hx + 19, hy - 1), 1.5, HAT_DARK)      # brim underside
    p.rr((hx - 10, hy - 9, hx + 11, hy - 6), 1.5, HAT_DARK)      # crown band

    # Shemagh bunched at the neck.
    p.rr((hx - 7, hy + 15, hx + 12, hy + 24), 4, SHEMAGH)
    p.rr((hx - 5, hy + 19, hx + 9, hy + 23), 2, SHEMAGH_D)
    p.poly([(hx - 7, hy + 20), (hx - 1, hy + 19), (hx - 3, hy + 29)], SHEMAGH_D)


# Just longer than the 48px from hip to planted foot, so standing is nearly straight
# and the bend appears only when the foot lifts. Set longer than that and the
# character stands in a permanent crouch.
THIGH, SHIN = 26.0, 24.0
BOOT_LEN = 13.0


def knee_for(hip, foot, forward=1.0):
    """Where the knee goes, given a hip and a foot.

    Two-bone inverse kinematics, because the previous version put the knee at half
    the foot's offset and called it a leg. That keeps the limb straight no matter
    where the foot is, so the walk looked like a pair of scissors: the knee never
    led the foot, and the leg never shortened as it lifted.

    Solved the standard way — the knee lies on the circle intersection of the two
    bone lengths — and the sign picks the solution that bends forward, since a leg
    that folds the other way is a bird's.
    """
    dx, dy = foot[0] - hip[0], foot[1] - hip[1]
    d = math.hypot(dx, dy)
    d = max(1e-3, min(d, THIGH + SHIN - 0.5))      # never ask for a straighter leg than exists

    a = (THIGH * THIGH - SHIN * SHIN + d * d) / (2 * d)
    h = math.sqrt(max(0.0, THIGH * THIGH - a * a))

    ux, uy = dx / d, dy / d
    px, py = uy, -ux                                # +x while the leg hangs straight down
    return hip[0] + ux * a + px * h * forward, hip[1] + uy * a + py * h * forward


def leg(p, hip, foot, tone, back=False):
    """Thigh, shin and boot, with the boot square to the shin."""
    f = 0.72 if back else 1.0
    cloth = mul(tone, f)
    knee = knee_for(hip, foot)

    p.limb(hip, knee, 19, cloth)
    p.limb(knee, foot, 15.5, cloth)
    p.ell((knee[0] - 8, knee[1] - 8, knee[0] + 8, knee[1] + 8), mul(cloth, 1.08))   # knee pad

    # The boot sits across the end of the shin rather than square to the world, so a
    # lifted leg points its toe instead of dragging a flat slab through the air.
    sx, sy = foot[0] - knee[0], foot[1] - knee[1]
    n = math.hypot(sx, sy) or 1.0
    tx, ty = sy / n, -sx / n                        # shin turned a quarter circle forward

    heel = (foot[0] - tx * 3.0, foot[1] - ty * 3.0)
    toe = (foot[0] + tx * BOOT_LEN, foot[1] + ty * BOOT_LEN)
    shade = mul(BOOT, 0.8 if back else 1.0)

    p.limb(heel, toe, 11, shade)
    p.limb((heel[0], heel[1] + 2.5), (toe[0], toe[1] + 2.5), 5,
           mul(BOOT_DARK, 0.8 if back else 1.0))


def walk_foot(phase, hip_x, foot_y, reach=15.0, lift=11.0):
    """Where one foot is at a point in the cycle.

    Only the foot is placed; the knee follows from it. Swinging both legs on a
    plain sine put the two passing positions in the same place, so half a cycle
    duplicated the other half and the whole thing read as a shuffle — the foot has
    to leave the ground on the way forward.
    """
    a = 2 * math.pi * phase
    return hip_x + math.cos(a) * reach, foot_y - max(0.0, -math.sin(a)) * lift


def body(seat_rgb01, pose, seed):
    tones = uniform_tones(seat_rgb01)
    base = tones[0]

    # Uniform on its own layer so the camouflage can be clipped to exactly what
    # it covers — cloth, not rig or skin.
    cloth = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
    pc = Pen(cloth)

    cx = 44
    hip_y, foot_y = 104, 152
    drop = 0
    lean = 0

    if pose.startswith('walk'):
        t = (int(pose[4:]) - 1) / WALK_FRAMES
        drop, lean = abs(math.cos(2 * math.pi * t)) * 2.5, 1
        front = walk_foot(t, cx + 5, foot_y)
        back = walk_foot(t + 0.5, cx - 5, foot_y)
    elif pose == 'jump':
        # Knees up, trailing leg extended: reads as airborne in silhouette alone.
        drop, lean = -1, 0
        front = (cx + 9, foot_y - 21)
        back = (cx + 18, foot_y - 6)
    elif pose == 'hurt':
        drop, lean = 3, -4
        front = (cx + 14, foot_y)
        back = (cx - 18, foot_y)
    else:
        front = (cx + 10, foot_y)
        back = (cx - 10, foot_y)

    top = hip_y + drop
    ty = 45 + drop

    # Only the feet are placed; the knees are solved from them.
    leg(pc, (cx - 5, top), back, base, back=True)
    leg(pc, (cx + 5, top), front, base)
    pc.limb((cx - 1 + lean, ty + 11), (cx + 9 + lean, ty + 34), 13.5, mul(base, 0.70)) # support arm
    pc.rr((cx - 20, top - 14, cx + 20, top + 6), 7, mul(base, 0.9))                    # hips
    pc.rr((cx - 20 + lean, ty, cx + 20 + lean, top - 4), 10, base)                     # torso

    # Clip the pattern to the cloth: blotches over the rig or the face would be
    # paint, not camouflage.
    camo = camouflage((W * S, H * S), tones, seed)
    mask = ImageChops.multiply(camo.getchannel('A'), cloth.getchannel('A'))
    cloth.paste(camo, (0, 0), mask)

    img = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
    img.alpha_composite(cloth)
    p = Pen(img)

    # Chest rig and belt kit, over the camouflage and deliberately not patterned.
    p.rr((cx - 16 + lean, ty + 7, cx + 17 + lean, top - 20), 5, RIG)
    p.rr((cx - 11 + lean, ty + 17, cx - 2 + lean, ty + 30), 2, RIG_DARK)
    p.rr((cx + 2 + lean, ty + 17, cx + 11 + lean, ty + 30), 2, RIG_DARK)
    p.rr((cx - 16 + lean, ty + 11, cx + 17 + lean, ty + 14), 1, RIG_LIT)
    p.rr((cx - 20 + lean, ty + 2, cx + 20 + lean, ty + 8), 2.5, STRAP)   # shoulder straps
    p.rr((cx - 20, top - 13, cx + 20, top - 6), 2.5, STRAP)              # belt
    p.rr((cx + 6, top + 2, cx + 18, top + 19), 3, RIG_DARK)              # dump pouch
    p.rr((cx - 19, top - 4, cx - 10, top + 7), 2.5, RIG_DARK)            # rear pouch

    p.rr((cx + 1 + lean, ty - 9, cx + 8 + lean, ty + 3), 3.5, SKIN_DARK) # neck
    head(p, cx + 1 + lean, ty - 24, tones)

    return img.resize((W, H), Image.LANCZOS)


# --- weapon arm -------------------------------------------------------------
GW, GH = 136, 53
GRIP = (24, 27)


def weapon_arm(seat_rgb01, seed):
    tones = uniform_tones(seat_rgb01)
    base = tones[0]

    sleeve = Image.new('RGBA', (GW * S, GH * S), (0, 0, 0, 0))
    ps = Pen(sleeve)
    sx, sy = GRIP
    st = sx + 4

    ps.limb((sx, sy), (sx + 19, sy + 15), 13, mul(base, 0.95))
    ps.limb((sx + 19, sy + 15), (st + 33, sy + 11), 11.5, base)
    ps.limb((sx + 5, sy + 8), (sx + 32, sy + 20), 9.5, mul(base, 1.12))
    ps.limb((sx + 32, sy + 20), (st + 66, sy + 7), 9.5, mul(base, 1.12))

    camo = camouflage((GW * S, GH * S), tones, seed)
    sleeve.paste(camo, (0, 0), ImageChops.multiply(camo.getchannel('A'), sleeve.getchannel('A')))

    img = Image.new('RGBA', (GW * S, GH * S), (0, 0, 0, 0))
    p = Pen(img)

    # Rifle under the arms, so the hands read as gripping it.
    p.rr((st, sy - 4, st + 24, sy + 3), 2.5, mul(GUNMETAL, 0.9))         # stock
    p.rr((st + 21, sy - 6, st + 56, sy + 3), 2.5, GUNMETAL)              # receiver
    p.rr((st + 28, sy - 11, st + 43, sy - 5), 1.5, mul(GUNMETAL, 1.3))   # optic
    p.rr((st + 33, sy - 14, st + 39, sy - 9), 1, mul(GUNMETAL, 1.15))
    p.rr((st + 34, sy + 3, st + 46, sy + 19), 2, mul(GUNMETAL, 0.8))     # magazine
    p.rr((st + 55, sy - 3, st + 95, sy + 1), 2, METAL)                   # barrel
    p.rr((st + 59, sy - 5, st + 77, sy + 3), 1.5, mul(GUNMETAL, 1.05))   # handguard
    p.rr((st + 91, sy - 5, st + 100, sy + 3), 1.5, METAL_LIT)            # muzzle

    img.alpha_composite(sleeve)
    p.ell((st + 29, sy + 6, st + 41, sy + 18), STRAP)                    # trigger hand
    p.ell((st + 61, sy + 1, st + 73, sy + 13), STRAP)                    # support hand

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
            body(rgb, pose, seed=i * 17 + 3).save(f'gen/Characters/player{i}_{pose}.png')
        weapon_arm(rgb, seed=i * 17 + 3).save(f'gen/Weapons/arm{i}.png')
    print(f'body {W}x{H}, arm {GW}x{GH} pivot {GRIP}')
    print(f'wrote {len(SEATS) * len(POSES)} frames + {len(SEATS)} arms')
