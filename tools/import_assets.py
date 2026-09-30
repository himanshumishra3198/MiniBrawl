"""Copies the art and audio the game uses into the project.

Characters and weapons are drawn by tools/commando.py and come from gen/. The
rest is a deliberately small subset of the Kenney packs: they total well over
100 MB, and everything copied here is something the game actually draws or plays.
"""
import os, shutil, sys

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
    for pose in ['stand', 'jump', 'hurt', 'walk1', 'walk2', 'walk3', 'walk4']:
        jobs.append((f'{GEN}/Characters/player{i}_{pose}.png', f'Art/Characters/player{i}_{pose}.png'))
    jobs.append((f'{GEN}/Weapons/arm{i}.png', f'Art/Weapons/arm{i}.png'))

# Level surface. Tiled across each block rather than stretched, and tinted dark at runtime:
# Kenney's tiles are light, and the level has to stay darker than the players standing on it.
TILES = f'{SRC}/platformer-art-deluxe/Base pack/Tiles'
jobs.append((f'{TILES}/castleCenter.png', 'Art/Tiles/platform.png'))

# Particles. Greyscale on purpose — every one of these gets tinted at runtime.
jobs += [
    (f'{P}/muzzle_01.png', 'Art/Particles/muzzle.png'),
    (f'{P}/flame_01.png',  'Art/Particles/flame.png'),
    (f'{P}/star_01.png',   'Art/Particles/spark.png'),
    (f'{P}/circle_05.png', 'Art/Particles/glow.png'),
    (f'{SM}/whitePuff12.png', 'Art/Particles/puff.png'),
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
    (f'{SCI}/laserSmall_000.ogg', 'Audio/SFX/shoot_0.ogg'),
    (f'{SCI}/laserSmall_001.ogg', 'Audio/SFX/shoot_1.ogg'),
    (f'{SCI}/laserSmall_002.ogg', 'Audio/SFX/shoot_2.ogg'),
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

print(f'copied {len(jobs) - len(missing)}/{len(jobs)} files, {total/1024/1024:.2f} MB')
for m in missing:
    print('MISSING', m)
sys.exit(1 if missing else 0)
