using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Command-line iOS build. Generates an Xcode project targeting the iOS Simulator
    /// SDK (no code signing needed), which is then compiled with xcodebuild.
    /// Run: Unity -batchmode -quit -buildTarget iOS -executeMethod ColorMergeExit.Editor.BuildScript.BuildIOSSimulator
    /// </summary>
    public static class BuildScript
    {
        public static void BuildIOSSimulator()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.ongil.colormergeexit");
            PlayerSettings.productName = "Color Merge Exit";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.ongil.colormergeexit");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "build/ios",
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Color Merge Exit] iOS build result: {s.result}, size: {s.totalSize} bytes, time: {s.totalTime}");
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Color Merge Exit] iOS build FAILED");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("[Color Merge Exit] Xcode project at: build/ios");
                EditorApplication.Exit(0);
            }
        }
    }
}
