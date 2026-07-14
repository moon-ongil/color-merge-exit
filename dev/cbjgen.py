"""Generate 'Color Block Jam'-style levels: colored polyomino blocks that slide out
through matching-color edge DOORS (must fit), clearing the whole board, with the
signature COLOR-MIXING twist (paint cells: R+B=Purple, R+Purple=Pink, ...).

Levels are built by REVERSE CONSTRUCTION: each block is placed flush at its door, a
door is opened for it, then it is slid BACKWARD into the board to its start position.
Replaying in reverse placement order (each block slides straight out its door) is a
guaranteed solution, so every emitted level is solvable. `verify()` replays it to be
sure. Difficulty scales via block count, shapes, colors, mixing and walls.

Emits the new LevelData schema (blocks with cells[]/color, doors, walls, paint).
"""
import json, os, random

R, B, Y, G, P, O, PINK, TEAL, LIME, BROWN = range(10)
PRIM = [R, B, Y]
SEC = [G, P, O]
TOP, BOT, LEFT, RIGHT = 0, 1, 2, 3
EXITDIR = {RIGHT: (1, 0), LEFT: (-1, 0), BOT: (0, 1), TOP: (0, -1)}

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


def mix_sources(target):
    """(primary, paint) pairs whose mix == target."""
    out = []
    for p in PRIM:
        for q in range(10):
            if q != p and trymix(p, q) == target:
                out.append((p, q))
    return out


SHAPES = {
    'o': [(0, 0)],
    'i2h': [(0, 0), (1, 0)], 'i2v': [(0, 0), (0, 1)],
    'i3h': [(0, 0), (1, 0), (2, 0)], 'i3v': [(0, 0), (0, 1), (0, 2)],
    'sq': [(0, 0), (1, 0), (0, 1), (1, 1)],
    'L': [(0, 0), (0, 1), (1, 1)], 'J': [(1, 0), (0, 1), (1, 1)],
    'T': [(0, 0), (1, 0), (2, 0), (1, 1)], 'S': [(1, 0), (2, 0), (0, 1), (1, 1)],
}


def norm(cells):
    mnx = min(c[0] for c in cells); mny = min(c[1] for c in cells)
    return sorted((x - mnx, y - mny) for (x, y) in cells)


def cells_at(shape, x, y):
    return [(x + cx, y + cy) for (cx, cy) in shape]


def bbox(shape):
    return max(c[0] for c in shape) + 1, max(c[1] for c in shape) + 1


MAXSEQ = 7  # max colors a single door cycles through
MAXDOORW = 2  # max door width (keeps doors small; lets same-color blocks share)


def door_conflict(doors, edge, start, length, exclude=None):
    """Distinct doors on the same edge must not share lanes (color-agnostic now)."""
    lanes = set(range(start, start + length))
    for d in doors:
        if d is exclude or d['edge'] != edge:
            continue
        if lanes & set(range(d['laneStart'], d['laneStart'] + d['length'])):
            return True
    return False


