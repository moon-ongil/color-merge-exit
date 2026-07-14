using System;

namespace ColorMergeExit.Core
{
    /// <summary>
    /// Integer grid coordinate. Origin is top-left: x grows right, y grows down.
    /// Kept engine-free so the core is unit-testable without UnityEngine.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPos Offset(int dx, int dy) => new GridPos(X + dx, Y + dy);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos p && Equals(p);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
    }
}
