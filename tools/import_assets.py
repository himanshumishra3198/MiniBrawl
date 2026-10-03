"""Copies the art and audio the game uses into the project.

Characters and weapons are drawn by tools/commando.py and come from gen/. The
rest is a deliberately small subset of the Kenney packs: they total well over
100 MB, and everything copied here is something the game actually draws or plays.
"""
import glob, os, re, shutil, sys

SRC  = 'unpacked'
GEN  = 'gen'
PROJ = '/Users/mishra/Documents/MiniBrawl/Assets/_Project'

P  = f'{SRC}/particle-pack/PNG (Transparent)'
SM = f'{SRC}/smoke-particles/PNG/White puff'
UI = f'{SRC}/mobile-controls/Sprites/Style C/Default'
IC = f'{SRC}/mobile-controls/Sprites/Icons/Default'
SCI = f'{SRC}/sci-fi-sounds/Audio'
IMP = f'{SRC}/impact-sounds/Audio'
DIG = f'{SRC}/digital-audio/Audio'
INT = f'{SRC}/interface-sounds/Audio'
JIN = f'{SRC}/music-jingles/Audio/8-Bit jingles'

jobs = []

# Six seats, five poses each, plus one arm-and-rifle per seat. The weapon is
# per-seat rather than shared because the firing arm wears the uniform colour.
for i in range(6):
    for pose in ['stand', 'jump', 'hurt'] + [f'walk{k}' for k in range(1, 9)]:
        jobs.append((f'{GEN}/Characters/player{i}_{pose}.png', f'Art/Characters/player{i}_{pose}.png'))
    for kind in ('rifle', 'shotgun', 'pistol'):
        jobs.append((f'{GEN}/Weapons/arm{i}_{kind}.png', f'Art/Weapons/arm{i}_{kind}.png'))

# Level surface. Tiled across each block rather than stretched, and tinted dark at runtime:
# Kenney's tiles are light, and the level has to stay darker than the players standing on it.
# The tileset is drawn by tools/tiles.py. Kenney's castle tile was one square
# repeated flat; ground needs a surface, ends, and more than one tile of variation.
for name in ('ground_top', 'ground_fill', 'edge_left', 'edge_right',
             'rock_a', 'rock_b', 'hills_far', 'hills_near', 'sea'):
    jobs.append((f'{GEN}/Tiles/{name}.png', f'Art/Tiles/{name}.png'))

# Island dressing. Palms are drawn by tools/island.py because every one of the
# thirty-five trees in Kenney's background pack is a conifer, a cactus or a round
# broadleaf, and none of those say island.
# Crate icons, drawn by tools/items.py. Colour alone is already doing the job of
# saying which player is which, so a crate needs a shape to say what is in it.
for name in ('health', 'shotgun', 'pistol'):
    jobs.append((f'{GEN}/Items/item_{name}.png', f'Art/Items/item_{name}.png'))

BG = f'{SRC}/background-elements/PNG'
for k in range(3):
    jobs.append((f'{GEN}/Island/palm{k}.png', f'Art/Island/palm{k}.png'))
for k, src in enumerate(['grass2', 'grass4', 'grass6']):
    jobs.append((f'{BG}/{src}.png', f'Art/Island/grass{k}.png'))
for k, src in enumerate(['cloud1', 'cloud4', 'cloud7']):
    jobs.append((f'{BG}/{src}.png', f'Art/Island/cloud{k}.png'))

# Particles. Greyscale on purpose — every one of these gets tinted at runtime.
jobs += [
    (f'{P}/muzzle_01.png', 'Art/Particles/muzzle.png'),
    (f'{P}/flame_01.png',  'Art/Particles/flame.png'),
    (f'{P}/star_01.png',   'Art/Particles/spark.png'),
    (f'{P}/circle_05.png', 'Art/Particles/glow.png'),
    (f'{SM}/whitePuff12.png', 'Art/Particles/puff.png'),
    # Drawn by tools/tracer.py. A flat square stretched along the shot read as a
    # stick lying in the air; a streak needs a bright head and a fading tail.
    (f'{GEN}/Particles/tracer.png', 'Art/Particles/tracer.png'),
]

