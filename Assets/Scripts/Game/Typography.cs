using TMPro;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Single source of truth for UI text sizing + weight. All TMP fontSizes should reference one
    /// of these constants so the app stays visually consistent (sizes are in world/ortho units).
    /// Weight is handled globally by <see cref="Tune"/>: the display font (Titan One) is already
    /// heavy, so instead of faux-Bold everywhere we apply a slight negative face-dilate once on the
    /// shared material — crisp, uniform, never muddy.
    /// </summary>
    public static class Typography
    {
        // ---- size scale (small → large) ----
        // Biased LARGER across the board: on a real phone the earlier sizes read too small. Every UI
        // string references one of these, so nudging the scale here scales the whole app consistently.
        public const float Caption = 3.4f;   // badges, tiny hints ("x3", refill timer)
        public const float Label   = 4.0f;   // secondary labels (ITEMS, settings rows)
        public const float Body    = 4.4f;   // dialog body / captions
        public const float Button  = 4.6f;   // button labels (NEXT / YES / CLOSE)
        public const float Heading = 5.0f;   // popup titles (SETTINGS, RESTART?)
        public const float Title   = 6.2f;   // stage label / home logo line
        public const float Readout = 7.0f;   // the countdown timer
        public const float Display = 9.0f;   // big result banner (CLEAR! / TIME UP)

        // Consistent weight for the heavy display font: a touch thinner than the raw glyphs.
        public const float FaceDilate = -0.09f;

        private static bool _tuned;

        /// <summary>Apply the uniform weight to the shared font material (once). Call from font loaders.</summary>
        public static void Tune(TMP_FontAsset font)
        {
            if (_tuned || font == null || font.material == null) return;
            font.material.SetFloat(ShaderUtilities.ID_FaceDilate, FaceDilate);
            _tuned = true;
        }
    }
}
