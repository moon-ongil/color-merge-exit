namespace ColorMergeExit.Core
{
    /// <summary>Block / door colors. Red, Blue, Yellow are primaries; Green, Purple,
    /// Orange are secondaries; Pink, Teal, Lime, Brown are further mixes (see
    /// <see cref="ColorMix"/>). Keep the first 6 first for the art sheet indexing.</summary>
    public enum CarColor
    {
        Red,
        Blue,
        Yellow,
        Green,
        Purple,
        Orange,
        Pink,
        Teal,
        Lime,
        Brown,
        Coral,   // Red + Orange
        Indigo,  // Blue + Purple
        Amber    // Yellow + Orange
    }

    /// <summary>Board edge a door sits on.</summary>
    public enum Edge
    {
        Top,    // y = -1 side
        Bottom, // y = height side
        Left,   // x = -1 side
        Right   // x = width side
    }

    /// <summary>Which axes a block may slide along. Free blocks move in all four
    /// directions; axis-locked "rail" blocks only slide along one axis.</summary>
    public enum MoveAxis
    {
        Free,
        Horizontal,
        Vertical
    }

    /// <summary>Outcome of a move attempt.</summary>
    public enum MoveResult
    {
        Invalid, // no such block / bad direction / axis-locked off its rail
        Blocked, // path obstructed or door doesn't fit; state unchanged
        Moved,   // slid by some amount (possibly recolored by a paint cell)
        Merged,  // slid into another block; the two combined into a mixed color
        Split,   // slid onto a splitter cell; a mixed block broke back into two blocks
        Exited   // block passed through a matching door and left the board
    }

    /// <summary>Color mixing for the signature "mix to match the door" twist.
    /// Primaries make secondaries; a primary plus the right secondary makes a tertiary
    /// (Pink/Teal/Lime); mixing two secondaries muddies to Brown.</summary>
    public static class ColorMix
    {
        public static bool IsPrimary(CarColor c) =>
            c == CarColor.Red || c == CarColor.Blue || c == CarColor.Yellow;

        public static bool IsSecondary(CarColor c) =>
            c == CarColor.Green || c == CarColor.Purple || c == CarColor.Orange;

        private static bool Pair(CarColor a, CarColor b, CarColor c, CarColor d) =>
            (a == c && b == d) || (a == d && b == c);

        /// <summary>Mix two colors; false if the combination has no defined result.</summary>
        public static bool TryMix(CarColor a, CarColor b, out CarColor result)
        {
            result = a;
            if (a == b) return false;

            // primary + primary -> secondary
            if (Pair(a, b, CarColor.Red, CarColor.Blue)) { result = CarColor.Purple; return true; }
            if (Pair(a, b, CarColor.Blue, CarColor.Yellow)) { result = CarColor.Green; return true; }
            if (Pair(a, b, CarColor.Red, CarColor.Yellow)) { result = CarColor.Orange; return true; }

            // primary + secondary -> tertiary. Only the NICE, analogous tertiaries are makeable
            // (a primary blended with a secondary it is a component of). The muddy complementary
            // combos (Red+Green, Blue+Orange, Yellow+Purple = brown/grey) and Teal (Blue+Green)
            // just block instead of merging, so no door ever needs an ugly dead-end colour.
            if (Pair(a, b, CarColor.Red, CarColor.Purple)) { result = CarColor.Pink; return true; }   // more red
            if (Pair(a, b, CarColor.Blue, CarColor.Purple)) { result = CarColor.Indigo; return true; } // more blue
            if (Pair(a, b, CarColor.Red, CarColor.Orange)) { result = CarColor.Coral; return true; }  // more red
            if (Pair(a, b, CarColor.Yellow, CarColor.Orange)) { result = CarColor.Amber; return true; } // more yellow
            if (Pair(a, b, CarColor.Yellow, CarColor.Green)) { result = CarColor.Lime; return true; }  // more yellow

            return false;
        }

        /// <summary>Reverse of <see cref="TryMix"/>: break a mixed color back into the two
        /// colors that make it (the signature "splitter" twist — a merged block that slides
        /// onto a splitter cell splits into its two components). Primaries can't be split.
        /// The decomposition is unique so the split is deterministic.</summary>
        public static bool TrySplit(CarColor c, out CarColor a, out CarColor b)
        {
            switch (c)
            {
                case CarColor.Purple: a = CarColor.Red; b = CarColor.Blue; return true;
                case CarColor.Green: a = CarColor.Blue; b = CarColor.Yellow; return true;
                case CarColor.Orange: a = CarColor.Red; b = CarColor.Yellow; return true;
                case CarColor.Pink: a = CarColor.Red; b = CarColor.Purple; return true;
                case CarColor.Indigo: a = CarColor.Blue; b = CarColor.Purple; return true;
                case CarColor.Coral: a = CarColor.Red; b = CarColor.Orange; return true;
                case CarColor.Amber: a = CarColor.Yellow; b = CarColor.Orange; return true;
                case CarColor.Lime: a = CarColor.Yellow; b = CarColor.Green; return true;
                default: a = c; b = c; return false;
            }
        }
    }
}
