"""Generate MERGE-centric levels (16-30): VARIED SHAPES (same-shape pairs), crossing lanes,
locked blocks — clean colors only.

Core: push two SAME-SHAPE primary blocks together -> merge into the SECONDARY the door needs
(R+B=Purple, B+Y=Green, R+Y=Orange). Blocks = R/B/Y, doors = P/G/O. Only identical shapes
merge (different shapes obstruct). A door is sized to the pair's cross-section. A GENERAL
polyomino forward DFS solver (slide / fit-through-door exit / same-shape merge / adjacent
unlock, mirrors the C# Board) proves every level solvable.
"""
import json, os, random

R, B, Y, G, P, O, PINK, TEAL, LIME, BROWN, CORAL, INDIGO, AMBER = range(13)
TOP, BOT, LEFT, RIGHT = 0, 1, 2, 3
EXITDIR = {RIGHT: (1, 0), LEFT: (-1, 0), BOT: (0, 1), TOP: (0, -1)}
DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
PAIR = {P: (R, B), G: (B, Y), O: (R, Y)}
SECON = (G, P, O)
# Must mirror C# ColorMix.TryMix exactly. Only NICE, analogous tertiaries are makeable; muddy
# complementary combos (R+G, B+O, Y+P) and Teal (B+G) block instead of merging.
_MIX = {frozenset((R, B)): P, frozenset((B, Y)): G, frozenset((R, Y)): O,        # prim+prim -> secondary
        frozenset((R, P)): PINK, frozenset((B, P)): INDIGO,                      # from Purple
        frozenset((R, O)): CORAL, frozenset((Y, O)): AMBER,                      # from Orange
        frozenset((Y, G)): LIME}                                                 # from Green
# Chain recipes (2-merge tertiaries): secondary-from-two-primaries, then add the third primary.
CHAINS = {PINK: ((R, B), R), INDIGO: ((R, B), B),    # Purple + R / B
          CORAL: ((R, Y), R), AMBER: ((R, Y), Y),    # Orange + R / Y
          LIME: ((B, Y), Y)}                          # Green  + Y
# Reverse of the mix (must mirror C# ColorMix.TrySplit): a mixed block that slides onto a
# splitter breaks into these two colors — the mover keeps the FIRST, a twin takes the SECOND.
TRYSPLIT = {P: (R, B), G: (B, Y), O: (R, Y),
            PINK: (R, P), INDIGO: (B, P), CORAL: (R, O), AMBER: (Y, O), LIME: (Y, G)}

SHAPES = {
    'o': [(0, 0)],
    'h2': [(0, 0), (1, 0)], 'v2': [(0, 0), (0, 1)],
    'h3': [(0, 0), (1, 0), (2, 0)], 'v3': [(0, 0), (0, 1), (0, 2)],
    'sq': [(0, 0), (1, 0), (0, 1), (1, 1)],
    'L': [(0, 0), (0, 1), (1, 1)], 'J': [(1, 0), (0, 1), (1, 1)],
}


def trymix(a, b):
    if a == b:
        return None
    return _MIX.get(frozenset((a, b)))


def cells_at(shape, x, y):
    return [(x + cx, y + cy) for (cx, cy) in shape]


def bbox(shape):
    return max(c[0] for c in shape) + 1, max(c[1] for c in shape) + 1


def extent(shape, axis):
    vals = [c[axis] for c in shape]
    return max(vals) - min(vals) + 1


def norm(cells):
    mnx = min(c[0] for c in cells); mny = min(c[1] for c in cells)
    return frozenset((x - mnx, y - mny) for (x, y) in cells)


def leading(cells_set, dx, dy):
    return [(a + dx, b + dy) for (a, b) in cells_set if (a + dx, b + dy) not in cells_set]


def door_edge(dx, dy):
    if dx > 0: return RIGHT
    if dx < 0: return LEFT
    return BOT if dy > 0 else TOP


def door_overlaps(doors, edge, laneStart, length):
    """True if a new door on `edge` spanning [laneStart, laneStart+length) would share a lane
    with an existing door. Two doors at the SAME edge+lane render on top of each other — once
    one is spent (grey) it hides the live one, so a block seems to exit a 'wrong/no' door."""
    end = laneStart + length - 1
    for d in doors:
        if d['edge'] == edge and not (end < d['laneStart'] or d['laneStart'] + d['length'] - 1 < laneStart):
            return True
    return False


