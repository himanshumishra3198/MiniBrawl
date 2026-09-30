"""Rifle shots, synthesised.

The game was firing Kenney's sci-fi laser, which is the wrong instrument for a
man holding an assault rifle. None of the CC0 packs used here carry real firearm
recordings, and a gunshot is one of the easier sounds to build from parts: a
crack, a body, and a tail.

  crack  — broadband noise on a very fast decay. This is the supersonic report
           and it is what makes the sound read as a gun rather than an impact.
  body   — two low sines an octave or so apart, decaying slower. The thump you
           feel rather than hear.
  click  — a few milliseconds of noise at the very front, for the transient.
           Without it the shot sounds like it fades up, however fast the attack.
  tail   — quiet noise on a long decay, standing in for the report coming back
           off the walls. A shot with no tail sounds like it happened indoors in
           a very small cupboard.

Three variants, seeded, because a single clip fired five times a second turns
into a machine-gun rattle of identical sounds.
"""
import math
import os
import struct
import wave

import numpy as np

SR = 44100


def one_pole_lowpass(x, cutoff, sr=SR):
    """Takes the hiss off the top of the noise so it reads as a report, not static."""
    a = math.exp(-2.0 * math.pi * cutoff / sr)
    out = np.empty_like(x)
    prev = 0.0
    for i, v in enumerate(x):
        prev = (1.0 - a) * v + a * prev
        out[i] = prev
    return out


def gunshot(seed):
    rng = np.random.default_rng(seed)
    n = int(SR * 0.30)
    t = np.arange(n) / SR

    noise = rng.normal(0.0, 1.0, n)

    crack = one_pole_lowpass(noise, rng.uniform(3400, 4400)) * np.exp(-t * rng.uniform(58, 72))

    f0 = rng.uniform(88, 104)
    body = (np.sin(2 * np.pi * f0 * t) * np.exp(-t * 32) * 0.9 +
            np.sin(2 * np.pi * f0 * 0.6 * t) * np.exp(-t * 22) * 0.6)

    click = np.zeros(n)
    k = int(SR * 0.0015)
    click[:k] = rng.normal(0.0, 1.0, k) * np.linspace(1.0, 0.0, k)

    tail = one_pole_lowpass(rng.normal(0.0, 1.0, n), 900) * np.exp(-t * 11.0) * 0.13

    x = crack * 1.0 + body * 0.75 + click * 0.8 + tail

    # Soft clip: a real report drives everything it is recorded with into
    # saturation, and tanh is the cheapest way to borrow that character.
    x = np.tanh(x * 2.2)

    peak = float(np.max(np.abs(x)))
    if peak > 0:
        x = x / peak * 0.92

    # 2ms fade out, so recycling a pooled voice cannot click.
    fade = int(SR * 0.002)
    x[-fade:] *= np.linspace(1.0, 0.0, fade)
    return x


def write_wav(path, samples):
    data = (np.clip(samples, -1.0, 1.0) * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


if __name__ == '__main__':
    out = 'gen/Audio'
    os.makedirs(out, exist_ok=True)
    for i in range(3):
        write_wav(f'{out}/shoot_{i}.wav', gunshot(seed=1000 + i * 7))
    print(f'wrote 3 gunshots to {out}')