def tier_for(i):
    E = [LEFT, RIGHT, TOP, BOT]
    if i <= 4:
        return dict(size=5, nblocks=2 + i // 3, shapes=['o', 'i2h', 'i2v'],
                    colors=[R, B, Y], edges=E, maxback=3, mix=False, mixprob=0, walls=0, lock=0.0, reuse=0.7)
    if i <= 8:
        return dict(size=5, nblocks=3 + (i - 5) // 2, shapes=['o', 'i2h', 'i2v', 'i3h', 'sq', 'L'],
                    colors=[R, B, Y, G], edges=E, maxback=4, mix=False, mixprob=0, walls=0, lock=0.2, reuse=0.7)
    if i <= 12:
        return dict(size=6, nblocks=4 + (i - 9) // 2, shapes=['o', 'i2h', 'i2v', 'i3h', 'i3v', 'sq', 'L', 'J', 'T'],
                    colors=[R, B, Y, G, P, O], edges=E, maxback=4, mix=(i >= 10), mixprob=0.4, walls=0, lock=0.3, reuse=0.7)
    # HARD, DENSE, PACKED tiers (16-30): a full board of interlocking polyominoes + walls
    # (obstacles/bottlenecks) + sequence doors + mixing + rails. Reverse-constructed so it
    # stays solvable, but packed so mis-moves jam you (the CBJ tension).
    ALL = ['o', 'i2h', 'i2v', 'i3h', 'i3v', 'sq', 'L', 'J', 'T', 'S', 'L', 'J', 'T', 'S']  # weight polyominoes
    if i <= 20:
        return dict(size=6, nblocks=11, maxdoors=3, shapes=ALL, colors=[R, B, Y, G, P, O],
                    edges=E, maxback=6, mix=True, mixprob=0.8, walls=5, lock=0.3, reuse=0.9)
    if i <= 25:
        return dict(size=6, nblocks=12, maxdoors=3, shapes=ALL, colors=[R, B, Y, G, P, O],
                    edges=E, maxback=6, mix=True, mixprob=0.85, walls=6, lock=0.35, reuse=0.9)
    return dict(size=6, nblocks=13, maxdoors=4, shapes=ALL, colors=[R, B, Y, G, P, O],
                edges=E, maxback=6, mix=True, mixprob=0.9, walls=6, lock=0.4, reuse=0.9)


def build(i, rng, t):
    W = H = t['size']
    occ = set(); reserved = set(); walls = set()
    for _ in range(t['walls']):
        for _try in range(20):
            wc = (rng.randrange(W), rng.randrange(H))
            if wc not in occ:
                walls.add(wc); occ.add(wc); break

    doors = []; paint = {}; placed = []
    bid = 1; attempts = 0
    while len(placed) < t['nblocks'] and attempts < 6000:
        attempts += 1
        shape = norm(SHAPES[rng.choice(t['shapes'])])
        w, h = bbox(shape)
        if w > W or h > H:
            continue

        # Prefer REUSING an existing door: the block passes through it and the door cycles
        # to this block's color next (so a few doors serve many blocks). Else make a new one.
        reusable = [d for d in doors if len(d['seq']) < MAXSEQ]
        # Cap the number of doors: once at the cap, blocks MUST funnel through existing
        # doors (few doors + many blocks = congestion = you must plan the order).
        if len(doors) >= t.get('maxdoors', 99):
            if not reusable:
                continue
            reuse = rng.choice(reusable)
        else:
            reuse = rng.choice(reusable) if (reusable and rng.random() < t['reuse']) else None
        color = rng.choice(t['colors'])
        edge = reuse['edge'] if reuse is not None else rng.choice(t['edges'])
        dx, dy = EXITDIR[edge]

        # flush against the edge (free lane position)
        if edge in (LEFT, RIGHT):
            ax = (W - w) if edge == RIGHT else 0
            choices = list(range(0, H - h + 1))
        else:
            ay = (H - h) if edge == BOT else 0
            choices = list(range(0, W - w + 1))
        if not choices:
            continue
        if edge in (LEFT, RIGHT):
            ay = rng.choice(choices)
        else:
            ax = rng.choice(choices)

        exitcells = cells_at(shape, ax, ay)
        if any(c in occ for c in exitcells):
            continue
        if edge in (LEFT, RIGHT):
            lanes = sorted(set(cy for (cx, cy) in exitcells))
        else:
            lanes = sorted(set(cx for (cx, cy) in exitcells))
        if lanes != list(range(lanes[0], lanes[-1] + 1)):
            continue  # non-contiguous cross-section can't use one door
        laneStart, length = lanes[0], lanes[-1] - lanes[0] + 1

        if reuse is not None:
            # a door may GROW to cover this block too, but only up to MAXDOORW wide
            lo = min(reuse['laneStart'], laneStart)
            hi = max(reuse['laneStart'] + reuse['length'] - 1, laneStart + length - 1)
            if hi - lo + 1 > MAXDOORW or door_conflict(doors, edge, lo, hi - lo + 1, exclude=reuse):
                continue
        elif door_conflict(doors, edge, laneStart, length):
            continue

        # slide backward into the board
        positions = [(ax, ay)]; cur = (ax, ay)
        for _ in range(rng.randint(1, t['maxback'])):
            nx, ny = cur[0] - dx, cur[1] - dy
            ncells = cells_at(shape, nx, ny)
            if any(not (0 <= cx < W and 0 <= cy < H) for (cx, cy) in ncells):
                break
            if any((cx, cy) in occ or (cx, cy) in reserved for (cx, cy) in ncells):
                break
            cur = (nx, ny); positions.append(cur)
        sx, sy = cur

        # axis-locked "rail" block: only slides along its exit axis (still exits fine)
        axis = 0
        if rng.random() < t['lock']:
            axis = 1 if edge in (LEFT, RIGHT) else 2

        start_color = color
        if t['mix'] and color not in PRIM and len(positions) >= 2 and rng.random() < t['mixprob']:
            srcs = mix_sources(color)
            if srcs:
                p, q = rng.choice(srcs)
                paintpos = positions[rng.randint(1, len(positions) - 1)]
                cand = [c for c in cells_at(shape, paintpos[0], paintpos[1])
                        if c not in occ and c not in reserved and c not in walls]
                if cand:
                    pc = rng.choice(cand)
                    paint[pc] = q; reserved.add(pc); start_color = p

        for c in cells_at(shape, sx, sy):
            occ.add(c)
        if reuse is not None:
            # this block exits BEFORE the ones already assigned (reverse order) -> prepend.
            reuse['laneStart'] = lo; reuse['length'] = hi - lo + 1
            reuse['seq'].insert(0, color)
        else:
            doors.append(dict(edge=edge, laneStart=laneStart, length=length, seq=[color]))
        placed.append(dict(id=bid, color=color, start_color=start_color, shape=shape,
                           x=sx, y=sy, edge=edge, axis=axis))
        bid += 1

    return dict(W=W, H=H, placed=placed, doors=doors, paint=paint, walls=walls)


