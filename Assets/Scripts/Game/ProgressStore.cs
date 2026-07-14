using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Persistent progress via PlayerPrefs: best star rating and best remaining time
    /// per level, and the highest unlocked level. Level 1 is always unlocked; clearing
    /// a level unlocks the next.
    /// </summary>
    public static class ProgressStore
    {
        public const int TotalLevels = 1000;

        public static int Stars(int level) => PlayerPrefs.GetInt(StarsKey(level), 0);

        /// <summary>Best remaining seconds at clear (higher = better), or -1 if never cleared.</summary>
        public static float BestRemaining(int level) => PlayerPrefs.GetFloat(TimeKey(level), -1f);

        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt("unlocked", 1), 1, TotalLevels);

        public static bool IsUnlocked(int level) => level >= 1 && level <= Unlocked;

        /// <summary>Record a clear: keep best stars/time and unlock the next level.</summary>
        public static void RecordClear(int level, int stars, float remaining)
        {
            if (stars > Stars(level)) PlayerPrefs.SetInt(StarsKey(level), stars);
            if (remaining > BestRemaining(level)) PlayerPrefs.SetFloat(TimeKey(level), remaining);

            int next = level + 1;
            if (next <= TotalLevels && next > PlayerPrefs.GetInt("unlocked", 1))
                PlayerPrefs.SetInt("unlocked", next);

            PlayerPrefs.Save();
        }

        /// <summary>Dev/testing helper: mark levels 1..upto as cleared and unlock upto+1,
        /// without overwriting a better existing star rating.</summary>
        public static void DevUnlock(int upto)
        {
            upto = Mathf.Clamp(upto, 1, TotalLevels);
            for (int i = 1; i <= upto; i++)
                if (Stars(i) < 1) PlayerPrefs.SetInt(StarsKey(i), 2);
            int next = Mathf.Min(upto + 1, TotalLevels);
            if (next > PlayerPrefs.GetInt("unlocked", 1)) PlayerPrefs.SetInt("unlocked", next);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            for (int i = 1; i <= TotalLevels; i++)
            {
                PlayerPrefs.DeleteKey(StarsKey(i));
                PlayerPrefs.DeleteKey(TimeKey(i));
            }
            PlayerPrefs.DeleteKey("unlocked");
            PlayerPrefs.Save();
        }

        private static string StarsKey(int level) => $"lvl.{level}.stars";
        private static string TimeKey(int level) => $"lvl.{level}.best";
    }
}
