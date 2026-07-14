"""Generate HARD, forced-shuffle Color Block Jam levels (stages 16-30) with VARIED
shapes and COLOR MIXING.

Each SEQUENCE DOOR gets a lane of blocks (1x1 and 2-long bars) with the first-to-exit
color FARTHEST from the door: to deliver it you must slide the blockers ASIDE (a real
shuffle), then feed the door its colors in order. Many doors need a MIX color, so a
primary block must cross a paint cell on its way out.

A general polyomino DFS solver (mirrors the C# Board: slide-to-stop + fit-through-door +
paint + sequence doors) proves each level solvable. Stages 1-15 are untouched.
"""
import json, os, random

R, B, Y, G, P, O, PINK, TEAL, LIME, BROWN = range(10)
SEC = {G, P, O}
TOP, BOT, LEFT, RIGHT = 0, 1, 2, 3
EXITDIR = {RIGHT: (1, 0), LEFT: (-1, 0), BOT: (0, 1), TOP: (0, -1)}
DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
# MUST match C# ColorMix.TryMix exactly (primary+primary, primary+secondary, sec+sec=Brown)
_MIX = {frozenset((R, B)): P, frozenset((B, Y)): G, frozenset((R, Y)): O,
        frozenset((R, P)): PINK, frozenset((B, G)): TEAL, frozenset((Y, G)): LIME}


def trymix(a, b):
    if a == b:
        return None
    s = frozenset((a, b))
    if s in _MIX:
        return _MIX[s]
    if a in SEC and b in SEC:
        return BROWN
    return None


def door_edge(dx, dy):
    if dx > 0: return RIGHT
    if dx < 0: return LEFT
    return BOT if dy > 0 else TOP


def cells_at(shape, x, y):
    return [(x + cx, y + cy) for (cx, cy) in shape]


def leading(shape, x, y, dx, dy):
    own = set(cells_at(shape, x, y))
    return [(a + dx, b + dy) for (a, b) in own if (a + dx, b + dy) not in own]


# ---------------- general polyomino DFS solver ----------------
def solve(cand, cap=150000):
    W, H = cand['W'], cand['H']
    walls = frozenset((w[0], w[1]) for w in cand['walls'])
    paint = {(k[0], k[1]): v for k, v in cand['paint'].items()}
    doors = cand['doors']
    shapes = {b['id']: tuple(map(tuple, b['shape'])) for b in cand['placed']}

    def door_of(edge, color, lane, didx):
        for k, d in enumerate(doors):
            if d['edge'] == edge and didx[k] < len(d['seq']) and d['seq'][didx[k]] == color \
               and d['laneStart'] <= lane <= d['laneStart'] + d['length'] - 1:
                return k
        return -1

    def can_exit(shape, x, y, color, dx, dy, didx, base):
        edge = door_edge(dx, dy)
        sx, sy = x, y
        used = -1
        guard = W + H + 4
        while guard > 0:
            guard -= 1
            if all(not (0 <= cx < W and 0 <= cy < H) for (cx, cy) in cells_at(shape, sx, sy)):
                return used
            for (lx, ly) in leading(shape, sx, sy, dx, dy):
                if 0 <= lx < W and 0 <= ly < H:
                    if (lx, ly) in base:
                        return -1
                else:
                    lane = ly if dx != 0 else lx
                    di = door_of(edge, color, lane, didx)
                    if di < 0:
                        return -1
                    used = di
            sx += dx; sy += dy
        return -1

    def neighbors(state):
        blocks, didx = state
        occ = set(walls)
        for (bid, x, y, col) in blocks:
            occ.update(cells_at(shapes[bid], x, y))
        out = []
        for (bid, x, y, col) in blocks:
            shape = shapes[bid]
            base = occ - set(cells_at(shape, x, y))
            for (dx, dy) in DIRS:
                cx, cy, ccol = x, y, col
                while True:
                    lead = leading(shape, cx, cy, dx, dy)
                    offb = any(not (0 <= lx < W and 0 <= ly < H) for (lx, ly) in lead)
                    blk = any((0 <= lx < W and 0 <= ly < H) and (lx, ly) in base for (lx, ly) in lead)
                    if blk:
                        break
                    if offb:
                        di = can_exit(shape, cx, cy, ccol, dx, dy, didx, base)
                        if di >= 0:
                            nb = tuple(t for t in blocks if t[0] != bid)
                            nd = list(didx); nd[di] += 1
                            out.append(((nb, tuple(nd)), True))
                        break
                    cx += dx; cy += dy
                    for c in cells_at(shape, cx, cy):
                        if c in paint:
                            m = trymix(ccol, paint[c])
                            if m is not None:
                                ccol = m
                    nb = tuple(sorted((t if t[0] != bid else (bid, cx, cy, ccol)) for t in blocks))
                    out.append(((nb, didx), False))
        return out

    start = (tuple(sorted((b['id'], b['x'], b['y'], b['start_color']) for b in cand['placed'])),
             tuple([0] * len(doors)))
    seen = {start}
    stack = [(start, 0)]
    while stack:
        state, depth = stack.pop()
        if not state[0]:
            return depth
        if len(seen) > cap:
            return None
        mv = neighbors(state)
        mv.sort(key=lambda m: m[1])  # exit moves tried first
        for ns, _ in mv:
            if ns not in seen:
                seen.add(ns); stack.append((ns, depth + 1))
    return None


