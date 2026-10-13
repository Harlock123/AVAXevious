#!/usr/bin/env python3
"""Synthesises AVAXevious's sound effects and tunes into ../src/AVAXevious/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAXevious", "Sounds")
RNG = random.Random(1982)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Zap.wav", tone(0.07, 2400, 900, "pulse", release=1.2), 0.25)
    write("Drop.wav", tone(0.45, 1800, 500, "tri", attack=0.01, release=0.3), 0.3)
    write("BombHit.wav", mix(tone(0.4, 900, 60, "noise", release=0.7), tone(0.3, 140, 50, "square", release=1.0)), 0.55)
    write("AirHit.wav", mix(tone(0.22, 2600, 300, "noise", release=0.8), tone(0.12, 700, 200, "square", release=1.0)), 0.45)
    write("Clang.wav", mix(tone(0.12, 1900, kind="square", release=1.4), tone(0.12, 2850, kind="tri", release=1.6)), 0.3)
    write("Sol.wav", cat(*[tone(0.06, hz(n), kind="tri", release=0.3) for n in ["G5", "C6", "E6", "G6", "C7"]], tone(0.25, hz("G7"), kind="pulse", release=1.0)), 0.45)
    write("Flag.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["C6", "E6", "G6", "C7"] * 2]), 0.45)
    write("EnemyFire.wav", tone(0.06, 500, 900, "square", release=1.0), 0.2)
    write("Death.wav", cat(mix(tone(1.0, 1200, 40, "noise", release=0.5), tone(1.0, 400, 50, "saw", release=0.6)), silence(0.1)), 0.6)
    # The mothership arriving: a deep, pulsing warning.
    write("MotherArrives.wav", cat(*[tone(0.28, 110, 140, "saw", release=0.3, vibrato=0.1, vib_rate=12) for _ in range(4)]), 0.5)
    write("MotherDown.wav", cat(mix(tone(1.6, 700, 30, "noise", release=0.4), tone(1.6, 220, 40, "square", release=0.5)),
                                *[tone(0.08, hz(n), kind="pulse", release=0.4) for n in ["C6", "E6", "G6", "C7", "E7"]]), 0.6)
    write("GunDown.wav", mix(tone(0.3, 600, 80, "noise", release=0.7), tone(0.2, 300, 90, "square", release=0.8)), 0.5)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["D6", "F#6", "A6", "D7"] * 2]), 0.45)

    # The flying music: a driving, looping figure over a pulsing bass in D minor (new).
    lead = [("D5", 0.5), ("A5", 0.5), ("F5", 0.5), ("A5", 0.5), ("D6", 0.5), ("C6", 0.5), ("A5", 1),
            ("Bb4", 0.5), ("F5", 0.5), ("D5", 0.5), ("F5", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("F5", 1),
            ("C5", 0.5), ("G5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("Bb5", 0.5), ("G5", 1),
            ("A4", 0.5), ("E5", 0.5), ("C#5", 0.5), ("E5", 0.5), ("A5", 0.5), ("G5", 0.5), ("E5", 1)]
    bass = [(n, 0.5) for n in ["D2", "D3"] * 4 + ["Bb1", "Bb2"] * 4 + ["C2", "C3"] * 4 + ["A1", "A2"] * 4]
    write("Music.wav", tune(144, lead, bass), 0.42)

    write("TitleTune.wav", tune(120,
        [("D5", 1), ("F5", 0.5), ("A5", 0.5), ("D6", 1), ("C6", 1),
         ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 0.5), ("E5", 1), ("A4", 1),
         ("D5", 0.5), ("E5", 0.5), ("F5", 0.5), ("G5", 0.5), ("A5", 1), ("Bb5", 1),
         ("A5", 0.5), ("G5", 0.5), ("E5", 0.5), ("C#5", 0.5), ("D5", 2)],
        [(n, 1) for n in ["D2", "A2", "D2", "A2", "G2", "D2", "A1", "A1", "D2", "D2", "F2", "G2", "A2", "A2", "D2", "D2"]]), 0.6)
    write("StartTune.wav", tune(160,
        [("D5", 0.5), ("F5", 0.5), ("A5", 0.5), ("D6", 0.5), ("C6", 0.5), ("A5", 0.5), ("D6", 1.5)],
        [("D2", 1), ("A2", 1), ("D3", 2)]), 0.6)
    write("GameOver.wav", tune(80,
        [("A4", 1), ("F4", 1), ("D4", 1), ("E4", 1), ("C#4", 1), ("D4", 2), ("-", 1)],
        [("D2", 2), ("Bb1", 2), ("A1", 2), ("D2", 2)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
