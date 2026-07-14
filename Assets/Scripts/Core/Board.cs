using System.Collections.Generic;

namespace ColorMergeExit.Core
{
    /// <summary>
    /// Pure-logic block-jam board. Colored polyomino blocks slide one axis at a time; a
    /// block leaves the board when it slides through a door of its own color that is wide
    /// enough for its whole cross-section. Paint cells recolor a block that stops on them
    /// (R+B=Purple …). The board is solved when every block has been cleared.
    ///
    /// A move fully succeeds (slides as far as requested / clears the block) or is fully
    /// reverted. Deterministic and engine-free so it is unit-testable without Unity.
    /// Coordinates: origin top-left, x grows right, y grows down.
    /// </summary>
    public sealed class Board
    {
        public int Width { get; }
        public int Height { get; }

        private readonly Dictionary<int, Block> _blocks;
        private readonly List<Exit> _doors;
        private readonly HashSet<GridPos> _walls;
        private readonly Dictionary<GridPos, CarColor> _paint;
        private readonly HashSet<GridPos> _splitters;
        private readonly Stack<Snapshot> _history = new Stack<Snapshot>();

        public IReadOnlyList<Exit> Exits => _doors;
        public IReadOnlyCollection<GridPos> Obstacles => _walls;
        public IReadOnlyDictionary<GridPos, CarColor> Paints => _paint;
        public IReadOnlyCollection<GridPos> Splitters => _splitters;
        public IEnumerable<Block> Blocks => _blocks.Values;
        public int BlockCount => _blocks.Count;
        public int MoveCount { get; private set; }

        /// <summary>True once a timer block detonated (its real-time countdown hit zero while still on the
        /// board) — the run is lost. Cleared by an undo (Restore).</summary>
        public bool Detonated { get; private set; }

        public Board(int width, int height,
            IEnumerable<Block> blocks,
            IEnumerable<Exit> doors,
            IEnumerable<GridPos> walls = null,
            IEnumerable<KeyValuePair<GridPos, CarColor>> paint = null,
            IEnumerable<GridPos> splitters = null)
        {
            Width = width;
            Height = height;
            _blocks = new Dictionary<int, Block>();
            foreach (var b in blocks) _blocks[b.Id] = b;
            _doors = new List<Exit>(doors);
            _walls = new HashSet<GridPos>();
            if (walls != null) foreach (var w in walls) _walls.Add(w);
            _paint = new Dictionary<GridPos, CarColor>();
            if (paint != null) foreach (var p in paint) _paint[p.Key] = p.Value;
            _splitters = new HashSet<GridPos>();
            if (splitters != null) foreach (var s in splitters) _splitters.Add(s);
        }

        public bool TryGetBlock(int id, out Block block) => _blocks.TryGetValue(id, out block);

        public bool InBounds(GridPos c) => c.X >= 0 && c.X < Width && c.Y >= 0 && c.Y < Height;

        private bool CellFree(GridPos c, int ignoreId)
        {
            if (!InBounds(c)) return false;
            if (_walls.Contains(c)) return false;
            foreach (var b in _blocks.Values)
            {
                if (b.Id == ignoreId) continue;
                foreach (var bc in b.Cells())
                    if (bc == c) return false;
            }
            return true;
        }

        /// <summary>Id of the block occupying cell c (other than ignoreId), or -1.</summary>
        private int OtherBlockAt(GridPos c, int ignoreId)
        {
            foreach (var b in _blocks.Values)
            {
                if (b.Id == ignoreId) continue;
                foreach (var bc in b.Cells())
                    if (bc == c) return b.Id;
            }
            return -1;
        }

        /// <summary>Inspect the block's leading face: whether it runs off-board, hits a wall,
        /// and the set of distinct blocks it touches.</summary>
        private void InspectLeading(Block b, int dx, int dy, out bool offBoard, out bool wallHit, out int single, out int distinct)
        {
            offBoard = false; wallHit = false; single = -1; distinct = 0;
            var seen = new HashSet<int>();
            foreach (var lead in LeadingCells(b, dx, dy))
            {
                if (!InBounds(lead)) { offBoard = true; continue; }
                if (_walls.Contains(lead)) { wallHit = true; continue; }
                int id = OtherBlockAt(lead, b.Id);
                if (id >= 0 && seen.Add(id)) { distinct++; single = id; }
            }
            if (distinct != 1) single = -1;
        }

