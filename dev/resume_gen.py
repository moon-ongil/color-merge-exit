import json, os, sys
import mergegen as m

START = int(sys.argv[1]) if len(sys.argv) > 1 else 462
END = int(sys.argv[2]) if len(sys.argv) > 2 else 500
outdir = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"

for i in range(START, END + 1):
    seed = 90000 + i * 59
    spec = m.REMERGE_LEVELS.get(i) or m.SPLIT_LEVELS.get(i)
    cap = 60000 if (i >= 160 and i not in m.SPLIT_LEVELS and i not in m.REMERGE_LEVELS) else 200000
    cand = m.gen(i, seed, spec, cap)
    if cand is None:
        print(f"level_{i:03d}  FAILED", flush=True); continue
    lvl = m.to_level(i, cand)
    json.dump(lvl, open(os.path.join(outdir, f"level_{i:03d}.json"), "w"), indent=2)
    locks = sum(1 for b in lvl['blocks'] if b['locked'])
    splits = len(lvl['splitters'])
    print(f"level_{i:03d}  blocks={len(lvl['blocks']):2d}  doors={len(lvl['doors'])}  "
          f"locks={locks}  splits={splits}  walls={len(lvl['walls'])}  t={int(lvl['timeLimitSeconds'])}s", flush=True)
print(f"\nDone ({START}-{END}).", flush=True)
