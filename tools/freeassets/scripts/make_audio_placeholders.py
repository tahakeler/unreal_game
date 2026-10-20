"""Generate in-house CC0 placeholder audio for HARVEST PANIC (stdlib only, deterministic).
Usage: python3 -I make_audio_placeholders.py <HarvestPanic/Assets/Audio/Resources/Audio>

Writes one folder per runtime event (folder name = event name, files = variants):
  stingers (slide whistle + splash, trombone wah-wah, cymbal crash, sad tuba, record scratch, sweetener),
  water splashes, wallet "wah", and stemmed music loops (Karplus-Strong banjo/guitar, sine organ, tuba).
Music loops are an exact number of bars long so they loop seamlessly and bar maths in AudioDirector holds.
22.05 kHz mono 16-bit WAV keeps the repo small; Unity re-encodes on import.
"""
import math, os, random, struct, sys, wave

SR = 22050
rng = random.Random(1337)


def write(path, samples, peak=0.89):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    m = max(1e-9, max(abs(s) for s in samples))
    g = peak / m
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * g)) * 32767)) for s in samples))


def silence(sec):
    return [0.0] * int(sec * SR)


def mix(dst, src, at=0, gain=1.0):
    need = at + len(src)
    if len(dst) < need:
        dst.extend([0.0] * (need - len(dst)))
    for i, s in enumerate(src):
        dst[at + i] += s * gain
    return dst


def env_adsr(n, a=0.005, d=0.05, s=0.6, r=0.1):
    out, A, D, R = [], int(a * SR), int(d * SR), int(r * SR)
    for i in range(n):
        if i < A: v = i / max(1, A)
        elif i < A + D: v = 1 - (1 - s) * (i - A) / max(1, D)
        elif i < n - R: v = s
        else: v = s * max(0.0, (n - i) / max(1, R))
        out.append(v)
    return out


def osc(freq_fn, sec, shape="sine"):
    out, ph = [], 0.0
    for i in range(int(sec * SR)):
        f = freq_fn(i / SR)
        ph += f / SR
        p = ph % 1.0
        if shape == "sine": v = math.sin(2 * math.pi * p)
        elif shape == "saw": v = 2 * p - 1
        elif shape == "square": v = 1.0 if p < 0.5 else -1.0
        else: v = 1 - 4 * abs(p - 0.5)  # triangle
        out.append(v)
    return out


def lowpass(x, cutoff):
    a = math.exp(-2 * math.pi * cutoff / SR); y, out = 0.0, []
    for s in x:
        y = (1 - a) * s + a * y; out.append(y)
    return out


def highpass(x, cutoff):
    lp = lowpass(x, cutoff)
    return [a - b for a, b in zip(x, lp)]


def noise(sec):
    return [rng.uniform(-1, 1) for _ in range(int(sec * SR))]


def apply(x, e):
    return [a * b for a, b in zip(x, e)]


def exp_decay(n, tau):
    return [math.exp(-i / (tau * SR)) for i in range(n)]


def pluck(freq, sec, damp=0.996, bright=0.5):
    """Karplus-Strong plucked string (banjo/guitar-ish)."""
    n = max(2, int(SR / freq)); buf = [rng.uniform(-1, 1) for _ in range(n)]
    buf = lowpass(buf, 2000 + 6000 * bright)
    out = []
    for i in range(int(sec * SR)):
        j = i % n; k = (i + 1) % n
        v = buf[j]; out.append(v)
        buf[j] = damp * 0.5 * (buf[j] + buf[k])
    return out


def note(name):
    names = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
             "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}
    p, o = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[p] + 12 * (o + 1) - 69) / 12)


# ---------------------------------------------------------------- stingers / sfx

def splash(big):
    sec = 0.9 if big else 0.45
    n = noise(sec)
    body = apply(lowpass(n, 1800 if big else 2600), exp_decay(len(n), 0.22 if big else 0.1))
    bubbles = []
    for k in range(10 if big else 4):
        f0 = rng.uniform(500, 1400); start = rng.uniform(0.05, sec * 0.7)
        b = osc(lambda t, f0=f0: f0 * (1 + 3 * t), 0.06)
        mix(bubbles, apply(b, exp_decay(len(b), 0.02)), int(start * SR), 0.25)
    out = mix(list(body), bubbles)
    return out


