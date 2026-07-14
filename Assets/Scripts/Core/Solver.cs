using System.Collections.Generic;
using System.Text;

namespace ColorMergeExit.Core
{
    /// <summary>
    /// Forward search that answers "can this position still be cleared?" — used to warn the
    /// player the moment a wrong merge has left the board unsolvable (so they can undo instead
    /// of flailing). Handles arbitrary polyomino blocks and mirrors the Board's slide /
    /// same-shape merge / fit-through-door exit / locked rules. Conservative: if the search is
    /// capped before completing it reports solvable, so the warning only fires on a *proven*
    /// dead end.
    ///
    /// Split into <see cref="Capture"/> (reads the live Board — call on the main thread) and
    /// <see cref="IsSolvable(Snapshot,int)"/> (pure, safe to run on a background thread) so the
    /// potentially-slow DFS never blocks a frame.
    /// </summary>
    public static class Solver
    {
        internal struct B { public int Id, X, Y; public CarColor C; public bool L; public GridPos[] Shape; public string Sig;
            // Cycle != null marks a colour-cycling block. Because it cycles in REAL time the player can
            // WAIT for any of its colours, so the solver treats its colour as a WILDCARD: the colour is
            // NOT part of the state key, and at a merge/exit we branch over every colour in Cycle.
            // Timer blocks need no solver state at all — a real-time countdown never changes which
            // moves are legal, only whether the player is fast enough (a separate, generous calibration).
            public CarColor[] Cycle; }

        internal struct DoorS { public Edge Edge; public int LaneStart, Length, StartIndex; public CarColor[] Seq; }

        /// <summary>Opaque, Unity-free copy of a board position for off-thread solving.</summary>
        public sealed class Snapshot
        {
            internal int W, H;
            internal HashSet<GridPos> Walls;
            internal HashSet<GridPos> Splitters;
            internal List<B> Blocks;
            internal DoorS[] Doors;
        }

