using UnityEditor;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Configures the startup splash so it plays "Unity logo → our logo" in sequence. On a Unity
    /// Personal license the "Made with Unity" logo is mandatory, so we append our own branded logo
    /// (Assets/Art/SplashLogo.png, the app-icon art) after it on a matching sky-blue background.
    /// Runs at the start of each build (see BuildScript); can also be invoked standalone:
    ///   Unity -batchmode -quit -projectPath . -executeMethod ColorMergeExit.Editor.SplashSetup.Apply
    /// </summary>
    public static class SplashSetup
    {
        private const string SplashPath = "Assets/Art/SplashLogo.png";

        // Near-black background: the transparent rainbow art pops on a dark backdrop and the (white)
        // Unity logo stays legible.
        private static readonly Color BgColor = new Color(0.05f, 0.05f, 0.06f, 1f);

        public static void Apply()
        {
            // The splash logo must be a Sprite; flip the import type if needed.
            var importer = AssetImporter.GetAtPath(SplashPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Splash] source not found at {SplashPath} — skipping splash setup.");
                return;
            }
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;   // a Single sprite is loadable as Sprite
                importer.SaveAndReimport();
                // Force the reimport to finish so the generated Sprite sub-asset is available now.
                AssetDatabase.ImportAsset(SplashPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Splash] could not load Sprite at {SplashPath} — skipping.");
                return;
            }

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;   // Personal license: shown regardless
            // Sequential: Unity logo first, then our logo full-screen (vs. all logos together).
            // Unity logo anchored at the BOTTOM of the screen (lower), our feathered logo centered above.
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
            PlayerSettings.SplashScreen.backgroundColor = BgColor;
            // No blur + minimal overlay so the solid sky-blue backgroundColor shows at full brightness
            // and the feathered logo edge melts into it (no muddy teal, no hard rectangle line).
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;   // Personal may clamp to its minimum
            PlayerSettings.SplashScreen.logos = new[]
            {
                PlayerSettings.SplashScreenLogo.Create(2.5f, sprite),   // 2.5s on our logo (2s min)
            };

            Debug.Log("[Splash] configured Unity → Color Merge Exit logo sequence on sky-blue background.");
        }
    }
}
