using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// World-space stage select drawn as a WINDING PATH that climbs upward (Candy-Crush style):
    /// big round level "nodes" sway left/right along a sine curve, connected by a dotted trail that
    /// is gold where already travelled and muted ahead. Drag vertically to walk the path; tap an
    /// unlocked node to play. Title + mute stay fixed on top. Built from primitives + TMP.
    /// </summary>
    public sealed class LevelSelectView : MonoBehaviour
    {
        // Path geometry: level 1 sits at the BOTTOM, higher levels climb UP a SMOOTH SERPENTINE.
        // Nodes sit on the alternating peaks of a cosine wave (x = -Amp*cos(k*pi) = -/+Amp), and the
        // trail traces that same continuous cosine between them -> a flowing S-curve, not straight
        // zigzag lines. The alternation keeps each node's stars clear of the node below.
        private const float Step = 1.55f;     // vertical spacing between consecutive nodes
        private const float Amp = 1.7f;       // horizontal peak offset of the serpentine
        private const int TrailDots = 5;      // small path dots per gap (trace the curve)
        private const float NodeR = 0.66f;    // node hit radius
        private const float DragThreshold = 0.35f;

        // Continuous serpentine X for a fractional level index u (u = level-1). Integer u lands a
        // node on a +/-Amp peak; the curve smoothly passes through centre at each half-step.
        private static float PathX(float u) => -Amp * Mathf.Cos(u * Mathf.PI);

        private Camera _cam;
        private GameSprites _sprites;
        private System.Action<int> _onSelect;
        private readonly List<(int level, Vector2 local)> _hit = new List<(int, Vector2)>();
        private Vector3 _muteBtnPos;
        private bool _hasMute;
        private float _halfH = 6.2f, _halfW = 3.5f, _safeTop = 6.2f;

        private Transform _content;   // holds the scrolling path
        private float _contentY, _offMin, _offMax;

        // pointer/drag state
        private bool _pressing, _dragging, _pressedMute;
        private Vector3 _pressWorld;
        private float _pressContentY;

        public void Build(Camera cam, GameSprites sprites, System.Action<int> onSelect)
        {
            _cam = cam;
            _sprites = sprites;
            _onSelect = onSelect;
        }

        // Node position in content-local space: on the serpentine peak for this level.
        private static Vector2 NodeLocal(int level)
        {
            int k = level - 1;
            return new Vector2(PathX(k), k * Step);
        }

        // True if point p lands in the vertical strip directly under a node — the gap that holds the
        // node's 3-star row. A trail dot here reads as a stray ball hovering above the stars, so we
        // drop it. (The trail still leaves the TOP of each node upward, so the path stays connected.)
        private static bool OverStars(Vector3 p, int level, int n)
        {
            if (level < 1 || level > n) return false;
            Vector2 np = NodeLocal(level);
            return Mathf.Abs(p.x - np.x) < 0.7f && p.y < np.y - 0.22f && p.y > np.y - 1.15f;
        }

        public void Show()
        {
            FrameCamera();
            int n = ProgressStore.TotalLevels;
            float footerY = -_halfH + 2.4f;            // where level 1 rests at the bottom of the trail
            // `usable` is chosen so that at the top of the scroll the highest level lands at
            // (_safeTop - 2.9) — just below the fixed header band — instead of hiding behind it.
            float usable = _safeTop + _halfH - 6.1f;
            float total = (n - 1) * Step;
            _offMax = footerY;                          // least-climbed (level 1 low on screen)
            _offMin = footerY - Mathf.Max(0f, total - usable);   // most-climbed (top levels in view)

            // focus the current level near screen centre so the player sees "where they are"
            int cur = Mathf.Clamp(ProgressStore.Unlocked, 1, n);
            _contentY = Mathf.Clamp(0f - NodeLocal(cur).y, _offMin, _offMax);
            Rebuild();
        }

        private void FrameCamera()
        {
            _cam.orthographic = true;
            float aspect = _cam.aspect <= 0f ? 0.5625f : _cam.aspect;
            _halfH = Mathf.Max(6.2f, (Amp + 1.0f) / aspect);   // guarantee the sway fits width-wise
            _halfW = _halfH * aspect;
            _cam.orthographicSize = _halfH;
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            _cam.backgroundColor = new Color(0.90f, 0.84f, 0.98f);

            // Keep the header below the iPhone camera cutout / notch (safe area top inset).
            float topInsetPx = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
            float worldPerPx = Screen.height > 0 ? (2f * _halfH) / Screen.height : 0f;
            _safeTop = _halfH - topInsetPx * worldPerPx - 0.25f;
        }

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _hit.Clear();

            // bright gradient behind everything
            var bg = MakeSprite(transform, "PageBg", new Vector3(0f, 0f, 1f), Color.white, -100);
            bg.sprite = VisualAssets.SoftGradient();
            bg.transform.localScale = new Vector3(30f, 30f, 1f);

            // scrolling content root (built first so the fixed header sorts above it)
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(transform, false);
            _content = contentGo.transform;
            _content.localPosition = new Vector3(0f, _contentY, 0f);

            int n = ProgressStore.TotalLevels;
            int unlocked = ProgressStore.Unlocked;

            // Only build nodes/dots near the visible window (500 nodes -> a few thousand renderers
            // otherwise). The window is generous so a drag doesn't reveal unbuilt gaps before the
            // next Rebuild; Rebuild runs on release of a scroll (see Update).
            float viewLocalCenter = -_contentY;            // content-local y currently at screen centre
            float halfWin = _halfH + 3f * Step;
            int lo = Mathf.Max(1, Mathf.FloorToInt((viewLocalCenter - halfWin) / Step) - 1);
            int hi = Mathf.Min(n, Mathf.CeilToInt((viewLocalCenter + halfWin) / Step) + 2);

            // SMOOTH CURVE trail: small evenly-spaced path dots tracing the cosine serpentine
            // between nodes (gold where travelled, muted ahead). No big midpoint "fake level" dot —
            // just a subtle dotted curve that leads the eye from one level to the next.
            var glossy = VisualAssets.GlossyCircle();
            float gb = Mathf.Max(0.0001f, glossy.bounds.size.x);
            for (int level = lo; level <= hi && level < n; level++)
            {
                bool travelled = level < unlocked;
                var col = travelled ? new Color(1f, 0.83f, 0.35f, 0.95f) : new Color(0.70f, 0.74f, 0.86f, 0.75f);
                for (int d = 1; d <= TrailDots; d++)
                {
                    float u = (level - 1) + d / (float)(TrailDots + 1);
                    var p = new Vector3(PathX(u), u * Step, 0f);
                    // don't drop a trail dot on top of a node's star row (the small arc just below a
                    // node) — it reads as a stray floating ball above the stars.
                    if (OverStars(p, level, n) || OverStars(p, level + 1, n)) continue;
                    var dot = MakeSprite(_content, $"Dot{level}_{d}", p, col, 5);
                    dot.sprite = glossy;
                    float ds = 0.24f / gb;
                    dot.transform.localScale = new Vector3(ds, ds, 1f);
                }
            }

            for (int level = lo; level <= hi; level++)
            {
                Vector2 lp = NodeLocal(level);
                MakeNode(level, lp, level == unlocked);
                _hit.Add((level, lp));
            }

            // ---- fixed header: opaque band covering the top, + colourful game logo + story tagline
            // (the "restore colour to the world" theme). Drawn above the scrolling trail. Pushed down
            // a little from the very top so it clears the notch and doesn't feel crammed.
            // Band is 20 tall (half-height 10); its bottom edge = center - 10 = _safeTop - 2.18,
            // sitting just under the (single-line) tagline so the header stays as short as possible.
            var band = MakeSprite(transform, "HeaderBand", new Vector3(0f, _safeTop + 7.82f, 0f),
                new Color(0.67f, 0.79f, 0.99f, 1f), 15);
            band.transform.localScale = new Vector3(40f, 20f, 1f);   // covers the notch + header

            // Colourful title logo on ONE line: each letter a different vivid colour. Auto-sizes
            // down to fit the screen width, with a thin outline (reduced border) for a cleaner look.
            var title = MakeText(transform, "Title", new Vector3(0f, _safeTop - 1.18f, 0f), 3.9f, 22);
            title.text = ColorfulTitle("COLOR MERGE EXIT");
            title.enableAutoSizing = true;
            title.fontSizeMin = 2.2f;
            title.fontSizeMax = 3.9f;
            title.rectTransform.sizeDelta = new Vector2(2f * _halfW - 0.16f, 2.6f);
            // Lighter weight: no Bold + a negative face dilate thins the strokes so the counters
            // (the gaps inside M / E) stay open and legible. No outline.
            title.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            title.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, -0.14f);

            // tagline sits close under the title (small gap)
            var tag = MakeText(transform, "Tagline", new Vector3(0f, _safeTop - 1.72f, 0f), 1.9f, 22);
            tag.text = Localization.Get(LocKeys.Tagline);
            tag.color = new Color(0.30f, 0.35f, 0.52f);

            bool muted = AudioManager.Instance != null && AudioManager.Instance.Muted;
            var soundSp = VisualAssets.UiIcon(muted ? 1 : 0, 1);
            _hasMute = soundSp != null || (_sprites != null && _sprites.btnSettings != null);
            if (_hasMute)
            {
                _muteBtnPos = new Vector3(_halfW - 0.65f, _safeTop - 0.34f, 0f);
                var sp = soundSp ?? _sprites.btnSettings;
                var mb = MakeSprite(transform, "MuteBtn", _muteBtnPos,
                    (soundSp == null && muted) ? new Color(1f, 1f, 1f, 0.3f) : Color.white, 16);
                mb.sprite = sp;
                var b = sp.bounds.size;
                float sc = 0.54f;   // small, unobtrusive sound button
                mb.transform.localScale = new Vector3(b.x > 0 ? sc / b.x : 1f, b.y > 0 ? sc / b.y : 1f, 1f);
            }

            BuildHearts();
        }

        // ---- lives / hearts (top-left of the header) ----
        private readonly List<GameObject> _heartGos = new List<GameObject>();
        private TMP_Text _heartTimer, _heartCount;
        private int _shownHearts = -1;
        private float _heartTick;

        private void BuildHearts()
        {
            foreach (var g in _heartGos) if (g != null) Destroy(g);
            _heartGos.Clear();
            _shownHearts = HeartStore.Current;
            // Compact "♥ ×N   +1 m:ss" row in the top-left: one heart icon, the count, then the
            // refill timer right beside it (bigger, per request) — clear of the centre notch.
            float hx = -_halfW + 0.55f, hy = _safeTop - 0.42f;
            var h = MakeSprite(transform, "Heart", new Vector3(hx, hy, 0f), Palette.HeartRed, 22);
            h.sprite = VisualAssets.Heart();
            var hb = h.sprite.bounds.size;
            float hs = 0.48f / Mathf.Max(0.0001f, hb.x);
            h.transform.localScale = new Vector3(hs, hs, 1f);
            _heartGos.Add(h.gameObject);

            _heartCount = MakeText(transform, "HeartCount", new Vector3(hx + 0.6f, hy, 0f), Typography.Label, 22);
            _heartCount.color = new Color(0.30f, 0.34f, 0.5f);
            _heartGos.Add(_heartCount.gameObject);

            // Timer LEFT-aligned (pivot at its left edge) so it hugs the count — the gap stays tight
            // regardless of text width, instead of a centred rect leaving a big blank beside "xN".
            _heartTimer = MakeText(transform, "HeartTimer", new Vector3(hx + 0.98f, hy, 0f), 2.6f, 22);
            _heartTimer.alignment = TextAlignmentOptions.Left;
            _heartTimer.rectTransform.pivot = new Vector2(0f, 0.5f);
            _heartTimer.rectTransform.sizeDelta = new Vector2(6f, 4f);
            _heartTimer.color = new Color(0.42f, 0.46f, 0.60f);
            _heartGos.Add(_heartTimer.gameObject);
            UpdateHeartTimer();
        }

        private void UpdateHeartTimer()
        {
            if (_heartCount != null) _heartCount.text = $"x{HeartStore.Current}";
            if (_heartTimer == null) return;
            if (HeartStore.Current >= HeartStore.Max) _heartTimer.text = "";
            else { int s = HeartStore.SecondsToNext; _heartTimer.text = $"+1  {s / 60}:{s % 60:00}"; }
        }

        // ---- out-of-hearts popup (gates play; offers a rewarded ad to refill) ----
        private GameObject _noHeartsRoot;
        private TMP_Text _noHeartsTimer;
        private Vector3 _watchAdPos, _closeHeartsPos, _heartsPanelPos = new Vector3(0f, 0.3f, 0f);
        private bool NoHeartsOpen => _noHeartsRoot != null && _noHeartsRoot.activeSelf;

        private void ShowNoHearts()
        {
            if (_noHeartsRoot != null) Destroy(_noHeartsRoot);
            _noHeartsRoot = new GameObject("NoHearts");
            _noHeartsRoot.transform.SetParent(transform, false);
            var t = _noHeartsRoot.transform;

            var back = MakeSprite(t, "Back", new Vector3(0f, 0f, 0.5f), Palette.Scrim(0.55f), Sorting.Backdrop);
            back.transform.localScale = new Vector3(60f, 60f, 1f);

            var panel = MakeSprite(t, "Panel", _heartsPanelPos + new Vector3(0f, 0f, 0.2f), Palette.Panel, Sorting.Panel);
            panel.sprite = VisualAssets.DialogPanel();
            panel.drawMode = SpriteDrawMode.Sliced; panel.size = new Vector2(6.0f, 4.8f);

            var bigHeart = MakeSprite(t, "BigHeart", _heartsPanelPos + new Vector3(0f, 1.55f, 0.1f), Palette.HeartRed, Sorting.PanelButton);
            bigHeart.sprite = VisualAssets.Heart();
            var hb = bigHeart.sprite.bounds.size; float hs = 1.1f / Mathf.Max(0.0001f, hb.x);
            bigHeart.transform.localScale = new Vector3(hs, hs, 1f);

            var title = MakeText(t, "NH_Title", _heartsPanelPos + new Vector3(0f, 0.55f, 0.1f), Typography.Body, Sorting.PanelText);
            title.text = Localization.Get(LocKeys.NoHearts); title.color = Palette.TextDark;
            title.fontStyle = FontStyles.Bold;

            _noHeartsTimer = MakeText(t, "NH_Timer", _heartsPanelPos + new Vector3(0f, -0.15f, 0.1f), Typography.Caption, Sorting.PanelText);
            _noHeartsTimer.color = new Color(0.4f, 0.45f, 0.58f);

            _watchAdPos = _heartsPanelPos + new Vector3(0f, -1.05f, 0f);
            var wa = MakeSprite(t, "WatchAd", _watchAdPos + new Vector3(0f, 0f, 0.1f), Palette.Blue, Sorting.PanelButton);
            wa.sprite = VisualAssets.RoundedSquare(); wa.drawMode = SpriteDrawMode.Sliced; wa.size = new Vector2(3.9f, 1.05f);
            var wal = MakeText(t, "WatchAdL", _watchAdPos + new Vector3(0f, 0f, 0.05f), 2.0f, Sorting.PanelText);
            wal.text = "▶ " + Localization.Get(LocKeys.WatchAd); wal.color = Color.white; wal.fontStyle = FontStyles.Bold;

            _closeHeartsPos = _heartsPanelPos + new Vector3(0f, -2.15f, 0f);
            var cl = MakeText(t, "NH_Close", _closeHeartsPos + new Vector3(0f, 0f, 0.05f), 1.7f, Sorting.PanelText);
            cl.text = Localization.Get(LocKeys.Close); cl.color = new Color(0.5f, 0.54f, 0.66f);

            UpdateNoHeartsTimer();
        }

        private void UpdateNoHeartsTimer()
        {
            if (_noHeartsTimer == null) return;
            int s = HeartStore.SecondsToNext;
            _noHeartsTimer.text = $"+1  {s / 60}:{s % 60:00}";
        }

        private void HideNoHearts() { if (_noHeartsRoot != null) { Destroy(_noHeartsRoot); _noHeartsRoot = null; } }

        private void MakeNode(int level, Vector2 local, bool isCurrent)
        {
            bool unlocked = ProgressStore.IsUnlocked(level);
            int stars = ProgressStore.Stars(level);
            var pos = new Vector3(local.x, local.y, 0f);

            // THEME: locked levels are grey (colour drained from the world); cleared levels glow with
            // a vivid restored colour; the current frontier is a bright blue with the "you are here" ring.
            var col = !unlocked ? new Color(0.66f, 0.70f, 0.80f)
                : stars > 0 ? RestoredColor(level)
                : new Color(0.30f, 0.66f, 1.0f);
            float size = isCurrent ? 1.42f : (unlocked ? 1.26f : 1.14f);

            // "you are here" glow ring behind the current node
            if (isCurrent)
            {
                var ring = MakeSprite(_content, "Ring" + level, pos + new Vector3(0f, 0f, 0.2f),
                    new Color(1f, 0.86f, 0.3f, 0.9f), 8);
                ring.sprite = VisualAssets.Ring();
                var rb = ring.sprite.bounds.size;
                float rs = (size + 0.7f) / Mathf.Max(0.0001f, rb.x);
                ring.transform.localScale = new Vector3(rs, rs, 1f);
            }

            var bgc = MakeSprite(_content, "Node" + level, pos, col, 10);
            bgc.sprite = VisualAssets.GlossyCircle();
            var bb = bgc.sprite.bounds.size;
            float s = size / Mathf.Max(0.0001f, bb.x);
            bgc.transform.localScale = new Vector3(s, s, 1f);

            // Level number: dead-centre in the circle, auto-sizing to fill it. The rect is bounded to
            // a fraction of the node diameter (`size`) so even a 4-digit number (level 1000+) shrinks
            // to stay inside the circle instead of overflowing.
            var num = MakeText(_content, "Num" + level, pos + new Vector3(0f, 0f, -0.1f),
                isCurrent ? 3.8f : 3.5f);
            num.text = level.ToString();
            num.alignment = TextAlignmentOptions.Center;
            num.enableAutoSizing = true;
            num.fontSizeMin = 1.1f;
            num.fontSizeMax = isCurrent ? 3.8f : 3.5f;
            num.rectTransform.sizeDelta = new Vector2(size * 0.92f, size * 0.74f);
            num.color = unlocked ? new Color(1f, 1f, 1f, 0.98f) : new Color(0.34f, 0.38f, 0.5f, 0.85f);

            if (!unlocked)
            {
                var lockSp = MakeSprite(_content, "Lock" + level, pos + new Vector3(0.34f, -0.34f, -0.15f),
                    new Color(0.42f, 0.46f, 0.58f), 12);
                lockSp.sprite = VisualAssets.Padlock();
                var lb = lockSp.sprite.bounds.size;
                float ls = 0.5f / Mathf.Max(0.0001f, lb.x);
                lockSp.transform.localScale = new Vector3(ls, ls, 1f);
                return;
            }

            // 3 stars in a small arc just below the node
            for (int st = 0; st < 3; st++)
            {
                var sp = MakeSprite(_content, $"Star{level}_{st}",
                    pos + new Vector3((st - 1) * 0.35f, -size * 0.5f - 0.2f + Mathf.Abs(st - 1) * -0.05f, -0.1f),
                    st < stars ? Palette.StarGold : new Color(0.55f, 0.58f, 0.68f, 0.8f), 11);
                if (_sprites != null && _sprites.star != null)
                {
                    sp.sprite = _sprites.star;
                    var sb = _sprites.star.bounds.size;
                    float ss = 0.32f / Mathf.Max(0.0001f, sb.x);
                    sp.transform.localScale = new Vector3(ss, ss, 1f);
                }
                else sp.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            }
        }

        private void Update()
        {
            // tick the hearts refill countdown (and rebuild the row when a heart refills)
            _heartTick += Time.deltaTime;
            if (_heartTick >= 1f)
            {
                _heartTick = 0f;
                if (HeartStore.Current != _shownHearts) BuildHearts();
                else UpdateHeartTimer();
            }

            bool down = false, held = false, up = false;
            Vector3 pos;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase == TouchPhase.Began;
                held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            }
            else
            {
                pos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
            }
            pos.z = 10f;
            var world = _cam.ScreenToWorldPoint(pos);

            // out-of-hearts popup is modal
            if (NoHeartsOpen)
            {
                UpdateNoHeartsTimer();
                if (up)
                {
                    if (Mathf.Abs(world.x - _watchAdPos.x) <= 2.0f && Mathf.Abs(world.y - _watchAdPos.y) <= 0.6f)
                    {
                        AudioManager.Instance?.Tap();
                        AdManager.ShowRewarded(() => { HeartStore.Refill(); BuildHearts(); });
                        HideNoHearts();
                    }
                    else if (Mathf.Abs(world.x - _closeHeartsPos.x) <= 1.6f && Mathf.Abs(world.y - _closeHeartsPos.y) <= 0.5f)
                        HideNoHearts();
                    else if (!(Mathf.Abs(world.x - _heartsPanelPos.x) <= 3.1f && Mathf.Abs(world.y - _heartsPanelPos.y) <= 2.6f))
                        HideNoHearts(); // tap outside the panel dismisses
                }
                return; // consume all input while open
            }

            if (down)
            {
                _pressing = true;
                _dragging = false;
                _pressWorld = world;
                _pressContentY = _contentY;
                _pressedMute = _hasMute &&
                    Mathf.Abs(world.x - _muteBtnPos.x) <= 0.6f && Mathf.Abs(world.y - _muteBtnPos.y) <= 0.6f;
            }
            else if (held && _pressing)
            {
                float dy = world.y - _pressWorld.y;
                if (!_dragging && Mathf.Abs(dy) > DragThreshold) _dragging = true;
                if (_dragging && !_pressedMute)
                {
                    _contentY = Mathf.Clamp(_pressContentY + dy, _offMin, _offMax);   // grab-scroll
                    if (_content != null) _content.localPosition = new Vector3(0f, _contentY, 0f);
                }
            }
            else if (up && _pressing)
            {
                _pressing = false;
                if (_pressedMute && !_dragging &&
                    Mathf.Abs(world.x - _muteBtnPos.x) <= 0.6f && Mathf.Abs(world.y - _muteBtnPos.y) <= 0.6f)
                {
                    AudioManager.Instance?.ToggleMute();
                    Rebuild();   // just swap the icon; keep current scroll (don't jump to current level)
                    return;
                }
                if (_dragging) { Rebuild(); return; }   // rebuild the visible window after a scroll

                // tap: hit-test nodes in content-local space
                foreach (var (level, lp) in _hit)
                {
                    if (Mathf.Abs(world.x - lp.x) <= NodeR + 0.15f &&
                        Mathf.Abs((world.y - _contentY) - lp.y) <= NodeR + 0.15f)
                    {
                        if (ProgressStore.IsUnlocked(level))
                        {
                            AudioManager.Instance?.Tap();
                            if (!HeartStore.HasHeart) { ShowNoHearts(); return; } // gated on lives
                            _onSelect?.Invoke(level);
                        }
                        return;
                    }
                }
            }
        }

        // Vivid palette for cleared nodes (colour "restored" to the path). All read with white numbers.
        private static readonly Color[] Restored =
        {
            new Color(0.96f, 0.28f, 0.30f), new Color(1f, 0.53f, 0.12f), new Color(0.20f, 0.75f, 0.38f),
            new Color(0.20f, 0.58f, 1f),    new Color(0.62f, 0.32f, 0.92f), new Color(1f, 0.42f, 0.72f),
            new Color(0.06f, 0.72f, 0.70f), new Color(1f, 0.44f, 0.42f),   new Color(0.40f, 0.36f, 0.90f),
        };
        private static Color RestoredColor(int level) => Restored[(level - 1) % Restored.Length];

        // Build a TMP rich-text string that tints each letter a different vivid colour.
        private static readonly string[] LogoHex =
            { "F5383D", "FF8719", "24C85B", "2185FF", "9947EB", "FF66B8", "0DC7BD", "FF706B", "5C52E6" };
        private static string ColorfulTitle(string s)
        {
            var sb = new System.Text.StringBuilder();
            int ci = 0;
            foreach (char c in s)
            {
                if (c == '\n') { sb.Append('\n'); continue; }
                if (c == ' ') { sb.Append("  "); continue; }
                sb.Append("<color=#").Append(LogoHex[ci % LogoHex.Length]).Append('>').Append(c).Append("</color>");
                ci++;
            }
            return sb.ToString();
        }

        // Thin forwarders to the shared toolkit so sprite/text construction lives in exactly one place.
        private SpriteRenderer MakeSprite(Transform parent, string name, Vector3 pos, Color color, int order)
            => Ui.Sprite(parent, name, pos, color, order);

        private TMP_Text MakeText(Transform parent, string name, Vector3 pos, float size, int order = 12)
            => Ui.Text(parent, name, pos, size, order);
    }
}
