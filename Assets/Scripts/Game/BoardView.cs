using System.Collections;
using System.Collections.Generic;
using ColorMergeExit.Core;
using TMPro;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Renders a block-jam <see cref="Board"/> with world-space sprites and owns the
    /// grid&lt;-&gt;world mapping. Blocks are colored glossy polyominoes; doors are colored
    /// bars just outside the matching edge; walls are dark tiles; paint cells are colored
    /// dots (color-mixing). Origin is top-left; the board is centered on world origin
    /// (one cell == one unit).
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private sealed class BlockView { public Transform Root; public SpriteRenderer Sr; public SpriteRenderer Lock;
            public TMP_Text Fuse; public SpriteRenderer CycleRing; public SpriteRenderer NextPip; }
        private sealed class DoorView { public Exit Door; public SpriteRenderer Bar; public SpriteRenderer NextSeg; public SpriteRenderer Arrow; public Vector3 Pos; public Vector3 Outward; }

        private readonly List<DoorView> _doorViews = new List<DoorView>();
        private bool _doorsHidden;

        /// <summary>Hide/show door colors (memory mode): hidden doors render grey.</summary>
        public void SetDoorsHidden(bool hidden)
        {
            _doorsHidden = hidden;
            RefreshExits();
        }

        /// <summary>World position of the top-left corner of cell (X,Y).</summary>
        private Vector3 TopLeftWorld(int x, int y) => new Vector3(_originX + x, _originY - y, 0f);

        private Board _board;
        private GameSprites _sprites;
        private float _originX, _originY;
        private Transform _boardRoot;

        private readonly Dictionary<int, BlockView> _blocks = new Dictionary<int, BlockView>();

        public int Width => _board.Width;
        public int Height => _board.Height;

        public void Build(Board board, GameSprites sprites = null)
        {
            _board = board;
            _sprites = sprites;
            _originX = -board.Width * 0.5f;
            _originY = board.Height * 0.5f;

            if (_boardRoot != null) Destroy(_boardRoot.gameObject);
            _blocks.Clear();
            _boardRoot = new GameObject("BoardRoot").transform;
            _boardRoot.SetParent(transform, false);

            BuildBackground();
            BuildGridTiles();
            BuildPaintCells();
            BuildSplitters();
            BuildWalls();
            BuildDoors();
            RefreshBlocks();
        }

        // ---- coordinate mapping ----
        public Vector3 CellCenterWorld(float x, float y) =>
            new Vector3(_originX + x + 0.5f, _originY - (y + 0.5f), 0f);

        public Vector3 ScreenToWorld(Camera cam, Vector3 screenPos) => cam.ScreenToWorldPoint(screenPos);

        public bool TryScreenToCell(Camera cam, Vector3 screenPos, out int cx, out int cy)
        {
            var w = cam.ScreenToWorldPoint(screenPos);
            cx = Mathf.FloorToInt(w.x - _originX);
            cy = Mathf.FloorToInt(_originY - w.y);
            return cx >= 0 && cx < _board.Width && cy >= 0 && cy < _board.Height;
        }

        public Block BlockAtCell(int cx, int cy)
        {
            foreach (var b in _board.Blocks)
                foreach (var c in b.Cells())
                    if (c.X == cx && c.Y == cy) return b;
            return null;
        }

        private Vector3 BlockCenterWorld(Block b)
        {
            float sx = 0f, sy = 0f; int n = 0;
            foreach (var c in b.Cells()) { var w = CellCenterWorld(c.X, c.Y); sx += w.x; sy += w.y; n++; }
            return n > 0 ? new Vector3(sx / n, sy / n, 0f) : Vector3.zero;
        }

        public Vector3 BlockCenter(Block b) => BlockCenterWorld(b);

        private readonly List<GameObject> _splitHints = new List<GameObject>();

        /// <summary>Show a pulsing teal ring on every block that CAN be force-split (a mixed colour),
        /// so after arming the split item the player sees which blocks are valid targets. false clears.</summary>
        public void SetSplittableHint(bool on)
        {
            foreach (var go in _splitHints) if (go != null) Destroy(go);
            _splitHints.Clear();
            if (!on || _board == null) return;
            foreach (var b in _board.Blocks)
            {
                if (!ColorMix.TrySplit(b.Color, out _, out _)) continue;
                var ring = MakeSprite("SplitHint", _boardRoot, VisualAssets.Ring(), new Color(0.10f, 0.78f, 0.72f, 0.95f), 9);
                ring.transform.position = BlockCenter(b);
                var rb = ring.sprite.bounds.size;
                var p = ring.gameObject.AddComponent<Pulser>();
                p.BaseScale = 1.25f / Mathf.Max(0.0001f, rb.x);
                _splitHints.Add(ring.gameObject);
            }
        }

        private sealed class Pulser : MonoBehaviour
        {
            public float BaseScale = 1f;
            private void Update()
            {
                float s = BaseScale * (1f + 0.10f * Mathf.Sin(Time.time * 6f));
                transform.localScale = new Vector3(s, s, 1f);
            }
        }

        // ---- construction ----
        // Forwards to the shared toolkit; board sprites set their own localPosition after creation.
        private SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Color color, int order)
            => Ui.Sprite(parent, name, Vector3.zero, color, order, sprite);

        private void BuildBackground()
        {
            var bg = MakeSprite("Background", _boardRoot, VisualAssets.RoundedPanel(), new Color(0.82f, 0.85f, 0.93f), 0);
            bg.transform.localScale = new Vector3(_board.Width + 0.6f, _board.Height + 0.6f, 1f);
        }

        private void BuildGridTiles()
        {
            for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
            {
                var tile = MakeSprite($"Tile_{x}_{y}", _boardRoot, VisualAssets.RoundedSquare(), new Color(0.94f, 0.96f, 0.99f), 1);
                tile.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
                tile.transform.localPosition = CellCenterWorld(x, y);
            }
        }

        private void BuildPaintCells()
        {
            foreach (var kv in _board.Paints)
            {
                var dot = MakeSprite($"Paint_{kv.Key.X}_{kv.Key.Y}", _boardRoot, VisualAssets.RoundedSquare(),
                    VisualAssets.ToUnity(kv.Value), 2);
                dot.transform.localScale = new Vector3(0.64f, 0.64f, 1f);
                dot.transform.localPosition = CellCenterWorld(kv.Key.X, kv.Key.Y);
                // a soft ring so it reads as "paint here", not a block
                var ring = MakeSprite("Ring", dot.transform, VisualAssets.RoundedSquare(), new Color(1f, 1f, 1f, 0.18f), 2);
                ring.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            }
        }

        // Splitter cells: a pale "prism" pad with an outward-arrows glyph — a mixed block that
        // slides onto it breaks back into its two component colors.
        private void BuildSplitters()
        {
            foreach (var s in _board.Splitters)
            {
                var pad = MakeSprite($"Split_{s.X}_{s.Y}", _boardRoot, VisualAssets.RoundedSquare(), new Color(0.42f, 0.58f, 0.95f, 0.20f), 2);
                pad.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                pad.transform.localPosition = CellCenterWorld(s.X, s.Y);
                var icon = MakeSprite("SplitIcon", _boardRoot, VisualAssets.SplitIcon(), new Color(0.28f, 0.44f, 0.86f, 0.95f), 3);
                icon.transform.localScale = new Vector3(0.66f, 0.66f, 1f);
                icon.transform.localPosition = CellCenterWorld(s.X, s.Y);
            }
        }

        private void BuildWalls()
        {
            foreach (var w in _board.Obstacles)
            {
                var ob = MakeSprite($"Wall_{w.X}_{w.Y}", _boardRoot, VisualAssets.RoundedSquare(), new Color(0.48f, 0.52f, 0.62f), 3);
                ob.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                ob.transform.localPosition = CellCenterWorld(w.X, w.Y);
            }
        }

        // ---- doors (colored bar + upcoming-color pips; cycles as blocks pass) ----
        private void BuildDoors()
        {
            _doorViews.Clear();
            // The door tab STRADDLES the board edge — its centre sits on the edge line (small `inset`
            // inward), so roughly half the fat coloured bar is on the grey border and half is outside,
            // reading as an "opening in the edge" without protruding off-screen or covering a whole
            // cell. Only the `thick` (perpendicular) axis is fat; the lane-spanning length stays put so
            // adjacent-lane doors never overlap. A big solid-white arrow points the exit direction.
            const float inset = -0.40f, thick = 0.80f;
            foreach (var d in _board.Exits)
            {
                float laneCenter = (d.LaneStart + d.LaneEnd) * 0.5f;
                Vector3 pos, scale, outward;
                if (d.Edge == Edge.Right)
                { pos = new Vector3(_originX + _board.Width - inset, _originY - (laneCenter + 0.5f), 0f); scale = new Vector3(thick, d.Length - 0.1f, 1f); outward = Vector3.right; }
                else if (d.Edge == Edge.Left)
                { pos = new Vector3(_originX + inset, _originY - (laneCenter + 0.5f), 0f); scale = new Vector3(thick, d.Length - 0.1f, 1f); outward = Vector3.left; }
                else if (d.Edge == Edge.Bottom)
                { pos = new Vector3(_originX + laneCenter + 0.5f, _originY - _board.Height + inset, 0f); scale = new Vector3(d.Length - 0.1f, thick, 1f); outward = Vector3.down; }
                else
                { pos = new Vector3(_originX + laneCenter + 0.5f, _originY - inset, 0f); scale = new Vector3(d.Length - 0.1f, thick, 1f); outward = Vector3.up; }

                var bar = MakeSprite($"Door_{d.Edge}_{d.LaneStart}", _boardRoot, VisualAssets.GlossyBar(), Color.white, 4);
                bar.transform.localPosition = pos;
                // 9-slice the door bar so its (small) rounded corners stay identical regardless of length
                bar.drawMode = SpriteDrawMode.Sliced;
                bar.size = new Vector2(scale.x, scale.y);

                // exit-direction arrow: a big SOLID-WHITE chevron centred on the bar, rotated so it
                // points OUTWARD — the way a matching block leaves.
                float angleZ = Mathf.Atan2(outward.x, -outward.y) * Mathf.Rad2Deg;  // rotate the down-chevron to face outward
                var arrowRot = Quaternion.Euler(0f, 0f, angleZ);
                var chev = VisualAssets.Chevron();
                float chW = Mathf.Max(0.0001f, chev.bounds.size.x);
                float aScale = 0.9f / chW;
                var arrow = MakeSprite("DoorArrow", _boardRoot, chev, Color.white, 7);
                arrow.transform.localPosition = pos + new Vector3(0f, 0f, -0.03f);
                arrow.transform.localRotation = arrowRot;
                arrow.transform.localScale = new Vector3(aScale, aScale, 1f);

                // Upcoming-colour band: the OUTER ~1/3 of the door (the BACK, away from the board) is
                // painted the NEXT colour in the cycle — the current colour sits at the mouth where the
                // block enters, and the next colour queues behind it, so it reads as "loaded next". On the
                // door itself so it stays clearly visible (unlike the old tiny off-edge pips).
                float seg = thick / 3f;
                bool vertBar = d.Edge == Edge.Right || d.Edge == Edge.Left; // thick runs along X
                var nextSeg = MakeSprite("DoorNext", _boardRoot, VisualAssets.RoundedSquareSmall(), Color.white, 6);
                nextSeg.transform.localPosition = pos + outward * (thick * 0.5f - seg * 0.5f) + new Vector3(0f, 0f, -0.02f);
                nextSeg.drawMode = SpriteDrawMode.Sliced;
                nextSeg.size = vertBar ? new Vector2(seg, Mathf.Max(0.2f, scale.y - 0.14f))
                                       : new Vector2(Mathf.Max(0.2f, scale.x - 0.14f), seg);
                _doorViews.Add(new DoorView { Door = d, Bar = bar, NextSeg = nextSeg, Arrow = arrow, Pos = pos, Outward = outward });
            }
            RefreshExits();
        }

        // ---- blocks ----
        public void RefreshBlocks()
        {
            var alive = new HashSet<int>();
            foreach (var b in _board.Blocks) alive.Add(b.Id);
            var remove = new List<int>();
            foreach (var kv in _blocks) if (!alive.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (var id in remove)
            {
                if (_blocks[id].Root != null) Destroy(_blocks[id].Root.gameObject);
                _blocks.Remove(id);
            }

            foreach (var b in _board.Blocks)
            {
                if (!_blocks.TryGetValue(b.Id, out var bv) || bv.Root == null)
                    bv = CreateBlockView(b);
                ApplyBlockVisual(bv, b);
            }
        }

        private BlockView CreateBlockView(Block b)
        {
            var root = new GameObject($"Block_{b.Id}").transform;
            root.SetParent(_boardRoot, false);
            // one smooth sprite for the whole polyomino (rounded outer corners, no seams)
            var sr = MakeSprite("Body", root, VisualAssets.BlockShapeSprite(b.Shape), Color.white, 7);
            // inset the body slightly so two adjacent SAME-COLOR blocks show a clear seam instead of
            // reading as one connected shape (same-color blocks can't merge, so they must look separate).
            int bw = 1, bh = 1;
            foreach (var c in b.Shape) { if (c.X + 1 > bw) bw = c.X + 1; if (c.Y + 1 > bh) bh = c.Y + 1; }
            const float inset = 0.06f;
            sr.transform.localPosition = new Vector3(inset, -inset, 0f);
            sr.transform.localScale = new Vector3((bw - 2f * inset) / bw, (bh - 2f * inset) / bh, 1f);

            // axis-locked "rail" block: overlay a direction arrow along its allowed axis
            if (b.Axis != MoveAxis.Free)
            {
                int wc = 1, hc = 1;
                float sumx = 0f, sumy = 0f;
                foreach (var c in b.Shape) { sumx += c.X + 0.5f; sumy += c.Y + 0.5f; if (c.X + 1 > wc) wc = c.X + 1; if (c.Y + 1 > hc) hc = c.Y + 1; }
                int n = b.Shape.Length;
                var rail = MakeSprite("Rail", root, VisualAssets.DoubleArrow(), new Color(0.1f, 0.11f, 0.13f, 0.5f), 8);
                rail.transform.localPosition = new Vector3(sumx / n, -sumy / n, -0.1f);
                bool horiz = b.Axis == MoveAxis.Horizontal;
                var bn = rail.sprite.bounds.size;
                float along = (horiz ? wc : hc) * 0.66f, cross = (horiz ? hc : wc) * 0.34f;
                rail.transform.localRotation = horiz ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
                rail.transform.localScale = new Vector3(bn.x > 0 ? along / bn.x : 1f, bn.y > 0 ? cross / bn.y : 1f, 1f);
            }

            // lock overlay (shown only while the block is locked), centered on the shape
            float lsx = 0f, lsy = 0f;
            foreach (var c in b.Shape) { lsx += c.X + 0.5f; lsy += c.Y + 0.5f; }
            int ln = b.Shape.Length;
            // BRIGHT white padlock with a dark rim behind it, so it stays visible on ANY block color
            var lockSr = MakeSprite("Lock", root, VisualAssets.Padlock(), new Color(0.99f, 0.99f, 1f, 1f), 9);
            lockSr.transform.localPosition = new Vector3(lsx / ln, -lsy / ln, -0.2f);
            lockSr.transform.localScale = new Vector3(0.52f, 0.52f, 1f);
            var lockRim = MakeSprite("LockRim", lockSr.transform, VisualAssets.Padlock(), new Color(0.07f, 0.07f, 0.10f, 0.92f), 8);
            lockRim.transform.localScale = new Vector3(1.28f, 1.28f, 1f); // relative to the lock -> dark outline
            lockSr.gameObject.SetActive(false);

            var center = new Vector3(lsx / ln, -(lsy / ln), 0f); // block centroid (reuse lock centroid)

            // TIMER block: a big real-time seconds countdown (white with a dark outline so it reads
            // on any colour)
            TMP_Text fuse = null;
            if (b.IsTimer)
            {
                fuse = Ui.Text(root, "Fuse", center + new Vector3(0f, 0f, -0.3f), 6f, 11);
                fuse.fontStyle = FontStyles.Bold;
                fuse.color = Color.white;
                fuse.outlineWidth = 0.28f;
                fuse.outlineColor = new Color32(10, 12, 24, 255);
            }

            // COLOUR-CYCLING block: a white ring marks it as special, and a corner pip previews the
            // NEXT colour so the player can time the merge.
            SpriteRenderer cycleRing = null, nextPip = null;
            if (b.IsChameleon)
            {
                cycleRing = MakeSprite("CycleRing", root, VisualAssets.Ring(), new Color(1f, 1f, 1f, 0.9f), 8);
                cycleRing.transform.localPosition = center + new Vector3(0f, 0f, -0.05f);
                var rb2 = cycleRing.sprite.bounds.size;
                float ringSz = 0.86f;
                cycleRing.transform.localScale = new Vector3(rb2.x > 0 ? ringSz / rb2.x : 1f, rb2.y > 0 ? ringSz / rb2.y : 1f, 1f);

                nextPip = MakeSprite("NextPip", root, VisualAssets.RoundedSquare(), Color.white, 10);
                nextPip.transform.localPosition = center + new Vector3(0.28f, 0.28f, -0.3f);
                nextPip.transform.localScale = new Vector3(0.30f, 0.30f, 1f);
            }

            var bv = new BlockView { Root = root, Sr = sr, Lock = lockSr, Fuse = fuse, CycleRing = cycleRing, NextPip = nextPip };
            _blocks[b.Id] = bv;
            return bv;
        }

        private void ApplyBlockVisual(BlockView bv, Block b)
        {
            bv.Root.localPosition = TopLeftWorld(b.X, b.Y);
            if (bv.Lock != null) bv.Lock.gameObject.SetActive(b.Locked);
            UpdateDynamicVisual(bv, b);
        }

        // Body colour + timer number + chameleon markings. Does NOT touch position, so it is safe to
        // call every frame (real-time timer/colour) without fighting the drag/slide animation.
        private void UpdateDynamicVisual(BlockView bv, Block b)
        {
            // keep a locked block at FULL color (only the padlock marks it) so its color stays readable
            bv.Sr.color = VisualAssets.ToUnity(b.Color);

            // real-time timer: whole seconds remaining, flashing red as it runs out
            if (bv.Fuse != null)
            {
                bool live = b.IsTimer;
                bv.Fuse.gameObject.SetActive(live);
                if (live)
                {
                    bv.Fuse.text = Mathf.CeilToInt(b.TimerSeconds).ToString();
                    bv.Fuse.color = b.TimerSeconds <= 3f ? new Color(1f, 0.40f, 0.40f) : Color.white;
                }
            }

            // colour-cycling: the body already shows the current colour; refresh the next-colour pip,
            // and drop the special markings once it has merged into a stable block.
            if (bv.CycleRing != null) bv.CycleRing.gameObject.SetActive(b.IsChameleon);
            if (bv.NextPip != null)
            {
                bv.NextPip.gameObject.SetActive(b.IsChameleon);
                if (b.IsChameleon)
                    bv.NextPip.color = VisualAssets.ToUnity(b.Cycle[(b.CycleIndex + 1) % b.Cycle.Length]);
            }
        }

        // Real-time blocks (timers counting down, chameleons cycling) need their visuals refreshed
        // every frame, not just after a move.
        private void Update()
        {
            if (_board == null) return;
            foreach (var b in _board.Blocks)
            {
                if (!b.IsTimer && !b.IsChameleon) continue;
                if (_blocks.TryGetValue(b.Id, out var bv) && bv.Root != null)
                    UpdateDynamicVisual(bv, b);
            }
        }

        // ---- drag / animation support ----
        public void SetDragOffset(int blockId, float gridDX, float gridDY)
        {
            if (!_blocks.TryGetValue(blockId, out var bv) || bv.Root == null) return;
            if (!_board.TryGetBlock(blockId, out var b)) return;
            bv.Root.localPosition = TopLeftWorld(b.X, b.Y) + new Vector3(gridDX, -gridDY, 0f);
        }

        public bool TryGetBlockLocalPosition(int id, out Vector3 pos)
        {
            if (_blocks.TryGetValue(id, out var bv) && bv.Root != null) { pos = bv.Root.localPosition; return true; }
            pos = default; return false;
        }

        public void SetBlockLocalPosition(int id, Vector3 pos)
        {
            if (_blocks.TryGetValue(id, out var bv) && bv.Root != null) bv.Root.localPosition = pos;
        }

        public Transform DetachBlock(int id)
        {
            if (_blocks.TryGetValue(id, out var bv)) { _blocks.Remove(id); return bv.Root; }
            return null;
        }

        /// <summary>Hint: pulse the block that should move next and nudge it a few times in the
        /// suggested direction so the player sees WHICH block and WHICH way.</summary>
        // partnerId >= 0 when the hinted move MERGES with another block: both get a gold ring so the
        // player clearly sees WHICH two blocks combine and which way to push.
        // Active hint animation state. The hint NUDGES the real block's view to show the direction, so
        // it must be cancellable + always end by resyncing the view to the block's LOGICAL position —
        // otherwise a block moved (e.g. merged) while its hint is running gets its view slammed back to
        // the stale start position, leaving view≠logical so taps miss it ("block won't move").
        private Coroutine _hintCo;
        private int _hintMoverId = -1;
        private readonly List<GameObject> _hintRings = new List<GameObject>();

        public void HintEffect(int id, int dx, int dy, int partnerId = -1)
        {
            if (!_blocks.TryGetValue(id, out var bv) || bv.Root == null) return;
            StopHint();   // never overlap two hint animations
            _hintMoverId = id;
            _hintCo = StartCoroutine(HintHighlight(id, partnerId, dx, dy));
        }

        /// <summary>Cancel any running hint animation and snap the hinted block back to its LOGICAL
        /// cell (never a stale captured position). Safe to call any time — call it the moment the
        /// player touches the board so the nudge can't fight the drag or leave a desynced view.</summary>
        public void StopHint()
        {
            if (_hintCo != null) { StopCoroutine(_hintCo); _hintCo = null; }
            foreach (var r in _hintRings) if (r != null) Destroy(r);
            _hintRings.Clear();
            if (_hintMoverId >= 0) { ResyncBlock(_hintMoverId); _hintMoverId = -1; }
        }

        // Put a block's view exactly on its current logical cell (undoes any transient drag/hint nudge).
        private void ResyncBlock(int id)
        {
            if (_blocks.TryGetValue(id, out var bv) && bv.Root != null && _board.TryGetBlock(id, out var b))
                bv.Root.localPosition = TopLeftWorld(b.X, b.Y);
        }

        private IEnumerator HintHighlight(int moverId, int partnerId, int dx, int dy)
        {
            _hintRings.Clear();
            void AddRing(int bid)
            {
                var b = BlockById(bid);
                if (b == null) return;
                var ring = MakeSprite("HintRing", _boardRoot, VisualAssets.Ring(), new Color(1f, 0.84f, 0.2f, 0.95f), 9);
                ring.transform.position = BlockCenterWorld(b);
                var rb = ring.sprite.bounds.size;
                ring.gameObject.AddComponent<Pulser>().BaseScale = 1.4f / Mathf.Max(0.0001f, rb.x);
                _hintRings.Add(ring.gameObject);
            }
            AddRing(moverId);
            if (partnerId >= 0) AddRing(partnerId);
            StartCoroutine(Shockwave(BlockCenterWorld(BlockById(moverId)), new Color(1f, 0.9f, 0.35f)));

            // repeatedly nudge the mover toward its target so the direction is unmistakable
            if (_blocks.TryGetValue(moverId, out var bv) && bv.Root != null)
            {
                var basePos = bv.Root.localPosition;
                var dir = new Vector3(dx, -dy, 0f);
                float total = 2.6f, el = 0f;
                while (el < total && bv.Root != null)
                {
                    el += Time.deltaTime;
                    float k = Mathf.Max(0f, Mathf.Sin(el * 4.2f));
                    bv.Root.localPosition = basePos + dir * (0.26f * k);
                    yield return null;
                }
            }
            else yield return new WaitForSeconds(2.6f);

            // resync to the CURRENT logical cell (the block may have moved during the hint), never the
            // captured start pos, then clean up.
            ResyncBlock(moverId);
            foreach (var r in _hintRings) if (r != null) Destroy(r);
            _hintRings.Clear();
            _hintCo = null; _hintMoverId = -1;
        }

        private Block BlockById(int id)
        {
            foreach (var b in _board.Blocks) if (b.Id == id) return b;
            return null;
        }

        private static IEnumerator HintNudge(Transform t, Vector3 dir)
        {
            var basePos = t.localPosition;
            for (int rep = 0; rep < 3 && t != null; rep++)
            {
                float el = 0f; const float dur = 0.42f;
                while (el < dur && t != null)
                {
                    el += Time.deltaTime;
                    float k = Mathf.Sin(el / dur * Mathf.PI); // 0->1->0 out-and-back
                    t.localPosition = basePos + dir * (0.28f * k);
                    yield return null;
                }
            }
            if (t != null) t.localPosition = basePos;
        }

        /// <summary>Juice for a merge: squash-punch the surviving block + a shockwave ring.</summary>
        public void MergeEffect(int id, Vector3 worldCenter, Color col)
        {
            if (_blocks.TryGetValue(id, out var bv) && bv.Root != null)
                StartCoroutine(Punch(bv.Root));
            StartCoroutine(Shockwave(worldCenter, col));
        }

        /// <summary>Juice for a split: a white shockwave plus two puffs in the component colors,
        /// so a block breaking apart reads as "one became two".</summary>
        public void SplitEffect(int moverId, Vector3 worldCenter, Color a, Color b)
        {
            if (_blocks.TryGetValue(moverId, out var bv) && bv.Root != null)
                StartCoroutine(Punch(bv.Root));
            StartCoroutine(Shockwave(worldCenter, Color.white));
            ParticleBurst.Emit(worldCenter, a, 16, 7f);
            ParticleBurst.Emit(worldCenter, b, 16, 7f, 0.15f, 0.55f);
        }

        private static IEnumerator Punch(Transform t)
        {
            const float dur = 0.2f; float el = 0f; Vector3 baseScale = Vector3.one;
            while (el < dur && t != null)
            {
                el += Time.deltaTime;
                float p = el / dur;
                float s = 1f + 0.36f * Mathf.Sin(p * Mathf.PI);   // bulge up then settle
                t.localScale = baseScale * s;
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        // A wrong-colour block was shoved at a door — jolt THAT door (bar + arrow + pips) outward with a
        // quick decaying shake so it clearly reads as "this isn't your exit".
        public void DoorBump(int blockId, Vector3 worldDir)
        {
            Edge edge = worldDir.x > 0.5f ? Edge.Right : worldDir.x < -0.5f ? Edge.Left
                      : worldDir.y < -0.5f ? Edge.Bottom : Edge.Top;
            if (!_board.TryGetBlock(blockId, out var b)) return;
            bool horiz = edge == Edge.Left || edge == Edge.Right;
            DoorView match = null;
            foreach (var dv in _doorViews)
            {
                if (dv.Door.Edge != edge || dv.Door.Done) continue;
                foreach (var c in b.Cells())
                    if (dv.Door.Covers(horiz ? c.Y : c.X)) { match = dv; break; }
                if (match != null) break;
            }
            if (match != null) StartCoroutine(ShakeDoor(match));
        }

        private IEnumerator ShakeDoor(DoorView dv)
        {
            Vector3 barRest = dv.Bar.transform.localPosition;
            Vector3 arrowRest = dv.Arrow != null ? dv.Arrow.transform.localPosition : Vector3.zero;
            Vector3 nextRest = dv.NextSeg != null ? dv.NextSeg.transform.localPosition : Vector3.zero;

            const float dur = 0.32f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float off = Mathf.Sin(p * Mathf.PI * 4f) * 0.14f * (1f - p); // ~2 decaying cycles, outward
                Vector3 delta = dv.Outward * off;
                dv.Bar.transform.localPosition = barRest + delta;
                if (dv.Arrow != null) dv.Arrow.transform.localPosition = arrowRest + delta;
                if (dv.NextSeg != null) dv.NextSeg.transform.localPosition = nextRest + delta;
                yield return null;
            }
            dv.Bar.transform.localPosition = barRest;
            if (dv.Arrow != null) dv.Arrow.transform.localPosition = arrowRest;
            if (dv.NextSeg != null) dv.NextSeg.transform.localPosition = nextRest;
        }

        /// <summary>Exit juice: punch the door bar nearest the exit point and pop particles
        /// at its mouth, so a block leaving reads as being pushed THROUGH the door.</summary>
        public void ExitEffect(Vector3 nearWorld, Color col)
        {
            DoorView best = null; float bd = float.MaxValue;
            foreach (var dv in _doorViews)
            {
                float d = (dv.Pos - nearWorld).sqrMagnitude;
                if (d < bd) { bd = d; best = dv; }
            }
            if (best == null) return;
            if (best.Bar != null) StartCoroutine(DoorPunch(best.Bar.transform, best.Outward));
            ParticleBurst.Emit(best.Pos + best.Outward * 0.25f, Color.Lerp(col, Color.white, 0.35f), 18, 6f, 0.2f, 0.6f);
        }

        private static IEnumerator DoorPunch(Transform t, Vector3 outward)
        {
            Vector3 b0 = t.localScale;
            Vector3 startPos = t.localPosition;
            const float dur = 0.26f; float el = 0f;
            while (el < dur && t != null)
            {
                el += Time.deltaTime;
                float p = el / dur;
                float k = Mathf.Sin(p * Mathf.PI);          // 0->1->0
                t.localScale = new Vector3(b0.x * (1f + 0.55f * k), b0.y * (1f + 0.35f * k), b0.z);
                t.localPosition = startPos + outward * (0.18f * k); // recoil outward
                yield return null;
            }
            if (t != null) { t.localScale = b0; t.localPosition = startPos; }
        }

        private static IEnumerator Shockwave(Vector3 worldCenter, Color col)
        {
            var go = new GameObject("MergeShock");
            go.transform.position = new Vector3(worldCenter.x, worldCenter.y, -0.4f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VisualAssets.Ring();
            sr.sortingOrder = 80;
            Color bright = Color.Lerp(col, Color.white, 0.55f);
            const float dur = 0.32f; float el = 0f;
            while (el < dur)
            {
                el += Time.deltaTime;
                float p = el / dur;
                float s = Mathf.Lerp(0.5f, 2.1f, p);
                go.transform.localScale = new Vector3(s, s, 1f);
                sr.color = new Color(bright.r, bright.g, bright.b, 0.85f * (1f - p));
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Update each door's bar to its current color and show the next colors as pips.</summary>
        public void RefreshExits()
        {
            var hiddenCol = new Color(0.72f, 0.75f, 0.83f);
            foreach (var dv in _doorViews)
            {
                bool done = dv.Door.Done;
                dv.Bar.color = done ? new Color(0.74f, 0.77f, 0.84f) // soft light (not dark) on the bright board
                    : _doorsHidden ? hiddenCol : VisualAssets.ToUnity(dv.Door.CurrentColor);

                // Next-colour band: paint the inner third with the immediate upcoming colour. Hidden on a
                // finished door, in memory mode, or for a single-colour door (nothing upcoming).
                CarColor? next = null;
                if (!done && !_doorsHidden)
                    foreach (var c in dv.Door.Upcoming) { next = c; break; }
                if (dv.NextSeg != null)
                {
                    dv.NextSeg.gameObject.SetActive(next.HasValue);
                    if (next.HasValue) dv.NextSeg.color = VisualAssets.ToUnity(next.Value);
                }
            }
        }
    }
}
