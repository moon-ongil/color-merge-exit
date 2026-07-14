using System;
using System.Collections.Generic;

namespace ColorMergeExit.Core
{
    /// <summary>
    /// Serializable level definition. Plain POCOs so it works with Unity's JsonUtility
    /// and plain .NET tooling/tests. A level is a set of colored polyomino blocks, colored
    /// edge doors, fixed walls, and optional paint cells; solved by clearing every block.
    /// </summary>
    [Serializable]
    public sealed class LevelData
    {
        public int id;
        public string name;
        public int width = 6;
        public int height = 6;

        public List<BlockSpawnData> blocks = new List<BlockSpawnData>();
        public List<DoorData> doors = new List<DoorData>();
        public List<CellData> walls = new List<CellData>();
        public List<PaintData> paint = new List<PaintData>();
        public List<CellData> splitters = new List<CellData>(); // break a merged block back into two

        public float timeLimitSeconds = 90f;
        public float star2SecondsLeft = 20f;
        public float star3SecondsLeft = 35f;

        public bool memorize = false; // show door colors briefly at start, then hide them

        public Board BuildBoard()
        {
            var bs = new List<Block>(blocks.Count);
            foreach (var b in blocks)
            {
                var axis = (MoveAxis)(b.axis < 0 ? 0 : b.axis > 2 ? 2 : b.axis);
                var cycle = (b.cycle != null && b.cycle.Count > 0) ? b.cycle : null;
                float cycSec = b.cycleSeconds <= 0f ? 5f : b.cycleSeconds;
                Block block;
                if (b.cells != null && b.cells.Count > 0)
                {
                    var shape = new List<GridPos>(b.cells.Count);
                    foreach (var c in b.cells) shape.Add(new GridPos(c.x, c.y));
                    block = new Block(b.id, shape, b.color, b.x, b.y, axis, b.locked, b.timerSeconds, cycle, cycSec);
                }
                else
                {
                    var w = b.w <= 0 ? 1 : b.w; var h = b.h <= 0 ? 1 : b.h;
                    var cells = new List<GridPos>(w * h);
                    for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) cells.Add(new GridPos(i, j));
                    block = new Block(b.id, cells, b.color, b.x, b.y, axis, b.locked, b.timerSeconds, cycle, cycSec);
                }
                bs.Add(block);
            }

            var ds = new List<Exit>(doors.Count);
            foreach (var d in doors)
            {
                var seq = (d.colorSequence != null && d.colorSequence.Count > 0)
                    ? d.colorSequence.ToArray() : new[] { d.color };
                ds.Add(new Exit(d.edge, d.laneStart, d.length <= 0 ? 1 : d.length, seq));
            }

            var ws = new List<GridPos>(walls.Count);
            foreach (var w in walls) ws.Add(new GridPos(w.x, w.y));

            var ps = new List<KeyValuePair<GridPos, CarColor>>(paint.Count);
            foreach (var p in paint)
                ps.Add(new KeyValuePair<GridPos, CarColor>(new GridPos(p.x, p.y), p.color));

            var ss = new List<GridPos>(splitters.Count);
            foreach (var s in splitters) ss.Add(new GridPos(s.x, s.y));

            return new Board(width, height, bs, ds, ws, ps, ss);
        }
    }

    [Serializable]
    public sealed class BlockSpawnData
    {
        public int id;
        public CarColor color;
        public int x;
        public int y;
        public int w = 1;                              // rectangle fallback if cells empty
        public int h = 1;
        public List<CellData> cells = new List<CellData>(); // explicit polyomino offsets
        public int axis = 0;                           // MoveAxis: 0=Free,1=Horizontal,2=Vertical
        public bool locked = false;                    // frozen until an adjacent merge
        public float timerSeconds = 0f;                // >0 = timer block: clear before this many REAL seconds
        public List<CarColor> cycle = new List<CarColor>(); // non-empty = colour-cycling block
        public float cycleSeconds = 5f;                // colour steps every this many REAL seconds
    }

    [Serializable]
    public sealed class DoorData
    {
        public Edge edge;
        public int laneStart;
        public int length = 1;
        public CarColor color;                                     // single-color fallback
        public List<CarColor> colorSequence = new List<CarColor>(); // cycles as blocks pass
    }

    [Serializable]
    public struct PaintData
    {
        public int x;
        public int y;
        public CarColor color;
    }

    [Serializable]
    public struct CellData
    {
        public int x;
        public int y;
        public CellData(int x, int y) { this.x = x; this.y = y; }
    }
}
