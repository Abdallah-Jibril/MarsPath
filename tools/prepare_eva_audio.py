"""Generate the original MarsPath EVA sound effects as 24 kHz mono WAV files."""

import math
import random
import struct
import wave
from pathlib import Path


RATE = 24_000
DESTINATION = Path(__file__).resolve().parents[1] / "MarsPathUnity/Assets/Resources/Audio/Eva"


def lowpass(samples, frequency, circular=False):
    decay = math.exp(-math.tau * frequency / RATE)
    feedback = 0.0
    if circular:
        for sample in samples:
            feedback = decay * feedback + (1 - decay) * sample
        feedback /= 1 - decay ** len(samples)

    result = []
    for sample in samples:
        feedback = decay * feedback + (1 - decay) * sample
        result.append(feedback)
    return result


def band(samples, low, high, circular=False):
    upper = lowpass(samples, high, circular)
    lower = lowpass(upper, low, circular)
    return [above - below for above, below in zip(upper, lower)]


def airflow():
    duration = 12
    rng = random.Random(3429)
    noise = [rng.uniform(-1, 1) for _ in range(RATE * duration)]
    ventilation = band(noise, 105, 510, circular=True)
    breath = band(noise, 270, 840, circular=True)
    samples = []
    for index, (air, soft_breath) in enumerate(zip(ventilation, breath)):
        phase = math.tau * index / len(noise)
        breathing = .50 + .50 * math.sin(3 * phase + .5)
        samples.append(.65 * air + .21 * soft_breath * (.35 + breathing * breathing))
    return samples


def footstep(variant):
    rng = random.Random(801 + variant)
    duration = .30 + .012 * variant
    noise = [rng.uniform(-1, 1) for _ in range(round(RATE * duration))]
    thud = band(noise, 65, 275)
    grit = band(noise, 360, 1860)
    scrape = band(noise, 165, 860)
    pebbles = []
    for _ in range(7):
        pebbles.append((rng.uniform(.024, .142), rng.uniform(.004, .014), rng.uniform(.12, .32)))

    samples = []
    for index, (impact, gravel, texture) in enumerate(zip(thud, grit, scrape)):
        time = index / RATE
        attack = min(1, time / .007)
        tail = min(1, (duration - time) / .028)
        crunch = sum(
            volume * math.exp(-((time - centre) / width) ** 2)
            for centre, width, volume in pebbles
        )
        settle = math.exp(-((time - .095) / .064) ** 2)
        samples.append(attack * tail * (
            .52 * impact * math.exp(-time / .039)
            + gravel * (.08 * math.exp(-time / .072) + crunch * .54)
            + .24 * texture * settle
        ))
    return samples


def telemetry(notes, duration):
    samples = [0.0] * round(RATE * duration)
    for start, frequency, length, level in notes:
        offset = round(RATE * start)
        for index in range(round(RATE * length)):
            elapsed = index / RATE
            attack = min(1, elapsed / .024)
            release = min(1, (length - elapsed) / .080)
            phase = math.tau * frequency * elapsed
            samples[index + offset] += level * attack * release * (
                math.sin(phase) + .07 * math.sin(2 * phase)
            )
    return samples


def write(name, samples, peak):
    gain = peak * 32767 / max(abs(sample) for sample in samples)
    frames = struct.pack(f"<{len(samples)}h", *(round(sample * gain) for sample in samples))
    with wave.open(str(DESTINATION / f"{name}.wav"), "wb") as clip:
        clip.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        clip.writeframes(frames)


def main():
    DESTINATION.mkdir(parents=True, exist_ok=True)
    write("Ambient", airflow(), .18)
    for number in range(1, 5):
        write(f"Footstep{number:02}", footstep(number), .49)
    write("Alert", telemetry([(0, 438, .14, .7), (.16, 365, .16, .44)], .35), .32)
    write("Arrival", telemetry([(0, 392, .23, .64), (.17, 523, .29, .82)], .50), .31)


if __name__ == "__main__":
    main()
