using TMPro;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Shared world-space UI primitives. Every view (HUD, level-select, board overlays) builds its
    /// sprites and text through these helpers, so the font, sizing, 9-slicing, weight and hit-testing
    /// are each defined ONCE instead of being copy-pasted (and drifting) per view.
    /// </summary>
    public static class Ui
    {
        private static TMP_FontAsset _font;

        /// <summary>The shared display font (heavy Titan One SDF, Pretendard fallback), weight-tuned once.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.Load<TMP_FontAsset>("Fonts/Game SDF") ??
                            Resources.Load<TMP_FontAsset>("Fonts/Pretendard SDF");
                Typography.Tune(_font);   // uniform, un-muddy weight for all text (applied once)
                return _font;
            }
        }

        /// <summary>A sprite renderer under <paramref name="parent"/>. Defaults to the plain square.</summary>
        public static SpriteRenderer Sprite(Transform parent, string name, Vector3 pos, Color color,
            int order, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : VisualAssets.Square();
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A centered TextMeshPro under <paramref name="parent"/> in the shared font.</summary>
        public static TMP_Text Text(Transform parent, string name, Vector3 pos, float fontSize,
            int order = Sorting.Text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Font;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(28f, 8f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = order;
            return tmp;
        }

        /// <summary>9-slice a rounded sprite to an exact world size so its corners never stretch.</summary>
        public static void Sliced(SpriteRenderer sr, float w, float h)
        {
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w, h);
            sr.transform.localScale = Vector3.one;
        }

        /// <summary>Set a text's mesh sorting order.</summary>
        public static void Order(TMP_Text t, int order)
        {
            var r = t.GetComponent<Renderer>();
            if (r != null) r.sortingOrder = order;
        }

        /// <summary>Thin the heavy display glyphs via a per-instance face-dilate (crisp, not muddy, no Bold).</summary>
        public static void Lighten(TMP_Text t, float dilate = Typography.FaceDilate)
        {
            if (t != null && t.fontMaterial != null)
                t.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
        }

        /// <summary>Axis-aligned box hit test: is <paramref name="p"/> within the half-extents of <paramref name="center"/>.</summary>
        public static bool Hit(Vector3 center, Vector3 p, float halfW, float halfH) =>
            Mathf.Abs(p.x - center.x) <= halfW && Mathf.Abs(p.y - center.y) <= halfH;

        /// <summary>Uniformly scale a sprite so it renders at an exact world width (keeps aspect ratio).</summary>
        public static void FitWidth(SpriteRenderer sr, float worldWidth)
        {
            if (sr == null || sr.sprite == null) return;
            float bx = sr.sprite.bounds.size.x;
            float s = worldWidth / Mathf.Max(0.0001f, bx);
            sr.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
