using System.Collections.Generic;
using ColorMergeExit.Core;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Loads a <see cref="LevelData"/> by id from Resources/Levels/level_NNN.json.
    /// Resources is used rather than StreamingAssets because on Android the latter
    /// lives inside the apk (a `jar:file://` url), where plain file IO always fails —
    /// which silently served the fallback level for every stage. Falls back to an
    /// in-code tutorial level if the asset is missing or malformed.
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
                // Resources paths are extension-less and always use forward slashes.
                var asset = Resources.Load<TextAsset>($"Levels/level_{id:000}");
                if (asset == null) return null;
                var data = JsonUtility.FromJson<LevelData>(asset.text);
                Resources.UnloadAsset(asset);
                return data;
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
