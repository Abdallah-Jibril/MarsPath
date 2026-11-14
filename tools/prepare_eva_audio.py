"""Synthesize the MarsPath in-suit EVA soundscape as 24 kHz mono WAV files.

These are designed simulation effects, not rover recordings. Soft, muffled
exterior texture follows NASA's description of Martian sound attenuation:
https://www.nasa.gov/solar-system/what-sounds-captured-by-nasas-perseverance-rover-reveal-about-mars/
Boot impacts represent contact transmitted through the suit; ventilation and
breathing represent the pressurized helmet, without a continuous space drone.
"""

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
    duration = 24
    rng = random.Random(3429)
    noise = [rng.uniform(-1, 1) for _ in range(RATE * duration)]
    ventilation = band(noise, 90, 360, circular=True)
    breath = band(noise, 190, 1050, circular=True)
    distant_air = band(noise, 40, 145, circular=True)
    samples = []
    for index, (air, soft_breath, exterior) in enumerate(zip(ventilation, breath, distant_air)):
        phase = math.tau * index / len(noise)
        # Five smooth, slightly varied breath cycles in a seamless 24-second loop.
        cycle = (5 * phase + .12 * math.sin(phase)) % math.tau
        inhale = max(0, math.sin(cycle)) ** 2
        exhale = max(0, -math.sin(cycle)) ** 1.5
        gust = (.5 + .5 * math.sin(phase - .4)) ** 3
        samples.append(.23 * air + soft_breath * (.28 * inhale + .19 * exhale)
                       + .11 * exterior * gust)
    return samples


def footstep(variant):
    rng = random.Random(801 + variant)
    duration = .36 + .011 * (variant % 4)
    noise = [rng.uniform(-1, 1) for _ in range(round(RATE * duration))]
    thud = band(noise, 45, 235)
    grit = band(noise, 280, 2400)
    scrape = band(noise, 140, 1100)
    pebbles = []
    for _ in range(9 + variant % 4):
        pebbles.append((rng.uniform(.025, .235), rng.uniform(.003, .012), rng.uniform(.09, .25)))

    samples = []
    for index, (impact, gravel, texture) in enumerate(zip(thud, grit, scrape)):
        time = index / RATE
        attack = min(1, time / .007)
        tail = min(1, (duration - time) / .028)
        crunch = sum(
            volume * math.exp(-((time - centre) / width) ** 2)
            for centre, width, volume in pebbles
        )
        heel = math.exp(-time / .028)
        toe = math.exp(-((time - .055) / .026) ** 2)
        settle = math.exp(-((time - .16) / .080) ** 2)
        contact = math.sin(math.tau * (76 + variant * 1.9) * time) * math.exp(-time / .035)
        samples.append(attack * tail * (
            .65 * impact * (heel + .4 * toe) + .033 * contact
            + gravel * (.055 * math.exp(-time / .065) + crunch * .39)
            + .16 * texture * settle
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
    write("Ambient", airflow(), .14)
    for number in range(1, 9):
        write(f"Footstep{number:02}", footstep(number), .48)
    write("Alert", telemetry([(0, 438, .14, .7), (.16, 365, .16, .44)], .35), .32)
    write("Arrival", telemetry([(0, 392, .23, .64), (.17, 523, .29, .82)], .50), .31)


if __name__ == "__main__":
    main()
