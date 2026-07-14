"""Generate solvable KLOTSKI-style BLOCK levels (dense pack, 4-directional sliding,
peelable color tiers).

The board is packed DENSELY with movable blocks of various sizes (1x1, 2x1, 1x2, 2x2)
leaving only a few empty cells, so the target block is genuinely ENTANGLED: you must
shuffle other blocks out of the way (like Klotski / Hua Rong Dao) to route it to a
matching-color edge exit. Delivering peels the target's top color layer; the last layer
removes it. Difficulty comes from the number of untangle MOVES (BFS min-solution length)
plus the color-tier memory, NOT from static maze walls.

Counter-intuitively, a denser board is BOTH harder (little room to maneuver) and cheaper
for BFS (short slides, low branching) — the same reason a 15-puzzle with one empty cell is
hard yet tractable.

A BFS solver (matching the C# Board) validates solvability and grades difficulty.
"""
import json, os, random
from collections import deque

R, B, Y, G, P, O = 0, 1, 2, 3, 4, 5
TOP, BOT, LEFT, RIGHT = 0, 1, 2, 3
DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]


def edge_for(dx, dy):
    if dx > 0: return RIGHT
    if dx < 0: return LEFT
    return BOT if dy > 0 else TOP


def cells(x, y, w, h):
    return [(x + i, y + j) for j in range(h) for i in range(w)]


def leading(x, y, w, h, dx, dy):
    if dx > 0: return [(x + w, y + j) for j in range(h)]
    if dx < 0: return [(x - 1, y + j) for j in range(h)]
    if dy > 0: return [(x + i, y + h) for i in range(w)]
    return [(x + i, y - 1) for i in range(w)]


def solve(level, max_states=200000):
    W, H = level['width'], level['height']
    sobs = set((o['x'], o['y']) for o in level.get('obstacles', []))
    exits = [(e['edge'], e['lane'], tuple(e['colorSequence'])) for e in level['exits']]
    blocks = []
    for b in level['blocks']:
        cs = tuple(b.get('colors') or [b.get('color', 0)])
        blocks.append((b['id'], b['x'], b['y'], cs, b.get('w', 1), b.get('h', 1),
                       b.get('isTarget', True), b.get('axis', 0)))

    def occ(bl):
        s = set()
        for (_id, x, y, cs, w, h, t, ax) in bl:
            for c in cells(x, y, w, h):
                s.add(c)
        return s

    def exit_at(edge, lane):
        for k, (ed, ln, sq) in enumerate(exits):
            if ed == edge and ln == lane:
                return k
        return None

    start = (tuple(blocks), tuple(0 for _ in exits))

    def key(state):
        bl, idx = state
        return (tuple(sorted((b[0], b[1], b[2], b[3]) for b in bl)), idx)

    def is_goal(state):
        return not any(b[6] for b in state[0])  # no target block remains

    def neighbors(state):
        bl, eidx = state
        out = []
        occupied = occ(bl)
        for bi, (bid, x, y, cs, w, h, t, ax) in enumerate(bl):
            own = set(cells(x, y, w, h))
            base = occupied - own
            for (dx, dy) in DIRS:
                if ax == 1 and dy != 0: continue  # horizontal-locked rail
                if ax == 2 and dx != 0: continue  # vertical-locked rail
                bx, by = x, y
                while True:
                    lead = leading(bx, by, w, h, dx, dy)
                    if any(not (0 <= lx < W and 0 <= ly < H) for (lx, ly) in lead):
                        # off-board: delivery if 1x1 target + matching exit
                        if w == 1 and h == 1 and t:
                            edge = edge_for(dx, dy)
                            lane = by if dx != 0 else bx
                            k = exit_at(edge, lane)
                            if k is not None:
                                ed, ln, sq = exits[k]
                                if sq[eidx[k] % len(sq)] == cs[0]:
                                    nidx = list(eidx); nidx[k] += 1
                                    ncs = cs[1:]
                                    nbl = list(bl)
                                    if ncs:
                                        nbl[bi] = (bid, bx, by, ncs, w, h, t, ax)
                                    else:
                                        nbl.pop(bi)
                                    out.append((tuple(nbl), tuple(nidx)))
                        break
                    if any((lx, ly) in base or (lx, ly) in sobs for (lx, ly) in lead):
                        break
                    bx += dx; by += dy
                    nbl = list(bl); nbl[bi] = (bid, bx, by, cs, w, h, t, ax)
                    out.append((tuple(nbl), eidx))
        return out

    seen = {key(start)}
    q = deque([(start, 0)])
    while q:
        st, d = q.popleft()
        if is_goal(st): return d
        if len(seen) > max_states: return None
        for nb in neighbors(st):
            kk = key(nb)
            if kk not in seen:
                seen.add(kk); q.append((nb, d + 1))
    return None