# ---------------- general forward solver ----------------
def solve(cand, cap=200000):
    W, H = cand['W'], cand['H']
    walls = frozenset((w[0], w[1]) for w in cand['walls'])
    splitters = frozenset((s[0], s[1]) for s in cand.get('splitters', []))
    doors = cand['doors']
    shp = {b['id']: tuple(map(tuple, b['shape'])) for b in cand['placed']}
    sig = {bid: norm(s) for bid, s in shp.items()}
    axis = {b['id']: b.get('axis', 0) for b in cand['placed']}  # 0 free, 1 horiz, 2 vert

    def door_of(edge, color, lane, didx):
        for k, d in enumerate(doors):
            if d['edge'] == edge and didx[k] < len(d['seq']) and d['seq'][didx[k]] == color \
               and d['laneStart'] <= lane <= d['laneStart'] + d['length'] - 1:
                return k
        return -1

    def can_exit(bid, x, y, color, dx, dy, didx, occ_other):
        edge = door_edge(dx, dy); sx, sy = x, y; used = -1; guard = W + H + 4
        while guard > 0:
            guard -= 1
            cs = set(cells_at(shp[bid], sx, sy))
            if all(not (0 <= cx < W and 0 <= cy < H) for (cx, cy) in cs):
                return used
            for (lx, ly) in leading(cs, dx, dy):
                if 0 <= lx < W and 0 <= ly < H:
                    if (lx, ly) in occ_other or (lx, ly) in walls:
                        return -1
                else:
                    di = door_of(edge, color, ly if dx != 0 else lx, didx)
                    if di < 0:
                        return -1
                    used = di
            sx += dx; sy += dy
        return -1

    def neighbors(state):
        blocks, didx = state
        cellmap = {}
        for (bid, x, y, col, lk) in blocks:
            for c in cells_at(shp[bid], x, y):
                cellmap[c] = bid
        info = {bid: (x, y, col, lk) for (bid, x, y, col, lk) in blocks}
        out = []
        for (bid, x, y, col, lk) in blocks:
            if lk:
                continue
            occ_other = set(c for c, o in cellmap.items() if o != bid)
            for (dx, dy) in DIRS:
                if axis[bid] == 1 and dy != 0:
                    continue   # horizontal-locked rail
                if axis[bid] == 2 and dx != 0:
                    continue   # vertical-locked rail
                cx, cy = x, y
                while True:
                    cur = set(cells_at(shp[bid], cx, cy))
                    lead = leading(cur, dx, dy)
                    offb = any(not (0 <= lx < W and 0 <= ly < H) for (lx, ly) in lead)
                    hitw = any((0 <= lx < W and 0 <= ly < H) and (lx, ly) in walls for (lx, ly) in lead)
                    blockers = set(cellmap[(lx, ly)] for (lx, ly) in lead
                                   if (0 <= lx < W and 0 <= ly < H) and (lx, ly) in cellmap and cellmap[(lx, ly)] != bid)
                    if hitw or blockers:
                        if (not hitw) and (not offb) and len(blockers) == 1:
                            oid = next(iter(blockers))
                            _ox, _oy, ocol, _olk = info[oid]
                            m = trymix(col, ocol)
                            # a locked block can't MOVE (skipped as mover) but CAN be merged into
                            if sig[bid] == sig[oid] and m is not None:
                                merged = (bid, cx, cy, m, False)
                                rest = [t for t in blocks if t[0] != bid and t[0] != oid]
                                nb = tuple(sorted(rest + [merged]))
                                out.append(((nb, didx), False))
                        break
                    if offb:
                        di = can_exit(bid, cx, cy, col, dx, dy, didx, occ_other)
                        if di >= 0:
                            nb = tuple(t for t in blocks if t[0] != bid)
                            nd = list(didx); nd[di] += 1
                            out.append(((nb, tuple(nd)), True))
                        break
                    cx, cy = cx + dx, cy + dy
                    # sliding onto a splitter breaks a mixed block into its two components —
                    # terminal (the block can't continue past). Twins are 1x1 (generator only
                    # splits 1x1 blocks), so a fresh id can safely reuse shp/sig as 1x1.
                    if splitters:
                        newfoot = set(cells_at(shp[bid], cx, cy))
                        comp = TRYSPLIT.get(col) if any(c in splitters for c in newfoot) else None
                        if comp is not None:
                            a, b = comp; px, py = cx - dx, cy - dy
                            twin = cells_at(shp[bid], px, py)
                            ok = True
                            for t in twin:
                                if t in newfoot or not (0 <= t[0] < W and 0 <= t[1] < H) \
                                   or t in walls or (t in cellmap and cellmap[t] != bid):
                                    ok = False; break
                            if ok:
                                newid = max(t[0] for t in blocks) + 1
                                shp[newid] = shp[bid]; sig[newid] = sig[bid]; axis[newid] = 0
                                rest = [t for t in blocks if t[0] != bid]
                                nb = tuple(sorted(rest + [(bid, cx, cy, a, False), (newid, px, py, b, False)]))
                                out.append(((nb, didx), False))
                                break
                    moved = (bid, cx, cy, col, lk)
                    nb = tuple(sorted([t for t in blocks if t[0] != bid] + [moved]))
                    out.append(((nb, didx), False))
        return out

    start = (tuple(sorted((b['id'], b['x'], b['y'], b['color'], b['locked']) for b in cand['placed'])),
             tuple([0] * len(doors)))
    seen = {start}; stack = [start]
    while stack:
        state = stack.pop()
        if not state[0]:
            return True
        if len(seen) > cap:
            return None
        mv = neighbors(state)
        mv.sort(key=lambda m: m[1])
        for ns, _ in mv:
            if ns not in seen:
                seen.add(ns); stack.append(ns)
    return None


