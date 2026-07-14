using TMPro;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// A tap target whose HIT REGION is derived from the SAME size it was drawn at, so the visual
    /// pill and the touch box can never drift apart. This couples the two values that used to be
    /// maintained separately as magic numbers — a <c>Sliced(sr, 3.1f, 1.25f)</c> for the look and a
    /// <c>Hit(pos, world, 1.5f, 0.7f)</c> for the touch — which is exactly why resizing a button kept
    /// silently breaking where it could be tapped.
    /// </summary>
    public sealed class UiButton
    {
        public readonly Transform Root;
        public readonly SpriteRenderer Body;
        public readonly TMP_Text Label;
        private readonly Vector3 _center;
        private readonly float _halfW, _halfH;

        private UiButton(Transform root, SpriteRenderer body, TMP_Text label,
            Vector3 center, float halfW, float halfH)
        {
            Root = root; Body = body; Label = label;
            _center = center; _halfW = halfW; _halfH = halfH;
        }

        /// <summary>True if a world point falls inside this button's (visual-derived) touch box.</summary>
        public bool Contains(Vector3 world) => Ui.Hit(_center, world, _halfW, _halfH);

        public void SetColor(Color c) { if (Body != null) Body.color = c; }

        /// <summary>
        /// A rounded pill of exact world <paramref name="size"/> with an optional centered label.
        /// The touch box is the pill's own half-extents grown by <paramref name="hitPad"/> (for a
        /// comfortable tap) — never a hand-tuned separate number.
        /// </summary>
        public static UiButton Pill(Transform parent, string name, Vector3 pos, Vector2 size, Color color,
            string label, float labelSize, Color labelColor,
            int bodyOrder = Sorting.DialogButton, int textOrder = Sorting.DialogText, float hitPad = 0.1f)
        {
            var body = Ui.Sprite(parent, name, pos, color, bodyOrder, VisualAssets.RoundedSquare());
            Ui.Sliced(body, size.x, size.y);

            TMP_Text t = null;
            if (!string.IsNullOrEmpty(label))
            {
                t = Ui.Text(parent, name + "_L", pos + new Vector3(0f, 0f, -1f), labelSize, textOrder);
                t.text = label;
                t.color = labelColor;
                Ui.Lighten(t);
            }
            return new UiButton(body.transform, body, t, pos, size.x * 0.5f + hitPad, size.y * 0.5f + hitPad);
        }
    }
}
