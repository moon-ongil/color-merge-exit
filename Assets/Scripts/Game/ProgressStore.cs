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

        /// <summary>Record a clear: keep best stars/time and unlock the next level. Each write is
        /// mirrored to cloud save (iCloud KVS on iOS) so progress survives reinstall / device change.</summary>
        public static void RecordClear(int level, int stars, float remaining)
        {
            if (stars > Stars(level)) { PlayerPrefs.SetInt(StarsKey(level), stars); CloudSave.SetInt(StarsKey(level), stars); }
            if (remaining > BestRemaining(level)) { PlayerPrefs.SetFloat(TimeKey(level), remaining); CloudSave.SetFloat(TimeKey(level), remaining); }

            int next = level + 1;
            if (next <= TotalLevels && next > PlayerPrefs.GetInt("unlocked", 1))
            {
                PlayerPrefs.SetInt("unlocked", next);
                CloudSave.SetInt("unlocked", next);
            }

            PlayerPrefs.Save();
            CloudSave.Flush();
        }

        /// <summary>Two-way MAX-merge between local PlayerPrefs and cloud save, run once at startup:
        /// whichever install/device reached further wins (higher unlocked, more stars, better time),
        /// and the winner is written back to both so they converge. No-op when cloud save is
        /// unavailable (editor / Android / unsigned iOS). Only levels up to the furthest unlocked are
        /// scanned, so the cost stays proportional to actual progress.</summary>
        public static void SyncFromCloud()
        {
            if (!CloudSave.Available) return;
            CloudSave.Flush();   // pull the latest cached cloud values

            int localU = PlayerPrefs.GetInt("unlocked", 1);
            int cloudU = CloudSave.GetInt("unlocked", 1);
            int bestU = Mathf.Clamp(Mathf.Max(localU, cloudU), 1, TotalLevels);
            if (bestU != localU) PlayerPrefs.SetInt("unlocked", bestU);
            if (bestU != cloudU) CloudSave.SetInt("unlocked", bestU);

            for (int i = 1; i <= bestU; i++)
            {
                int ls = PlayerPrefs.GetInt(StarsKey(i), 0), cs = CloudSave.GetInt(StarsKey(i), 0);
                int bs = Mathf.Max(ls, cs);
                if (bs > 0) { if (bs != ls) PlayerPrefs.SetInt(StarsKey(i), bs); if (bs != cs) CloudSave.SetInt(StarsKey(i), bs); }

                float lb = PlayerPrefs.GetFloat(TimeKey(i), -1f), cb = CloudSave.GetFloat(TimeKey(i), -1f);
                float bb = Mathf.Max(lb, cb);
                if (bb >= 0f) { if (bb != lb) PlayerPrefs.SetFloat(TimeKey(i), bb); if (bb != cb) CloudSave.SetFloat(TimeKey(i), bb); }
            }

            PlayerPrefs.Save();
            CloudSave.Flush();
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
                CloudSave.SetInt(StarsKey(i), 0);
                CloudSave.SetFloat(TimeKey(i), -1f);
            }
            PlayerPrefs.DeleteKey("unlocked");
            CloudSave.SetInt("unlocked", 1);
            PlayerPrefs.Save();
            CloudSave.Flush();
        }

        private static string StarsKey(int level) => $"lvl.{level}.stars";
        private static string TimeKey(int level) => $"lvl.{level}.best";
    }
}