# ---------------- generation ----------------
def line_cells(edge, lane, W, H):
    """Cells of a full row/column, index 0 = the door edge, growing inward."""
    if edge == RIGHT: return [(W - 1 - k, lane) for k in range(W)]
    if edge == LEFT:  return [(k, lane) for k in range(W)]
    if edge == BOT:   return [(lane, H - 1 - k) for k in range(H)]
    return [(lane, k) for k in range(H)]  # TOP


# One SPLIT lane: reserve a full row/col, put a SECONDARY block at idx2, a splitter at idx3,
# and a PRIMARY door at each end (mover-component exits forward, twin exits backward). Returns
# (new_bid, placed_ok). Used to build dedicated, spacious split-only levels (no merge blocks).
def try_split_lane(rng, W, H, occ, mergeclear, wallclear, colorat, doors, placed, splitters, bid):
    if rng.random() < 0.5:
        lane = rng.randrange(H); line = [(x, lane) for x in range(W)]; fEdge, bEdge = RIGHT, LEFT
    else:
        lane = rng.randrange(W); line = [(lane, y) for y in range(H)]; fEdge, bEdge = BOT, TOP
    if len(line) < 6 or any(c in occ or c in mergeclear or c in wallclear for c in line):
        return bid, False
    if door_overlaps(doors, fEdge, lane, 1) or door_overlaps(doors, bEdge, lane, 1):
        return bid, False
    Spos, splpos = line[2], line[3]
    S = rng.choice([P, G, O]); a, b = TRYSPLIT[S]   # mover keeps a (exits forward), twin b (backward)
    occ.add(Spos)
    for c in line:
        if c != Spos:
            mergeclear.add(c)                        # splitter + both exit paths: no blocks/walls
    splitters.add(splpos)
    doors.append(dict(edge=fEdge, laneStart=lane, length=1, seq=[a]))
    doors.append(dict(edge=bEdge, laneStart=lane, length=1, seq=[b]))
    placed.append(dict(id=bid, color=S, x=Spos[0], y=Spos[1], shape=[[0, 0]], locked=False, axis=0))
    colorat[Spos] = S
    return bid + 1, True


# Dedicated SPLIT level: only secondary blocks + splitters + primary doors, spread out, NO merge
# blocks — so the split is the whole puzzle and can't be confused with a merge survivor.
def build_split_level(rng, spec):
    W = H = spec.get('size', 6)
    nsplit = spec['nsplit']; nwalls_t = spec.get('nwalls', 0)
    occ = set(); mergeclear = set(); wallclear = set(); colorat = {}
    doors = []; placed = []; splitters = set(); bid = 1
    nsp = 0; ct = 0
    while nsp < nsplit and ct < 900:
        ct += 1
        bid, ok = try_split_lane(rng, W, H, occ, mergeclear, wallclear, colorat, doors, placed, splitters, bid)
        if ok:
            nsp += 1
    if nsp < nsplit:
        return None
    blocked = occ | mergeclear | wallclear
    empties = [(x, y) for x in range(W) for y in range(H) if (x, y) not in blocked]
    rng.shuffle(empties)
    walls = set(empties[:min(len(empties), nwalls_t)])
    return dict(W=W, H=H, placed=placed, doors=doors, walls=walls, splitters=splitters)


