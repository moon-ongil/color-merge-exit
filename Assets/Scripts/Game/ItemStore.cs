using UnityEngine;

namespace ColorMergeExit.Game
{
    public enum ItemType { Hint = 0, AddTime = 1, ForceSplit = 2 }

    /// <summary>
    /// Persistent, cross-level item inventory (PlayerPrefs). Each item starts with
    /// <see cref="StartCount"/> charges but is LOCKED until its tutorial unlocks it.
    /// Spent charges persist across levels; refills come only from rewarded ads / purchases.
    /// </summary>
    public static class ItemStore
    {
        public const int StartCount = 2;

        private static string CountKey(ItemType t) => $"item.{(int)t}.count";
        private static string UnlockKey(ItemType t) => $"item.{(int)t}.unlocked";
        private static string InitKey(ItemType t) => $"item.{(int)t}.init";

        /// <summary>Current charges. Seeds the starting count once, on first access.</summary>
        public static int Count(ItemType t)
        {
            if (PlayerPrefs.GetInt(InitKey(t), 0) == 0)
            {
                PlayerPrefs.SetInt(CountKey(t), StartCount);
                PlayerPrefs.SetInt(InitKey(t), 1);
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetInt(CountKey(t), StartCount);
        }

        public static bool IsUnlocked(ItemType t) => PlayerPrefs.GetInt(UnlockKey(t), 0) == 1;

        public static void Unlock(ItemType t)
        {
            if (IsUnlocked(t)) return;
            PlayerPrefs.SetInt(UnlockKey(t), 1);
            PlayerPrefs.Save();
        }

        public static void Add(ItemType t, int n)
        {
            PlayerPrefs.SetInt(CountKey(t), Mathf.Max(0, Count(t) + n));
            PlayerPrefs.Save();
        }

        /// <summary>Consume one charge; false (no change) if none left.</summary>
        public static bool Spend(ItemType t)
        {
            int c = Count(t);
            if (c <= 0) return false;
            PlayerPrefs.SetInt(CountKey(t), c - 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
