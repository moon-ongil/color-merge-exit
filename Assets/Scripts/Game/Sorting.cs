namespace ColorMergeExit.Game
{
    /// <summary>
    /// Named sprite/text sorting orders. Kept in one place so overlapping layers (HUD, popups,
    /// scrims, dialogs) never silently collide and the z-order intent is readable instead of a
    /// bare int scattered across views.
    /// </summary>
    public static class Sorting
    {
        // Default world text (HUD labels/timer not part of a popup).
        public const int Text = 50;

        // ---- in-play HUD chrome (a small self-contained stack below the popups) ----
        public const int HudBar      = 40;  // timer-bar background / nav + item button bodies
        public const int HudBarFill  = 41;  // timer-bar fill (above the bar bg)
        public const int HudGlyph    = 42;  // glyphs on nav buttons / count badges
        public const int HudGlyphTop = 43;  // item glyphs / padlocks (above the badge)
        public const int HudHeart    = 45;  // hearts row

        // ---- shared modal popup stack (settings / info / no-hearts): a panel on a backdrop ----
        public const int Backdrop    = 300;  // dim behind the panel
        public const int Panel       = 310;  // the rounded panel body
        public const int PanelButton = 320;  // buttons / pills sitting on the panel
        public const int PanelIcon   = 322;  // small icons / colour dots on the panel
        public const int PanelText   = 330;  // panel labels

        // ---- result / confirm dialogs (title + buttons float on a scrim, no panel) ----
        public const int Scrim        = 350; // full-screen dim
        public const int DialogButton = 358; // button body
        public const int DialogText   = 360; // title / button label / stars
    }
}