        private static Edge EdgeFor(int dx, int dy)
        {
            if (dx > 0) return Edge.Right;
            if (dx < 0) return Edge.Left;
            return dy > 0 ? Edge.Bottom : Edge.Top;
        }

        /// <summary>Cells just outside the block's leading face in direction (dx,dy).</summary>
        private static IEnumerable<GridPos> LeadingCells(Block b, int dx, int dy)
        {
            var own = new HashSet<GridPos>(b.Cells());
            foreach (var c in own)
            {
                var n = new GridPos(c.X + dx, c.Y + dy);
                if (!own.Contains(n)) yield return n;
            }
        }

        private bool DoorCovers(Edge edge, CarColor color, int lane)
        {
            foreach (var d in _doors)
                if (d.Edge == edge && !d.Done && d.CurrentColor == color && d.Covers(lane)) return true;
            return false;
        }

        /// <summary>The single door a block would exit through in direction (dx,dy).</summary>
        private Exit FindExitDoor(Block b, int dx, int dy)
        {
            var edge = EdgeFor(dx, dy);
            bool horiz = dx != 0;
            var lanes = new HashSet<int>();
            foreach (var c in b.Cells()) lanes.Add(horiz ? c.Y : c.X);
            foreach (var d in _doors)
            {
                if (d.Edge != edge || d.Done || d.CurrentColor != b.Color) continue;
                bool all = true;
                foreach (var lane in lanes) if (!d.Covers(lane)) { all = false; break; }
                if (all) return d;
            }
            return null;
        }

        /// <summary>Simulate sliding the block fully off the board in direction (dx,dy):
        /// every cell must cross through a same-color door lane, and the block's in-board
        /// cells must not collide with anything on the way out. Non-mutating.</summary>
        private bool CanExit(Block b, int dx, int dy)
        {
            var edge = EdgeFor(dx, dy);
            bool horiz = dx != 0;
            var sim = b.Clone();
            int guard = Width + Height + 4;
            while (guard-- > 0)
            {
                bool allOff = true;
                foreach (var c in sim.Cells()) if (InBounds(c)) { allOff = false; break; }
                if (allOff) return true;

                foreach (var lead in LeadingCells(sim, dx, dy))
                {
                    if (InBounds(lead))
                    {
                        if (!CellFree(lead, b.Id)) return false; // blocked on the way out
                    }
                    else
                    {
                        int lane = horiz ? lead.Y : lead.X;
                        if (!DoorCovers(edge, b.Color, lane)) return false; // no matching door lane
                    }
                }
                sim.X += dx; sim.Y += dy;
            }
            return false;
        }

