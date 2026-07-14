using System.Collections.Generic;
using System.IO;
using ColorMergeExit.Core;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Loads a <see cref="LevelData"/> by id from StreamingAssets/Levels/level_NNN.json
    /// (editor/standalone). Falls back to an in-code tutorial level so the game is
    /// always playable even before any JSON is authored / on platforms where the
    /// StreamingAssets path is not a readable file (e.g. Android inside the apk).
    /// </summary>
    public static class LevelRepository
    {
        public static LevelData Load(int id)
        {
            return TryLoadJson(id) ?? BuildDefault();
        }

        private static LevelData TryLoadJson(int id)
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Levels", $"level_{id:000}.json");
                if (File.Exists(path))
                    return JsonUtility.FromJson<LevelData>(File.ReadAllText(path));
            }
            catch
            {
                // ignore and fall back
            }
            return null;
        }

        /// <summary>Tutorial level: slide each colored block out its matching-color door.</summary>
        public static LevelData BuildDefault()
        {
            return new LevelData
            {
                id = 1,
                name = "Tutorial",
                width = 6,
                height = 6,
                timeLimitSeconds = 90f,
                star2SecondsLeft = 27f,
                star3SecondsLeft = 45f,
                blocks = new List<BlockSpawnData>
                {
                    new BlockSpawnData { id = 1, color = CarColor.Red, x = 1, y = 1, w = 2, h = 1 },
                    new BlockSpawnData { id = 2, color = CarColor.Blue, x = 4, y = 3, w = 1, h = 2 },
                    new BlockSpawnData { id = 3, color = CarColor.Yellow, x = 2, y = 4, w = 1, h = 1 },
                },
                doors = new List<DoorData>
                {
                    new DoorData { edge = Edge.Right, laneStart = 1, length = 1, color = CarColor.Red },
                    new DoorData { edge = Edge.Bottom, laneStart = 4, length = 1, color = CarColor.Blue },
                    new DoorData { edge = Edge.Left, laneStart = 4, length = 1, color = CarColor.Yellow },
                },
            };
        }
    }
}