        private static readonly (int dx, int dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public static Snapshot Capture(Board board)
        {
            var blocks = new List<B>();
            foreach (var bl in board.Blocks)
            {
                var shape = (GridPos[])bl.Shape.Clone();
                blocks.Add(new B { Id = bl.Id, X = bl.X, Y = bl.Y, C = bl.Color, L = bl.Locked, Shape = shape, Sig = Sig(shape),
                    Cycle = bl.Cycle });
            }
            var doors = new List<Exit>(board.Exits);
            var ds = new DoorS[doors.Count];
            for (int i = 0; i < doors.Count; i++)
            {
                var seq = new CarColor[doors[i].Sequence.Count];
                for (int k = 0; k < seq.Length; k++) seq[k] = doors[i].Sequence[k];
                ds[i] = new DoorS { Edge = doors[i].Edge, LaneStart = doors[i].LaneStart, Length = doors[i].Length, StartIndex = doors[i].Index, Seq = seq };
            }
            return new Snapshot { W = board.Width, H = board.Height, Walls = new HashSet<GridPos>(board.Obstacles), Splitters = new HashSet<GridPos>(board.Splitters), Blocks = blocks, Doors = ds };
        }

        public static bool IsSolvable(Board board, int cap = 60000) => IsSolvable(Capture(board), cap);

        public static bool IsSolvable(Snapshot snap, int cap = 60000)
        {
            var startDoor = new int[snap.Doors.Length];
            for (int i = 0; i < snap.Doors.Length; i++) startDoor[i] = snap.Doors[i].StartIndex;

            var seen = new HashSet<string> { Key(snap.Blocks, startDoor) };
            var stack = new Stack<(List<B> blocks, int[] didx)>();
            stack.Push((snap.Blocks, startDoor));

            while (stack.Count > 0)
            {
                var (blocks, didx) = stack.Pop();
                if (blocks.Count == 0) return true;
                if (seen.Count > cap) return true; // gave up searching -> don't false-alarm

                foreach (var ns in Neighbors(blocks, didx, snap))
                    if (seen.Add(Key(ns.blocks, ns.didx))) stack.Push((ns.blocks, ns.didx));
            }
            return false; // search fully exhausted with no clear -> proven dead end
        }

        /// <summary>The first move of some solution: which block to move and in what direction.
        /// Used by the in-game "hint" item. Found=false if no solution within the cap.</summary>
        public struct Move { public bool Found; public int Id, Dx, Dy; }

        // Finding the first move of SOME winning line is a hard search on dense boards. No single
        // ordering works everywhere (a plain DFS wanders; pure best-first gets stuck on others), so
        // the hint runs a ladder of strategies and returns the first that reaches the empty board:
        //   1. best-first (fewest blocks remaining, FIFO within a tier) — nearest merge first
        //   2. exit-first DFS — commit toward door exits
        //   3. random-restart greedy — diversified tie-breaking cracks deep/narrow lines the
        //      deterministic passes miss (proven to clear every one of the 1000 levels)
        // Runs off the main thread (see GameController.DoHint), so the extra passes never drop a frame.
        public static Move Hint(Snapshot snap, int cap = 150000)
        {
            if (snap.Blocks.Count == 0) return new Move { Found = false };
            var m = HintBestFirst(snap, cap);
            if (m.Found) return m;
            m = HintExitDfs(snap, cap);
            if (m.Found) return m;
            for (int seed = 1; seed <= 40; seed++)
            {
                m = HintRandom(snap, cap, seed);
                if (m.Found) return m;
            }
            return new Move { Found = false };
        }

        private static (int[] startDoor, string startKey, HashSet<string> seen,
            Dictionary<string, (string pk, int id, int dx, int dy)> parent) HintInit(Snapshot snap)
        {
            var startDoor = new int[snap.Doors.Length];
            for (int i = 0; i < snap.Doors.Length; i++) startDoor[i] = snap.Doors[i].StartIndex;
            string startKey = Key(snap.Blocks, startDoor);
            var seen = new HashSet<string> { startKey };
            var parent = new Dictionary<string, (string pk, int id, int dx, int dy)> { [startKey] = (null, 0, 0, 0) };
            return (startDoor, startKey, seen, parent);
        }

        private static Move Backtrack(Dictionary<string, (string pk, int id, int dx, int dy)> parent, string startKey, string key)
        {
            string k = key;
            while (parent[k].pk != null && parent[k].pk != startKey) k = parent[k].pk;
            var m = parent[k];
            return new Move { Found = true, Id = m.id, Dx = m.dx, Dy = m.dy };
        }

        private static bool DidxChanged(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return true;
            return false;
        }

        // best-first by blocks-remaining; each tier FIFO so the nearest merge/exit is reached first.
        private static Move HintBestFirst(Snapshot snap, int cap)
        {
            var (startDoor, startKey, seen, parent) = HintInit(snap);
            var buckets = new List<Queue<(List<B> blocks, int[] didx, string key)>>();
            void Enq((List<B> blocks, int[] didx, string key) it)
            {
                int n = it.blocks.Count;
                while (buckets.Count <= n) buckets.Add(new Queue<(List<B>, int[], string)>());
                buckets[n].Enqueue(it);
            }
            Enq((snap.Blocks, startDoor, startKey));
            while (true)
            {
                int b = -1;
                for (int i = 0; i < buckets.Count; i++) if (buckets[i].Count > 0) { b = i; break; }
                if (b < 0) return new Move { Found = false };
                var (blocks, didx, key) = buckets[b].Dequeue();
                if (blocks.Count == 0) return Backtrack(parent, startKey, key);
                if (seen.Count > cap) return new Move { Found = false };
                foreach (var ns in Neighbors(blocks, didx, snap))
                {
                    string nk = Key(ns.blocks, ns.didx);
                    if (seen.Add(nk)) { parent[nk] = (key, ns.mid, ns.mdx, ns.mdy); Enq((ns.blocks, ns.didx, nk)); }
                }
            }
        }

        // DFS that explores door-exit moves first (they push the board toward being cleared).
        private static Move HintExitDfs(Snapshot snap, int cap)
        {
            var (startDoor, startKey, seen, parent) = HintInit(snap);
            var stack = new Stack<(List<B> blocks, int[] didx, string key)>();
            stack.Push((snap.Blocks, startDoor, startKey));
            var nonExit = new List<(List<B> blocks, int[] didx, string key)>();
            var exit = new List<(List<B> blocks, int[] didx, string key)>();
            while (stack.Count > 0)
            {
                var (blocks, didx, key) = stack.Pop();
                if (blocks.Count == 0) return Backtrack(parent, startKey, key);
                if (seen.Count > cap) return new Move { Found = false };
                nonExit.Clear(); exit.Clear();
                foreach (var ns in Neighbors(blocks, didx, snap))
                {
                    string nk = Key(ns.blocks, ns.didx);
                    if (!seen.Add(nk)) continue;
                    parent[nk] = (key, ns.mid, ns.mdx, ns.mdy);
                    (DidxChanged(didx, ns.didx) ? exit : nonExit).Add((ns.blocks, ns.didx, nk));
                }
                foreach (var it in nonExit) stack.Push(it);
                foreach (var it in exit) stack.Push(it); // exits pushed last -> popped first
            }
            return new Move { Found = false };
        }

        // greedy best-first with RANDOM tie-breaking within the fewest-blocks tier; restart with a
        // fresh seed to diversify. Cracks deep/narrow winning lines the deterministic passes miss.
        private static Move HintRandom(Snapshot snap, int cap, int seed)
        {
            var (startDoor, startKey, seen, parent) = HintInit(snap);
            var rng = new System.Random(seed);
            var buckets = new List<List<(List<B> blocks, int[] didx, string key)>>();
            void Add((List<B> blocks, int[] didx, string key) it)
            {
                int n = it.blocks.Count;
                while (buckets.Count <= n) buckets.Add(new List<(List<B>, int[], string)>());
                buckets[n].Add(it);
            }
            Add((snap.Blocks, startDoor, startKey));
            while (true)
            {
                int b = -1;
                for (int i = 0; i < buckets.Count; i++) if (buckets[i].Count > 0) { b = i; break; }
                if (b < 0) return new Move { Found = false };
                var bl = buckets[b];
                int idx = rng.Next(bl.Count);
                var (blocks, didx, key) = bl[idx];
                bl[idx] = bl[bl.Count - 1]; bl.RemoveAt(bl.Count - 1); // swap-remove
                if (blocks.Count == 0) return Backtrack(parent, startKey, key);
                if (seen.Count > cap) return new Move { Found = false };
                foreach (var ns in Neighbors(blocks, didx, snap))
                {
                    string nk = Key(ns.blocks, ns.didx);
                    if (seen.Add(nk)) { parent[nk] = (key, ns.mid, ns.mdx, ns.mdy); Add((ns.blocks, ns.didx, nk)); }
                }
            }
        }

        private static IEnumerable<(List<B> blocks, int[] didx, int mid, int mdx, int mdy)> Neighbors(List<B> blocks, int[] didx, Snapshot snap)
        {
            int w = snap.W, h = snap.H;
            // cell -> block index
            var cellOwner = new Dictionary<GridPos, int>();
            for (int i = 0; i < blocks.Count; i++)
                foreach (var c in Footprint(blocks[i])) cellOwner[c] = i;

            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (b.L) continue; // locked: can't move (but can be merged INTO)

                foreach (var (dx, dy) in Dirs)
                {
                    int cx = b.X, cy = b.Y;
                    while (true)
                    {
                        var cur = FootprintAt(b.Shape, cx, cy);
                        bool offb = false, hitw = false; int blocker = -2; bool multi = false;
                        foreach (var lc in cur)
                        {
                            var lead = new GridPos(lc.X + dx, lc.Y + dy);
                            if (cur.Contains(lead)) continue;
                            if (lead.X < 0 || lead.X >= w || lead.Y < 0 || lead.Y >= h) { offb = true; continue; }
                            if (snap.Walls.Contains(lead)) { hitw = true; continue; }
                            if (cellOwner.TryGetValue(lead, out int oi) && oi != i)
                            {
                                if (blocker == -2) blocker = oi;
                                else if (blocker != oi) multi = true;
                            }
                        }

                        if (hitw || blocker >= 0)
                        {
                            if (!hitw && !offb && blocker >= 0 && !multi)
                            {
                                var o = blocks[blocker];
                                if (b.Sig == o.Sig)
                                {
                                    // WILDCARD colours: a chameleon can be waited into any of its cycle
                                    // colours, so try every (mover colour, partner colour) pairing.
                                    foreach (var bx in EffColors(b))
                                        foreach (var oy in EffColors(o))
                                            if (ColorMix.TryMix(bx, oy, out var mixed))
                                            {
                                                var nb = new List<B>(blocks);
                                                var mover = nb[i]; mover.X = cx; mover.Y = cy; mover.C = mixed; mover.Cycle = null; nb[i] = mover;
                                                nb.RemoveAt(blocker);
                                                yield return (nb, didx, b.Id, dx, dy);
                                            }
                                }
                            }
                            break;
                        }
                        if (offb)
                        {
                            foreach (var bx in EffColors(b))
                            {
                                int di = CanExit(b.Shape, cx, cy, bx, dx, dy, didx, snap, cellOwner, i, w, h);
                                if (di >= 0)
                                {
                                    var nb = new List<B>(blocks); nb.RemoveAt(i);
                                    var nd = (int[])didx.Clone(); nd[di]++;
                                    yield return (nb, nd, b.Id, dx, dy);
                                }
                            }
                            break;
                        }
                        cx += dx; cy += dy;
                        // sliding onto a splitter breaks a mixed block into two — terminal, so
                        // the block stops here and cannot continue past the splitter.
                        var split = SplitState(blocks, i, cx, cy, dx, dy, snap, cellOwner);
                        if (split != null) { yield return (split, didx, b.Id, dx, dy); break; }
                        var mv = new List<B>(blocks);
                        var m2 = mv[i]; m2.X = cx; m2.Y = cy; mv[i] = m2;
                        yield return (mv, didx, b.Id, dx, dy);
                    }
                }
            }
        }