# One SPLIT-THEN-REMERGE lane (the hard mechanic): a SECONDARY block splits on a splitter into its
# two primaries, and EACH primary must then merge with the THIRD primary to make the secondary a
# door needs. Row layout: [exit-a][partner][splitter][SECONDARY][partner][exit-b]. e.g. Purple ->
# Red+Blue; Red+Yellow=Orange (exits one end), Blue+Yellow=Green (exits the other). You can't just
# split-and-exit — you must split, then re-merge each half with a different color.
def try_split_remerge_lane(rng, W, H, occ, mergeclear, wallclear, colorat, doors, placed, splitters, bid):
    if rng.random() < 0.5:
        lane = rng.randrange(H); line = [(x, lane) for x in range(W)]; fEdge, bEdge = LEFT, RIGHT
    else:
        lane = rng.randrange(W); line = [(lane, y) for y in range(H)]; fEdge, bEdge = TOP, BOT
    if len(line) < 6 or any(c in occ or c in mergeclear or c in wallclear for c in line):
        return bid, False
    S = rng.choice([P, G, O]); a, b = TRYSPLIT[S]           # mover keeps a, twin gets b
    third = list({R, B, Y} - {a, b})[0]                    # the remaining primary both halves need
    doorA, doorB = trymix(a, third), trymix(b, third)      # secondaries the two re-merges produce
    if doorA is None or doorB is None:
        return bid, False
    if door_overlaps(doors, fEdge, lane, 1) or door_overlaps(doors, bEdge, lane, 1):
        return bid, False
    exitA, partA, spl, sc, partB, exitB = line[0], line[1], line[2], line[3], line[4], line[5]
    for c in (partA, sc, partB):
        occ.add(c)
    for c in (exitA, spl, exitB):
        mergeclear.add(c)                                  # splitter + both exit paths: no blocks/walls
    splitters.add(spl)
    doors.append(dict(edge=fEdge, laneStart=lane, length=1, seq=[doorA]))  # mover-half (a) side
    doors.append(dict(edge=bEdge, laneStart=lane, length=1, seq=[doorB]))  # twin-half (b) side
    for (cx, cy), col in ((partA, third), (sc, S), (partB, third)):
        placed.append(dict(id=bid, color=col, x=cx, y=cy, shape=[[0, 0]], locked=False, axis=0)); bid += 1
        colorat[(cx, cy)] = col
    return bid, True


# Dedicated SPLIT-REMERGE level (hardest mechanic): only split-then-remerge units, spread out.
def build_split_remerge_level(rng, spec):
    W = H = spec.get('size', 6)
    nunits = spec['nunits']; nwalls_t = spec.get('nwalls', 0)
    occ = set(); mergeclear = set(); wallclear = set(); colorat = {}
    doors = []; placed = []; splitters = set(); bid = 1
    got = 0; ct = 0
    while got < nunits and ct < 1500:
        ct += 1
        bid, ok = try_split_remerge_lane(rng, W, H, occ, mergeclear, wallclear, colorat, doors, placed, splitters, bid)
        if ok:
            got += 1
    if got < nunits:
        return None
    blocked = occ | mergeclear | wallclear
    empties = [(x, y) for x in range(W) for y in range(H) if (x, y) not in blocked]
    rng.shuffle(empties)
    walls = set(empties[:min(len(empties), nwalls_t)])
    return dict(W=W, H=H, placed=placed, doors=doors, walls=walls, splitters=splitters)


