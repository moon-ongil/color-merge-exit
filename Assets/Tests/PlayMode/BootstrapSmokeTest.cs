using System.Collections;
using ColorMergeExit.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorMergeExit.Tests
{
    /// <summary>
    /// Runtime smoke test for the app flow: the scene boots into the stage select,
    /// and selecting a level builds the playable board/HUD from code without throwing.
    /// </summary>
    public class BootstrapSmokeTest
    {
        [UnityTest]
        public IEnumerator MainScene_ShowsSelect_ThenPlaysLevel()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.IsNotNull(Camera.main, "Bootstrap should create a MainCamera");
            Assert.IsNotNull(GameObject.Find("LevelSelect"), "stage select shown at start");

            var flow = Object.FindFirstObjectByType<GameFlow>();
            Assert.IsNotNull(flow, "GameFlow present");

            flow.PlayLevel(1);
            yield return null;
            yield return null;

            Assert.IsNotNull(GameObject.Find("Game"), "game object active after selecting a level");
            Assert.IsNotNull(GameObject.Find("BoardRoot"), "board built");
            Assert.IsNotNull(GameObject.Find("Block_1"), "level 1 target block rendered");
            Assert.IsNotNull(GameObject.Find("HUD"), "HUD active");
            Assert.IsNotNull(GameObject.Find("RestartBtn"), "HUD restart button built");

            yield return null;
        }
    }
}