def slide_whistle():
    sec = 0.9
    f = lambda t: 600 + 900 * math.sin(math.pi * min(1, t / sec)) * (1 if t < sec * 0.5 else 1) - 500 * max(0, t - 0.45) / 0.45
    x = osc(lambda t: f(t) * (1 + 0.01 * math.sin(2 * math.pi * 6 * t)), sec)
    return apply(x, env_adsr(len(x), 0.02, 0.1, 0.8, 0.15))


def trombone(notes, dur, vib=True):
    out = []
    for k, n in enumerate(notes):
        last = k == len(notes) - 1
        d = dur * (2.2 if last else 1.0)
        f0 = note(n)
        x = osc(lambda t, f0=f0, last=last: f0 * (1 + (0.025 * math.sin(2 * math.pi * 5.5 * t) * min(1, t * 3) if (vib and last) else 0)), d, "saw")
        x = lowpass(lowpass(x, 900), 1400)
        out.extend(apply(x, env_adsr(len(x), 0.03, 0.08, 0.75, 0.12)))
    return out


def cymbal():
    n = noise(1.6)
    x = highpass(highpass(n, 4000), 3000)
    return apply(x, exp_decay(len(x), 0.45))


def record_scratch():
    sec = 0.45; n = noise(sec); out = []
    for i, s in enumerate(n):
        t = i / SR
        out.append(s * (0.5 + 0.5 * math.sin(2 * math.pi * (14 + 30 * t) * t)))
    tone = osc(lambda t: 300 + 900 * abs(math.sin(2 * math.pi * 2.2 * t)), sec, "saw")
    out = mix(lowpass(out, 3000), lowpass(tone, 1500), 0, 0.5)
    return apply(out, env_adsr(len(out), 0.002, 0.05, 0.9, 0.08))


def sweetener():
    out = []
    for k, f in enumerate([note("E5"), note("C5")]):
        x = osc(lambda t, f=f: f, 0.18, "triangle")
        mix(out, apply(x, exp_decay(len(x), 0.08)), int(k * 0.12 * SR))
    return out


def wah():
    x = osc(lambda t: note("Bb2") * (1 - 0.12 * t), 0.7, "saw")
    out = []
    a = 0.0
    for i, s in enumerate(x):
        t = i / SR
        cutoff = 300 + 1200 * math.sin(math.pi * min(1, t / 0.7))
        c = math.exp(-2 * math.pi * cutoff / SR)
        a = (1 - c) * s + c * a; out.append(a)
    return apply(out, env_adsr(len(out), 0.02, 0.1, 0.8, 0.2))


def throat_clear():
    n = noise(0.5)
    x = apply(lowpass(n, 700), [0.5 + 0.5 * math.sin(2 * math.pi * 9 * i / SR) for i in range(len(n))])
    return apply(x, env_adsr(len(x), 0.03, 0.1, 0.7, 0.15))


# ---------------------------------------------------------------- music

def chord_notes(root, minor=False, octave=3):
    base = note(f"{root}{octave}")
    third = 2 ** ((3 if minor else 4) / 12); fifth = 2 ** (7 / 12)
    return [base, base * third, base * fifth, base * 2]


