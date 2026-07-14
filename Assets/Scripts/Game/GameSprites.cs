using ColorMergeExit.Core;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Optional sprite set for the game. When assigned (via a Resources asset), the
    /// views render with these sprites; when a slot is empty, the view falls back to
    /// the runtime-generated colored squares, so the game always renders.
    ///
    /// Color arrays are indexed by <see cref="CarColor"/>:
    /// 0=Red, 1=Blue, 2=Yellow, 3=Green, 4=Purple, 5=Orange.
    ///
    /// Rows 1-6 map to the single GPT sprite sheet (blocks_sheet.png):
    /// 1=color blocks, 2=exit portals, 3=movable obstacles, 4=static walls, 5=tiles,
    /// 6=UI/effects. HUD button icons still come from the legacy ui_buttons sheet.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSprites", menuName = "Color Merge Exit/Game Sprites")]
    public sealed class GameSprites : ScriptableObject
    {
        [Header("Row 1 — color blocks (index: Red,Blue,Yellow,Green,Purple,Orange)")]
        public Sprite[] block = new Sprite[6];

        [Header("Row 2 — exit portals by color (index as above)")]
        public Sprite[] exitByColor = new Sprite[6];

        [Header("Row 3 — movable obstacle blocks (neutral variants)")]
        public Sprite[] movableObstacles = new Sprite[6];

        [Header("Row 4 — static wall tiles (variants)")]
        public Sprite[] staticWalls = new Sprite[6];

        [Header("Row 5 — board/background tiles (variants)")]
        public Sprite[] tiles = new Sprite[6];

        [Header("Row 6 — UI / effects (optional)")]
        public Sprite btnPause;
        public Sprite btnNext;
        public Sprite btnClose;
        public Sprite btnSound;
        public Sprite sparkle;

        [Header("Board defaults")]
        public Sprite obstacle; // legacy single default (= movableObstacles[0])

        [Header("HUD button icons (from legacy ui_buttons sheet)")]
        public Sprite btnRestart;
        public Sprite btnUndo;
        public Sprite btnHome;
        public Sprite btnSettings;
        public Sprite star;

        /// <summary>The glossy block sprite for a color, or null to fall back to a tinted square.</summary>
        public Sprite BlockSprite(CarColor color) => At(block, (int)color);

        public Sprite ExitSprite(CarColor color) => At(exitByColor, (int)color);

        /// <summary>A movable-obstacle sprite; <paramref name="variant"/> picks among the neutral looks.</summary>
        public Sprite MovableObstacle(int variant)
        {
            var s = At(movableObstacles, ((variant % 6) + 6) % 6);
            return s ?? obstacle;
        }

        private static Sprite At(Sprite[] arr, int i) =>
            arr != null && i >= 0 && i < arr.Length ? arr[i] : null;
    }
}
