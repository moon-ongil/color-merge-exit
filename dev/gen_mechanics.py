"""Generate demonstration levels that introduce the two new block types:
  - TIMER ("fuse") block:  must be cleared within N moves (a countdown badge).
  - COLOUR-CYCLING ("chameleon") block: its colour steps every move; merge on the right colour.

Levels are built from INDEPENDENT horizontal lanes — each row is a self-contained unit (a block or
pair with a straight exit door on that row). Because lanes never interfere, the whole board is
solvable by construction (clear one lane at a time). Every emitted level is still cross-checked with
the REAL C# Solver (dev/gen_mechanics_verify) so nothing ships unsolvable.

Colours (CarColor enum): R=0 B=1 Y=2 G=3 P=4 O=5.  Edges: Top=0 Bottom=1 Left=2 Right=3.
Mixes: R+B=P(4), B+Y=G(3), R+Y=O(5).
"""
import json, os, random, sys

R, B, Y, G, P, O = 0, 1, 2, 3, 4, 5
RIGHT = 3
OUTDIR = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"

# (a,b) -> secondary door colour
MIX = {frozenset((R, B)): P, frozenset((B, Y)): G, frozenset((R, Y)): O}

def cell0():
    return [dict(x=0, y=0)]

def block(bid, color, x, y, **extra):
    b = dict(id=bid, color=color, x=x, y=y, w=1, h=1, cells=cell0(), axis=0, locked=False)
    b.update(extra)
    return b

def door_right(row, color):
    return dict(edge=RIGHT, laneStart=row, length=1, color=color, colorSequence=[color])

# Blocks sit CLOSE to their exit (mover at x=1, partner at x=2) on a small 6-wide board so there is
# little empty room to slide into — that keeps the solver's state space tiny, so Hint / dead-end
# detection stay fast. Sparse boards with lots of empty cells explode the search.
def normal_lane(bid, row, rng):
    """Two 1x1 blocks A,B that mix to secondary S; slide A right into B -> S -> exit right."""
    a, b = rng.choice([(R, B), (B, Y), (R, Y)])
    s = MIX[frozenset((a, b))]
    blocks = [block(bid, a, 1, row), block(bid + 1, b, 2, row)]
    return blocks, [door_right(row, s)], bid + 2

def timer_lane(bid, row, tsec, rng):
    """A timer block with a straight matching exit — clear it before `tsec` REAL seconds elapse."""
    col = rng.choice([R, B, Y, G, P, O])
    return [block(bid, col, 2, row, timerSeconds=tsec)], [door_right(row, col)], bid + 1

def chameleon_lane(bid, row, rng, cyc_seconds=5.0):
    """Chameleon cycles two colours every `cyc_seconds` REAL seconds; slides into a fixed partner to
    make a secondary door colour. The player WAITS for the right colour, so the merge always works
    (the door colour is a mix of one cycle colour + the partner). Solver treats the cycle colour as a
    wildcard, so this is cheap regardless of count."""
    a, b = rng.choice([(R, B), (B, Y), (R, Y)])   # both primaries; a merges with partner b
    s = MIX[frozenset((a, b))]                      # secondary door colour
    other = rng.choice([c for c in (G, P, O) if c != s])  # a secondary: inert vs a primary, != door
    cycle = rng.choice([[a, other], [other, a]])
    cham = block(bid, cycle[0], 1, row, cycle=cycle, cycleSeconds=cyc_seconds)
    partner = block(bid + 1, b, 2, row)
    return [cham, partner], [door_right(row, s)], bid + 2

def build_level(i, rng):
    """Escalating recipe across the demo band. Both mechanics are now REAL-TIME and solver-cheap
    (timer isn't modelled logically; chameleon colour is a wildcard), so levels can be richer."""
    # small, DENSE 6x6 board: many blocks on a roomy board explode the slide-search (that is
    # independent of the timer/chameleon cost), so keep it compact and few-block.
    W = H = 6
    k = i - 501                                  # 0-based within the band
    blocks, doors = [], []
    bid = 1
    rows = list(range(H)); rng.shuffle(rows)
    ri = 0
    def take_row():
        nonlocal ri
        r = rows[ri]; ri += 1; return r
    # difficulty ramp (total lanes capped at 6 rows -> at most ~9 blocks)
    n_cham = 1 + k // 6                             # 1 -> 2 colour-cyclers
    n_timer = 1 + k // 5                            # 1 -> 2 timer lanes
    n_normal = 1                                    # one background merge
    tsec = float(max(6, 12 - k // 2))              # 12s -> 6s real-time window (tighter as it climbs)
    csec = float(max(3, 5 - k // 6))               # colour flips every 5s -> 3s
    for _ in range(n_cham):
        if ri >= H: break
        bl, dr, bid = chameleon_lane(bid, take_row(), rng, csec); blocks += bl; doors += dr
    for _ in range(n_timer):
        if ri >= H: break
        bl, dr, bid = timer_lane(bid, take_row(), tsec, rng); blocks += bl; doors += dr
    for _ in range(n_normal):
        if ri >= H: break
        bl, dr, bid = normal_lane(bid, take_row(), rng); blocks += bl; doors += dr
    nb = len(blocks)
    tlimit = float(35 + nb * 8)
    return dict(id=i, name=f"Stage {i}", width=W, height=H, blocks=blocks, doors=doors,
                walls=[], splitters=[], paint=[], memorize=False,
                timeLimitSeconds=tlimit, star2SecondsLeft=round(tlimit * 0.30, 1),
                star3SecondsLeft=round(tlimit * 0.55, 1))

if __name__ == "__main__":
    start = int(sys.argv[1]) if len(sys.argv) > 1 else 501
    end = int(sys.argv[2]) if len(sys.argv) > 2 else 520
    for i in range(start, end + 1):
        rng = random.Random(4242 + i * 131)
        lvl = build_level(i, rng)
        json.dump(lvl, open(os.path.join(OUTDIR, f"level_{i:03d}.json"), "w"), indent=2)
        t = sum(1 for b in lvl['blocks'] if b.get('timerSeconds'))
        c = sum(1 for b in lvl['blocks'] if b.get('cycle'))
        print(f"level_{i:03d}  blocks={len(lvl['blocks'])}  timer={t}  chameleon={c}  t={int(lvl['timeLimitSeconds'])}s")
    print(f"\nwrote {start}-{end}. VERIFY with the real solver next.")