        // Sweep the shape off the board along (dx,dy); every leading cell must either be a
        // matching door (record it) or in-board & empty. Returns the door index used, or -1.
        private static int CanExit(GridPos[] shape, int sx, int sy, CarColor col, int dx, int dy,
            int[] didx, Snapshot snap, Dictionary<GridPos, int> owner, int selfIndex, int w, int h)
        {
            Edge edge = dx > 0 ? Edge.Right : dx < 0 ? Edge.Left : dy > 0 ? Edge.Bottom : Edge.Top;
            int used = -1, guard = w + h + 4;
            while (guard-- > 0)
            {
                var cur = FootprintAt(shape, sx, sy);
                bool allOff = true;
                foreach (var c in cur) if (c.X >= 0 && c.X < w && c.Y >= 0 && c.Y < h) { allOff = false; break; }
                if (allOff) return used;
                foreach (var lc in cur)
                {
                    var lead = new GridPos(lc.X + dx, lc.Y + dy);
                    if (cur.Contains(lead)) continue;
                    if (lead.X >= 0 && lead.X < w && lead.Y >= 0 && lead.Y < h)
                    {
                        if (snap.Walls.Contains(lead)) return -1;
                        if (owner.TryGetValue(lead, out int oi) && oi != selfIndex) return -1;
                    }
                    else
                    {
                        int lane = dx != 0 ? lead.Y : lead.X;
                        int di = DoorAt(snap.Doors, didx, edge, lane, col);
                        if (di < 0) return -1;
                        used = di;
                    }
                }
                sx += dx; sy += dy;
            }
            return -1;
        }

