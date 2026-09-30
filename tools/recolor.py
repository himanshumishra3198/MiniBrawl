"""Six player skins, recoloured from one Kenney alien to match PlayerColors.

Kenney ships five alien colours; the roster holds six seats, and two players in
the same colour is a gameplay bug wearing an art costume. Recolouring one sprite
also means all six animate identically and share a silhouette.

The hues are not chosen here. PlayerColors already owns seat colour — it paints
the scoreboard, the kill feed and the damage sparks — so the skins are derived
from that same table and stay in step with it by construction.

Only saturated pixels move: the suit, visor and outline are near-grey and must
stay that way, or the alien turns into one solid blob of hue.
"""
import colorsys, os
from PIL import Image

A = 'unpacked/platformer-art-deluxe/Extra animations and enemies/Alien sprites'
POSES = ['stand', 'walk1', 'walk2', 'jump', 'duck', 'hurt']

# Mirrors PlayerColors.k_Colors, in seat order. Kept as RGB so the two tables can
# be eyeballed against each other; hue is derived below.
SEATS = [
    ('cyan',   (0.35, 0.85, 1.00)),
    ('green',  (0.55, 0.95, 0.45)),
    ('amber',  (1.00, 0.75, 0.30)),
    ('violet', (0.80, 0.55, 1.00)),
    ('pink',   (1.00, 0.55, 0.75)),
    ('white',  (0.95, 0.95, 0.95)),
]

def tint(im, rgb):
    """Move every coloured pixel onto the seat's hue, keeping the art's own shading."""
    h_t, s_t, _ = colorsys.rgb_to_hsv(*rgb)
    im = im.convert('RGBA')
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s < 0.22:              # white suit, grey visor, dark outline
                continue
            if s_t < 0.12:
                # The white seat has no hue to move to. Draining the colour instead
                # keeps the shading that tells the body apart from the suit.
                r2, g2, b2 = colorsys.hsv_to_rgb(h, s * 0.10, min(1.0, v * 1.12))
            else:
                # Scale rather than replace saturation, so the darker shaded patches
                # stay darker instead of flattening into one flat fill.
                r2, g2, b2 = colorsys.hsv_to_rgb(h_t, min(1.0, s * (s_t / 0.55)), v)
            px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    return im

if __name__ == '__main__':
    out = 'gen/Characters'
    os.makedirs(out, exist_ok=True)
    for index, (name, rgb) in enumerate(SEATS):
        for pose in POSES:
            src = Image.open(f'{A}/alienGreen_{pose}.png')
            tint(src, rgb).save(f'{out}/player{index}_{pose}.png')
    print('wrote', len(SEATS) * len(POSES), 'frames ->', out)
