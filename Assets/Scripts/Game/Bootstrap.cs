using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Scene entry point. Spawns the <see cref="GameFlow"/> which builds everything
    /// (camera, audio, stage select, gameplay) from code — no prefabs/wiring.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        // Dev/testing: pre-complete stages 1..N so you can jump straight to testing later
        // ones. Set to 0 before release.
        private const int DevUnlockUpTo = 500;

        private void Awake()
        {
            if (DevUnlockUpTo > 0) ProgressStore.DevUnlock(DevUnlockUpTo);
            new GameObject("GameFlow").AddComponent<GameFlow>();
        }
    }
}
