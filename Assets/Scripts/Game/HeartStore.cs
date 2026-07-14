using System;
using System.Globalization;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Lives/hearts economy (persisted in PlayerPrefs). Max 5 hearts; one refills every 5 minutes
    /// while below max; a heart is spent when a level is FAILED. A rewarded ad can top them up.
    /// Time is wall-clock (survives app restarts) via a stored "next refill" unix timestamp.
    /// </summary>
    public static class HeartStore
    {
        public const int Max = 5;
        public const int RefillSeconds = 300; // 5 minutes

        private const string CountKey = "hearts.count";
        private const string RefillAtKey = "hearts.refillAt"; // unix seconds when the next heart arrives

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static long GetRefillAt() =>
            long.TryParse(PlayerPrefs.GetString(RefillAtKey, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0L;
        private static void SetRefillAt(long v) => PlayerPrefs.SetString(RefillAtKey, v.ToString(CultureInfo.InvariantCulture));

        /// <summary>Bring the stored state up to date with elapsed wall-clock time.</summary>
        private static void Sync()
        {
            int count = PlayerPrefs.GetInt(CountKey, Max);
            if (count >= Max) { count = Max; PlayerPrefs.SetInt(CountKey, count); return; }

            long refillAt = GetRefillAt();
            if (refillAt <= 0) { SetRefillAt(Now + RefillSeconds); PlayerPrefs.Save(); return; }

            long now = Now;
            while (count < Max && now >= refillAt) { count++; refillAt += RefillSeconds; }
            PlayerPrefs.SetInt(CountKey, count);
            if (count >= Max) SetRefillAt(0); else SetRefillAt(refillAt);
            PlayerPrefs.Save();
        }

        public static int Current { get { Sync(); return PlayerPrefs.GetInt(CountKey, Max); } }

        public static bool HasHeart => Current > 0;

        /// <summary>Seconds until the next heart refills, or 0 if already full.</summary>
        public static int SecondsToNext
        {
            get
            {
                Sync();
                if (PlayerPrefs.GetInt(CountKey, Max) >= Max) return 0;
                return (int)Mathf.Max(0, GetRefillAt() - Now);
            }
        }

        /// <summary>Spend one heart (on a level failure). Starts the refill timer if it was full.</summary>
        public static void Spend()
        {
            Sync();
            int count = PlayerPrefs.GetInt(CountKey, Max);
            if (count <= 0) return;
            bool wasFull = count >= Max;
            count--;
            PlayerPrefs.SetInt(CountKey, count);
            if (wasFull) SetRefillAt(Now + RefillSeconds); // timer wasn't running while full
            PlayerPrefs.Save();
        }

        /// <summary>Add hearts (e.g. from a rewarded ad), capped at Max.</summary>
        public static void Add(int n)
        {
            Sync();
            int count = Mathf.Min(Max, PlayerPrefs.GetInt(CountKey, Max) + Mathf.Max(0, n));
            PlayerPrefs.SetInt(CountKey, count);
            if (count >= Max) SetRefillAt(0);
            PlayerPrefs.Save();
        }

        /// <summary>Refill to full (rewarded ad "refill hearts").</summary>
        public static void Refill() => Add(Max);
    }
}