        /// <summary>Move a block by up to |steps| along one axis. It slides until blocked
        /// or, at the edge, exits through a matching door (whole-block fit).</summary>
        public MoveResult TryMove(int blockId, int stepX, int stepY)
        {
            if ((stepX == 0) == (stepY == 0)) return MoveResult.Invalid;
            if (!_blocks.TryGetValue(blockId, out var b)) return MoveResult.Invalid;
            if (b.Locked) return MoveResult.Invalid; // a locked block can't move

            int dx = stepX > 0 ? 1 : stepX < 0 ? -1 : 0;
            int dy = stepY > 0 ? 1 : stepY < 0 ? -1 : 0;
            if (!b.CanMove(dx, dy)) return MoveResult.Invalid;
            int count = stepX != 0 ? System.Math.Abs(stepX) : System.Math.Abs(stepY);

            var snapshot = Capture();
            bool movedAny = false;
            for (int s = 0; s < count; s++)
            {
                InspectLeading(b, dx, dy, out bool offBoard, out bool wallHit, out int single, out int distinct);

                if (wallHit || distinct > 0)
                {
                    // MERGE: sliding into exactly one other block of the SAME SHAPE and a
                    // mixable color fuses them. A LOCKED block can't move itself, but it CAN
                    // be merged INTO (bring its match to it) — that resolves it.
                    if (!wallHit && !offBoard && single >= 0 &&
                        _blocks.TryGetValue(single, out var other) &&
                        b.SameShapeAs(other) &&
                        ColorMix.TryMix(b.Color, other.Color, out var mixed))
                    {
                        b.Color = mixed;
                        b.Cycle = null; // a merged chameleon becomes a stable mixed block
                        _blocks.Remove(single);
                        _history.Push(snapshot);
                        MoveCount++;
                        return MoveResult.Merged;
                    }
                    break; // plain blocked
                }
                if (offBoard)
                {
                    if (CanExit(b, dx, dy))
                    {
                        FindExitDoor(b, dx, dy)?.Advance(); // door cycles to its next color
                        _blocks.Remove(blockId);
                        _history.Push(snapshot);
                        MoveCount++;
                        return MoveResult.Exited;
                    }
                    break; // reached the edge but can't fit through a matching door
                }
                b.X += dx; b.Y += dy; movedAny = true;
                ApplyPaint(b); // passing over a paint cell mixes the block's color

                // SPLIT: if the block just slid onto a splitter cell and its color decomposes,
                // it breaks back into its two component colors — the mover keeps its shape but
                // takes one component; a same-shape twin of the other spawns in the cell(s) it
                // just vacated. Terminal, like a merge/exit.
                if (CanSplit(b, dx, dy, out var s1, out var s2))
                {
                    b.Color = s1;
                    b.Cycle = null; // a split block is stable afterward
                    int twinId = NextFreeId();
                    _blocks[twinId] = new Block(twinId, b.Shape, s2, b.X - dx, b.Y - dy, MoveAxis.Free);
                    _history.Push(snapshot);
                    MoveCount++;
                    return MoveResult.Split;
                }
            }

            if (movedAny)
            {
                _history.Push(snapshot);
                MoveCount++;
                return MoveResult.Moved;
            }
            return MoveResult.Blocked;
        }

        /// <summary>Free cells a block can slide in direction (dx,dy) without mutating,
        /// plus whether it could then pass off the board through a matching door.</summary>
        public int MaxSlide(int blockId, int dx, int dy, out bool reachesExit, out bool reachesMerge, out bool reachesSplit)
        {
            reachesExit = false; reachesMerge = false; reachesSplit = false;
            if (!_blocks.TryGetValue(blockId, out var b)) return 0;
            if (b.Locked || !b.CanMove(dx, dy)) return 0;

            var probe = b.Clone();
            int steps = 0;
            while (true)
            {
                InspectLeading(probe, dx, dy, out bool offBoard, out bool wallHit, out int single, out int distinct);
                if (wallHit || distinct > 0)
                {
                    if (!wallHit && !offBoard && single >= 0 &&
                        _blocks.TryGetValue(single, out var other) &&
                        b.SameShapeAs(other) &&
                        ColorMix.TryMix(b.Color, other.Color, out _))
                        reachesMerge = true;
                    break;
                }
                if (offBoard)
                {
                    if (CanExit(probe, dx, dy)) reachesExit = true;
                    break;
                }
                probe.X += dx; probe.Y += dy; steps++;
                ApplyPaint(probe); // mirror TryMove so the exit prediction uses the mixed color
                if (CanSplit(probe, dx, dy, out _, out _)) { reachesSplit = true; break; } // splitter halts the slide
            }
            return steps;
        }

        /// <summary>Whether block <paramref name="b"/> (already at its post-step position) would
        /// split here: it rests on a splitter cell, its color decomposes, and its same-shape
        /// twin has room in the cell(s) it just vacated (moving by (dx,dy)). Non-mutating.</summary>
        private bool CanSplit(Block b, int dx, int dy, out CarColor c1, out CarColor c2)
        {
            c1 = b.Color; c2 = b.Color;
            if (_splitters.Count == 0) return false;
            if (b.IsChameleon) return false; // a cycling block isn't a fixed mixed colour, so it can't split

            bool onSplitter = false;
            foreach (var c in b.Cells()) if (_splitters.Contains(c)) { onSplitter = true; break; }
            if (!onSplitter) return false;
            if (!ColorMix.TrySplit(b.Color, out c1, out c2)) return false;

            // the twin occupies the block's previous footprint; it must fit clear of walls,
            // other blocks and the block's own new footprint.
            var own = new HashSet<GridPos>(b.Cells());
            foreach (var cell in b.Shape)
            {
                var t = new GridPos(b.X - dx + cell.X, b.Y - dy + cell.Y);
                if (own.Contains(t)) return false;      // overlaps where the block now sits
                if (!CellFree(t, b.Id)) return false;    // off-board / wall / another block
            }
            return true;
        }

