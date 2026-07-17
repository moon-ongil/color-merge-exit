using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Command-line iOS builds. <see cref="BuildIOSSimulator"/> targets the Simulator SDK (no signing),
    /// compiled by dev/build_ios_sim.sh. <see cref="BuildIOSDevice"/> targets the Device SDK with
    /// automatic signing under the ongil team, archived + uploaded to TestFlight by dev/build_ios_release.sh
    /// using an App Store Connect API key (cloud signing — no local distribution cert needed).
    /// </summary>
    public static class BuildScript
    {
        private const string TeamId = "A8DBT8RJ43"; // ongil org Apple Developer team (shared across me.ongil.*)

        // Run: Unity -batchmode -quit -buildTarget iOS -executeMethod ColorMergeExit.Editor.BuildScript.BuildIOSSimulator
        public static void BuildIOSSimulator() => BuildIOS(device: false);

        // Run: Unity -batchmode -quit -buildTarget iOS -executeMethod ColorMergeExit.Editor.BuildScript.BuildIOSDevice
        public static void BuildIOSDevice() => BuildIOS(device: true);

        private static void BuildIOS(bool device)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "me.ongil.colormergeexit");
            PlayerSettings.productName = "Color Merge Exit";
            AppIconSetup.Apply();   // assign Assets/Art/AppIcon.png to all icon slots
            SplashSetup.Apply();    // Unity logo → our logo splash sequence
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            // Scripting defines: FIREBASE_ANALYTICS is always on (SDK is imported). PRODUCTION_ADS is set
            // ONLY when the PRODUCTION_ADS env var == "1" (a real App Store build) — otherwise AdManager
            // serves TEST ads, so Editor/simulator/TestFlight builds always show ads while testing. The
            // list is rewritten every build so a stale PRODUCTION_ADS never lingers on a test build.
            {
                var defs = new System.Collections.Generic.List<string>(
                    PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS)
                        .Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
                void SetDef(string d, bool on) { defs.Remove(d); if (on) defs.Add(d); }
                SetDef("FIREBASE_ANALYTICS", true);
                SetDef("PRODUCTION_ADS", System.Environment.GetEnvironmentVariable("PRODUCTION_ADS") == "1");
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, string.Join(";", defs));
            }
            PlayerSettings.iOS.sdkVersion = device ? iOSSdkVersion.DeviceSDK : iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad; // Universal: iPad runs full-screen (phone-width board, background fills the sides)

            // Device build → automatic cloud signing under the ongil team (the actual signing cert +
            // profile are resolved by xcodebuild -allowProvisioningUpdates with the ASC API key at
            // archive time). Simulator build needs no signing.
            if (device)
            {
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                PlayerSettings.iOS.appleDeveloperTeamID = TeamId;
            }
            else
            {
                PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            string outPath = device ? "build/ios-release" : "build/ios";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Color Merge Exit] iOS {(device ? "device" : "simulator")} build result: {s.result}, size: {s.totalSize} bytes, time: {s.totalTime}");
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Color Merge Exit] iOS build FAILED");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log($"[Color Merge Exit] Xcode project at: {outPath}");
                EditorApplication.Exit(0);
            }
        }
    }
}