# ---------------- generation ----------------
def lane_line(edge, lane, W, H):
    """Cells along a lane, ordered from the door inward."""
    if edge == RIGHT: return [(W - 1 - j, lane) for j in range(W)]
    if edge == LEFT:  return [(j, lane) for j in range(W)]
    if edge == BOT:   return [(lane, H - 1 - j) for j in range(H)]
    return [(lane, j) for j in range(H)]


def build_hard(i, rng):
    W = H = 6
    ndoors = min(4, 3 + (i - 16) // 5)
    palette = [R, B, Y, G, P, O]
    doors = []; placed = []; occ = set(); paint = {}; bid = 1
    used = set()

    tries = 0
    while len(doors) < ndoors and tries < 80:
        tries += 1
        edge = rng.choice([LEFT, RIGHT, TOP, BOT])
        span = H if edge in (LEFT, RIGHT) else W
        lane = rng.randrange(span)
        if (edge, lane) in used:
            continue
        line = lane_line(edge, lane, W, H)
        dx, dy = EXITDIR[edge]
        along = (-dx, -dy)  # inward direction along the lane

        # build a stack of blocks along the lane: each is 1x1 or a 2-long bar (along the lane)
        nblocks = rng.randint(2, 3 + (i - 16) // 6)
        pos = 0  # index into `line` (cells consumed from the door inward)
        stack = []  # (shape_cells_local, anchor_cell)
        ok = True
        for _ in range(nblocks):
            length = rng.choice([1, 1, 1, 2]) if pos + 1 < len(line) - 1 else 1
            need = [line[pos + t] for t in range(length)]
            if pos + length > len(line) - 1 or any(c in occ for c in need):
                ok = False; break
            # normalize the bar's local cells (anchor = min corner)
            xs = [c[0] for c in need]; ys = [c[1] for c in need]
            ax, ay = min(xs), min(ys)
            shape = sorted((c[0] - ax, c[1] - ay) for c in need)
            stack.append((shape, (ax, ay), need))
            pos += length
        if not ok or len(stack) < 2:
            continue

        used.add((edge, lane))
        k = len(stack)
        # door sequence: one color per stacked block, first-to-exit (FARTHEST) = seq[0]
        seq = []
        for _ in range(k):
            seq.append(rng.choice([c for c in palette if not seq or c != seq[-1]] or palette))
        doors.append(dict(edge=edge, laneStart=lane, length=1, seq=seq))
        for m, (shape, anchor, need) in enumerate(stack):
            for c in need:
                occ.add(c)
            color = seq[k - 1 - m]     # nearest to door = last to exit; farthest = seq[0]
            placed.append(dict(id=bid, color=color, start_color=color,
                               shape=[list(c) for c in shape], x=anchor[0], y=anchor[1],
                               edge=edge, axis=0))
            bid += 1

    # VISIBLE color-mixing lanes: a primary block slides through a paint cell sitting on an
    # EMPTY (visible) cell, mixing to a secondary color that matches its door.
    nmix = 0 if i < 17 else min(2, 1 + (i - 17) // 6)
    added = 0; mtries = 0
    while added < nmix and mtries < 80:
        mtries += 1
        edge = rng.choice([LEFT, RIGHT, TOP, BOT])
        span = H if edge in (LEFT, RIGHT) else W
        lane = rng.randrange(span)
        if (edge, lane) in used:
            continue
        line = lane_line(edge, lane, W, H)
        run = 0
        for c in line:
            if c in occ:
                break
            run += 1
        if run < 4:
            continue
        S = rng.choice([P, G, O])
        srcs = [(p, q) for p in (R, B, Y) for q in (R, B, Y) if trymix(p, q) == S]
        if not srcs:
            continue
        p, q = rng.choice(srcs)
        j = rng.randint(3, run - 1)      # block cell index (farther from door)
        pj = rng.randint(1, j - 1)       # paint cell index (empty, visible, between block & door)
        used.add((edge, lane))
        doors.append(dict(edge=edge, laneStart=lane, length=1, seq=[S]))
        bcell = line[j]; occ.add(bcell)
        placed.append(dict(id=bid, color=p, start_color=p, shape=[[0, 0]],
                           x=bcell[0], y=bcell[1], edge=edge, axis=0))
        bid += 1
        paint[line[pj]] = q
        added += 1

    return dict(W=W, H=H, placed=placed, doors=doors, paint=paint, walls=set())


def to_level(i, cand):
    blocks = [dict(id=b['id'], color=b['start_color'], x=b['x'], y=b['y'],
                   w=1, h=1, cells=[dict(x=c[0], y=c[1]) for c in b['shape']], axis=b['axis'])
              for b in cand['placed']]
    nb = len(blocks)
    time = float(min(220, 50 + nb * 12))
    return dict(id=i, name=f"Stage {i}", width=cand['W'], height=cand['H'],
                blocks=blocks,
                doors=[dict(edge=d['edge'], laneStart=d['laneStart'], length=d['length'],
                            color=d['seq'][0], colorSequence=d['seq']) for d in cand['doors']],
                walls=[],
                paint=[dict(x=k[0], y=k[1], color=v) for k, v in sorted(cand['paint'].items())],
                timeLimitSeconds=time, star2SecondsLeft=round(time * 0.30, 1),
                star3SecondsLeft=round(time * 0.50, 1))


if __name__ == "__main__":
    outdir = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
    for i in range(16, 31):
        rng = random.Random(70000 + i * 37)
        target = 6 + (i - 16) // 2
        best = None
        for _ in range(110):
            cand = build_hard(i, rng)
            nb = len(cand['placed'])
            if nb < 5:
                continue
            if solve(cand, cap=60000) is None:
                continue  # not solvable within the (fast) cap -> skip
            paint_n = len(cand['paint'])
            score = nb + paint_n * 3
            if best is None or score > best[1]:
                best = (cand, score, nb, paint_n)
            if nb >= target and paint_n >= 1:
                break
        if best is None:
            print(f"level_{i:03d}  FAILED", flush=True); continue
        cand = best[0]
        bars = sum(1 for b in cand['placed'] if len(b['shape']) > 1)
        lvl = to_level(i, cand)
        json.dump(lvl, open(os.path.join(outdir, f"level_{i:03d}.json"), "w"), indent=2)
        print(f"level_{i:03d}  blocks={len(lvl['blocks']):2d}  bars={bars}  doors={len(lvl['doors'])}  "
              f"paint={len(lvl['paint'])}  t={int(lvl['timeLimitSeconds'])}s", flush=True)
    print("\nDone (stages 16-30).", flush=True)