def music_loop(bpm, beats_per_bar, bars, progression, minor=False, style="banjo", stems=("base", "lead")):
    """Returns {stem: samples}; every stem has exactly bars*beats_per_bar beats."""
    beat = 60.0 / bpm
    total = int(round(bars * beats_per_bar * beat * SR))
    res = {s: [0.0] * total for s in stems}
    for bar in range(bars):
        root = progression[bar % len(progression)]
        ch = chord_notes(root, minor)
        t0 = bar * beats_per_bar * beat
        for b in range(beats_per_bar):
            tb = t0 + b * beat; at = int(tb * SR)
            bass = ch[0] / 2 if b % 2 == 0 else ch[2] / 2
            if style == "organ":
                if b == 0:
                    for f in ch[:3]:
                        x = osc(lambda t, f=f: f * (1 + 0.004 * math.sin(2 * math.pi * 5 * t)), beats_per_bar * beat * 0.98)
                        x = apply(x, env_adsr(len(x), 0.3, 0.2, 0.7, 0.5))
                        mix(res["base"], x, at, 0.22)
            elif style == "oompah":
                tub = osc(lambda t, f=bass: f, beat * 0.45, "saw")
                mix(res["base"], apply(lowpass(tub, 500), env_adsr(len(tub), 0.01, 0.05, 0.7, 0.08)), at, 0.6 if b % beats_per_bar == 0 else 0.0)
                if b % 2 == 1:
                    for f in ch[1:3]:
                        x = osc(lambda t, f=f: f * 2, beat * 0.3, "saw")
                        mix(res["base"], apply(lowpass(x, 1600), env_adsr(len(x), 0.01, 0.05, 0.6, 0.06)), at, 0.18)
            else:
                b1 = pluck(bass, beat * 0.9, 0.994, 0.2)
                mix(res["base"], b1, at, 0.55)
                if b % 2 == 1 or style == "waltz" and b > 0:
                    for k, f in enumerate(ch[:3]):
                        mix(res["base"], pluck(f, beat * 0.8, 0.993, 0.6), at + int(k * 0.012 * SR), 0.22)
            if "lead" in res:
                # banjo roll: 4 eighth/sixteenth picks per beat over the chord
                picks = 4 if style == "banjo" else 2
                for p in range(picks):
                    f = ch[[0, 2, 3, 1][(b * picks + p) % 4]] * 2
                    tp = at + int(p * beat / picks * SR)
                    mix(res["lead"], pluck(f, beat * 0.6, 0.992, 0.9), tp, 0.25)
    # trim/pad exact length (plucks can overrun the final bar)
    for s in res:
        x = res[s][:total]
        if len(x) < total: x += [0.0] * (total - len(x))
        # wrap tail energy from overrun would be lost; short fade avoids clicks at the loop point
        fade = int(0.01 * SR)
        for i in range(fade):
            x[i] *= i / fade; x[-1 - i] *= i / fade
        res[s] = x
    return res



# ---------------------------------------------------------------- critters / voice / ambience

def pig_squeal(seed):
    r = random.Random(seed); sec = r.uniform(0.55, 0.8)
    f0 = r.uniform(700, 900)
    f = lambda t: f0 * (1 + 0.6 * math.sin(math.pi * min(1, t / sec))) * (1 + 0.04 * math.sin(2 * math.pi * 31 * t))
    x = osc(f, sec, "saw")
    x = mix(lowpass(x, 3500), osc(lambda t: f(t) * 2.01, sec), 0, 0.3)
    return apply(x, env_adsr(len(x), 0.02, 0.1, 0.8, 0.2))


def chicken_panic(seed):
    r = random.Random(seed); out = []
    t = 0.0
    for k in range(r.randint(4, 6)):
        dur = r.uniform(0.07, 0.14); f0 = r.uniform(900, 1400)
        b = osc(lambda u, f0=f0, dur=dur: f0 * (1.3 - 0.5 * u / dur), dur, "square")
        b = apply(lowpass(b, 4000), env_adsr(len(b), 0.004, 0.03, 0.5, 0.03))
        out = mix(out, b, int(t * SR), 0.8); t += dur + r.uniform(0.02, 0.07)
    return out


def bull_telegraph():
    # 0.8 s mandatory wind-up (audio-feedback rule 4): snort + hoof scrape + rising growl.
    sec = 0.8
    growl = osc(lambda t: 70 + 60 * t / sec, sec, "saw")
    growl = apply(lowpass(growl, 600), env_adsr(len(growl), 0.1, 0.1, 0.9, 0.08))
    snort = apply(highpass(noise(0.25), 800), exp_decay(int(0.25 * SR), 0.06))
    scrape = apply(lowpass(noise(0.3), 1500), env_adsr(int(0.3 * SR), 0.05, 0.1, 0.6, 0.1))
    out = mix(list(growl), snort, 0, 0.7)
    return mix(out, scrape, int(0.4 * SR), 0.6)


def player_shout(seed):
    r = random.Random(seed); sec = r.uniform(0.4, 0.6); f0 = r.uniform(180, 240)
    f = lambda t: f0 * (1 + 0.25 * math.sin(math.pi * t / sec))
    x = mix(osc(f, sec, "saw"), osc(lambda t: f(t) * 2, sec, "saw"), 0, 0.4)
    x = lowpass(x, 1800)
    return apply(x, env_adsr(len(x), 0.03, 0.08, 0.8, 0.15))


