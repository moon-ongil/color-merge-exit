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
        private Vector3 _settingsBtnPos;
        private bool _hasSettings;
        private float _halfH = 6.2f, _halfW = 3.5f, _safeTop = 6.2f;

        private Transform _content;   // holds the scrolling path
        private float _contentY, _offMin, _offMax;

        // pointer/drag state
        private bool _pressing, _dragging, _pressedSettings;
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
            BuildFixedBackdrop();   // built once per Show; NOT touched by the per-scroll Rebuild
            Rebuild();
        }

        // The sky (gradient + bokeh + rainbow + clouds) is screen-fixed and lives under its own root so
        // the frequent per-scroll Rebuild never destroys/recreates it — that recreation left a one-frame
        // "ghost" of the old clouds on every drag.
        private Transform _backdrop;
        private void BuildFixedBackdrop()
        {
            if (_backdrop != null) Destroy(_backdrop.gameObject);
            var root = new GameObject("Backdrop");
            root.transform.SetParent(transform, false);
            _backdrop = root.transform;

            var bg = MakeSprite(_backdrop, "PageBg", new Vector3(0f, 0f, 1f), Color.white, -100);
            bg.sprite = VisualAssets.SoftGradient();
            bg.transform.localScale = new Vector3(30f, 30f, 1f);

            BuildBackdrop();
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
            {
                var child = transform.GetChild(i);
                if (child == _backdrop) continue;   // the fixed sky persists across scroll rebuilds
                Destroy(child.gameObject);
            }
            _hit.Clear();

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
            var band = MakeSprite(transform, "HeaderBand", new Vector3(0f, _safeTop + 8.00f, 0f),
                new Color(0.67f, 0.79f, 0.99f, 1f), 15);
            band.transform.localScale = new Vector3(40f, 19.34f, 1f);   // covers the notch + header (bottom @ _safeTop-1.67)

            // Soft gradient under the band's bottom edge: same band colour fading to transparent, so the
            // hard seam melts into the scrolling content (and nodes fade out as they scroll under it).
            var fade = MakeSprite(transform, "HeaderBandFade", new Vector3(0f, _safeTop - 1.67f, 0f),
                new Color(0.67f, 0.79f, 0.99f, 1f), 14);
            fade.sprite = VisualAssets.BandFade();                       // pivot at TOP, fades downward
            fade.transform.localScale = new Vector3(40f, 2.2f, 1f);

            // Colourful title logo on ONE line: each letter a different vivid colour. Auto-sizes
            // down to fit the screen width, with a thin outline (reduced border) for a cleaner look.
            var title = MakeText(transform, "Title", new Vector3(0f, _safeTop - 1.00f, 0f), 3.9f, 22);
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
            var tag = MakeText(transform, "Tagline", new Vector3(0f, _safeTop - 1.34f, 0f), 1.9f, 22);
            tag.text = Localization.Get(LocKeys.Tagline);
            tag.color = new Color(0.30f, 0.35f, 0.52f);

            // Top-right SETTINGS gear — opens the home settings popup (sound + cloud-save status).
            _hasSettings = true;
            _settingsBtnPos = new Vector3(_halfW - 0.65f, _safeTop - 0.12f, 0f);
            var gearBg = MakeSprite(transform, "SettingsBtn", _settingsBtnPos, Palette.Blue, 16);
            gearBg.sprite = VisualAssets.RoundedSquare(); gearBg.drawMode = SpriteDrawMode.Sliced; gearBg.size = new Vector2(0.68f, 0.68f);
            var gear = MakeSprite(transform, "SettingsGear", _settingsBtnPos + new Vector3(0f, 0f, -0.1f), Color.white, 17);
            gear.sprite = VisualAssets.GearIcon();
            var gearB = gear.sprite.bounds.size;
            float gsc = 0.42f;
            gear.transform.localScale = new Vector3(gearB.x > 0 ? gsc / gearB.x : 1f, gearB.y > 0 ? gsc / gearB.y : 1f, 1f);

            BuildHearts();
        }

        // Large, soft, translucent pastel blobs scattered over the gradient. Positions are given as
        // fractions of the camera half-extents so they spread nicely on any aspect. Sorted just above
        // the page gradient (-90) and well below the scrolling trail, so they read as ambient depth.
        private static readonly (float fx, float fy, float scale, float r, float g, float b, float a)[] _blobSpec =
        {
            (-0.58f,  0.34f, 9.0f, 1.00f, 0.72f, 0.80f, 0.17f),  // pink
            ( 0.72f,  0.12f, 10.0f, 0.70f, 0.95f, 0.82f, 0.15f), // mint
            (-0.78f, -0.30f, 8.5f, 0.80f, 0.75f, 0.98f, 0.18f),  // lavender
            ( 0.60f, -0.58f, 9.5f, 1.00f, 0.83f, 0.66f, 0.16f),  // peach
            (-0.22f, -0.90f, 8.5f, 0.68f, 0.86f, 1.00f, 0.16f),  // sky
            ( 0.30f,  0.66f, 7.5f, 1.00f, 0.95f, 0.68f, 0.14f),  // lemon
        };

        // live blobs for the gentle drift animation (rebuilt on each Rebuild → re-registered here)
        private readonly List<(Transform t, Vector3 basePos, float scale, float seed)> _blobs =
            new List<(Transform, Vector3, float, float)>();

        // Clouds drifting horizontally across the sky (the visible motion the menu was missing).
        //   fy = height as a fraction of the half-height; scale; speed (units/sec, sign = direction).
        private static readonly (float fy, float scale, float speed)[] _cloudSpec =
        {
            ( 0.52f, 2.6f,  0.36f),
            ( 0.22f, 3.2f, -0.26f),
            (-0.10f, 2.1f,  0.22f),
            (-0.40f, 2.4f, -0.32f),
        };
        private readonly List<(Transform t, float baseY, float speed, float phase)> _clouds =
            new List<(Transform, float, float, float)>();

        private void BuildBackdrop()
        {
            _blobs.Clear();
            for (int i = 0; i < _blobSpec.Length; i++)
            {
                var s = _blobSpec[i];
                var pos = new Vector3(s.fx * _halfW, s.fy * _halfH, 0.5f);
                var blob = MakeSprite(_backdrop, "Blob", pos, new Color(s.r, s.g, s.b, s.a), -90);
                blob.sprite = VisualAssets.SoftBlob();
                blob.transform.localScale = new Vector3(s.scale, s.scale, 1f);
                _blobs.Add((blob.transform, pos, s.scale, i * 1.7f));   // per-blob phase so they drift out of sync
            }

            // Fixed rainbow rising from the bottom edge — a splash of colour for the "bring colour back"
            // theme. Behind the path (nodes/dots sort above it) but above the blobs.
            float rw = 2f * _halfW * 1.15f;                       // slightly wider than the screen
            var rainbow = MakeSprite(_backdrop, "Rainbow", new Vector3(0f, -_halfH - 0.6f, 0.4f), Color.white, -80);
            rainbow.sprite = VisualAssets.SoftRainbow();
            rainbow.transform.localScale = new Vector3(rw, rw, 1f);

            // Drifting clouds in the sky above the rainbow (behind the path).
            _clouds.Clear();
            for (int i = 0; i < _cloudSpec.Length; i++)
            {
                var c = _cloudSpec[i];
                var cloud = MakeSprite(_backdrop, "Cloud", new Vector3(0f, c.fy * _halfH, 0.3f),
                    new Color(1f, 1f, 1f, 0.9f), -70);
                cloud.sprite = VisualAssets.SoftCloud();
                cloud.transform.localScale = new Vector3(c.scale * 1.4f, c.scale, 1f);   // wide cloud
                _clouds.Add((cloud.transform, c.fy * _halfH, c.speed, i * 2.3f));
            }
        }

        // Blobs breathe gently; clouds slide horizontally and wrap around — the sky's visible motion.
        // Uses unscaled real time (the menu never changes Time.timeScale).
        private void AnimateBackdrop()
        {
            float tt = Time.unscaledTime;
            foreach (var (t, basePos, scale, seed) in _blobs)
            {
                if (t == null) continue;
                float dx = 0.8f * Mathf.Sin(tt * 0.32f + seed);
                float dy = 0.6f * Mathf.Sin(tt * 0.24f + seed * 1.3f);
                t.localPosition = new Vector3(basePos.x + dx, basePos.y + dy, basePos.z);
                float br = 1f + 0.09f * Mathf.Sin(tt * 0.38f + seed);   // gentle breathing
                t.localScale = new Vector3(scale * br, scale * br, 1f);
            }

            float bound = _halfW + 2.8f;    // wrap just off-screen so clouds glide fully across
            float span = 2f * bound;
            foreach (var (t, baseY, speed, phase) in _clouds)
            {
                if (t == null) continue;
                float x = Mathf.Repeat(phase * bound + speed * tt + bound, span) - bound;
                float bob = 0.10f * Mathf.Sin(tt * 0.5f + phase);
                t.localPosition = new Vector3(x, baseY + bob, 0.3f);
            }
        }

        // ---- home settings popup (opened by the top-right gear): sound toggle + cloud-save status ----
        private GameObject _settingsRoot;
        private readonly Vector3 _setPanelPos = new Vector3(0f, 0.4f, 0f);
        private Vector3 _soundTogglePos, _setClosePos, _privacyPos, _termsPos;
        private bool SettingsOpen => _settingsRoot != null && _settingsRoot.activeSelf;

        private void HideSettings() { if (_settingsRoot != null) { Destroy(_settingsRoot); _settingsRoot = null; } }

        // Open the hosted legal doc in the browser, in the player's language (Korean default, else English).
        private void OpenLegal(string doc)
        {
            string suffix = Localization.Current == Locale.Ko ? "" : "?lang=en";
            Application.OpenURL($"https://color-merge-exit.ongil.me/legal/{doc}{suffix}");
        }

        private void ShowSettings()
        {
            if (_settingsRoot != null) Destroy(_settingsRoot);
            _settingsRoot = new GameObject("HomeSettings");
            _settingsRoot.transform.SetParent(transform, false);
            var t = _settingsRoot.transform;

            var back = MakeSprite(t, "S_Back", new Vector3(0f, 0f, 0.5f), Palette.Scrim(0.55f), 200);
            back.transform.localScale = new Vector3(60f, 60f, 1f);

            var panel = MakeSprite(t, "S_Panel", _setPanelPos + new Vector3(0f, 0f, -0.2f), Palette.Panel, 201);
            panel.sprite = VisualAssets.DialogPanel();
            panel.drawMode = SpriteDrawMode.Sliced; panel.size = new Vector2(5.2f, 6.9f);   // taller: fits the legal links

            var title = MakeText(t, "S_Title", _setPanelPos + new Vector3(0f, 2.75f, -0.3f), 4.0f, 203);
            title.text = "SETTINGS"; title.color = Palette.TextDark; title.fontStyle = FontStyles.Bold;

            const float lblX = -1.95f, valX = 1.5f;   // lblX = the shared LEFT edge of both labels

            // Sound row: label + ON/OFF toggle
            var soundLbl = MakeText(t, "S_SoundLbl", _setPanelPos + new Vector3(lblX, 1.6f, -0.3f), 3.0f, 203);
            soundLbl.text = "SOUND"; soundLbl.color = Palette.TextDark;
            soundLbl.alignment = TextAlignmentOptions.Left;
            soundLbl.rectTransform.pivot = new Vector2(0f, 0.5f); soundLbl.rectTransform.sizeDelta = new Vector2(3.2f, 1f);
            bool soundOn = !(AudioManager.Instance != null && AudioManager.Instance.Muted);
            _soundTogglePos = _setPanelPos + new Vector3(valX, 1.6f, 0f);
            var togBg = MakeSprite(t, "S_SoundTog", _soundTogglePos + new Vector3(0f, 0f, -0.2f),
                soundOn ? Palette.Blue : new Color(0.62f, 0.65f, 0.74f), 202);
            togBg.sprite = VisualAssets.RoundedSquare(); togBg.drawMode = SpriteDrawMode.Sliced; togBg.size = new Vector2(1.4f, 0.78f);
            var togTxt = MakeText(t, "S_SoundTxt", _soundTogglePos + new Vector3(0f, 0f, -0.3f), 2.9f, 203);
            togTxt.text = soundOn ? "ON" : "OFF"; togTxt.color = Color.white; togTxt.fontStyle = FontStyles.Bold;

            // Cloud-save row: label + platform status (no login — iCloud/Google handle it via the OS account)
            var cloudLbl = MakeText(t, "S_CloudLbl", _setPanelPos + new Vector3(lblX, 0.75f, -0.3f), 3.0f, 203);
            cloudLbl.text = "CLOUD SAVE"; cloudLbl.color = Palette.TextDark;
            cloudLbl.alignment = TextAlignmentOptions.Left;
            cloudLbl.rectTransform.pivot = new Vector2(0f, 0.5f); cloudLbl.rectTransform.sizeDelta = new Vector2(3.2f, 1f);
            var cloudStat = MakeText(t, "S_CloudStat", _setPanelPos + new Vector3(valX, 0.75f, -0.3f), 2.9f, 203);
            cloudStat.text = CloudSave.Available ? "iCloud" : "On";
            cloudStat.color = new Color(0.24f, 0.66f, 0.42f); cloudStat.fontStyle = FontStyles.Bold;

            // Legal links: open the hosted privacy policy / terms in the browser (store compliance).
            _privacyPos = _setPanelPos + new Vector3(0f, -0.2f, 0f);
            var privacy = MakeText(t, "S_Privacy", _privacyPos + new Vector3(0f, 0f, -0.3f), 2.7f, 203);
            privacy.text = "Privacy Policy"; privacy.color = Palette.Blue; privacy.fontStyle = FontStyles.Underline;
            _termsPos = _setPanelPos + new Vector3(0f, -1.0f, 0f);
            var terms = MakeText(t, "S_Terms", _termsPos + new Vector3(0f, 0f, -0.3f), 2.7f, 203);
            terms.text = "Terms of Service"; terms.color = Palette.Blue; terms.fontStyle = FontStyles.Underline;

            _setClosePos = _setPanelPos + new Vector3(0f, -2.4f, 0f);
            var closeBg = MakeSprite(t, "S_Close", _setClosePos + new Vector3(0f, 0f, -0.2f), Palette.Blue, 202);
            closeBg.sprite = VisualAssets.RoundedSquare(); closeBg.drawMode = SpriteDrawMode.Sliced; closeBg.size = new Vector2(3.2f, 0.98f);
            var closeTxt = MakeText(t, "S_CloseTxt", _setClosePos + new Vector3(0f, 0f, -0.3f), 3.3f, 203);
            closeTxt.text = "CLOSE"; closeTxt.color = Color.white; closeTxt.fontStyle = FontStyles.Bold;
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
            // Bare heart at the top-left (mirrors the settings gear's x/height), with the life COUNT
            // (just the number) + refill timer to its right.
            float cx = -_halfW + 0.65f, cy = _safeTop - 0.12f;   // mirror of _settingsBtnPos
            var h = MakeSprite(transform, "Heart", new Vector3(cx, cy, 0f), Palette.HeartRed, 22);
            h.sprite = VisualAssets.Heart();
            var hb = h.sprite.bounds.size;
            float hs = 0.5f / Mathf.Max(0.0001f, hb.x);
            h.transform.localScale = new Vector3(hs, hs, 1f);
            _heartGos.Add(h.gameObject);

            _heartCount = MakeText(transform, "HeartCount", new Vector3(cx + 0.6f, cy, 0f), Typography.Label, 22);
            _heartCount.alignment = TextAlignmentOptions.Left;
            _heartCount.rectTransform.pivot = new Vector2(0f, 0.5f);
            _heartCount.rectTransform.sizeDelta = new Vector2(2f, 1f);
            _heartCount.color = new Color(0.30f, 0.34f, 0.5f);
            _heartGos.Add(_heartCount.gameObject);

            // Timer LEFT-aligned (pivot at its left edge) so it hugs the count.
            _heartTimer = MakeText(transform, "HeartTimer", new Vector3(cx + 1.05f, cy, 0f), 2.6f, 22);
            _heartTimer.alignment = TextAlignmentOptions.Left;
            _heartTimer.rectTransform.pivot = new Vector2(0f, 0.5f);
            _heartTimer.rectTransform.sizeDelta = new Vector2(6f, 4f);
            _heartTimer.color = new Color(0.42f, 0.46f, 0.60f);
            _heartGos.Add(_heartTimer.gameObject);
            UpdateHeartTimer();
        }

        private void UpdateHeartTimer()
        {
            if (_heartCount != null) _heartCount.text = $"{HeartStore.Current}";   // count only, no "x"
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

            // 3 stars in a small arc just below the node — only once the level has been cleared.
            // An uncleared level (stars == 0) shows no tray at all, so the early screens read clean
            // and the star row never looks like a pre-earned reward.
            if (stars <= 0) return;
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
            AnimateBackdrop();   // gentle bokeh drift

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

            // home settings popup is modal
            if (SettingsOpen)
            {
                if (up)
                {
                    if (Mathf.Abs(world.x - _soundTogglePos.x) <= 1.0f && Mathf.Abs(world.y - _soundTogglePos.y) <= 0.55f)
                    {
                        AudioManager.Instance?.ToggleMute();
                        ShowSettings();   // rebuild the popup to refresh the ON/OFF label
                    }
                    else if (Mathf.Abs(world.x - _privacyPos.x) <= 1.9f && Mathf.Abs(world.y - _privacyPos.y) <= 0.38f)
                    { AudioManager.Instance?.Tap(); OpenLegal("privacy-policy"); }
                    else if (Mathf.Abs(world.x - _termsPos.x) <= 1.9f && Mathf.Abs(world.y - _termsPos.y) <= 0.38f)
                    { AudioManager.Instance?.Tap(); OpenLegal("terms-of-service"); }
                    else if (Mathf.Abs(world.x - _setClosePos.x) <= 1.6f && Mathf.Abs(world.y - _setClosePos.y) <= 0.55f)
                    { AudioManager.Instance?.Tap(); HideSettings(); }
                    else if (!(Mathf.Abs(world.x - _setPanelPos.x) <= 2.7f && Mathf.Abs(world.y - _setPanelPos.y) <= 3.5f))
                        HideSettings();   // tap outside the panel dismisses
                }
                return; // consume all input while open
            }

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
                _pressedSettings = _hasSettings &&
                    Mathf.Abs(world.x - _settingsBtnPos.x) <= 0.6f && Mathf.Abs(world.y - _settingsBtnPos.y) <= 0.6f;
            }
            else if (held && _pressing)
            {
                float dy = world.y - _pressWorld.y;
                if (!_dragging && Mathf.Abs(dy) > DragThreshold) _dragging = true;
                if (_dragging && !_pressedSettings)
                {
                    _contentY = Mathf.Clamp(_pressContentY + dy, _offMin, _offMax);   // grab-scroll
                    if (_content != null) _content.localPosition = new Vector3(0f, _contentY, 0f);
                }
            }
            else if (up && _pressing)
            {
                _pressing = false;
                if (_pressedSettings && !_dragging &&
                    Mathf.Abs(world.x - _settingsBtnPos.x) <= 0.6f && Mathf.Abs(world.y - _settingsBtnPos.y) <= 0.6f)
                {
                    AudioManager.Instance?.Tap();
                    ShowSettings();
                    return;
                }
                if (_dragging) { Rebuild(); return; }   // rebuild the visible window after a scroll

                // A tap on the opaque header band (bottom edge ≈ _safeTop-1.67) must not reach a node that
                // has scrolled up behind it — otherwise tapping the logo/title launches a hidden level.
                if (world.y > _safeTop - 1.67f) return;

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
