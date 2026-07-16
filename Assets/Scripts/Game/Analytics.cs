#if FIREBASE_ANALYTICS
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
#endif
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Provider-agnostic analytics facade. Game code only ever calls these static methods, so the
    /// project compiles and runs whether or not the Firebase SDK is present. The Firebase
    /// implementation is compiled in ONLY when the <c>FIREBASE_ANALYTICS</c> scripting-define symbol
    /// is set (add it in Project Settings ▸ Player ▸ Scripting Define Symbols once the
    /// FirebaseAnalytics unitypackage is imported). Without the define every call is a cheap no-op.
    ///
    /// Event schema (used for "levels passed" + "average pass time" reporting):
    ///   level_start    { level_id, attempt }
    ///   level_complete { level_id, duration_sec, stars, moves, time_left }
    ///   level_fail     { level_id, reason ("timeout" | "deadend"), duration_sec, moves }
    /// Average pass time per level = AVG(duration_sec) of level_complete grouped by level_id
    /// (via the BigQuery export, or a Firestore running sum/count aggregation).
    /// </summary>
    public static class Analytics
    {
#if FIREBASE_ANALYTICS
        private static bool _ready;   // true once Firebase dependencies resolve; gates every LogEvent

        public static void Init()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    _ = FirebaseApp.DefaultInstance;
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);   // belt-and-suspenders
                    _ready = true;
                    Debug.Log("[Analytics] Firebase ready — analytics collection enabled");
                }
                else
                {
                    Debug.LogError($"[Analytics] Firebase dependencies unavailable: {task.Result}");
                }
            });
        }

        public static void LevelStart(int levelId, int attempt)
        {
            if (!_ready) return;
            Debug.Log($"[Analytics] level_start level_id={levelId} attempt={attempt}");
            FirebaseAnalytics.LogEvent("level_start",
                new Parameter("level_id", (long)levelId),
                new Parameter("attempt", (long)attempt));
        }

        public static void LevelComplete(int levelId, float durationSec, int stars, int moves, float timeLeft)
        {
            if (!_ready) return;
            Debug.Log($"[Analytics] level_complete level_id={levelId} duration={durationSec:F1} stars={stars}");
            FirebaseAnalytics.LogEvent("level_complete",
                new Parameter("level_id", (long)levelId),
                new Parameter("duration_sec", (double)durationSec),
                new Parameter("stars", (long)stars),
                new Parameter("moves", (long)moves),
                new Parameter("time_left", (double)timeLeft));
        }

        public static void LevelFail(int levelId, string reason, float durationSec, int moves)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent("level_fail",
                new Parameter("level_id", (long)levelId),
                new Parameter("reason", reason),
                new Parameter("duration_sec", (double)durationSec),
                new Parameter("moves", (long)moves));
        }
#else
        // Firebase SDK not imported / FIREBASE_ANALYTICS not defined → no-ops (keeps the build green).
        public static void Init() { }
        public static void LevelStart(int levelId, int attempt) { }
        public static void LevelComplete(int levelId, float durationSec, int stars, int moves, float timeLeft) { }
        public static void LevelFail(int levelId, string reason, float durationSec, int moves) { }
#endif
    }
}