def farm_day_bed(sec=8.0):
    # Seamless loop: filtered wind + sparse birds; crossfade the tail into the head.
    n = int(sec * SR)
    wind = lowpass(noise(sec + 1), 400)
    wind = [w * (0.6 + 0.4 * math.sin(2 * math.pi * i / n)) for i, w in enumerate(wind)]
    bed = list(wind[:n])
    xf = int(SR)
    for i in range(xf):
        a = i / xf
        bed[i] = bed[i] * a + wind[n + i] * (1 - a)
    for k in range(6):
        f0 = rng.uniform(2500, 4000); start = rng.uniform(0.2, sec - 0.6)
        b = osc(lambda t, f0=f0: f0 * (1 + 0.3 * math.sin(2 * math.pi * 18 * t)), 0.18)
        b = apply(b, env_adsr(len(b), 0.01, 0.05, 0.5, 0.05))
        mix(bed, b, int(start * SR), 0.12)
    return bed[:n]


def main():
    root = sys.argv[1]
    def put(evt, name, samples):
        write(os.path.join(root, evt, name + ".wav"), samples)

    put("SFX_Water_Splash_Big", "sfx_water_splash_big_01", splash(True))
    put("SFX_Water_Splash_Big", "sfx_water_splash_big_02", splash(True))
    put("SFX_Water_Splash_Small", "sfx_water_splash_small_01", splash(False))
    put("SFX_Water_Splash_Small", "sfx_water_splash_small_02", splash(False))
    put("SFX_Wallet_Wah", "sfx_wallet_wah_01", wah())
    put("STG_Disaster_River", "stg_disaster_river_01", mix(slide_whistle(), splash(True), int(0.75 * SR), 0.8))
    put("STG_Disaster_Plank", "stg_disaster_plank_01", trombone(["Bb3", "A3", "Ab3", "G3"], 0.28))
    put("STG_Disaster_Wagon", "stg_disaster_wagon_01", cymbal())
    put("STG_Disaster_Generic", "stg_disaster_generic_01", trombone(["F2", "E2", "Eb2", "D2"], 0.3))
    put("STG_Disaster_RecordScratch", "stg_disaster_recordscratch_01", record_scratch())
    put("STG_Disaster_Sweetener", "stg_disaster_sweetener_01", sweetener())
    put("STG_Music_ThroatClear", "stg_music_throatclear_01", throat_clear())

    loops = {
        "MUS_Morning_Porch": music_loop(92, 4, 8, ["G", "C", "G", "D"], style="banjo", stems=("base",)),
        "MUS_Workday_Roundup": music_loop(132, 4, 8, ["D", "G", "D", "A"], style="banjo"),
        "MUS_Workday_Crunch": music_loop(152, 4, 8, ["E", "A", "E", "B"], style="banjo"),
        "MUS_Night_Dusk": music_loop(70, 4, 4, ["D", "C", "Bb", "A"], minor=True, style="organ", stems=("base",)),
        "MUS_Payout_Market": music_loop(108, 3, 8, ["C", "F", "C", "G"], style="waltz", stems=("base",)),
        "MUS_Sunday_Landlord": music_loop(100, 4, 8, ["Bb", "Eb", "Bb", "F"], style="oompah", stems=("base",)),
    }
    for evt, stems in loops.items():
        for stem, x in stems.items():
            write(os.path.join(root, evt, f"{evt.lower()}_{stem}.wav"), x, peak=0.7)

    # Added later: generated after the loops so earlier outputs stay byte-identical (shared rng).
    for i in range(3):
        put("SFX_Critter_Pig_Squeal", f"sfx_critter_pig_squeal_{i+1:02d}", pig_squeal(100 + i))
        put("SFX_Critter_Chicken_Panic", f"sfx_critter_chicken_panic_{i+1:02d}", chicken_panic(200 + i))
        put("SFX_Player_Shout", f"sfx_player_shout_{i+1:02d}", player_shout(300 + i))
    put("SFX_Critter_Bull_Telegraph", "sfx_critter_bull_telegraph_01", bull_telegraph())
    write(os.path.join(root, "AMB_Farm_Day", "amb_farm_day_loop.wav"), farm_day_bed(), peak=0.5)


if __name__ == "__main__":
    main()