jobs += [
    (f'{UI}/joystick_circle_pad_c.png', 'Art/UI/stick_pad.png'),
    (f'{UI}/joystick_circle_nub_a.png', 'Art/UI/stick_nub.png'),
    (f'{UI}/button_circle.png',         'Art/UI/button_round.png'),
    (f'{UI}/button_square_wide.png',    'Art/UI/button_wide.png'),
    (f'{IC}/icon_fire.png',             'Art/UI/icon_fire.png'),
    (f'{IC}/icon_cross.png',            'Art/UI/icon_leave.png'),
    (f'{IC}/icon_checkmark.png',        'Art/UI/icon_ready.png'),
]

# Audio. Several variants where the sound repeats often enough that one clip
# would turn into a machine-gun rattle; Sfx picks between them at random.
jobs += [
    # Gunshots are synthesised by tools/gunshot.py. The sci-fi laser this replaced was the
    # wrong instrument for a man holding an assault rifle.
    (f'{GEN}/Audio/shoot_0.wav', 'Audio/SFX/shoot_0.wav'),
    (f'{GEN}/Audio/shoot_1.wav', 'Audio/SFX/shoot_1.wav'),
    (f'{GEN}/Audio/shoot_2.wav', 'Audio/SFX/shoot_2.wav'),
    (f'{IMP}/impactPunch_medium_000.ogg', 'Audio/SFX/hit_body_0.ogg'),
    (f'{IMP}/impactPunch_medium_001.ogg', 'Audio/SFX/hit_body_1.ogg'),
    (f'{IMP}/impactPlate_light_000.ogg',  'Audio/SFX/hit_wall_0.ogg'),
    (f'{IMP}/impactPlate_light_001.ogg',  'Audio/SFX/hit_wall_1.ogg'),
    (f'{SCI}/explosionCrunch_000.ogg',    'Audio/SFX/death_0.ogg'),
    (f'{SCI}/explosionCrunch_002.ogg',    'Audio/SFX/death_1.ogg'),
    (f'{SCI}/thrusterFire_000.ogg',       'Audio/SFX/jetpack_loop.ogg'),
    (f'{DIG}/powerUp1.ogg',               'Audio/SFX/respawn.ogg'),
    (f'{DIG}/zapThreeToneUp.ogg',         'Audio/SFX/kill.ogg'),
    (f'{INT}/click_001.ogg',              'Audio/SFX/ui_click.ogg'),
    (f'{INT}/confirmation_001.ogg',       'Audio/SFX/ui_confirm.ogg'),
    (f'{INT}/error_002.ogg',              'Audio/SFX/ui_error.ogg'),
    (f'{JIN}/jingles_NES07.ogg',          'Audio/SFX/match_start.ogg'),
    (f'{JIN}/jingles_NES13.ogg',          'Audio/SFX/match_end.ogg'),
]

missing, total = [], 0
for src, rel in jobs:
    if not os.path.exists(src):
        missing.append(src)
        continue
    dst = os.path.join(PROJ, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src, dst)
    total += os.path.getsize(dst)

# iCloud syncs this Documents folder and periodically leaves conflict copies next to
# the originals -- "flame 2.png", "flame 3.png" and so on, each with its own .meta,
# each imported by Unity. They are referenced by nothing, but they accumulate every
# time these assets are rewritten, so they are swept here rather than by hand.
strays = [f for f in glob.glob(f'{PROJ}/**/*', recursive=True)
          if re.match(r'.* \d+\.(png|ogg|wav|meta)$', os.path.basename(f))]
for f in strays:
    os.remove(f)

print(f'copied {len(jobs) - len(missing)}/{len(jobs)} files, {total/1024/1024:.2f} MB'
      + (f', swept {len(strays)} iCloud conflict copies' if strays else ''))
for m in missing:
    print('MISSING', m)
sys.exit(1 if missing else 0)