def verify(cand):
    """Replay in reverse placement order: each block slides straight out its door.
    Doors cycle through their color sequence as blocks pass."""
    W, H = cand['W'], cand['H']
    walls = cand['walls']; paint = cand['paint']; doors = cand['doors']
    didx = [0] * len(doors)
    pos = {b['id']: (b['x'], b['y'], b['start_color']) for b in cand['placed']}
    shp = {b['id']: b['shape'] for b in cand['placed']}
    edge = {b['id']: b['edge'] for b in cand['placed']}

    def occ_except(mid):
        s = set(walls)
        for k, (x, y, _c) in pos.items():
            if k != mid:
                s.update(cells_at(shp[k], x, y))
        return s

    def cur(di):
        d = doors[di]
        return d['seq'][didx[di]] if didx[di] < len(d['seq']) else None

    def covers(di, lane):
        d = doors[di]
        return d['laneStart'] <= lane <= d['laneStart'] + d['length'] - 1

    def door_open(e, color, lane):
        return any(doors[di]['edge'] == e and cur(di) == color and covers(di, lane) for di in range(len(doors)))

    def used_door(e, color, lanes):
        for di in range(len(doors)):
            if doors[di]['edge'] == e and cur(di) == color and all(covers(di, l) for l in lanes):
                return di
        return None

    for b in reversed(cand['placed']):
        mid = b['id']; dx, dy = EXITDIR[edge[mid]]
        x, y, color = pos[mid]; shape = shp[mid]; other = occ_except(mid)
        exited = False
        guard = W + H + 4
        while guard > 0:
            guard -= 1
            occset = set(cells_at(shape, x, y))
            lead = [(cx + dx, cy + dy) for (cx, cy) in occset if (cx + dx, cy + dy) not in occset]
            offb = any(not (0 <= lx < W and 0 <= ly < H) for (lx, ly) in lead)
            blocked = any((0 <= lx < W and 0 <= ly < H) and (lx, ly) in other for (lx, ly) in lead)
            if blocked:
                break
            if offb:
                sx, sy, scol = x, y, color
                ok = True; g2 = W + H + 4
                while g2 > 0:
                    g2 -= 1
                    cs = cells_at(shape, sx, sy)
                    if all(not (0 <= cx < W and 0 <= cy < H) for (cx, cy) in cs):
                        break
                    ld = [(cx + dx, cy + dy) for (cx, cy) in cs if (cx + dx, cy + dy) not in set(cs)]
                    for (lx, ly) in ld:
                        if 0 <= lx < W and 0 <= ly < H:
                            if (lx, ly) in other:
                                ok = False; break
                        else:
                            lane = ly if dx != 0 else lx
                            if not door_open(edge[mid], scol, lane):
                                ok = False; break
                    if not ok:
                        break
                    sx += dx; sy += dy
                if ok:
                    lanes = set(ly if dx != 0 else lx for (lx, ly) in cells_at(shape, x, y))
                    di = used_door(edge[mid], color, lanes)
                    if di is None:
                        ok = False
                    else:
                        didx[di] += 1
                exited = ok
                break
            x += dx; y += dy
            for c in cells_at(shape, x, y):
                if c in paint:
                    m = trymix(color, paint[c])
                    if m is not None:
                        color = m
        if not exited:
            return False
        del pos[mid]
    return True


