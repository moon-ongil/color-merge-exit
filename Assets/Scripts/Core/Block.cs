using System.Collections.Generic;

namespace ColorMergeExit.Core
{
    /// <summary>
    /// A colored polyomino block (rectangles, L/T/S, lines, squares…). Slides freely in
    /// all four directions unless axis-locked. A block leaves the board when it slides
    /// through a door of its own color that is wide enough for its whole cross-section.
    /// The goal is to clear EVERY block off the board.
    ///
    /// The color is a single (mutable) value — it can change when the block passes a
    /// paint cell (R+B=Purple …), the signature color-mixing mechanic.
    ///
    /// Anchor is (X,Y); Shape holds normalized local cell offsets (min corner at 0,0).
    /// Coordinates: origin top-left, x grows right, y grows down.
    /// </summary>
    public sealed class Block
    {
        public readonly int Id;
        public readonly GridPos[] Shape; // local offsets, normalized so min x == min y == 0
        public readonly MoveAxis Axis;

        public int X;
        public int Y;
        public CarColor Color; // uniform; may be recolored by paint cells / merging / colour-cycling
        public bool Locked;    // frozen until a merge happens in an adjacent cell

        // TIMER block: a REAL-TIME countdown (seconds). >0 = live timer; it ticks down with wall-clock
        // while the run is Playing and DETONATES (run lost) if it hits 0 while still on the board — so
        // it must be cleared FAST. 0 = an ordinary block. Ticks in Board.TickRealtime, NOT on moves.
        public float TimerSeconds;

        // COLOUR-CYCLING ("chameleon") block: its colour steps through Cycle every CycleSeconds of
        // REAL time, so a merge/exit only works when it is currently the matching colour — the player
        // can WAIT for the right colour. Cycle == null / empty = an ordinary block. Once it merges it
        // becomes a stable mixed block (Cycle cleared). Ticks in Board.TickRealtime.
        public CarColor[] Cycle;
        public readonly float CycleSeconds; // real seconds per colour step
        public int CycleIndex;              // current position in Cycle (Color == Cycle[CycleIndex])
        public float CycleElapsed;          // real seconds accumulated toward the next step

        public Block(int id, IEnumerable<GridPos> shape, CarColor color, int x, int y,
            MoveAxis axis = MoveAxis.Free, bool locked = false,
            float timerSeconds = 0f, IReadOnlyList<CarColor> cycle = null, float cycleSeconds = 5f)
        {
            Id = id;
            var list = new List<GridPos>(shape);
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in list) { if (c.X < minX) minX = c.X; if (c.Y < minY) minY = c.Y; }
            if (list.Count == 0) { minX = minY = 0; }
            for (int i = 0; i < list.Count; i++) list[i] = new GridPos(list[i].X - minX, list[i].Y - minY);
            Shape = list.ToArray();
            X = x;
            Y = y;
            Axis = axis;
            Locked = locked;
            TimerSeconds = timerSeconds < 0f ? 0f : timerSeconds;
            CycleSeconds = cycleSeconds <= 0f ? 5f : cycleSeconds;
            if (cycle != null && cycle.Count > 0)
            {
                Cycle = new CarColor[cycle.Count];
                for (int i = 0; i < cycle.Count; i++) Cycle[i] = cycle[i];
                CycleIndex = 0;
                Color = Cycle[0]; // a chameleon always starts on the first colour of its cycle
            }
            else
            {
                Color = color;
            }
        }

        /// <summary>Convenience: build a W×H rectangular block.</summary>
        public static Block Rect(int id, CarColor color, int x, int y, int w = 1, int h = 1,
            MoveAxis axis = MoveAxis.Free, bool locked = false, float timerSeconds = 0f)
        {
            var cells = new List<GridPos>(w * h);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    cells.Add(new GridPos(i, j));
            return new Block(id, cells, color, x, y, axis, locked, timerSeconds);
        }

        public bool IsChameleon => Cycle != null && Cycle.Length > 0;
        public bool IsTimer => TimerSeconds > 0f;

        /// <summary>True if the other block has the exact same (normalized) shape.</summary>
        public bool SameShapeAs(Block o)
        {
            if (o == null || Shape.Length != o.Shape.Length) return false;
            foreach (var c in Shape)
            {
                bool found = false;
                foreach (var d in o.Shape) if (d.X == c.X && d.Y == c.Y) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        public int CellCount => Shape.Length;

        public IEnumerable<GridPos> Cells()
        {
            foreach (var c in Shape) yield return new GridPos(X + c.X, Y + c.Y);
        }

        /// <summary>Whether this block is allowed to slide in direction (dx,dy).</summary>
        public bool CanMove(int dx, int dy)
        {
            if (Axis == MoveAxis.Horizontal) return dy == 0;
            if (Axis == MoveAxis.Vertical) return dx == 0;
            return true;
        }

        public Block Clone()
        {
            // Build with cycle=null so the ctor keeps the exact Color; carry CycleSeconds (readonly)
            // and TimerSeconds through, then restore the live cycling state by hand.
            var c = new Block(Id, Shape, Color, X, Y, Axis, Locked, TimerSeconds, null, CycleSeconds);
            c.Cycle = Cycle; // shared cycle list is fine (never mutated in place)
            c.CycleIndex = CycleIndex;
            c.CycleElapsed = CycleElapsed;
            return c;
        }
    }
}