        private static int DoorAt(DoorS[] doors, int[] didx, Edge edge, int lane, CarColor col)
        {
            for (int k = 0; k < doors.Length; k++)
            {
                var d = doors[k];
                if (d.Edge == edge && lane >= d.LaneStart && lane <= d.LaneStart + d.Length - 1
                    && didx[k] < d.Seq.Length && d.Seq[didx[k]] == col)
                    return k;
            }
            return -1;
        }

        // If block i, now at (cx,cy), rests on a splitter and its color decomposes, return the
        // post-split block list (mover recolored + a same-shape twin in the vacated cell). Mirrors
        // Board.CanSplit: the twin must fit clear of the mover's new footprint, walls and others.
        private static List<B> SplitState(List<B> blocks, int i, int cx, int cy, int dx, int dy,
            Snapshot snap, Dictionary<GridPos, int> cellOwner)
        {
            if (snap.Splitters == null || snap.Splitters.Count == 0) return null;
            var b = blocks[i];
            if (b.Cycle != null) return null; // a cycling block has no fixed colour to split
            var newFoot = FootprintAt(b.Shape, cx, cy);
            bool on = false;
            foreach (var c in newFoot) if (snap.Splitters.Contains(c)) { on = true; break; }
            if (!on) return null;
            if (!ColorMix.TrySplit(b.C, out var c1, out var c2)) return null;

            int px = cx - dx, py = cy - dy;
            var twinFoot = FootprintAt(b.Shape, px, py);
            foreach (var t in twinFoot)
            {
                if (newFoot.Contains(t)) return null;
                if (t.X < 0 || t.X >= snap.W || t.Y < 0 || t.Y >= snap.H) return null;
                if (snap.Walls.Contains(t)) return null;
                if (cellOwner.TryGetValue(t, out int oi) && oi != i) return null;
            }

            int maxId = 0;
            foreach (var bb in blocks) if (bb.Id > maxId) maxId = bb.Id;
            var nb = new List<B>(blocks);
            var mover = nb[i]; mover.X = cx; mover.Y = cy; mover.C = c1; mover.Cycle = null; nb[i] = mover;
            nb.Add(new B { Id = maxId + 1, X = px, Y = py, C = c2, L = false, Shape = b.Shape, Sig = b.Sig });
            return nb;
        }