def build(i, rng, spec=None):
    W = H = 6
    if spec is not None and spec.get('remerge'):      # dedicated split-then-remerge levels
        return build_split_remerge_level(rng, spec)
    if spec is not None and spec.get('split_only'):   # dedicated split levels
        return build_split_level(rng, spec)
    if spec is not None:      # explicit curriculum (tutorial levels 1-15)
        npairs = spec['npairs']; nlocks = spec.get('nlocks', 0)
        nchain = spec.get('nchain', 0); nwalls_t = spec.get('nwalls', 0)
        perp = spec.get('perp', True); pool = spec.get('pool', ['o']); strict = True
    else:                     # derived difficulty (16-500): smooth ramp — pure MERGE (splits live
                              # in their own dedicated levels, never mixed into dense merge boards)
        t = i - 16                                            # 0-based progression
        # BOARD-SIZE RAMP restores real progression once the 7x7 plateau is exhausted:
        #   6x6 (<24) -> 7x7 (24-159) -> 8x8 (160+). A bigger board lifts the density/count
        #   ceilings so blocks, locks, chains and walls can keep climbing instead of freezing.
        if i >= 160:
            W = H = 8
        elif i >= 24:
            W = H = 7
        if i < 160:                                          # 6x6/7x7 tiers (unchanged: keeps 1-159 stable)
            base = min(7, 3 + t // 9)                        # 3 -> 7 goals
            nchain = 0 if i < 20 else min(3, 1 + (i - 22) // 22)  # 1 -> 3 tertiary chains (Pink/Lime)
            nlocks = 0 if i < 20 else min(4, (i - 20) // 13)     # 0 -> 4 locks
            nwalls_t = min(8, 1 + t // 10)                       # walls grow slowly
        else:                                                # 8x8 tier (160-500): fresh board, MODERATE
            # density so the polyomino DFS stays tractable (dense 8x8 solves explode). The bigger
            # board is mainly for VARIETY (roomier, fits real 2D shapes) + a slow count climb; the
            # difficulty ESCALATION comes from the time/star ramp in to_level, not from packing.
            j = i - 160                                          # 0-based within the 8x8 tier
            base = min(8, 7 + j // 220)                          # 7 -> 8 goals
            nchain = min(3, 2 + j // 250)                        # 2 -> 3 tertiary chains (block-heavy)
            nlocks = min(5, 4 + j // 200)                        # 4 -> 5 locks
            nwalls_t = min(9, 6 + j // 150)                      # 6 -> 9 walls (kept light: ~40% fill)
            if i > 500:                                          # EXTENDED tier 501-1000: keep climbing
                k = i - 500                                      # (1-500 stay byte-identical: guarded)
                nwalls_t = min(12, 9 + k // 100)                 # 9 -> 12 walls (more hurdles / denser maze)
                nlocks   = min(7, 5 + k // 150)                  # 5 -> 7 locks
                nchain   = min(4, 3 + k // 300)                  # 3 -> 4 tertiary chains
        npairs = max(2, base - nchain)                       # chains take goal-slots from pairs
        # richer shape variety as levels climb: bars from 20 (6x6), + real 2D shapes on 7x7+.
        pool = ['o', 'h2', 'v2'] if i < 20 else \
               (['o', 'h2', 'v2', 'h3', 'v3'] if i < 24
                else ['o', 'h2', 'v2', 'h3', 'v3', 'sq', 'L', 'J'])
        perp = True; strict = False

    occ = set()          # cells holding a block (blocks may never overlap)
    mergeclear = set()   # gap/landing cells — must stay clear of BOTH blocks and walls
    wallclear = set()    # exit-lane cells — walls avoid, but blocks MAY cross (interaction)
    colorat = {}         # cell -> color, to avoid confusing same-color adjacency
    doors = []; placed = []; bid = 1; splitters = set()
    reg_pairs = []       # (mover_id, target_id) per regular pair (chains excluded)
    # rotate the merge-result colors so every level shows a spread (not all one secondary).
    sec_order = [P, G, O]; rng.shuffle(sec_order)
    # PROGRESSIVE tertiary palette: early levels use a couple of tertiaries, and MORE colours
    # unlock as levels climb (Pink/Lime -> +Coral -> +Indigo -> +Amber) so the mix stays fresh.
    TERT_POOL = [PINK, LIME, CORAL, INDIGO, AMBER]
    n_tert = 2 if i < 150 else (3 if i < 250 else (4 if i < 360 else 5))
    chain_order = TERT_POOL[:n_tert]; rng.shuffle(chain_order)

    def adj_same(cell, col):
        return any(colorat.get((cell[0] + dx, cell[1] + dy)) == col
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))

    # CHAIN lanes: a full row/col holding a 3-block chain that makes a TERTIARY door color
    # in two merges — Pink = (R+B=Purple)+R, Lime = (B+Y=Green)+Y. 1x1 only, one per lane.
    used_lanes = set(); ct = 0; nc = 0
    while nc < nchain and ct < 300:
        ct += 1
        edge = rng.choice([LEFT, RIGHT, TOP, BOT])
        span = H if edge in (LEFT, RIGHT) else W
        lane = rng.randrange(span)
        if (edge, lane) in used_lanes:
            continue
        line = line_cells(edge, lane, W, H)
        if len(line) < 6:
            continue
        need = line[:6]
        if any(c in occ or c in mergeclear or c in wallclear for c in need):
            continue
        if door_overlaps(doors, edge, lane, 1):
            continue
        T = chain_order[nc % len(chain_order)]; (pa, pb), p3 = CHAINS[T]   # cycle the tertiary pool
        if rng.random() < 0.5: pa, pb = pb, pa
        used_lanes.add((edge, lane)); nc += 1
        b3c, b2c, b1c = line[1], line[3], line[5]
        for c in (b1c, b2c, b3c): occ.add(c)
        for c in (line[0], line[2], line[4]): mergeclear.add(c)
        doors.append(dict(edge=edge, laneStart=lane, length=1, seq=[T]))
        for (cx, cy), col in ((b1c, pa), (b2c, pb), (b3c, p3)):
            placed.append(dict(id=bid, color=col, x=cx, y=cy, shape=[[0, 0]], locked=False, axis=0)); bid += 1
            colorat[(cx, cy)] = col

    # Regular pairs. MERGE happens away from the door, then the survivor must be STEERED to
    # its exit. When perp=True the merge axis is PERPENDICULAR to the exit, so you merge, then
    # TURN and push it out — a two-step maneuver. Same-shape pairs (bars/squares) for variety.
    def try_place(shape):
        nonlocal bid
        w, h = bbox(shape)
        Ex = rng.choice([RIGHT, LEFT, TOP, BOT]); ex = EXITDIR[Ex]
        Dm = rng.choice([(0, 1), (0, -1)] if ex[0] != 0 else [(1, 0), (-1, 0)]) if perp else ex
        eD = extent(shape, 0 if Dm[0] != 0 else 1)            # shape length along the merge axis
        ax, ay = rng.randint(0, W - w), rng.randint(0, H - h)  # A = survivor anchor
        Ac = cells_at(shape, ax, ay)
        p1a = (ax - Dm[0] * (eD + 1), ay - Dm[1] * (eD + 1))  # mover start
        p2a = (ax + Dm[0] * eD, ay + Dm[1] * eD)              # target
        p1c = cells_at(shape, *p1a); p2c = cells_at(shape, *p2a)
        if any(not (0 <= c[0] < W and 0 <= c[1] < H) for c in p1c + p2c + Ac):
            return False
        mpath = set(Ac)
        for k in range(1, eD + 2):
            mpath.update(cells_at(shape, p1a[0] + Dm[0] * k, p1a[1] + Dm[1] * k))
        mpath -= set(p2c)
        exitset = set(); k = 1
        while True:
            cc = cells_at(shape, ax + ex[0] * k, ay + ex[1] * k)
            if any(not (0 <= a < W and 0 <= b < H) for (a, b) in cc): break
            exitset.update(cc); k += 1
        if not exitset:
            return False
        occ_cells = set(p1c) | set(p2c)
        mclear = mpath - occ_cells
        if any(c in occ or c in mergeclear for c in occ_cells): return False
        if any(c in occ or c in mergeclear or c in wallclear for c in mclear): return False
        # cycle secondaries so all of Purple/Green/Orange appear across a level's pairs
        S = sec_order[len(reg_pairs) % 3] if rng.random() < 0.8 else rng.choice([P, G, O])
        c1, c2 = PAIR[S]
        if rng.random() < 0.5: c1, c2 = c2, c1
        if any(adj_same(c, c1) for c in p1c) or any(adj_same(c, c2) for c in p2c): return False
        cross = sorted(set(cy for (cx, cy) in Ac)) if ex[0] != 0 else sorted(set(cx for (cx, cy) in Ac))
        laneStart, length = cross[0], cross[-1] - cross[0] + 1
        if door_overlaps(doors, Ex, laneStart, length): return False   # no stacked doors
        for c in occ_cells: occ.add(c)
        for c in mclear: mergeclear.add(c)
        for c in exitset:
            if c not in occ: wallclear.add(c)
        for c in p1c: colorat[c] = c1
        for c in p2c: colorat[c] = c2
        doors.append(dict(edge=Ex, laneStart=laneStart, length=length, seq=[S]))
        id1, id2 = bid, bid + 1; bid += 2
        sj = [list(c) for c in shape]
        placed.append(dict(id=id1, color=c1, x=p1a[0], y=p1a[1], shape=sj, locked=False, axis=0))
        placed.append(dict(id=id2, color=c2, x=p2a[0], y=p2a[1], shape=sj, locked=False, axis=0))
        reg_pairs.append((id1, id2))   # (mover, target) — lock the TARGET to keep the geometry
        return True

    shaped_pool = [s for s in pool if s != 'o']
    fat = [s for s in shaped_pool if s in ('sq', 'L', 'J')]    # fat 2D shapes need the MOST room
    bar = [s for s in shaped_pool if s not in ('sq', 'L', 'J')]
    target_shaped = 0 if not shaped_pool else min(npairs, max(2, npairs * 2 // 3))  # most pairs shaped
    ns = 0
    # Phase A1: place the fat 2D shapes FIRST (they only fit an empty board), aim for a few.
    if fat:
        want_fat = min(target_shaped, 3)
        t = 0
        while ns < want_fat and t < 3000:
            t += 1
            if try_place(SHAPES[rng.choice(fat)]): ns += 1
    # Phase A2: fill remaining shaped slots with bars.
    tries = 0
    while ns < target_shaped and tries < 1600:
        tries += 1
        if try_place(SHAPES[rng.choice(bar if bar else shaped_pool)]): ns += 1
    tries = 0
    while len(reg_pairs) < npairs and tries < 900:             # Phase B: fill with 1x1
        tries += 1
        try_place(SHAPES['o'])

    if strict:
        if len(reg_pairs) < npairs or nc < nchain:
            return None   # tutorial: demand the exact curriculum content
    elif len(reg_pairs) < npairs or nc < nchain:
        return None       # keep difficulty consistent: hit the target counts

    byid = {b['id']: b for b in placed}

    # LOCK the TARGET of some pairs: it can't move (a fixed obstacle) but the mover is pushed
    # INTO it to resolve it. Locking the target (not the mover) preserves the merge geometry.
    if nlocks > 0 and reg_pairs:
        pidx = list(range(len(reg_pairs))); rng.shuffle(pidx)
        for pi in pidx[:nlocks]:
            byid[reg_pairs[pi][1]]['locked'] = True

    blocked = occ | mergeclear | wallclear
    empties = [(x, y) for x in range(W) for y in range(H) if (x, y) not in blocked]
    rng.shuffle(empties)
    nwalls = min(len(empties), nwalls_t)
    walls = set(empties[:nwalls])
    return dict(W=W, H=H, placed=placed, doors=doors, walls=walls, splitters=splitters)


def to_level(i, cand):
    blocks = [dict(id=b['id'], color=b['color'], x=b['x'], y=b['y'], w=1, h=1,
                   cells=[dict(x=c[0], y=c[1]) for c in b['shape']], axis=b.get('axis', 0),
                   locked=b['locked'])
              for b in cand['placed']]
    nb = len(blocks)
    W = cand['W']
    # TIME-PRESSURE RAMP: bigger/denser boards need more absolute time (more moves), so the flat
    # 185s ceiling is lifted with board size — but the STAR thresholds tighten as levels climb, so
    # earning 3 stars demands an ever-faster solve. Absolute time stays fair (no unfair 1-star),
    # the pressure comes from the star bar. 3 stars: 45% -> 55% left; 2 stars: 20% -> 30% left.
    per = 9.0
    cap = 185 + max(0, W - 7) * 35                       # 7x7->185 (unchanged), 8x8->220
    time = float(min(cap, 40 + nb * per))
    ramp = max(0, i - 100)
    # 1-500 unchanged (caps below aren't reached until well past 500); 501-1000 keep tightening so
    # a 3-star solve stays demanding as levels climb.
    c3, c2 = (0.62, 0.36) if i > 500 else (0.55, 0.30)
    star3 = min(c3, 0.45 + ramp * 0.00022)              # 0.45 -> ~0.55 by L500 -> ~0.62 by L1000
    star2 = min(c2, 0.20 + ramp * 0.00025)              # 0.20 -> ~0.30 by L500 -> ~0.36 by L1000
    return dict(id=i, name=f"Stage {i}", width=cand['W'], height=cand['H'],
                blocks=blocks,
                doors=[dict(edge=d['edge'], laneStart=d['laneStart'], length=d['length'],
                            color=d['seq'][0], colorSequence=d['seq']) for d in cand['doors']],
                walls=[dict(x=x, y=y) for (x, y) in sorted(cand['walls'])],
                splitters=[dict(x=x, y=y) for (x, y) in sorted(cand.get('splitters', []))],
                paint=[], memorize=False,   # no path/preview hints — learn by failing
                timeLimitSeconds=time, star2SecondsLeft=round(time * star2, 1),
                star3SecondsLeft=round(time * star3, 1))


# Tutorial curriculum for 1-15: one new idea at a time, lots of empty board.
#   1-2 straight merge -> 3+ the TURN (merge then steer to the door) -> locks -> chains
TUTORIAL = {
    1:  dict(npairs=1, perp=False, nwalls=0),                       # push together = merge, push out = exit
    2:  dict(npairs=1, perp=False, nwalls=0),
    3:  dict(npairs=1, perp=True, nwalls=0),                        # merge, then TURN to reach the door
    4:  dict(npairs=2, perp=True, nwalls=0),
    5:  dict(npairs=2, perp=True, nwalls=1),
    6:  dict(npairs=3, perp=True, nwalls=1),
    7:  dict(npairs=2, perp=True, nlocks=1, nwalls=0),              # first lock (push its match into it)
    8:  dict(npairs=3, perp=True, nlocks=1, nwalls=1),
    9:  dict(npairs=3, perp=True, nlocks=2, nwalls=1),
    10: dict(npairs=4, perp=True, nlocks=1, nwalls=2),
    11: dict(npairs=0, nchain=1, nwalls=0),                         # first chain (2-step -> tertiary)
    12: dict(npairs=1, perp=True, nchain=1, nwalls=0),
    13: dict(npairs=2, perp=True, nchain=1, nwalls=1),
    14: dict(npairs=2, perp=True, nlocks=1, nchain=1, nwalls=1),
    15: dict(npairs=3, perp=True, nlocks=2, nchain=1, nwalls=2),    # everything
}


# Dedicated SPLIT levels — spacious, split-only puzzles interleaved every ~8 levels as a distinct
# mechanic (no merge blocks, so the splitter is unambiguous). Lanes scale 1 -> 3 as they recur.
def _split_schedule():
    sched = {}
    slots = [19] + list(range(27, 501, 8))   # 19, 27, 35, 43, ... 499
    for k, lvl in enumerate(slots):
        # ESCALATION: lanes climb 1->4, board grows 6->8 so more lanes fit, walls add pressure.
        # These are recurring "twist" beats, so they must keep getting harder rather than staying
        # a fixed 3-block dip among dense merge boards.
        size = 6 if lvl < 200 else (7 if lvl < 400 else 8)
        nsplit = min(4, 1 + lvl // 130)      # 1 -> 4 lanes as levels climb
        nwalls = min(5, 1 + lvl // 110)
        sched[lvl] = dict(split_only=True, nsplit=nsplit, nwalls=nwalls, size=size)
    return sched

SPLIT_LEVELS = _split_schedule()

# SPLIT-THEN-REMERGE levels (hardest mechanic) at a few late slots — split a secondary, then
# re-merge each half with a different color to make the door's secondary.
REMERGE_LEVELS = {
    90:  dict(remerge=True, nunits=1, nwalls=1),
    95:  dict(remerge=True, nunits=2, nwalls=1),
    100: dict(remerge=True, nunits=2, nwalls=2),
}
# The split-then-remerge mechanic (split a secondary, then re-merge EACH half with a different
# color) is a signature "compound" puzzle the player finds fun, so feature it PROMINENTLY: every
# ~14 levels from 110 through 500, ESCALATING via a bigger board (6->7->8) so units climb 2->3->4.
for _lvl in range(110, 501, 14):
    # Remerge stays on 6x6/7x7 boards: it's a lane-based puzzle that reads fine small, and the
    # split+remerge solver DFS explodes on 8x8 (a single 8x8 nunits=3 level took ~15 min to find).
    # nunits 2 -> 3; 3 is the reliable ceiling (4 lanes can't fit without overlap).
    _size = 6 if _lvl < 250 else 7
    _nunits = 2 if _lvl < 250 else 3
    REMERGE_LEVELS[_lvl] = dict(remerge=True, nunits=_nunits, nwalls=2, size=_size)


def gen(i, seed, spec, cap):
    rng = random.Random(seed)
    for _ in range(3500):
        c = build(i, rng, spec)
        if c is not None and solve(c, cap=cap):
            return c
    return None


if __name__ == "__main__":
    outdir = "/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
    # 8x8 merge boards (i>=160) use a LOWER solve cap: the DFS explodes on big boards, so a tight
    # cap abandons a pathological candidate fast and retries an easier seed (we only need SOME
    # solvable level per slot). 7x7 and specials keep the generous cap.
    plan = [(i, 70000 + i * 37, TUTORIAL[i], 120000) for i in range(1, 16)] \
        + [(i, 90000 + i * 59, REMERGE_LEVELS.get(i) or SPLIT_LEVELS.get(i),
            60000 if (i >= 160 and i not in SPLIT_LEVELS and i not in REMERGE_LEVELS) else 200000)
           for i in range(16, 501)]
    for i, seed, spec, cap in plan:
        cand = gen(i, seed, spec, cap)
        if cand is None:
            print(f"level_{i:03d}  FAILED", flush=True); continue
        lvl = to_level(i, cand)
        json.dump(lvl, open(os.path.join(outdir, f"level_{i:03d}.json"), "w"), indent=2)
        locks = sum(1 for b in lvl['blocks'] if b['locked'])
        rails = sum(1 for b in lvl['blocks'] if b['axis'] != 0)
        bars = sum(1 for b in lvl['blocks'] if len(b['cells']) > 1)
        splits = len(lvl['splitters'])
        tag = "tut " if spec else "    "
        print(f"{tag}level_{i:03d}  blocks={len(lvl['blocks']):2d}  shaped={bars}  doors={len(lvl['doors'])}  "
              f"locks={locks}  rails={rails}  splits={splits}  walls={len(lvl['walls'])}  t={int(lvl['timeLimitSeconds'])}s", flush=True)
    print("\nDone (stages 1-500).", flush=True)
