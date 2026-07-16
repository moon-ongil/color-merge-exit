using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Assigns the game's app icon (Assets/Art/AppIcon.png) to every iOS + Android icon slot, plus the
    /// legacy default icon. Runs automatically at the start of each build (see BuildScript) and can be
    /// invoked standalone:
    ///   Unity -batchmode -quit -projectPath . -executeMethod ColorMergeExit.Editor.AppIconSetup.Apply
    /// </summary>
    public static class AppIconSetup
    {
        private const string IconPath = "Assets/Art/AppIcon.png";

        public static void Apply()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (tex == null)
            {
                Debug.LogWarning($"[AppIcon] source not found at {IconPath} — skipping icon assignment.");
                return;
            }

            // Legacy default icon (fallback shown in a few places / older pipelines).
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);

            SetPlatformIcons(BuildTargetGroup.iOS, tex);
            SetPlatformIcons(BuildTargetGroup.Android, tex);

            AssetDatabase.SaveAssets();
            Debug.Log($"[AppIcon] applied {IconPath} to iOS + Android icon slots.");
        }

        // Fill every layer of every icon of every supported kind with the source texture.
        private static void SetPlatformIcons(BuildTargetGroup group, Texture2D tex)
        {
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKindsForPlatform(group))
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(group, kind);
                foreach (PlatformIcon icon in icons)
                {
                    var layers = new List<Texture2D>();
                    for (int layer = 0; layer < Mathf.Max(1, icon.maxLayerCount); layer++)
                        layers.Add(tex);
                    icon.SetTextures(layers.ToArray());
                }
                PlayerSettings.SetPlatformIcons(group, kind, icons);
            }
        }
    }
}