        /// <summary>The "force-split" item: split a mixed block in place into its two components
        /// without needing a splitter cell — the block keeps one component, a same-shape twin of
        /// the other spawns in the first free adjacent slot. False if the block can't be split
        /// (primary) or there's no room for the twin. Undoable.</summary>
        public bool ForceSplit(int blockId)
        {
            if (!_blocks.TryGetValue(blockId, out var b)) return false;
            if (!ColorMix.TrySplit(b.Color, out var c1, out var c2)) return false;

            foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                var own = new HashSet<GridPos>(b.Cells());
                bool fits = true;
                foreach (var cell in b.Shape)
                {
                    var t = new GridPos(b.X + dx + cell.X, b.Y + dy + cell.Y);
                    if (own.Contains(t) || !CellFree(t, b.Id)) { fits = false; break; }
                }
                if (!fits) continue;

                var snap = Capture();
                b.Color = c1;
                b.Cycle = null; // a force-split block is stable afterward
                int twinId = NextFreeId();
                _blocks[twinId] = new Block(twinId, b.Shape, c2, b.X + dx, b.Y + dy, MoveAxis.Free);
                _history.Push(snap);
                MoveCount++;
                return true;
            }
            return false;
        }

        /// <summary>Smallest positive id not currently used by a block.</summary>
        private int NextFreeId()
        {
            int max = 0;
            foreach (var id in _blocks.Keys) if (id > max) max = id;
            return max + 1;
        }

        /// <summary>Advance REAL time by <paramref name="dt"/> seconds: colour-cycling blocks step
        /// their colour every CycleSeconds, and timer blocks count down — a timer reaching zero while
        /// still on the board detonates (sets <see cref="Detonated"/>). Returns true if a timer just
        /// detonated. Driven by GameSession.Tick, independent of moves.</summary>
        public bool TickRealtime(float dt)
        {
            if (dt <= 0f) return false;
            bool boom = false;
            foreach (var b in _blocks.Values)
            {
                if (b.IsChameleon)
                {
                    b.CycleElapsed += dt;
                    while (b.CycleElapsed >= b.CycleSeconds)
                    {
                        b.CycleElapsed -= b.CycleSeconds;
                        b.CycleIndex = (b.CycleIndex + 1) % b.Cycle.Length;
                        b.Color = b.Cycle[b.CycleIndex];
                    }
                }
                if (b.TimerSeconds > 0f)
                {
                    b.TimerSeconds -= dt;
                    if (b.TimerSeconds <= 0f) { b.TimerSeconds = 0f; boom = true; }
                }
            }
            if (boom) Detonated = true;
            return boom;
        }

        /// <summary>Recolor the block if it now rests on paint cells (color mixing).</summary>
        private void ApplyPaint(Block b)
        {
            if (_paint.Count == 0) return;
            foreach (var c in b.Cells())
                if (_paint.TryGetValue(c, out var pc) && ColorMix.TryMix(b.Color, pc, out var mixed))
                    b.Color = mixed;
        }

        public bool Undo()
        {
            if (_history.Count == 0) return false;
            Restore(_history.Pop());
            MoveCount--;
            return true;
        }

        public bool IsWon() => _blocks.Count == 0;

        private readonly struct Snapshot
        {
            public readonly Block[] Blocks;
            public readonly int[] DoorIdx;
            public Snapshot(Block[] blocks, int[] doorIdx) { Blocks = blocks; DoorIdx = doorIdx; }
        }

        private Snapshot Capture()
        {
            var bs = new Block[_blocks.Count];
            int i = 0;
            foreach (var b in _blocks.Values) bs[i++] = b.Clone();
            var idx = new int[_doors.Count];
            for (int d = 0; d < _doors.Count; d++) idx[d] = _doors[d].Index;
            return new Snapshot(bs, idx);
        }

        private void Restore(Snapshot snap)
        {
            _blocks.Clear();
            foreach (var b in snap.Blocks) _blocks[b.Id] = b;
            for (int d = 0; d < _doors.Count; d++) _doors[d].SetIndex(snap.DoorIdx[d]);
            Detonated = false; // undoing the fatal move revives the run
        }
    }
}
