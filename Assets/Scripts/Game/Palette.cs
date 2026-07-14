using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Shared UI-chrome colours. One definition per semantic role so the same navy / blue / grey
    /// isn't re-typed as a raw <c>new Color(...)</c> literal across views — that copy-paste was the
    /// source of subtle, hard-to-spot mismatches. Block/board colours stay in <see cref="VisualAssets"/>;
    /// this is only the reusable interface chrome.
    /// </summary>
    public static class Palette
    {
        // ---- text ----
        public static readonly Color TextDark  = new Color(0.20f, 0.24f, 0.44f); // titles, stage label
        public static readonly Color TextLabel = new Color(0.30f, 0.34f, 0.50f); // captions / heart count
        public static readonly Color TextMuted = new Color(0.36f, 0.41f, 0.56f); // secondary hints

        // ---- accents / buttons ----
        public static readonly Color Blue       = new Color(0.36f, 0.66f, 0.99f); // primary action (close / watch-ad / retry)
        public static readonly Color BlueBright = new Color(0.30f, 0.66f, 0.99f); // home / info circle / current node
        public static readonly Color Slate      = new Color(0.46f, 0.52f, 0.70f); // settings / restart circle
        public static readonly Color Grey       = new Color(0.60f, 0.64f, 0.74f); // secondary / cancel / exit
        public static readonly Color Green      = new Color(0.30f, 0.78f, 0.44f); // confirm / next
        public static readonly Color Orange     = new Color(0.98f, 0.58f, 0.24f); // refill

        // ---- hearts / stars ----
        public static readonly Color HeartRed   = new Color(0.96f, 0.26f, 0.34f);
        public static readonly Color HeartEmpty = new Color(0.72f, 0.75f, 0.84f);
        public static readonly Color StarGold   = new Color(1f, 0.86f, 0.30f);
        public static readonly Color StarGrey   = new Color(0.55f, 0.58f, 0.68f);

        // ---- surfaces ----
        public static readonly Color Panel = new Color(0.97f, 0.97f, 1f);        // rounded popup panel
        public static Color Scrim(float a) => new Color(0f, 0f, 0f, a);          // dim behind modals
    }
}
