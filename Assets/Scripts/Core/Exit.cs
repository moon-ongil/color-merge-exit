using System.Collections.Generic;

namespace ColorMergeExit.Core
{
    /// <summary>
    /// A colored door on a board edge, spanning a contiguous range of lanes. A door
    /// carries a COLOR SEQUENCE: only a block whose color equals the door's CURRENT color
    /// (and that fits the door's width) may pass; each block that leaves advances the door
    /// to its next color. So one door can serve several blocks of different colors in order.
    ///
    /// Lane = the row (Y) for Left/Right doors, or the column (X) for Top/Bottom doors.
    /// A door covers lanes [LaneStart, LaneStart + Length).
    /// </summary>
    public sealed class Exit
    {
        public readonly Edge Edge;
        public readonly int LaneStart;
        public readonly int Length;
        public readonly IReadOnlyList<CarColor> Sequence;

        public int Index { get; private set; }

        public Exit(Edge edge, int laneStart, int length, IReadOnlyList<CarColor> sequence, int index = 0)
        {
            if (sequence == null || sequence.Count == 0)
                throw new System.ArgumentException("Door color sequence must have at least one color.", nameof(sequence));
            Edge = edge;
            LaneStart = laneStart;
            Length = length < 1 ? 1 : length;
            Sequence = sequence;
            Index = index;
        }

        /// <summary>Convenience single-color door.</summary>
        public Exit(Edge edge, int laneStart, int length, CarColor color)
            : this(edge, laneStart, length, new[] { color }) { }

        public int LaneEnd => LaneStart + Length - 1;
        public bool Covers(int lane) => lane >= LaneStart && lane <= LaneEnd;

        /// <summary>Color a block must currently be to pass this door.</summary>
        public CarColor CurrentColor => Sequence[Index % Sequence.Count];

        /// <summary>Colors still to come after the current one (for the UI hint).</summary>
        public IEnumerable<CarColor> Upcoming
        {
            get { for (int i = Index + 1; i < Sequence.Count; i++) yield return Sequence[i]; }
        }

        public bool Done => Index >= Sequence.Count;

        public void Advance() => Index++;
        public void SetIndex(int index) => Index = index;
    }
}