        // The colours a block can present at a merge/exit: a chameleon can be waited into ANY of its
        // cycle colours; an ordinary block only has its own colour.
        private static IEnumerable<CarColor> EffColors(B b)
        {
            if (b.Cycle != null && b.Cycle.Length > 0) { foreach (var c in b.Cycle) yield return c; }
            else yield return b.C;
        }

        private static HashSet<GridPos> Footprint(B b) => FootprintAt(b.Shape, b.X, b.Y);

        private static HashSet<GridPos> FootprintAt(GridPos[] shape, int x, int y)
        {
            var set = new HashSet<GridPos>();
            foreach (var c in shape) set.Add(new GridPos(x + c.X, y + c.Y));
            return set;
        }

        private static string Sig(GridPos[] shape)
        {
            int mnx = int.MaxValue, mny = int.MaxValue;
            foreach (var c in shape) { if (c.X < mnx) mnx = c.X; if (c.Y < mny) mny = c.Y; }
            var cells = new List<(int, int)>();
            foreach (var c in shape) cells.Add((c.X - mnx, c.Y - mny));
            cells.Sort();
            var sb = new StringBuilder();
            foreach (var c in cells) sb.Append(c.Item1).Append(':').Append(c.Item2).Append(',');
            return sb.ToString();
        }

        private static string Key(List<B> blocks, int[] didx)
        {
            var arr = new List<B>(blocks);
            arr.Sort((a, z) => a.Id - z.Id);
            var sb = new StringBuilder();
            foreach (var b in arr)
            {
                sb.Append(b.Id).Append(',').Append(b.X).Append(',').Append(b.Y).Append(',');
                // a chameleon's colour is a wildcard (it can be waited into any cycle colour), so it
                // is NOT part of the state — only its position matters.
                if (b.Cycle != null) sb.Append('~'); else sb.Append((int)b.C);
                sb.Append(b.L ? 'L' : '.').Append(';');
            }
            sb.Append('|');
            foreach (var d in didx) sb.Append(d).Append(',');
            return sb.ToString();
        }
    }
}
