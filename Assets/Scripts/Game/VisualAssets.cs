using System.Collections.Generic;
using System.Text;
using ColorMergeExit.Core;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Runtime-generated art so the project needs no imported sprites/fonts.
    /// A single 1x1 white sprite is tinted per use; the built-in font backs all text.
    /// </summary>
    public static class VisualAssets
    {
        private static Sprite _square;
        private static Sprite _rounded;
        private static Sprite _glossy;
        private static Sprite _arrow;
        private static Sprite _lock;
        private static Sprite _ring;
        private static Sprite _split;
        private static Sprite _glossyBar;
        private static Sprite _bulb;
        private static Sprite _clock;
        private static Sprite _heart;
        private static Sprite _house;
        private static Sprite _star;
        private static Sprite _hand;
        private static Sprite _chevron;
        private static Sprite _arrowCW, _arrowCCW;
        private static Sprite _arrowCursor;
        private static Font _font;
        private static readonly Dictionary<string, Sprite> _shapeCache = new Dictionary<string, Sprite>();

        private static Sprite _grad;
        private static Sprite _blob;
        private static Sprite _rainbow;
        private static Sprite _cloud;

        // ---- UI sprite sheet (Resources/ui_sheet.png): a 2048x2048 POT grid of glossy buttons/icons ----
        private const int UiCols = 8, UiRows = 8;
        private static Texture2D _uiTex;
        private static bool _uiTried;
        private static readonly Dictionary<int, Sprite> _uiCache = new Dictionary<int, Sprite>();

        /// <summary>A sprite for cell (col,row) of the UI sheet, or null if the sheet is missing.
        /// Sheet rows: 0 nav buttons, 1 item buttons, 2 markers, 3-4 blocks/fx (see make_sheet).</summary>
        public static Sprite UiIcon(int col, int row)
        {
            if (!_uiTried)
            {
                _uiTried = true;
                // Resources works on every platform and needs no extra engine module. The PNG may
                // import as a Texture2D or as a Sprite depending on the project default — handle both.
                _uiTex = Resources.Load<Texture2D>("ui_sheet");
                if (_uiTex == null)
                {
                    var s = Resources.Load<Sprite>("ui_sheet");
                    if (s != null) _uiTex = s.texture;
                }
            }
            if (_uiTex == null) return null;
            int key = row * 100 + col;
            if (_uiCache.TryGetValue(key, out var sp)) return sp;
            // derive the cell size from the actual texture so any import scaling stays aligned
            float cw = _uiTex.width / (float)UiCols, ch = _uiTex.height / (float)UiRows;
            float y = _uiTex.height - (row + 1) * ch; // sheet is top-down; Unity texture is bottom-up
            sp = Sprite.Create(_uiTex, new Rect(col * cw, y, cw, ch), new Vector2(0.5f, 0.5f), cw);
            _uiCache[key] = sp;
            return sp;
        }

        /// <summary>A soft top-to-bottom pastel gradient (sky blue -> lavender) used as a bright,
        /// pretty full-screen background. 1 unit tall at the sprite's native size; scale to cover.</summary>
        private static Sprite _bandFade;

        /// <summary>A vertical fade: opaque white at the TOP edge → fully transparent at the bottom.
        /// Tint it the header-band colour and place it just under the band so the band's hard bottom
        /// edge melts into the scrolling content instead of showing a sharp seam.</summary>
        public static Sprite BandFade()
        {
            if (_bandFade != null) return _bandFade;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            {
                // texture y=0 is the bottom → alpha 0 there, alpha 1 at the top (y=N-1). Smoothstep for
                // a natural, non-linear falloff.
                float t = (float)y / (N - 1);
                float a = t * t * (3f - 2f * t);
                for (int x = 0; x < N; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _bandFade = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 1f), N); // pivot at TOP
            return _bandFade;
        }

        public static Sprite SoftGradient()
        {
            if (_grad != null) return _grad;
            const int N = 64;                            // square so native size is 1x1 unit
            var top = new Color(0.60f, 0.78f, 0.99f);   // sky blue (screen top)
            var bot = new Color(0.90f, 0.84f, 0.98f);   // soft lavender (screen bottom)
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            {
                float t = (float)y / (N - 1);
                var c = Color.Lerp(bot, top, t);
                for (int x = 0; x < N; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            _grad = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _grad;
        }

        /// <summary>A big, very soft radial blob (opaque centre fading smoothly to fully transparent
        /// at the rim) for dreamy pastel "bokeh" in the background. Tint + scale it per use.</summary>
        public static Sprite SoftBlob()
        {
            if (_blob != null) return _blob;
            const int N = 128; const float c = (N - 1) * 0.5f, rad = (N - 1) * 0.5f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c;
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / rad);   // 0 centre -> 1 rim
                float a = 1f - t; a = a * a * (3f - 2f * a);                     // smoothstep feather
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _blob = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _blob;
        }

        /// <summary>A soft half-circle RAINBOW (seven bands, red outermost), transparent outside the
        /// arc and below the horizon. Pivot is bottom-centre so it rises from the screen bottom. Fits
        /// the "bring colour back to the world" theme as a fixed splash of colour behind the path.</summary>
        public static Sprite SoftRainbow()
        {
            if (_rainbow != null) return _rainbow;
            const int N = 1024; const float cx = (N - 1) * 0.5f;   // high-res so the big arc stays smooth
            float rOut = N * 0.49f, rIn = N * 0.30f;
            float feather = N * 0.018f;                            // rim fade, scaled to resolution
            // inner -> outer: violet, indigo, blue, green, yellow, orange, red
            var bands = new[]
            {
                new Color(0.66f, 0.45f, 0.89f), new Color(0.42f, 0.44f, 0.86f),
                new Color(0.35f, 0.66f, 0.98f), new Color(0.46f, 0.82f, 0.47f),
                new Color(1.00f, 0.87f, 0.32f), new Color(1.00f, 0.62f, 0.26f),
                new Color(0.98f, 0.34f, 0.34f),
            };
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - cx, dy = y;                 // centre at bottom (y = 0)
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < rIn || d > rOut) { tex.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }
                // smoothly interpolate ACROSS the seven anchor colours (like a real rainbow) so the
                // band-to-band seams blend instead of meeting at a hard line.
                float f = Mathf.Clamp01((d - rIn) / (rOut - rIn)) * 6f;
                int i0 = Mathf.Clamp((int)f, 0, 6), i1 = Mathf.Min(i0 + 1, 6);
                var c = Color.Lerp(bands[i0], bands[i1], f - i0);
                float edge = Mathf.Min(d - rIn, rOut - d);           // fade the inner & outer rims
                float a = Mathf.Clamp01(edge / feather) * 0.7f;
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
            tex.Apply();
            _rainbow = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0f), N);
            return _rainbow;
        }

        /// <summary>A soft, puffy white cloud (union of a few discs, flat-ish bottom) for the sky.
        /// Tint/alpha it per use; the sprite is wider than tall so scale it uniformly.</summary>
        public static Sprite SoftCloud()
        {
            if (_cloud != null) return _cloud;
            const int N = 256;
            // discs in normalised [0,1] space: (cx, cy, r) — a lumpy top, flat bottom around y=0.40
            var discs = new[]
            {
                new Vector3(0.50f, 0.46f, 0.20f), new Vector3(0.32f, 0.44f, 0.15f),
                new Vector3(0.68f, 0.44f, 0.16f), new Vector3(0.42f, 0.56f, 0.15f),
                new Vector3(0.60f, 0.55f, 0.14f), new Vector3(0.22f, 0.43f, 0.11f),
                new Vector3(0.78f, 0.43f, 0.12f),
            };
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float nx = (float)x / N, ny = (float)y / N;
                float a = 0f;
                foreach (var dsc in discs)
                {
                    float dd = Mathf.Sqrt((nx - dsc.x) * (nx - dsc.x) + (ny - dsc.y) * (ny - dsc.y));
                    a = Mathf.Max(a, Mathf.Clamp01((dsc.z - dd) / 0.05f));   // soft-edged union
                }
                if (ny < 0.40f) a *= Mathf.Clamp01((ny - 0.30f) / 0.10f);    // flatten the underside
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _cloud = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _cloud;
        }

        /// <summary>A 1x1 white sprite (pixelsPerUnit = 1 so one unit == one cell).</summary>
        public static Sprite Square()
        {
            if (_square != null) return _square;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _square;
        }

        private static Sprite _panel;

        /// <summary>A large rounded-rectangle sprite with a SMALL relative corner radius, so when it
        /// is scaled up to the whole board it gets gently rounded corners (not an over-rounded blob).</summary>
        public static Sprite RoundedPanel()
        {
            if (_panel != null) return _panel;
            const int N = 256;
            const float r = 12f; // ~0.047 of the sprite -> a soft, modest round when scaled to the board
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(r - x, x - (N - 1 - r), 0f);
                float dy = Mathf.Max(r - y, y - (N - 1 - r), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            // border lets this be drawn 9-sliced (SpriteDrawMode.Sliced) so corners never stretch.
            _panel = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0,
                SpriteMeshType.FullRect, new Vector4(r + 3, r + 3, r + 3, r + 3));
            return _panel;
        }

        /// <summary>A white rounded-square sprite (64px, tintable) for block tiles.</summary>
        public static Sprite RoundedSquare()
        {
            if (_rounded != null) return _rounded;
            const int N = 64;
            const float r = 12f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(r - x, x - (N - 1 - r), 0f);
                float dy = Mathf.Max(r - y, y - (N - 1 - r), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist + 0.5f); // soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            // border lets this be drawn 9-sliced so wide/tall rects keep crisp rounded corners.
            _rounded = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0,
                SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            return _rounded;
        }

        private static Sprite _roundedSmall;
        /// <summary>Like <see cref="RoundedSquare"/> but with a SMALL corner radius — a barely-rounded
        /// rectangle for tight bands (e.g. the door's next-colour segment) where the generous radius
        /// reads as a pill. 9-sliced so the corner stays crisp at any size.</summary>
        public static Sprite RoundedSquareSmall()
        {
            if (_roundedSmall != null) return _roundedSmall;
            const int N = 64;
            const float r = 8f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(r - x, x - (N - 1 - r), 0f);
                float dy = Mathf.Max(r - y, y - (N - 1 - r), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _roundedSmall = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0,
                SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            return _roundedSmall;
        }

        private static Sprite _dialogPanel;
        /// <summary>A rounded panel with a GENEROUS corner radius for dialogs — clearly rounded (unlike
        /// the near-square <see cref="RoundedPanel"/>), 9-sliceable so any dialog size keeps soft corners.</summary>
        public static Sprite DialogPanel()
        {
            if (_dialogPanel != null) return _dialogPanel;
            const int N = 96;
            const float r = 28f; // ~0.29 of the sprite -> clearly rounded corners at dialog scale
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(r - x, x - (N - 1 - r), 0f);
                float dy = Mathf.Max(r - y, y - (N - 1 - r), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _dialogPanel = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0,
                SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            return _dialogPanel;
        }

        /// <summary>A lightbulb glyph (white, tintable) for the HINT item.</summary>
        public static Sprite BulbIcon()
        {
            if (_bulb != null) return _bulb;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float a = 0f;
                // glass bulb: circle in the upper area
                float dg = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 40f) * (y - 40f));
                a = Mathf.Max(a, Mathf.Clamp01(15f - dg + 0.5f));
                // screw base: a narrower rounded block below, with two thread gaps
                if (x >= 25f && x <= 39f && y >= 12f && y <= 26f)
                {
                    float t = 1f;
                    if (Mathf.Abs(y - 20f) < 1.1f || Mathf.Abs(y - 16f) < 1.1f) t = 0f; // thread lines
                    // round the very bottom corners a touch
                    if (y < 14f && (x < 27f || x > 37f)) t = 0f;
                    a = Mathf.Max(a, t);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _bulb = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _bulb;
        }

        /// <summary>A simple house glyph (white, tintable) for the HOME button — roof triangle +
        /// body with a door cut-out. 2×2 supersampled.</summary>
        public static Sprite HouseIcon()
        {
            if (_house != null) return _house;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float px = x + (sx + 0.5f) * 0.5f, py = y + (sy + 0.5f) * 0.5f;
                    bool on = false;
                    // roof: triangle, apex at top-centre (y=52), base at y=36
                    if (py >= 36f && py <= 52f)
                    {
                        float t = (52f - py) / 16f;            // 0 apex .. 1 base
                        if (Mathf.Abs(px - 32f) <= t * 22f) on = true;
                    }
                    // body
                    if (px >= 18f && px <= 46f && py >= 12f && py <= 37f) on = true;
                    // door cut-out
                    if (px >= 27f && px <= 37f && py >= 12f && py <= 27f) on = false;
                    if (on) cov += 0.25f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
            }
            tex.Apply();
            _house = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _house;
        }

        /// <summary>A cartoon tap-hand cursor (white, tintable) for tutorial demos: a rounded palm
        /// with one extended index finger and a thumb, drawn with rounded capsules so it reads as a
        /// hand rather than an arrow. Pivot at the fingertip so it points AT a target. 2×2 supersampled.</summary>
        public static Sprite HandIcon()
        {
            if (_hand != null) return _hand;
            const int N = 64;
            // distance from point p to segment a-b (for rounded-capsule limbs)
            float SegDist(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a, ap = p - a;
                float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(1e-4f, Vector2.Dot(ab, ab)));
                return Vector2.Distance(p, a + ab * t);
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    var p = new Vector2(x + (sx + 0.5f) * 0.5f, y + (sy + 0.5f) * 0.5f);
                    bool on = false;
                    // index finger: rounded capsule, tip at top-centre (y≈57) down into the palm
                    if (SegDist(p, new Vector2(27f, 57f), new Vector2(27f, 33f)) <= 6.5f) on = true;
                    // palm: a big rounded blob (capsule with near-equal endpoints)
                    if (SegDist(p, new Vector2(29f, 20f), new Vector2(31f, 24f)) <= 15f && p.y <= 33f) on = true;
                    // thumb: shorter capsule angled off the left side
                    if (SegDist(p, new Vector2(17f, 30f), new Vector2(24f, 22f)) <= 5.5f) on = true;
                    if (on) cov += 0.25f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
            }
            tex.Apply();
            // pivot at the fingertip (x=27/64, y=57/64) so the hand points at whatever it's placed on
            _hand = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(27f / N, 57f / N), N);
            return _hand;
        }

        /// <summary>A circular refresh/undo arrow (white, tintable): a ~270° ring with a triangular
        /// arrowhead at one end. <paramref name="clockwise"/> = restart (↻); false = undo (↺, mirrored).</summary>
        public static Sprite CircleArrow(bool clockwise)
        {
            if (clockwise && _arrowCW != null) return _arrowCW;
            if (!clockwise && _arrowCCW != null) return _arrowCCW;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float cx = 32f, cy = 32f, rad = 16f, th = 4.2f;
            // Arc spans ~300°, gap at the top between gapLo and gapHi; the arrowhead caps the gapHi end.
            const float gapLo = 55f, gapHi = 118f;
            float headDeg = gapHi;
            float ha = headDeg * Mathf.Deg2Rad;
            var pt = new Vector2(cx + rad * Mathf.Cos(ha), cy + rad * Mathf.Sin(ha));   // ring point at the head
            var tan = new Vector2(Mathf.Sin(ha), -Mathf.Cos(ha));                        // clockwise tangent
            var nrm = new Vector2(-tan.y, tan.x);
            var tip = pt + tan * 9f;
            var b1 = pt + nrm * 6.5f;
            var b2 = pt - nrm * 6.5f;

            float Sign(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            bool InTri(Vector2 p)
            {
                float d1 = Sign(p, tip, b1), d2 = Sign(p, b1, b2), d3 = Sign(p, b2, tip);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                return !(neg && pos);
            }
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float px = x + (sx + 0.5f) * 0.5f, py = y + (sy + 0.5f) * 0.5f;
                    float mx = clockwise ? px : (N - px);   // mirror x for the counter-clockwise variant
                    var p = new Vector2(mx, py);
                    float r = Vector2.Distance(p, new Vector2(cx, cy));
                    float ang = Mathf.Atan2(py - cy, mx - cx) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    bool on = (r >= rad - th && r <= rad + th && !(ang > gapLo && ang < gapHi)) || InTri(p);
                    if (on) cov += 0.25f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
            }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            if (clockwise) _arrowCW = sp; else _arrowCCW = sp;
            return sp;
        }

        /// <summary>A chunky diagonal pointer arrow (white, tintable) for tutorial demos — points
        /// up-left, pivot at the tip so it aims AT whatever it is placed on. 2×2 supersampled.</summary>
        public static Sprite ArrowCursor()
        {
            if (_arrowCursor != null) return _arrowCursor;
            const int N = 64;
            float SegDist(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a, ap = p - a;
                float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(1e-4f, Vector2.Dot(ab, ab)));
                return Vector2.Distance(p, a + ab * t);
            }
            // A bold up-left arrow: a big solid triangular head at the tip + a thick shaft to the tail.
            var tip = new Vector2(11f, 53f);
            var tail = new Vector2(48f, 16f);
            var dir = (tail - tip).normalized;          // down-right (shaft direction)
            var perp = new Vector2(-dir.y, dir.x);      // normal
            var hbase = tip + dir * 26f;                // back of the arrowhead
            var c1 = hbase + perp * 16f;                // wide head so it clearly reads as an arrow
            var c2 = hbase - perp * 16f;
            float Sign(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            bool InHead(Vector2 p)
            {
                float d1 = Sign(p, tip, c1), d2 = Sign(p, c1, c2), d3 = Sign(p, c2, tip);
                return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    var p = new Vector2(x + (sx + 0.5f) * 0.5f, y + (sy + 0.5f) * 0.5f);
                    bool on = SegDist(p, hbase, tail) <= 6.5f || InHead(p);   // thick shaft + solid head
                    if (on) cov += 0.25f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
            }
            tex.Apply();
            _arrowCursor = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(tip.x / N, tip.y / N), N);
            return _arrowCursor;
        }

        /// <summary>A downward chevron (white, tintable) used as a text-free "tap to continue" hint.</summary>
        public static Sprite Chevron()
        {
            if (_chevron != null) return _chevron;
            const int N = 48;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float px = x + (sx + 0.5f) * 0.5f, py = y + (sy + 0.5f) * 0.5f;
                    // two strokes forming a ˅: distance to the '\' and '/' arms, thickness ~5
                    float d1 = Mathf.Abs((py - 34f) - (-1f) * (px - 24f)) / Mathf.Sqrt(2f); // left arm '\'
                    float d2 = Mathf.Abs((py - 34f) - (1f) * (px - 24f)) / Mathf.Sqrt(2f);  // right arm '/'
                    bool onLeft = d1 <= 3.2f && px <= 24f && py <= 34f && py >= 12f;
                    bool onRight = d2 <= 3.2f && px >= 24f && py <= 34f && py >= 12f;
                    if (onLeft || onRight) cov += 0.25f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
            }
            tex.Apply();
            _chevron = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _chevron;
        }

        /// <summary>A heart glyph (tintable) for the lives UI, via the implicit heart curve
        /// (x²+y²−1)³ − x²y³ ≤ 0, 2×2 supersampled for smooth edges.</summary>
        public static Sprite Heart()
        {
            if (_heart != null) return _heart;
            const int N = 72;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float px = x + (sx + 0.5f) * 0.5f, py = y + (sy + 0.5f) * 0.5f;
                    float u = (px - N * 0.5f) / (N * 0.40f);
                    float v = (py - N * 0.46f) / (N * 0.40f);   // v points up; heart point at bottom
                    float t = u * u + v * v - 1f;
                    float f = t * t * t - u * u * v * v * v;
                    if (f <= 0f) cov += 0.25f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
            }
            tex.Apply();
            _heart = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _heart;
        }

        /// <summary>A clock-with-plus glyph (white, tintable) for the ADD-TIME item.</summary>
        public static Sprite ClockIcon()
        {
            if (_clock != null) return _clock;
            const int N = 64; const float cx = 28f, cy = 30f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float a = 0f;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                a = Mathf.Max(a, Mathf.Clamp01(2.4f - Mathf.Abs(d - 16f) + 0.3f));       // ring
                if (x >= cx - 1.6f && x <= cx + 1.6f && y >= cy && y <= cy + 13f) a = Mathf.Max(a, 1f); // minute hand (up)
                if (y >= cy - 1.6f && y <= cy + 1.6f && x >= cx && x <= cx + 10f) a = Mathf.Max(a, 1f);  // hour hand (right)
                // "+" badge (top-right) meaning add time
                if (Mathf.Abs(y - 50f) <= 6.5f && Mathf.Abs(x - 50f) <= 2.0f) a = Mathf.Max(a, 1f);
                if (Mathf.Abs(x - 50f) <= 6.5f && Mathf.Abs(y - 50f) <= 2.0f) a = Mathf.Max(a, 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _clock = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _clock;
        }

        /// <summary>A filled 5-point star (tintable) for the win celebration.</summary>
        public static Sprite Star()
        {
            if (_star != null) return _star;
            const int N = 64; const float c = (N - 1) / 2f, outer = 30f, inner = 13f;
            const float sector = Mathf.PI * 2f / 5f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c;
                float phase = Mathf.Repeat((Mathf.Atan2(dy, dx) - Mathf.PI / 2f) / sector, 1f);
                float boundary = inner + (outer - inner) * Mathf.Abs(phase * 2f - 1f); // point at phase 0
                float a = Mathf.Clamp01(boundary - Mathf.Sqrt(dx * dx + dy * dy) + 0.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _star = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _star;
        }

        /// <summary>A "split" glyph: FOUR triangles pointing outward (up/down/left/right) so the
        /// cell reads as "one block scatters apart". Tintable; used to mark splitter cells.</summary>
        public static Sprite SplitIcon()
        {
            if (_split != null) return _split;
            const int N = 64; const float c = (N - 1) / 2f;
            const float apex = 7f, len = 17f, taper = 0.62f;   // arrow apex, length, half-width slope
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float a = 0f;
                float ul = x - apex;          // left arrow (apex at x=7, points -x), widens inward
                if (ul >= 0f && ul <= len) a = Mathf.Max(a, Mathf.Clamp01(ul * taper - Mathf.Abs(y - c) + 0.5f));
                float ur = (N - 1 - apex) - x;// right arrow (apex at x=56, points +x)
                if (ur >= 0f && ur <= len) a = Mathf.Max(a, Mathf.Clamp01(ur * taper - Mathf.Abs(y - c) + 0.5f));
                float ud = y - apex;          // down arrow (apex at y=7, points -y)
                if (ud >= 0f && ud <= len) a = Mathf.Max(a, Mathf.Clamp01(ud * taper - Mathf.Abs(x - c) + 0.5f));
                float uu = (N - 1 - apex) - y;// up arrow (apex at y=56, points +y)
                if (uu >= 0f && uu <= len) a = Mathf.Max(a, Mathf.Clamp01(uu * taper - Mathf.Abs(x - c) + 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _split = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _split;
        }

        /// <summary>A soft hollow ring (tintable) for the merge shockwave.</summary>
        public static Sprite Ring()
        {
            if (_ring != null) return _ring;
            const int N = 64; const float c = (N - 1) / 2f;
            const float outer = 30f, inner = 20f, mid = (outer + inner) / 2f, half = (outer - inner) / 2f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / half); // peak on the ring, fades both sides
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            _ring = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _ring;
        }

        /// <summary>A flat top-down GLOSSY jelly block (tintable): rounded corners, a soft
        /// vertical shade and a top highlight so a plain color tint reads as a 3D-ish block.
        /// Used for movable blocker pieces so they clearly look like slidable blocks.</summary>
        public static Sprite GlossyBlock()
        {
            if (_glossy != null) return _glossy;
            const int N = 128;
            const float r = 30f;                      // generous, candy-soft corners
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(r - x, x - (N - 1 - r), 0f);
                float dy = Mathf.Max(r - y, y - (N - 1 - r), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist + 0.5f);
                if (a <= 0f) { tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f)); continue; }

                float ty = (float)y / (N - 1);                       // 0 bottom .. 1 top
                // keep the block mostly its TRUE colour (high base) with only a gentle sheen so the
                // colour stays readable — a small top highlight, no washed-out bright hotspot.
                float body = Mathf.Lerp(0.84f, 1.0f, ty * ty * (3f - 2f * ty));
                float sx = (x - N * 0.5f) / (N * 0.44f), sy = (y - N * 0.72f) / (N * 0.16f);
                float sheen = Mathf.Clamp01(1f - (sx * sx + sy * sy)); sheen *= sheen;
                float b = body + sheen * 0.11f;
                float bd = Mathf.Min(Mathf.Min(x, N - 1 - x), Mathf.Min(y, N - 1 - y)); // rim definition
                b *= Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(bd / 4f));
                b = Mathf.Clamp01(b);
                tex.SetPixel(x, y, new Color(b, b, b, a));
            }
            tex.Apply();
            // border enables 9-slice so a stretched bar (e.g. a door) keeps consistent rounded corners.
            _glossy = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0,
                SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _glossy;
        }

        /// <summary>Same glossy jelly look as GlossyBlock but with a MUCH smaller corner radius,
        /// for door/exit bars that should read as crisp rounded rectangles (9-sliced = consistent
        /// corners at any length).</summary>
        public static Sprite GlossyBar()
        {
            if (_glossyBar != null) return _glossyBar;
            const int N = 128;
            const float r = 28f;                      // generous radius -> softly-rounded door bar
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(r - x, x - (N - 1 - r), 0f);
                float dy = Mathf.Max(r - y, y - (N - 1 - r), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist + 0.5f);
                if (a <= 0f) { tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f)); continue; }

                float ty = (float)y / (N - 1);
                float body = Mathf.Lerp(0.84f, 1.0f, ty * ty * (3f - 2f * ty));
                float sx = (x - N * 0.5f) / (N * 0.44f), sy = (y - N * 0.72f) / (N * 0.16f);
                float sheen = Mathf.Clamp01(1f - (sx * sx + sy * sy)); sheen *= sheen;
                float b = body + sheen * 0.10f;
                float bd = Mathf.Min(Mathf.Min(x, N - 1 - x), Mathf.Min(y, N - 1 - y));
                b *= Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(bd / 4f));
                b = Mathf.Clamp01(b);
                tex.SetPixel(x, y, new Color(b, b, b, a));
            }
            tex.Apply();
            _glossyBar = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0,
                SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _glossyBar;
        }

        /// <summary>A horizontal double-headed arrow (tintable) drawn on a transparent
        /// sprite. Rotated 90° for a vertical rail. Marks axis-locked "rail" blocks.</summary>
        public static Sprite DoubleArrow()
        {
            if (_arrow != null) return _arrow;
            const int N = 64;
            const float cy = (N - 1) * 0.5f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Abs(y - cy);
                bool on = false;
                if (x >= 14 && x <= 49 && d <= 3.5f) on = true;                 // shaft
                if (x >= 6 && x <= 20 && d <= (x - 6) * 0.9f) on = true;         // left head
                if (x >= 43 && x <= 57 && d <= (57 - x) * 0.9f) on = true;       // right head
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, on ? 1f : 0f));
            }
            tex.Apply();
            _arrow = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _arrow;
        }

        /// <summary>A single smooth sprite for a whole polyomino block: only the OUTER
        /// corners are rounded, internal seams are filled (no notches), with a soft gloss.
        /// White base, tinted per block. Cached by shape. Pivot = top-left, 1 cell = 1 unit.</summary>
        public static Sprite BlockShapeSprite(GridPos[] shape)
        {
            var key = ShapeKey(shape);
            if (_shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var occ = new HashSet<int>();
            int W = 1, H = 1;
            foreach (var c in shape)
            {
                occ.Add(Pack(c.X, c.Y));
                if (c.X + 1 > W) W = c.X + 1;
                if (c.Y + 1 > H) H = c.Y + 1;
            }
            bool Filled(int x, int y) => occ.Contains(Pack(x, y));

            const int C = 64;
            const float r = 15f;
            int TW = W * C, TH = H * C;
            var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[TW * TH];
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < TH; y++)
            for (int x = 0; x < TW; x++)
            {
                int idx = y * TW + x;
                px[idx] = clear;
                int rowFromTop = TH - 1 - y;      // 0 at the top
                int cx = x / C, cy = rowFromTop / C;
                if (!Filled(cx, cy)) continue;
                float lx = x - cx * C;             // 0..C-1 left->right within the cell
                float ly = rowFromTop - cy * C;    // 0..C-1 top->bottom within the cell

                float a = 1f;
                if (lx < r && ly < r && !Filled(cx - 1, cy) && !Filled(cx, cy - 1))
                    a = CornerAlpha(lx, ly, r, r, r);
                else if (lx > C - 1 - r && ly < r && !Filled(cx + 1, cy) && !Filled(cx, cy - 1))
                    a = CornerAlpha(lx, ly, C - 1 - r, r, r);
                else if (lx < r && ly > C - 1 - r && !Filled(cx - 1, cy) && !Filled(cx, cy + 1))
                    a = CornerAlpha(lx, ly, r, C - 1 - r, r);
                else if (lx > C - 1 - r && ly > C - 1 - r && !Filled(cx + 1, cy) && !Filled(cx, cy + 1))
                    a = CornerAlpha(lx, ly, C - 1 - r, C - 1 - r, r);
                if (a <= 0f) continue;

                // gentle jelly shading over the WHOLE polyomino — mostly the TRUE colour (high base)
                // with just a soft top sheen, so the block's colour stays clearly readable.
                float fx = TW > 1 ? (float)x / (TW - 1) : 0.5f;         // 0..1 left->right
                float fy = TH > 1 ? 1f - (float)rowFromTop / (TH - 1) : 1f; // 0 bottom .. 1 top
                float body = Mathf.Lerp(0.84f, 1.0f, fy * fy * (3f - 2f * fy));
                float sxx = (fx - 0.5f) / 0.48f, syy = (fy - 0.74f) / 0.18f;
                float sheen = Mathf.Clamp01(1f - (sxx * sxx + syy * syy)); sheen *= sheen;
                float b = body + sheen * 0.11f;
                // rim darkening along the polyomino's OUTER boundary only (internal seams stay smooth)
                float ed = 999f;
                if (!Filled(cx - 1, cy)) ed = Mathf.Min(ed, lx);
                if (!Filled(cx + 1, cy)) ed = Mathf.Min(ed, (C - 1) - lx);
                if (!Filled(cx, cy - 1)) ed = Mathf.Min(ed, ly);
                if (!Filled(cx, cy + 1)) ed = Mathf.Min(ed, (C - 1) - ly);
                b *= Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(ed / 4f));
                b = Mathf.Clamp01(b);
                px[idx] = new Color(b, b, b, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, TW, TH), new Vector2(0f, 1f), C);
            _shapeCache[key] = sprite;
            return sprite;
        }

        private static float CornerAlpha(float lx, float ly, float cxp, float cyp, float r)
        {
            float dx = lx - cxp, dy = ly - cyp;
            return Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
        }

        private static int Pack(int x, int y) => (x + 64) * 1000 + (y + 64);

        private static string ShapeKey(GridPos[] shape)
        {
            var arr = new List<int>(shape.Length);
            foreach (var c in shape) arr.Add(Pack(c.X, c.Y));
            arr.Sort();
            var sb = new StringBuilder();
            foreach (var v in arr) { sb.Append(v); sb.Append(','); }
            return sb.ToString();
        }

        /// <summary>A simple padlock (tintable) on a transparent sprite, for locked blocks.</summary>
        public static Sprite Padlock()
        {
            if (_lock != null) return _lock;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float a = 0f;
                // body: rounded rect, lower part
                float bx = Mathf.Max(16 - x, x - 47, 0f);
                float by = Mathf.Max(8 - y, y - 36, 0f);
                if (Mathf.Sqrt(bx * bx + by * by) <= 5f) a = 1f;
                // shackle: a ring in the upper part (open bottom)
                float dx = x - 32f, dy = y - 38f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (y >= 36 && d >= 8f && d <= 13f) a = 1f;
                // keyhole punched out of the body
                float kx = x - 32f, ky = y - 24f;
                if (Mathf.Sqrt(kx * kx + ky * ky) <= 4f) a = 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _lock = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _lock;
        }

        private static Sprite _glossyCircle;

        /// <summary>A glossy filled circle (tintable) matching the GlossyBlock shading — used as a
        /// round button background (e.g. settings) so it sits in the same family as the sheet's
        /// circular home/undo/restart buttons.</summary>
        public static Sprite GlossyCircle()
        {
            if (_glossyCircle != null) return _glossyCircle;
            const int N = 128; const float c = (N - 1) * 0.5f, rad = 60f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(rad - d + 0.5f);
                if (a <= 0f) { tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f)); continue; }
                float ty = (float)y / (N - 1);
                float body = Mathf.Lerp(0.84f, 1.0f, ty * ty * (3f - 2f * ty)); // mostly true colour
                float sx = (x - N * 0.5f) / (N * 0.42f), sy = (y - N * 0.72f) / (N * 0.16f);
                float sheen = Mathf.Clamp01(1f - (sx * sx + sy * sy)); sheen *= sheen;       // gentle sheen
                float b = body + sheen * 0.11f;
                b *= Mathf.Lerp(0.86f, 1f, Mathf.Clamp01((rad - d) / 4f));                   // rim
                b = Mathf.Clamp01(b);
                tex.SetPixel(x, y, new Color(b, b, b, a));
            }
            tex.Apply();
            _glossyCircle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _glossyCircle;
        }

        private static Sprite _gear;

        /// <summary>A settings gear (tintable) on a transparent sprite, for the settings button.</summary>
        public static Sprite GearIcon()
        {
            if (_gear != null) return _gear;
            const int N = 64; const float c = (N - 1) * 0.5f;
            const int teeth = 8;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float tooth = Mathf.Clamp01(Mathf.Cos(teeth * ang) * 2.2f + 0.5f); // squared-off gear teeth
                float boundary = 17f + 8f * tooth;
                float a = Mathf.Clamp01(boundary - d + 0.7f);
                if (d <= 8f) a = 0f;                     // hollow hub
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _gear = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _gear;
        }

        public static Font BuiltinFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }

        /// <summary>Map a gameplay color to a display color.</summary>
        public static Color ToUnity(CarColor c)
        {
            switch (c)
            {
                // Vivid palette with the easily-confused colours pushed apart. The warm R+Y family
                // (Red / Coral / Orange / Amber / Yellow) used to blur into one orange gradient, so the
                // two warm tertiaries are re-hued by lightness + hue: Coral = light salmon-pink,
                // Amber = deep gold/mustard — each clearly distinct from pure Orange, Red and Yellow.
                case CarColor.Red: return new Color(0.93f, 0.18f, 0.20f);
                case CarColor.Blue: return new Color(0.13f, 0.52f, 1.00f);
                case CarColor.Yellow: return new Color(1.00f, 0.84f, 0.10f);
                case CarColor.Green: return new Color(0.16f, 0.80f, 0.36f);
                case CarColor.Purple: return new Color(0.62f, 0.28f, 0.93f);
                case CarColor.Orange: return new Color(1.00f, 0.50f, 0.00f);  // pure vivid orange
                case CarColor.Pink: return new Color(1.00f, 0.38f, 0.74f);    // hot magenta pink
                case CarColor.Teal: return new Color(0.05f, 0.78f, 0.74f);
                case CarColor.Lime: return new Color(0.68f, 0.90f, 0.10f);
                case CarColor.Brown: return new Color(0.52f, 0.35f, 0.20f);
                case CarColor.Coral: return new Color(1.00f, 0.56f, 0.56f);   // light salmon-pink
                case CarColor.Indigo: return new Color(0.34f, 0.30f, 0.92f);  // deep blue-violet
                case CarColor.Amber: return new Color(0.92f, 0.68f, 0.16f);   // deep gold / mustard
                default: return Color.gray;
            }
        }
    }
}
