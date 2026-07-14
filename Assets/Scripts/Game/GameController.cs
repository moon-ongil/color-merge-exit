using System.Collections;
using System.Collections.Generic;
using ColorMergeExit.Core;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Drives one level: owns the <see cref="GameSession"/>, wires the board/HUD views,
    /// reads pointer input (drag a block in any of the 4 directions), ticks the timer
    /// and resolves win/lose. Delivering a block to a matching exit peels its top color
    /// tier; the last tier removes it. No EventSystem / Input System dependency.
    /// </summary>
    [RequireComponent(typeof(BoardView))]
    public sealed class GameController : MonoBehaviour
    {
        private Camera _cam;
        private BoardView _board;
        private HudView _hud;
        private GameSprites _sprites;
        private GameSession _session;
        private LevelData _level;
        private int _levelId;
        private System.Action _onHome, _onNext;
        private bool _ended, _won;

        // drag state
        private bool _dragging;
        private int _dragBlockId;
        private Vector3 _pressWorld;
        private float _offX, _offY;
        private bool _axisX;
        private MoveAxis _dragAxis;
        private int _maxR, _maxL, _maxD, _maxU;
        private bool _exR, _exL, _exD, _exU;
        private bool _mgR, _mgL, _mgD, _mgU;
        private HudButton _pressedButton = HudButton.None;
        private Coroutine _snapTween;
        private GameObject _celebration;
        private TutorialOverlay _tutorial;
        private bool TutorialBlocking => _tutorial != null && _tutorial.Blocking;

        public void Configure(Camera cam, HudView hud, GameSprites sprites, System.Action onHome, System.Action onNext)
        {
            _cam = cam; _hud = hud; _sprites = sprites; _onHome = onHome; _onNext = onNext;
            _board = GetComponent<BoardView>();

            // bright pretty gradient behind everything (persists across levels)
            var bg = new GameObject("PageBg");
            bg.transform.SetParent(transform, false);
            bg.transform.position = new Vector3(0f, 0f, 1f);
            bg.transform.localScale = new Vector3(24f, 22f, 1f);
            var bgsr = bg.AddComponent<SpriteRenderer>();
            bgsr.sprite = VisualAssets.SoftGradient();
            bgsr.sortingOrder = -100;
        }

        public void Play(int levelId) { _levelId = levelId; StartLevel(); }

        private void StartLevel()
        {
            _ended = false; _won = false; _dragging = false; _pressedButton = HudButton.None;
            _hintReady = false; _hintPending = false;
            if (_tutorial != null) _tutorial.Stop();   // never carry a stale coach-mark into a new level
            _deadEndResult = -1; _lastAppliedGen = -1; _checkGen++; // discard any in-flight check from the last level
            if (_celebration != null) Destroy(_celebration);

            _level = LevelRepository.Load(_levelId);
            _session = new GameSession(_level);
            _board.Build(_session.Board, _sprites);
            FrameCamera();
            _hud.Build(_board.Width, _board.Height, _sprites, _levelId, _cam.orthographicSize, _level.timeLimitSeconds);
            _hud.SetTime(_session.TimeRemaining);
            _hud.HideBanner();

            StopAllCoroutines();
            _board.SetDoorsHidden(false);
            if (_level.memorize)
                StartCoroutine(MemorizeThenStart());
            else
            {
                _session.Start();
                MaybeShowTutorial();
            }
        }

        // ---- tutorials: levels 1-2 teach the basics; L3/L4 unlock the hint/+time items; the first
        // level with splitters teaches the split mechanic and unlocks the split item. Each fires once
        // (remembered in TutorialStore). The coach-mark freezes the clock + input while it is up.
        private void MaybeShowTutorial()
        {
            var steps = new List<TutorialStep>();
            var grants = new List<System.Action>();

            // Board-move demos (L1, L2, and the first splitter level) animate the actual winning
            // first move with a ghost hand + ghost block, so the player learns by watching the motion.
            bool boardDemo = false;
            if (_levelId == 1 && !TutorialStore.Seen("intro1")) { boardDemo = true; grants.Add(() => TutorialStore.MarkSeen("intro1")); }
            if (_levelId == 2 && !TutorialStore.Seen("intro2")) { boardDemo = true; grants.Add(() => TutorialStore.MarkSeen("intro2")); }
            bool splitLevel = _level.splitters.Count > 0 && !TutorialStore.Seen("split");
            if (splitLevel) boardDemo = true;

            if (boardDemo && TryDragDemo(out var dragStep)) steps.Add(dragStep);

            // Item unlocks: a hand taps the item button, then that item unlocks (hint→L3, +time→L4,
            // split→first splitter level).
            if (_levelId >= 3 && !TutorialStore.Seen("item_hint") && !ItemStore.IsUnlocked(ItemType.Hint))
            {
                steps.Add(TutorialStep.Tap(_hud.HintButtonWorld));
                grants.Add(() => { TutorialStore.MarkSeen("item_hint"); ItemStore.Unlock(ItemType.Hint); _hud.RefreshItems(); });
            }
            if (_levelId >= 4 && !TutorialStore.Seen("item_addtime") && !ItemStore.IsUnlocked(ItemType.AddTime))
            {
                steps.Add(TutorialStep.Tap(_hud.AddTimeButtonWorld));
                grants.Add(() => { TutorialStore.MarkSeen("item_addtime"); ItemStore.Unlock(ItemType.AddTime); _hud.RefreshItems(); });
            }
            if (splitLevel)
            {
                steps.Add(TutorialStep.Tap(_hud.ForceSplitButtonWorld));
                grants.Add(() => { TutorialStore.MarkSeen("split"); ItemStore.Unlock(ItemType.ForceSplit); _hud.RefreshItems(); });
            }

            if (steps.Count == 0) return;

            if (_tutorial == null)
            {
                var go = new GameObject("Tutorial");
                go.transform.SetParent(transform, false);
                _tutorial = go.AddComponent<TutorialOverlay>();
            }
            _tutorial.Play(_cam, steps, () => { foreach (var g in grants) g(); });
        }

        private static readonly (int dx, int dy)[] _demoDirs = { (0, -1), (0, 1), (-1, 0), (1, 0) };

        // Build the drag-demo step. Level 1 shows the simplest lesson — a block sliding straight OUT
        // its matching exit (no merge). Level 2+ shows a MERGE move (two colors combining). Falls back
        // to the other kind, then to the solver's first move, if the preferred kind isn't available.
        private bool TryDragDemo(out TutorialStep step)
        {
            bool preferMerge = _levelId >= 2;
            if (TryFindDemoMove(preferMerge, out step)) return true;
            if (TryFindDemoMove(!preferMerge, out step)) return true;
            var m = Solver.Hint(Solver.Capture(_session.Board), 60000);
            if (m.Found) return BuildDemo(m.Id, m.Dx, m.Dy, out step);
            step = default;
            return false;
        }

        private bool TryFindDemoMove(bool wantMerge, out TutorialStep step)
        {
            foreach (var b in _session.Board.Blocks)
                foreach (var (dx, dy) in _demoDirs)
                {
                    int cells = _session.Board.MaxSlide(b.Id, dx, dy, out bool exit, out bool merge, out _);
                    if (cells < 0) continue;
                    if (wantMerge ? merge : exit) return BuildDemo(b.Id, dx, dy, out step);
                }
            step = default;
            return false;
        }

        private bool BuildDemo(int id, int dx, int dy, out TutorialStep step)
        {
            step = default;
            if (!_session.Board.TryGetBlock(id, out var b)) return false;
            int cells = _session.Board.MaxSlide(id, dx, dy, out bool exit, out bool merge, out _);
            int dist = Mathf.Clamp(cells + ((exit || merge) ? 1 : 0), 1, 5);
            Vector3 from = _board.BlockCenter(b);
            Vector3 unit = _board.CellCenterWorld(dx, dy) - _board.CellCenterWorld(0, 0);
            step = TutorialStep.Drag(from, from + unit * dist, VisualAssets.ToUnity(b.Color));
            return true;
        }

        // Memory mode: show door colors briefly, then hide them and start the clock.
        private IEnumerator MemorizeThenStart()
        {
            _hud.ShowBanner(Localization.Get(LocKeys.Memorize), new Color(0.95f, 0.9f, 0.5f));
            yield return new WaitForSeconds(2.6f);
            _hud.HideBanner();
            _board.SetDoorsHidden(true);
            _session.Start();
        }

        private void FrameCamera()
        {
            _cam.orthographic = true;
            float aspect = _cam.aspect <= 0f ? 0.5625f : _cam.aspect;
            float sizeForHeight = _board.Height * 0.5f + 1.9f;
            float sizeForWidth = (_board.Width * 0.5f + 1.0f) / aspect;
            _cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            _cam.backgroundColor = new Color(0.90f, 0.84f, 0.98f); // matches the gradient's bottom
        }

        private void Update()
        {
            if (_session == null) return;
            // popups AND the tutorial coach-mark pause the clock
            if (_session.State == SessionState.Playing && !_hud.SettingsOpen && !_hud.InfoOpen
                && !TutorialBlocking && !_hud.ConfirmOpen)
            {
                _session.Tick(Time.deltaTime);
                _hud.SetTime(_session.TimeRemaining);
                if (_session.State == SessionState.Lost) OnLost();
            }
            ApplyDeadEndResult();
            ApplyHintResult();
            if (TutorialBlocking) return;   // swallow game input while a coach-mark is up
            HandlePointer();
        }

        // ---- items: hint (solver first move) + add time ----
        private volatile bool _hintReady, _hintPending;
        private Solver.Move _hintMove;

        private void DoHint()
        {
            if (_session.State != SessionState.Playing || _hintPending) return;
            if (!_hud.IsUnlocked(ItemType.Hint)) return;   // locked until its tutorial
            if (_hud.HintCount <= 0) { AdManager.ShowRewarded(() => _hud.AddHint(3)); return; } // out -> rewarded ad
            // Don't spend the charge yet — the solver runs off-thread and may not return a move.
            // The charge is consumed in ApplyHintResult ONLY when a hint is actually shown, so a
            // failed search never silently eats a hint.
            _hintPending = true;
            var snap = Solver.Capture(_session.Board); // main thread reads the board
            System.Threading.Tasks.Task.Run(() =>
            {
                var m = Solver.Hint(snap, 150000);
                _hintMove = m;
                _hintReady = true; // publish last
            });
        }

        private void ApplyHintResult()
        {
            if (!_hintReady) return;
            _hintReady = false; _hintPending = false;
            if (!_hintMove.Found || _ended || _session.State != SessionState.Playing) return;
            if (!_hud.UseHint()) return; // spend one charge now that a move was found
            _board.HintEffect(_hintMove.Id, _hintMove.Dx, _hintMove.Dy, MergePartnerOf(_hintMove.Id, _hintMove.Dx, _hintMove.Dy));
        }

        // If the hinted move merges, return the partner block's id (for a clear "these two combine"
        // highlight); otherwise -1.
        private int MergePartnerOf(int id, int dx, int dy)
        {
            if (!_session.Board.TryGetBlock(id, out var b)) return -1;
            int max = _session.Board.MaxSlide(id, dx, dy, out _, out bool merge, out _);
            if (!merge) return -1;
            foreach (var c in b.Cells())
            {
                int px = c.X + dx * (max + 1), py = c.Y + dy * (max + 1);
                foreach (var other in _session.Board.Blocks)
                {
                    if (other.Id == id) continue;
                    foreach (var oc in other.Cells())
                        if (oc.X == px && oc.Y == py) return other.Id;
                }
            }
            return -1;
        }

        private void DoAddTime()
        {
            if (_session.State != SessionState.Playing) return;
            if (!_hud.IsUnlocked(ItemType.AddTime)) return;   // locked until its tutorial
            if (_hud.AddTimeCount <= 0) { AdManager.ShowRewarded(() => _hud.AddAddTime(3)); return; } // out -> rewarded ad
            _hud.UseAddTime();
            _session.AddTime(HudView.AddTimeSeconds);
            _hud.SetTime(_session.TimeRemaining);
            AudioManager.Instance?.Merge(1.35f);
            ParticleBurst.Emit(new Vector3(0f, 0.5f, 0f), new Color(0.4f, 0.9f, 0.55f), 24, 7f);
        }

        // force-split item: one tap splits EVERY splittable (mixed-colour) block on the board at once.
        private void UseForceSplitAll()
        {
            if (_session.State != SessionState.Playing) return;
            if (!_hud.IsUnlocked(ItemType.ForceSplit)) return;   // locked until its tutorial
            if (_hud.ForceSplitCount <= 0) { AdManager.ShowRewarded(() => _hud.AddForceSplit(3)); return; } // out -> rewarded ad

            // Snapshot the ids of currently splittable blocks first (ForceSplit adds twins mid-loop).
            var targets = new List<int>();
            foreach (var b in _session.Board.Blocks)
                if (ColorMix.TrySplit(b.Color, out _, out _)) targets.Add(b.Id);
            if (targets.Count == 0) return;   // nothing to split -> don't spend the item

            int split = 0;
            foreach (var id in targets)
            {
                if (!_session.Board.TryGetBlock(id, out var b)) continue;
                var preColor = b.Color;
                var center = _board.BlockCenter(b);
                if (!_session.Board.ForceSplit(id)) continue;  // no adjacent room -> skip this one
                Color ca = VisualAssets.ToUnity(preColor), cb = ca;
                if (ColorMix.TrySplit(preColor, out var k1, out var k2))
                { ca = VisualAssets.ToUnity(k1); cb = VisualAssets.ToUnity(k2); }
                _board.SplitEffect(id, center, ca, cb);
                split++;
            }
            if (split == 0) return;            // splittable existed but no room anywhere -> keep the item

            _hud.UseForceSplit();              // one use splits all reachable blocks
            _board.RefreshBlocks();
            _board.RefreshExits();
            AudioManager.Instance?.Merge(0.72f);
            CheckDeadEnd();
        }

        // ---- input ----
        private void HandlePointer()
        {
            bool down = false, up = false, held = false;
            Vector3 pos;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase == TouchPhase.Began;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
            }
            else
            {
                pos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                up = Input.GetMouseButtonUp(0);
                held = Input.GetMouseButton(0);
            }
            pos.z = 10f;

            if (down) OnPress(pos);
            else if (up) OnRelease(pos);
            else if (held && _dragging) OnDrag(pos);
        }

        private void OnPress(Vector3 screenPos)
        {
            var world = _board.ScreenToWorld(_cam, screenPos);

            // Settings popup is modal: consume all taps while it's open.
            if (_hud.SettingsOpen)
            {
                var hit = _hud.PopupHit(world);
                if (hit != HudView.SettingsHit.None) AudioManager.Instance?.Tap();
                if (hit == HudView.SettingsHit.Sound) _hud.ToggleSound();
                else if (hit == HudView.SettingsHit.Restart) { _hud.CloseSettings(); _hud.ShowConfirm("restart", "RESTART?"); }
                else if (hit == HudView.SettingsHit.Exit) { _hud.CloseSettings(); _hud.ShowConfirm("exit", "EXIT?"); }
                else if (hit == HudView.SettingsHit.Language) _hud.ToggleLanguage();
                else if (hit == HudView.SettingsHit.Close || hit == HudView.SettingsHit.Backdrop) _hud.CloseSettings();
                _pressedButton = HudButton.None;
                return;
            }

            // Info (color-mix) popup is modal too: tap the button or outside to dismiss.
            if (_hud.InfoOpen)
            {
                var hit = _hud.InfoPopupHit(world);
                if (hit != HudView.SettingsHit.None) AudioManager.Instance?.Tap();
                if (hit == HudView.SettingsHit.Close || hit == HudView.SettingsHit.Backdrop) _hud.CloseInfo();
                _pressedButton = HudButton.None;
                return;
            }

            // Result overlay is modal: only its own buttons respond (handled on release).
            if (_hud.ResultOpen) { _pressedButton = HudButton.None; return; }

            // Confirm dialog is modal: YES runs the pending action (restart / exit — both cost a heart).
            if (_hud.ConfirmOpen)
            {
                var ch = _hud.ConfirmHit(world);
                if (ch != HudView.ConfirmResult.None) AudioManager.Instance?.Tap();
                if (ch == HudView.ConfirmResult.Yes)
                {
                    string id = _hud.ConfirmId;
                    _hud.HideConfirm();
                    HeartStore.Spend();                 // both restart and exit forfeit a life
                    if (id == "exit") _onHome?.Invoke();
                    else StartLevel();
                }
                else if (ch == HudView.ConfirmResult.No) _hud.HideConfirm();
                _pressedButton = HudButton.None;
                return;
            }

            var btn = _hud.ButtonAtWorld(world);
            if (btn != HudButton.None) { _pressedButton = btn; return; }
            _pressedButton = HudButton.None;

            if (_session.State != SessionState.Playing) return;

            if (_board.TryScreenToCell(_cam, screenPos, out int cx, out int cy))
            {
                var b = _board.BlockAtCell(cx, cy);

                if (b != null)
                {
                    if (_snapTween != null) { StopCoroutine(_snapTween); _snapTween = null; }
                    _dragging = true;
                    _dragBlockId = b.Id;
                    _pressWorld = world;
                    _offX = _offY = 0f;
                    _axisX = true;
                    _dragAxis = b.Axis;
                    // splitter cells halt a slide within maxslide (no +1 push), so the reachesSplit
                    // flag is informational only — the drag clamp already lets the block reach them.
                    _maxR = _session.Board.MaxSlide(b.Id, 1, 0, out _exR, out _mgR, out _);
                    _maxL = _session.Board.MaxSlide(b.Id, -1, 0, out _exL, out _mgL, out _);
                    _maxD = _session.Board.MaxSlide(b.Id, 0, 1, out _exD, out _mgD, out _);
                    _maxU = _session.Board.MaxSlide(b.Id, 0, -1, out _exU, out _mgU, out _);
                }
            }
        }

        private void OnDrag(Vector3 screenPos)
        {
            if (!_session.Board.TryGetBlock(_dragBlockId, out _)) { _dragging = false; return; }
            var world = _board.ScreenToWorld(_cam, screenPos);
            float gdx = world.x - _pressWorld.x;          // grid +x = right
            float gdy = -(world.y - _pressWorld.y);        // grid +y = down

            _axisX = UseXAxis(gdx, gdy);
            if (_axisX)
            {
                float hi = _maxR + ((_exR || _mgR) ? 1f : 0f), lo = -(_maxL + ((_exL || _mgL) ? 1f : 0f));
                _offX = Mathf.Clamp(gdx, lo, hi); _offY = 0f;
            }
            else
            {
                float hi = _maxD + ((_exD || _mgD) ? 1f : 0f), lo = -(_maxU + ((_exU || _mgU) ? 1f : 0f));
                _offY = Mathf.Clamp(gdy, lo, hi); _offX = 0f;
            }
            _board.SetDragOffset(_dragBlockId, _offX, _offY);
        }

        /// <summary>Pick the drag axis: forced along the rail for axis-locked blocks,
        /// otherwise the dominant pointer axis.</summary>
        private bool UseXAxis(float gdx, float gdy)
        {
            if (_dragAxis == MoveAxis.Horizontal) return true;
            if (_dragAxis == MoveAxis.Vertical) return false;
            return Mathf.Abs(gdx) >= Mathf.Abs(gdy);
        }

        private void OnRelease(Vector3 screenPos)
        {
            var world = _board.ScreenToWorld(_cam, screenPos);

            if (_pressedButton != HudButton.None)
            {
                if (_hud.ButtonAtWorld(world) == _pressedButton)
                {
                    AudioManager.Instance?.Tap();
                    if (_pressedButton == HudButton.Restart) _hud.ShowConfirm("restart", "RESTART?");
                    else if (_pressedButton == HudButton.Undo) DoUndo();
                    else if (_pressedButton == HudButton.Home) _onHome?.Invoke();
                    else if (_pressedButton == HudButton.Hint) DoHint();
                    else if (_pressedButton == HudButton.AddTime) DoAddTime();
                    else if (_pressedButton == HudButton.ForceSplit) UseForceSplitAll();
                    else if (_pressedButton == HudButton.Settings) _hud.ToggleSettings();
                    else if (_pressedButton == HudButton.Info) _hud.ToggleInfo();
                }
                _pressedButton = HudButton.None;
                return;
            }

            if (_ended)
            {
                var rb = _hud.ResultHit(world);
                if (rb == HudView.ResultButton.None) return;   // ignore taps off the buttons (modal)
                AudioManager.Instance?.Tap();
                if (rb == HudView.ResultButton.Next) _onNext?.Invoke();
                else if (rb == HudView.ResultButton.Retry) StartLevel();
                else if (rb == HudView.ResultButton.Exit) _onHome?.Invoke();
                else if (rb == HudView.ResultButton.Refill)
                    AdManager.ShowRewarded(() => { HeartStore.Refill(); StartLevel(); });
                return;
            }

            if (!_dragging) return;
            _dragging = false;
            bool moved = false;

            if (_session.Board.TryGetBlock(_dragBlockId, out var b) && _session.State == SessionState.Playing)
            {
                float gdx = world.x - _pressWorld.x, gdy = -(world.y - _pressWorld.y);
                int stepX = 0, stepY = 0;
                if (UseXAxis(gdx, gdy))
                {
                    bool pR = _exR || _mgR, pL = _exL || _mgL;
                    float off = Mathf.Clamp(gdx, -(_maxL + (pL ? 1f : 0f)), _maxR + (pR ? 1f : 0f));
                    if (pR && off >= _maxR + 0.5f) stepX = _maxR + 1;
                    else if (pL && off <= -(_maxL + 0.5f)) stepX = -(_maxL + 1);
                    else stepX = Mathf.RoundToInt(Mathf.Clamp(off, -_maxL, _maxR));
                }
                else
                {
                    bool pD = _exD || _mgD, pU = _exU || _mgU;
                    float off = Mathf.Clamp(gdy, -(_maxU + (pU ? 1f : 0f)), _maxD + (pD ? 1f : 0f));
                    if (pD && off >= _maxD + 0.5f) stepY = _maxD + 1;
                    else if (pU && off <= -(_maxU + 0.5f)) stepY = -(_maxU + 1);
                    else stepY = Mathf.RoundToInt(Mathf.Clamp(off, -_maxU, _maxD));
                }

                if (stepX != 0 || stepY != 0)
                {
                    var color = b.Color;
                    var center = _board.BlockCenter(b);
                    var result = _session.Move(_dragBlockId, stepX, stepY);
                    if (result == MoveResult.Exited)
                    {
                        AudioManager.Instance?.Exit();
                        Vector3 wdir = stepX != 0
                            ? new Vector3(Mathf.Sign(stepX), 0f, 0f)
                            : new Vector3(0f, -Mathf.Sign(stepY), 0f);
                        var t = _board.DetachBlock(_dragBlockId);
                        Vector3 mouth = t != null ? t.position : center;
                        _board.ExitEffect(mouth, VisualAssets.ToUnity(color));
                        if (t != null) StartCoroutine(ExitSlide(t, wdir));
                    }
                    else if (result == MoveResult.Merged)
                    {
                        Color mixColor = VisualAssets.ToUnity(color);
                        bool advanced = false;
                        if (_session.Board.TryGetBlock(_dragBlockId, out var mb))
                        {
                            mixColor = VisualAssets.ToUnity(mb.Color);
                            // tertiary/brown (chain) merges ping brighter
                            advanced = !ColorMix.IsPrimary(mb.Color) && !ColorMix.IsSecondary(mb.Color);
                        }
                        AudioManager.Instance?.Merge(advanced ? 1.22f : 1f);
                        _board.MergeEffect(_dragBlockId, center, mixColor);
                        ParticleBurst.Emit(center, mixColor, 30, 8f);
                        ParticleBurst.Emit(center, Color.white, 10, 5f, 0.18f, 0.5f);
                    }
                    else if (result == MoveResult.Split)
                    {
                        // a mixed block broke back into its two component colors — puff them apart
                        Color puffA = VisualAssets.ToUnity(color), puffB = puffA;
                        if (ColorMix.TrySplit(color, out var k1, out var k2))
                        { puffA = VisualAssets.ToUnity(k1); puffB = VisualAssets.ToUnity(k2); }
                        AudioManager.Instance?.Merge(0.72f); // lower-pitched "un-merge" ding
                        _board.SplitEffect(_dragBlockId, center, puffA, puffB);
                    }
                    else if (result == MoveResult.Moved)
                    {
                        AudioManager.Instance?.Slide();
                    }
                    moved = result == MoveResult.Moved || result == MoveResult.Merged
                        || result == MoveResult.Split || result == MoveResult.Exited;
                }
            }

            bool haveFrom = _board.TryGetBlockLocalPosition(_dragBlockId, out var fromPos);
            _board.RefreshBlocks();
            _board.RefreshExits();
            if (haveFrom && _board.TryGetBlockLocalPosition(_dragBlockId, out var toPos))
            {
                if (_snapTween != null) StopCoroutine(_snapTween);
                _snapTween = StartCoroutine(SnapEase(_dragBlockId, fromPos, toPos, 0.09f));
            }

            if (_session.State == SessionState.Won) OnWon();
            else if (moved) CheckDeadEnd();
        }

        // Dead-end detection runs the (potentially slow) solver on a BACKGROUND thread so it
        // never stalls a frame. The verdict is published as ONE packed int (gen<<1 | stuck) via a
        // monotonic compare-and-swap, so a slower OLDER check can never clobber a newer check's
        // result. (Two separate fields raced here: on bigger/slower 7x7 boards an older "solvable"
        // task finishing late overwrote a newer "stuck" verdict, dropping a real dead end.)
        private int _checkGen;
        private int _lastAppliedGen = -1;
        private volatile int _deadEndResult = -1; // -1 = none; else (gen<<1) | (stuck ? 1 : 0)

        private void CheckDeadEnd()
        {
            if (_ended) return;
            int gen = ++_checkGen;
            var snap = Solver.Capture(_session.Board); // main thread reads the board
            System.Threading.Tasks.Task.Run(() =>
            {
                bool stuck;
                try { stuck = !Solver.IsSolvable(snap, 200000); }
                catch { return; } // never let a solver error silently kill detection
                int packed = (gen << 1) | (stuck ? 1 : 0);
                int cur;
                do
                {
                    cur = _deadEndResult;
                    if (cur >= 0 && (cur >> 1) >= gen) return; // a newer verdict already published
                }
                while (System.Threading.Interlocked.CompareExchange(ref _deadEndResult, packed, cur) != cur);
            });
        }

        private void ApplyDeadEndResult()
        {
            int r = _deadEndResult;
            if (r < 0) return;
            int gen = r >> 1;
            bool stuck = (r & 1) != 0;
            if (gen == _lastAppliedGen) return;                                          // already handled
            if (gen != _checkGen || _ended || _session.State != SessionState.Playing) return; // stale
            _lastAppliedGen = gen;
            if (stuck)
                OnDeadEnd();   // board is a PROVEN dead end -> fail immediately, don't wait for the clock
            else
                _hud.HideBanner();
        }

        // A proven-unsolvable board is an immediate failure: stop the clock and show the fail
        // dialog right away (tap to retry) instead of leaving the player stuck until time runs out.
        private void OnDeadEnd()
        {
            if (_ended) return;
            _ended = true; _won = false;
            HeartStore.Spend();   // a failure costs a life
            _session.Abort();
            _hud.ShowResult(false, HeartStore.HasHeart, 0, Localization.Get(LocKeys.DeadEnd), new Color(0.98f, 0.5f, 0.5f));
            AudioManager.Instance?.Lose();
        }

        private IEnumerator SnapEase(int id, Vector3 from, Vector3 to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                k = 1f - (1f - k) * (1f - k) * (1f - k);
                _board.SetBlockLocalPosition(id, Vector3.Lerp(from, to, k));
                yield return null;
            }
            _board.SetBlockLocalPosition(id, to);
            _snapTween = null;
        }

        // CBJ-style thread-through: the block pauses a hair at the mouth, then accelerates out
        // while squeezing narrow (perpendicular to travel) and fading — like being pushed through.
        private IEnumerator ExitSlide(Transform block, Vector3 worldDir)
        {
            var srs = block.GetComponentsInChildren<SpriteRenderer>();
            bool horiz = Mathf.Abs(worldDir.x) > 0.5f;
            var start = block.localPosition;
            var end = start + worldDir * 4.2f;
            float t = 0f; const float dur = 0.36f;
            while (t < dur && block != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                block.localPosition = Vector3.Lerp(start, end, p * p);              // accelerate out
                float squeeze = Mathf.Lerp(1f, 0.28f, p);                           // narrow into the slot
                block.localScale = horiz ? new Vector3(1f, squeeze, 1f) : new Vector3(squeeze, 1f, 1f);
                float a = 1f - Mathf.Clamp01((p - 0.45f) / 0.55f);                  // fade out in the 2nd half
                foreach (var sr in srs) if (sr != null) { var c = sr.color; c.a = a; sr.color = c; }
                yield return null;
            }
            if (block != null) Destroy(block.gameObject);
        }

        private void DoUndo()
        {
            if (_session.Undo())
            {
                _board.RefreshBlocks();
                _board.RefreshExits();
                CheckDeadEnd(); // undo may have escaped the dead end -> clear the warning
            }
        }

        private void OnWon()
        {
            _ended = true; _won = true;
            int stars = _session.Stars();
            ProgressStore.RecordClear(_levelId, stars, _session.TimeRemaining);
            _hud.ShowResult(true, true, stars, Localization.Get(LocKeys.Clear), new Color(0.4f, 0.92f, 0.55f));
            AudioManager.Instance?.Win();
            ParticleBurst.Emit(new Vector3(0f, 0.5f, 0f), new Color(1f, 0.85f, 0.3f), 70, 10f, 0.3f, 1.2f);
            StartCoroutine(StarCelebration(stars));
        }

        // Three stars bounce in one-by-one above the board; earned ones flash gold with an
        // ascending ding + sparkle, missed ones settle dim.
        private IEnumerator StarCelebration(int stars)
        {
            if (_celebration != null) Destroy(_celebration);
            _celebration = new GameObject("StarCelebration");
            const float y = 2.1f;
            var slots = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"Star{i}");
                go.transform.SetParent(_celebration.transform);
                go.transform.position = new Vector3((i - 1) * 0.95f, y, -0.5f);
                go.transform.localScale = Vector3.zero;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = VisualAssets.Star();
                sr.sortingOrder = 130;
                sr.color = new Color(0.28f, 0.28f, 0.32f, 0.9f);
                slots[i] = go.transform;
            }
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 3; i++)
            {
                var tr = slots[i];
                if (tr == null) yield break;
                if (i < stars)
                {
                    tr.GetComponent<SpriteRenderer>().color = new Color(1f, 0.82f, 0.22f, 1f);
                    AudioManager.Instance?.Merge(1.05f + i * 0.18f);
                    ParticleBurst.Emit(tr.position, new Color(1f, 0.9f, 0.45f), 16, 5f, 0.2f, 0.7f);
                }
                yield return StartCoroutine(PopIn(tr, 0.62f));
                yield return new WaitForSeconds(0.13f);
            }
        }

        private static IEnumerator PopIn(Transform tr, float finalScale)
        {
            const float dur = 0.3f; float el = 0f;
            while (el < dur && tr != null)
            {
                el += Time.deltaTime;
                float p = el / dur;
                float s = p < 0.7f ? Mathf.Lerp(0f, 1.28f, p / 0.7f)
                                   : Mathf.Lerp(1.28f, 1f, (p - 0.7f) / 0.3f); // overshoot then settle
                tr.localScale = Vector3.one * (finalScale * s);
                yield return null;
            }
            if (tr != null) tr.localScale = Vector3.one * finalScale;
        }

        private void OnLost()
        {
            if (_ended) return;
            _ended = true; _won = false;
            HeartStore.Spend();   // a failure costs a life
            // a timer block that detonated shows a distinct message from a plain clock timeout
            string msg = _session.Board.Detonated ? LocKeys.Detonated : LocKeys.TimeUp;
            _hud.ShowResult(false, HeartStore.HasHeart, 0, Localization.Get(msg), new Color(0.98f, 0.5f, 0.5f));
            AudioManager.Instance?.Lose();
        }

    }
}