# ---------------- generation ----------------
# Difficulty = untangle MOVES (dense pack) + color TIERS (memory). No maze walls.
# `empty` = cells left free (fewer = tighter jam). `sizes` = weighted block-shape pool.
# `lock_frac` = chance a blocker is an axis-locked "rail" block (Rush-Hour jam),
# introduced from level 13 on and taught only after pure Klotski (levels 1-12).
def tier_for(i):
    if i <= 6:
        return dict(size=5, colors=[R, B, Y], tiers=(1, 2), empty=(3, 4),
                    sizes=[(1, 1), (1, 1), (2, 1), (1, 2)], minM=2, maxM=9, lock=0.0)
    if i <= 12:
        return dict(size=5, colors=[R, B, Y, G], tiers=(2, 2), empty=(2, 3),
                    sizes=[(1, 1), (2, 1), (1, 2), (2, 1), (1, 2), (2, 2)], minM=7, maxM=18, lock=0.0)
    if i <= 18:
        return dict(size=6, colors=[R, B, Y, G, P], tiers=(2, 3), empty=(2, 3),
                    sizes=[(1, 1), (2, 1), (1, 2), (2, 1), (1, 2), (2, 2)], minM=11, maxM=26, lock=0.35)
    if i <= 24:
        return dict(size=6, colors=[R, B, Y, G, P], tiers=(3, 3), empty=(2, 3),
                    sizes=[(1, 1), (2, 1), (1, 2), (2, 1), (1, 2), (2, 2)], minM=15, maxM=34, lock=0.45)
    return dict(size=6, colors=[R, B, Y, G, P, O], tiers=(3, 4), empty=(2, 3),
                sizes=[(1, 1), (2, 1), (1, 2), (2, 1), (1, 2), (2, 2), (2, 2)], minM=18, maxM=48, lock=0.5)


def time_for(moves):
    """Generous total time that scales with untangle difficulty; stars reward speed."""
    return float(max(60, min(150, 42 + moves * 3)))


def rint(v): return v if isinstance(v, int) else random.randint(*v)


def build_candidate(i, rng, t):
    W = H = t['size']
    occ = set()

    # exit on a random edge/lane
    edge = rng.choice([RIGHT, LEFT, TOP, BOT])
    horiz = edge in (LEFT, RIGHT)
    lane = rng.randrange(H if horiz else W)

    # color stack (no immediate repeats)
    T = rint(t['tiers'])
    seq = []
    for _ in range(T):
        pool = [c for c in t['colors'] if not seq or c != seq[-1]] or t['colors']
        seq.append(rng.choice(pool))

    # target 1x1 on the exit's lane line, a bit inside
    if horiz:
        ty = lane; tx = rng.randint(1, W - 2)
    else:
        tx = lane; ty = rng.randint(1, H - 2)
    blocks = [dict(id=1, colors=list(seq), x=tx, y=ty, w=1, h=1, isTarget=True)]
    for c in cells(tx, ty, 1, 1): occ.add(c)

    # densely fill the rest with movable blocks until only `empty` cells remain
    target_empty = rint(t['empty'])
    vid = 2
    tries = 0
    total = W * H
    while (total - len(occ)) > target_empty and tries < 400:
        tries += 1
        w, h = rng.choice(t['sizes'])
        x = rng.randint(0, W - w); y = rng.randint(0, H - h)
        cs = cells(x, y, w, h)
        if any(c in occ for c in cs): continue
        for c in cs: occ.add(c)
        # Some blockers are axis-locked rails: lock along the long side (Rush-Hour car),
        # or a random axis for 1x1s. Target stays free (omitted -> axis 0).
        axis = 0
        if t.get('lock', 0.0) and rng.random() < t['lock']:
            axis = 1 if w > h else 2 if h > w else rng.choice([1, 2])
        blocks.append(dict(id=vid, colors=[rng.choice(t['colors'])], x=x, y=y, w=w, h=h, isTarget=False, axis=axis))
        vid += 1

    return dict(id=i, name=f"Stage {i}", width=W, height=H,
                timeLimitSeconds=90.0, star2SecondsLeft=18.0, star3SecondsLeft=32.0,
                blocks=blocks,
                exits=[dict(edge=edge, lane=lane, colorSequence=list(seq))],
                obstacles=[])


def finalize(lv, d):
    """Set time budget + star thresholds from the solved difficulty."""
    time = time_for(d)
    lv['timeLimitSeconds'] = time
    lv['star2SecondsLeft'] = round(time * 0.30, 1)
    lv['star3SecondsLeft'] = round(time * 0.50, 1)
    return lv


def gen(i, rng):
    t = tier_for(i)
    best = None  # fallback: hardest solvable seen, even if below the target band
    for _ in range(4000):
        lv = build_candidate(i, rng, t)
        d = solve(lv, max_states=200000)
        if d is None:
            continue
        if t['minM'] <= d <= t['maxM']:
            return finalize(lv, d), d
        if d >= t['minM'] and (best is None or d > best[1]):
            best = (lv, d)
    return (finalize(best[0], best[1]), best[1]) if best else None


if __name__ == "__main__":
    outdir = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
    os.makedirs(outdir, exist_ok=True)
    made = 0
    for i in range(1, 31):
        rng = random.Random(7000 + i * 13)
        res = gen(i, rng)
        if res is None:
            print(f"level_{i:03d}  FAILED", flush=True); continue
        lv, d = res
        with open(os.path.join(outdir, f"level_{i:03d}.json"), "w") as f:
            json.dump(lv, f, indent=2)
        made += 1
        tg = lv['blocks'][0]
        empties = lv['width'] * lv['height'] - sum(b['w'] * b['h'] for b in lv['blocks'])
        rails = sum(1 for b in lv['blocks'] if b.get('axis', 0) != 0)
        print(f"level_{i:03d}  moves={d:2d}  tiers={len(tg['colors'])}  blocks={len(lv['blocks']):2d}  "
              f"rails={rails:2d}  empty={empties}  size={lv['width']}  t={int(lv['timeLimitSeconds'])}s", flush=True)
    print(f"\nGenerated {made}/30 dense Klotski block levels.", flush=True)
