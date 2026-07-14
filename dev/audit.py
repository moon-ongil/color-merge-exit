import json, os, glob
from collections import Counter, defaultdict

D = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
rows = []
for i in range(1, 501):
    d = json.load(open(os.path.join(D, f"level_{i:03d}.json")))
    blocks = d['blocks']
    nb = len(blocks)
    shaped = sum(1 for b in blocks if len(b['cells']) > 1)
    locks = sum(1 for b in blocks if b.get('locked'))
    rails = sum(1 for b in blocks if b.get('axis', 0) != 0)
    splits = len(d.get('splitters', []))
    walls = len(d.get('walls', []))
    doors = d['doors']
    W, H = d['width'], d['height']
    # door colors
    dcols = []
    for dr in doors:
        seq = dr.get('colorSequence') or [dr.get('color')]
        dcols.extend(seq)
    density = (sum(len(b['cells']) for b in blocks) + walls) / (W*H)
    rows.append(dict(i=i, nb=nb, shaped=shaped, locks=locks, rails=rails, splits=splits,
                     walls=walls, doors=len(doors), W=W, H=H, t=int(d['timeLimitSeconds']),
                     density=round(density,2), dcols=dcols, cells=sum(len(b['cells']) for b in blocks)))
    d['_type'] = 'split' if splits and nb<=6 and locks==0 else ('remerge' if splits and 'remerge' else 'merge')

def band(lo, hi):
    r = [x for x in rows if lo <= x['i'] <= hi]
    def avg(k): return round(sum(x[k] for x in r)/len(r),1)
    def rng(k): return f"{min(x[k] for x in r)}-{max(x[k] for x in r)}"
    ncolors = round(sum(len(set(x['dcols'])) for x in r)/len(r),1)
    return (f"L{lo:>3}-{hi:<3} | blocks {rng('nb'):>7} (avg {avg('nb'):>4}) | shaped {avg('shaped'):>3} | "
            f"locks {avg('locks')} | walls {avg('walls'):>3} | doors {avg('doors')} | "
            f"time {rng('t'):>7} | dens {avg('density')} | door-colors/lvl {ncolors}")

print("=== DIFFICULTY BANDS ===")
for lo in range(1, 501, 25):
    print(band(lo, min(lo+24,500)))

print("\n=== MECHANIC INTRODUCTION (first level each appears) ===")
firsts = {}
for x in rows:
    if x['shaped'] and 'shaped' not in firsts: firsts['shaped']=x['i']
    if x['locks'] and 'locks' not in firsts: firsts['locks']=x['i']
    if x['rails'] and 'rails' not in firsts: firsts['rails']=x['i']
    if x['splits'] and 'splits' not in firsts: firsts['splits']=x['i']
    if x['W']==7 and '7x7' not in firsts: firsts['7x7']=x['i']
print(firsts)

print("\n=== SPLIT / REMERGE SPECIAL LEVELS (difficulty DIPS among dense boards) ===")
specials = [x['i'] for x in rows if x['splits']>0 and x['nb']<=6]
print(f"count={len(specials)}: {specials}")

print("\n=== POTENTIAL MONOTONY: how many DISTINCT (nb,locks,doors,walls,W) signatures per band ===")
for lo in range(76, 501, 50):
    r=[x for x in rows if lo<=x['i']<lo+50]
    sigs=set((x['nb'],x['locks'],x['doors'],x['W']) for x in r)
    print(f"L{lo}-{lo+49}: {len(sigs)} distinct (nb,locks,doors,size) signatures out of {len(r)} levels")

print("\n=== TIME PRESSURE (all identical late?) ===")
late=[x['t'] for x in rows if x['i']>=90]
print(f"L90-500 time: min {min(late)} max {max(late)} distinct values {sorted(set(late))}")

print("\n=== COLOR VARIETY: door-color palette usage over whole game ===")
allc = Counter()
for x in rows:
    for c in set(x['dcols']): allc[c]+=1
print(dict(allc))
