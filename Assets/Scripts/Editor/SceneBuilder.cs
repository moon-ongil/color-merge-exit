using System.IO;
using ColorMergeExit.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Regenerates the playable Main scene using Unity's own scene API, so a valid
    /// scene can always be produced from the Editor even if the checked-in
    /// Main.unity is ever lost or fails to load.
    /// Menu: Tools ▸ Color Exit.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/Color Exit/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject("Bootstrap");
            boot.AddComponent<Bootstrap>();

            var dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log($"[Color Exit] Rebuilt {ScenePath} and set it as build scene 0.");
        }

        [MenuItem("Tools/Color Exit/Open Main Scene")]
        public static void OpenMainScene()
        {
            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath);
            else
                RebuildMainScene();
        }
    }
}
