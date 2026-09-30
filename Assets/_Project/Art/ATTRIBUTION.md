# Third-party assets

Every art and audio asset in the game is **CC0 (public domain)**. CC0 imposes no
obligation to credit, so nothing here is a licence condition — the file exists so
the origin of each asset stays known, and so a future contributor can tell at a
glance what may be redistributed.

## Kenney (kenney.nl) — CC0 1.0

| Pack | Used for |
|---|---|
| Platformer Art Deluxe | Character sprites (`Art/Characters`) |
| Particle Pack | Muzzle flash, jetpack flame, sparks, glow |
| Smoke Particles | Jetpack exhaust puffs |
| Mobile Controls | Thumbsticks, buttons, HUD icons |
| Sci-Fi Sounds | Shooting, thruster, explosions |
| Impact Sounds | Bullet impacts on bodies and geometry |
| Digital Audio | Respawn, kill confirmation |
| Interface Sounds | Menu clicks, confirm, error |
| Music Jingles | Match start and match end stings |

## Derived work

`Art/Characters` are recoloured from Kenney's `alienGreen` frames by
`tools/recolor.py`. Kenney ships five alien colours and the roster seats six, so
the skins are generated from one sprite against the seat hues in
`PlayerColors` — that keeps a player's character, scoreboard entry and hit
sparks the same colour by construction rather than by memory.

`Art/Weapons/weapon_blaster.png` is drawn by `tools/gun.py`. Kenney's raygun is
yellow and purple, which fights all six seat colours at once, and a weapon that
rotates a full 360 degrees needs its pivot on the grip to the pixel.

Derived assets inherit CC0 from their source.
