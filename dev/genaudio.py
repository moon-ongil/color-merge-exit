"""Generate royalty-free BGM (seamless chiptune loop) + improved SFX (pure Python)."""
import wave, struct, math, random

SR = 44100
DST = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/Resources/Audio"

def save(name, s):
    peak = max(1e-6, max(abs(x) for x in s))
    g = min(1.0, 0.92 / peak)
    with wave.open(f"{DST}/{name}.wav", "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x * g)) * 32000)) for x in s))

def square(f, t):  return 1.0 if math.sin(2*math.pi*f*t) >= 0 else -1.0
def tri(f, t):     return 2/math.pi * math.asin(math.sin(2*math.pi*f*t))
def sine(f, t):    return math.sin(2*math.pi*f*t)

def pluck(freq, dur, vol=0.5, wave_fn=square, decay=14.0):
    """A short plucked note with exponential decay (near-0 at end -> loop-safe)."""
    n = int(SR*dur); out = []
    for i in range(n):
        t = i/SR
        env = math.exp(-decay*t) * (1 - math.exp(-400*t))  # fast attack, exp decay
        out.append(vol*env*wave_fn(freq, t))
    return out

def add_into(buf, seq, start):
    for i, x in enumerate(seq):
        j = start + i
        if 0 <= j < len(buf):
            buf[j] += x

# ---------------- BGM: seamless 8s chiptune loop ----------------
def make_bgm():
    beat = 0.5                       # 120 BPM
    bar = beat*4
    total = bar*4                    # 8 s, 4 chords
    n = int(SR*total)
    buf = [0.0]*n
    # chords: (bass root, [chord tones]) — I V vi IV in C
    prog = [
        (130.81, [261.63, 329.63, 392.00]),   # C
        (98.00,  [196.00, 246.94, 293.66]),   # G
        (110.00, [220.00, 261.63, 329.63]),   # Am
        (87.31,  [174.61, 220.00, 261.63]),   # F
    ]
    for b, (bass, tones) in enumerate(prog):
        bar_start = int(b*bar*SR)
        # bass on each beat
        for k in range(4):
            add_into(buf, pluck(bass, beat, 0.34, square, 8.0), bar_start + int(k*beat*SR))
        # soft chord pad plucked on beats 1 and 3 (triangle)
        for k in (0, 2):
            for tn in tones:
                add_into(buf, pluck(tn, beat*2, 0.10, tri, 4.0), bar_start + int(k*beat*SR))
        # melody: arpeggio in eighth notes through chord tones (+octave sparkle)
        arp = [tones[0], tones[1], tones[2], tones[1]*2, tones[2], tones[1], tones[0]*2, tones[1]]
        for k in range(8):
            add_into(buf, pluck(arp[k], beat*0.5, 0.20, square, 12.0), bar_start + int(k*beat*0.5*SR))
    save("bgm", buf)
    return total

# ---------------- SFX ----------------
def sfx_tap():
    n = int(SR*0.05); out = []
    for i in range(n):
        t = i/SR; env = math.exp(-60*t)
        out.append(0.5*env*(sine(1400, t) + 0.4*sine(2600, t)))
    save("tap", out)

def sfx_slide():
    n = int(SR*0.16); out = []; last = 0.0
    for i in range(n):
        t = i/SR; env = math.exp(-9*t)*(1-math.exp(-200*t))
        w = random.uniform(-1, 1); last = 0.72*last + 0.28*w   # lowpassed noise
        pitch = 260 + 240*(t/0.16)
        out.append(env*(0.34*last + 0.14*tri(pitch, t)))
    save("slide", out)

def sfx_exit():
    n = int(SR*0.34); out = []
    for i in range(n):
        t = i/SR; env = math.exp(-5.5*t)*(1-math.exp(-300*t))
        f = 420 + 900*(t/0.34)                    # rising swoosh
        v = 0.42*square(f, t) + 0.2*sine(2*f, t)
        out.append(env*v)
    # add a bright pop near the end
    for i in range(int(SR*0.05)):
        t = i/SR; env = math.exp(-40*t)
        j = int(SR*0.24)+i
        if j < len(out): out[j] += 0.4*env*sine(1600, t)
    save("exit", out)

def sfx_merge():
    """Two tones glide together (a 'blend') capped with a bright bell ding."""
    n = int(SR*0.24); out = []
    for i in range(n):
        t = i/SR; env = math.exp(-10*t)*(1-math.exp(-350*t))
        f1 = 300 + 160*(t/0.24)          # rising
        f2 = 560 - 160*(t/0.24)          # falling -> they converge
        out.append(env*(0.28*tri(f1, t) + 0.28*tri(f2, t)))
    for i in range(int(SR*0.13)):        # bell ding on top
        t = i/SR; env = math.exp(-16*t)
        out[i] += 0.30*env*sine(880, t) + 0.16*env*sine(1320, t)
    save("merge", out)

def sfx_win():
    notes = [523.25, 659.25, 783.99, 1046.50]     # C E G C
    out = []
    for k, f in enumerate(notes):
        dur = 0.12 if k < 3 else 0.32
        seg = pluck(f, dur, 0.5, square, 6.0)
        # sparkle harmonic
        for i in range(len(seg)):
            t = i/SR; seg[i] += 0.18*math.exp(-8*t)*sine(f*2, t)
        out += seg
    # gentle echo tail
    tail = [0.0]*int(SR*0.18) + [0.35*x for x in out]
    for i in range(len(out)):
        if i < len(tail): out[i] += tail[i]
    save("win", out)

def sfx_lose():
    notes = [(440, 0.18), (349.23, 0.34)]         # A -> F descending
    out = []
    for f, dur in notes:
        n = int(SR*dur)
        for i in range(n):
            t = i/SR; env = math.exp(-5*t)*(1-math.exp(-200*t))
            vib = 1 + 0.01*math.sin(2*math.pi*6*t)
            out.append(0.45*env*tri(f*vib, t))
    save("lose", out)

if __name__ == "__main__":
    dur = make_bgm()
    sfx_tap(); sfx_slide(); sfx_exit(); sfx_merge(); sfx_win(); sfx_lose()
    import os
    print(f"BGM loop: {dur:.1f}s")
    for f in sorted(os.listdir(DST)):
        if f.endswith(".wav"): print(" ", f, os.path.getsize(f"{DST}/{f}"), "B")
