"""Procedurally generate solvable levels 008-030 with a difficulty curve.
Reuses the BFS solver from levels.py to guarantee each level is solvable and to
grade difficulty by minimum move count. Right-edge exits (core color-sequence
mechanic); targets are horizontal cars in the exit row, blockers are vertical
cars that cross rows (Rush-Hour style), plus obstacles in later tiers.
"""
import json, os, random
from levels import solve, R, B, Y, G, P, O, H, V, RIGHT

W = HGT = 6

def cells(x, y, length, orient):
    return [(x+i, y) for i in range(length)] if orient == H else [(x, y+i) for i in range(length)]

def fits(cs, occ, obstacles):
    for (x, y) in cs:
        if not (0 <= x < W and 0 <= y < HGT): return False
        if (x, y) in occ or (x, y) in obstacles: return False
    return True

def veh(vid, color, length, orient, x, y, target):
    return {"id": vid, "color": color, "length": length, "orientation": orient,
            "x": x, "y": y, "isTarget": target}

def tier_for(i):
    """Difficulty params as a function of level id (8..30)."""
    if i <= 12:      # easy
        return dict(colors=[R, B, Y], exits=1, maxSeq=1, lengths=[2],
                    blockers=(2, 3), obstacles=0, minM=3, maxM=7,
                    time=86 - (i-8)*2)
    if i <= 20:      # medium
        return dict(colors=[R, B, Y, G], exits=1 if i < 17 else random.choice([1, 2]),
                    maxSeq=2, lengths=[2, 3], blockers=(3, 4), obstacles=(0, 1),
                    minM=6, maxM=11, time=76 - (i-13)*1)
    # hard 21..30
    return dict(colors=[R, B, Y, G, P], exits=random.choice([1, 2]), maxSeq=2,
                lengths=[2, 3], blockers=(4, 5), obstacles=(1, 2),
                minM=9, maxM=17, time=64 - (i-21)*1)

def place_targets(rng, row, seq, occ, obstacles, out, nid):
    """Targets horizontal in `row`; seq[0] must be rightmost (exits first)."""
    xr = W - 1 - rng.randint(0, 1)  # rightmost target's right cell
    for k, color in enumerate(seq):
        L = 2
        x = xr - (L - 1)
        cs = cells(x, row, L, H)
        if not fits(cs, occ, obstacles): return False
        for c in cs: occ.add(c)
        out.append(veh(nid(), color, L, H, x, row, True))
        xr = x - 1 - rng.randint(1, 2)
        if xr < 0 and k < len(seq) - 1: return False
    return True

def place_blockers(rng, n, colors, lengths, occ, obstacles, out, nid, rows_of_interest):
    tries = 0
    placed = 0
    while placed < n and tries < 200:
        tries += 1
        vert = rng.random() < 0.7
        L = rng.choice(lengths)
        if vert:
            x = rng.randrange(W)
            y = rng.randint(0, HGT - L)
        else:
            y = rng.randrange(HGT)
            x = rng.randint(0, W - L)
        orient = V if vert else H
        cs = cells(x, y, L, orient)
        if not fits(cs, occ, obstacles): continue
        for c in cs: occ.add(c)
        out.append(veh(nid(), rng.choice(colors), L, orient, x, y, False))
        placed += 1

def make(idnum, rng):
    t = tier_for(idnum)
    minM, maxM = t['minM'], t['maxM']
    for attempt in range(6000):
        occ = set(); obstacles = set(); vehicles = []
        counter = [0]
        def nid():
            counter[0] += 1
            return counter[0]

        nObs = t['obstacles'] if isinstance(t['obstacles'], int) else rng.randint(*t['obstacles'])
        for _ in range(nObs):
            ox, oy = rng.randrange(W), rng.randrange(HGT)
            obstacles.add((ox, oy))

        # exits on right edge, distinct rows
        rows = rng.sample(range(HGT), t['exits'])
        exit_defs = []
        ok = True
        for row in rows:
            seqLen = rng.randint(1, t['maxSeq'])
            seq = [rng.choice(t['colors']) for _ in range(seqLen)]
            if not place_targets(rng, row, seq, occ, obstacles, vehicles, nid):
                ok = False; break
            exit_defs.append((row, seq))
        if not ok:
            continue

        nBlk = rng.randint(*t['blockers'])
        place_blockers(rng, nBlk, t['colors'], t['lengths'], occ, obstacles, vehicles, nid, rows)

        level = dict(id=idnum, name=f"Stage {idnum}", width=W, height=HGT,
                     timeLimitSeconds=float(t['time']), star2SecondsLeft=18.0, star3SecondsLeft=32.0,
                     vehicles=vehicles, exits=[{"edge": RIGHT, "lane": r, "colorSequence": s} for (r, s) in exit_defs],
                     obstacles=[{"x": x, "y": y} for (x, y) in obstacles])
        d = solve(level, max_states=700000)
        if d is not None and minM <= d <= maxM:
            return level, d
    return None

if __name__ == "__main__":
    outdir = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
    os.makedirs(outdir, exist_ok=True)
    made = 0
    for idnum in range(8, 31):
        rng = random.Random(1000 + idnum)  # reproducible per level
        res = make(idnum, rng)
        if res is None:
            print(f"level_{idnum:03d}  FAILED to generate"); continue
        level, d = res
        with open(os.path.join(outdir, f"level_{idnum:03d}.json"), "w") as f:
            json.dump(level, f, indent=2)
        made += 1
        print(f"level_{idnum:03d}  moves={d:2d}  cars={len(level['vehicles'])}  obst={len(level['obstacles'])}  exits={len(level['exits'])}  t={int(level['timeLimitSeconds'])}s")
    print(f"\nGenerated {made}/23 levels (total {7+made} with the 7 hand-made).")