def to_level(i, cand, t):
    blocks = []
    for b in cand['placed']:
        w, h = bbox(b['shape'])
        blocks.append(dict(id=b['id'], color=b['start_color'], x=b['x'], y=b['y'],
                           w=w, h=h, cells=[dict(x=cx, y=cy) for (cx, cy) in b['shape']], axis=b.get('axis', 0)))
    nb = len(cand['placed'])
    nmix = len(cand['paint'])
    time = float(min(180, 45 + nb * 12 + nmix * 8))
    return dict(id=i, name=f"Stage {i}", width=cand['W'], height=cand['H'],
                blocks=blocks,
                doors=[dict(edge=d['edge'], laneStart=d['laneStart'], length=d['length'],
                            color=d['seq'][0], colorSequence=d['seq']) for d in cand['doors']],
                walls=[dict(x=x, y=y) for (x, y) in sorted(cand['walls'])],
                paint=[dict(x=x, y=y, color=c) for ((x, y), c) in sorted(cand['paint'].items())],
                timeLimitSeconds=time, star2SecondsLeft=round(time * 0.30, 1), star3SecondsLeft=round(time * 0.50, 1))


if __name__ == "__main__":
    outdir = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
    os.makedirs(outdir, exist_ok=True)
    made = 0
    for i in range(16, 31):  # regenerate only the hard levels; 1-15 stay as-is
        t = tier_for(i)
        rng = random.Random(9000 + i * 17)
        best = None; best_n = 0
        for _ in range(700):
            cand = build(i, rng, t)
            n = len(cand['placed'])
            if n < 8:            # need a reasonably full board
                continue
            if n > best_n and verify(cand):
                best = cand; best_n = n
            if best_n >= t['nblocks']:
                break
        if best is None:
            print(f"level_{i:03d}  FAILED", flush=True); continue
        lvl = to_level(i, best, t)
        json.dump(lvl, open(os.path.join(outdir, f"level_{i:03d}.json"), "w"), indent=2)
        made += 1
        print(f"level_{i:03d}  blocks={len(lvl['blocks']):2d}  doors={len(lvl['doors'])}  "
              f"paint={len(lvl['paint'])}  walls={len(lvl['walls'])}  size={lvl['width']}  t={int(lvl['timeLimitSeconds'])}s",
              flush=True)
    print(f"\nGenerated {made}/30 Color Block Jam levels.", flush=True)
