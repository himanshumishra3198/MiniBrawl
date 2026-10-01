"""The bullet streak sprite.

Tracers were drawn with the same flat white square as everything else, stretched
along the shot. A rectangle of uniform opacity reads as a stick lying in the air:
it has two hard ends, no direction, and nothing to say which way it is going.

A real streak is brightest at the head and fades out behind it. That is what this
is — opacity ramping along the length, and a soft falloff across the width so the
edges are not a hard line. Drawn square so a single pixels-per-unit makes the
sprite exactly one world unit each way, which lets the transform's scale read
directly as length and thickness.

White on purpose: every tracer is tinted at runtime.
"""
import os

from PIL import Image

SIZE = 64


def tracer():
    img = Image.new('RGBA', (SIZE, SIZE), (255, 255, 255, 0))
    px = img.load()

    for x in range(SIZE):
        # Head at +x, because the streak is rotated to face the way it travels.
        # Raised to a power so the tail thins out quickly rather than fading flat.
        along = (x / (SIZE - 1.0)) ** 1.7

        for y in range(SIZE):
            # Cosine across the width: zero at both edges, one through the middle.
            t = (y / (SIZE - 1.0)) * 2.0 - 1.0
            across = max(0.0, 1.0 - t * t) ** 1.4

            a = along * across
            # A hot core down the centre line, so the middle reads as brighter
            # than the edges rather than the whole streak being one flat tone.
            core = along * max(0.0, 1.0 - abs(t) * 3.2)
            px[x, y] = (255, 255, 255, int(min(1.0, a * 0.80 + core * 0.55) * 255))

    return img


if __name__ == '__main__':
    out = 'gen/Particles'
    os.makedirs(out, exist_ok=True)
    tracer().save(f'{out}/tracer.png')
    print(f'wrote {out}/tracer.png ({SIZE}x{SIZE})')
