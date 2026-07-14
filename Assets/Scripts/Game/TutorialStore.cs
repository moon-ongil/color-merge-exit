using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>Remembers which one-time tutorials the player has already seen (PlayerPrefs).</summary>
    public static class TutorialStore
    {
        public static bool Seen(string id) => PlayerPrefs.GetInt(Key(id), 0) == 1;

        public static void MarkSeen(string id)
        {
            PlayerPrefs.SetInt(Key(id), 1);
            PlayerPrefs.Save();
        }

        private static string Key(string id) => $"tut.{id}";
    }
}
