"""Generate levels 501-1000 (pure escalating MERGE boards with a climbing hurdle/wall
count). Reuses mergegen's build+solver so every emitted level is solvable. Levels 1-500
are never touched. Resilient: if a seed can't be solved, retries alternate seeds before
giving up, and records any hole in gen_extend_fail.log so nothing silently falls back to
the tutorial level at runtime.
"""
import json, os, sys, time
import mergegen as m

START = int(sys.argv[1]) if len(sys.argv) > 1 else 501
END = int(sys.argv[2]) if len(sys.argv) > 2 else 1000
outdir = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
faillog = os.path.join(os.path.dirname(__file__), "gen_extend_fail.log")

made = 0
fails = []
open(faillog, "w").close()
t0 = time.time()
for i in range(START, END + 1):
    cand = None
    for attempt in range(6):                       # a few alternate seeds before conceding
        seed = 90000 + i * 59 + attempt * 7919
        cand = m.gen(i, seed, None, 60000)         # spec=None -> derived escalating merge difficulty
        if cand is not None:
            break
    if cand is None:
        fails.append(i)
        with open(faillog, "a") as f:
            f.write(f"{i}\n")
        print(f"level_{i:03d}  FAILED", flush=True)
        continue
    lvl = m.to_level(i, cand)
    json.dump(lvl, open(os.path.join(outdir, f"level_{i:03d}.json"), "w"), indent=2)
    made += 1
    locks = sum(1 for b in lvl['blocks'] if b['locked'])
    if i % 10 == 0 or i < START + 5:
        el = time.time() - t0
        print(f"level_{i:03d}  blocks={len(lvl['blocks']):2d}  locks={locks}  "
              f"walls={len(lvl['walls'])}  t={int(lvl['timeLimitSeconds'])}s  "
              f"[{made}/{i - START + 1} in {el/60:.1f}m]", flush=True)

print(f"\nDONE {START}-{END}: made {made}, failed {len(fails)} "
      f"in {(time.time()-t0)/60:.1f}m", flush=True)
if fails:
    print(f"FAILED levels: {fails}", flush=True)
