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

        // Run: Unity -batchmode -quit -buildTarget Android -executeMethod ColorMergeExit.Editor.BuildScript.BuildAndroidApk
        // Debug-signed APK for emulator/device smoke tests (no keystore needed).
        public static void BuildAndroidApk() => BuildAndroid(bundle: false);

        // Run: Unity -batchmode -quit -buildTarget Android -executeMethod ColorMergeExit.Editor.BuildScript.BuildAndroidAab
        // Play-Store AAB signed with the upload keystore. Requires env:
        //   ANDROID_KEYSTORE_PATH, ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS, ANDROID_KEYALIAS_PASS
        public static void BuildAndroidAab() => BuildAndroid(bundle: true);

        private static void BuildAndroid(bool bundle)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "me.ongil.colormergeexit");
            PlayerSettings.productName = "Color Merge Exit";
            AppIconSetup.Apply();
            SplashSetup.Apply();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // Same define policy as iOS: FIREBASE_ANALYTICS always, PRODUCTION_ADS only for store builds.
            {
                var defs = new System.Collections.Generic.List<string>(
                    PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                        .Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
                void SetDef(string d, bool on) { defs.Remove(d); if (on) defs.Add(d); }
                SetDef("FIREBASE_ANALYTICS", true);
                SetDef("PRODUCTION_ADS", System.Environment.GetEnvironmentVariable("PRODUCTION_ADS") == "1");
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", defs));
            }
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // Play 64-bit 요건 + Apple Silicon 에뮬레이터
            // GMA(AdMob) Unity 플러그인은 GameActivity를 지원하지 않는다 — UMP 동의 콜백이
            // UnityPlayerGameActivity에서 ReflectionHelper.nativeProxyInvoke UnsatisfiedLinkError로
            // 크래시함. Google 문서대로 legacy Activity 진입점을 강제한다.
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            if (bundle)
            {
                string ksPath = System.Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
                string ksPass = System.Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
                string alias = System.Environment.GetEnvironmentVariable("ANDROID_KEYALIAS");
                string aliasPass = System.Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");
                if (string.IsNullOrEmpty(ksPath) || string.IsNullOrEmpty(ksPass) ||
                    string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(aliasPass))
                {
                    Debug.LogError("[Color Merge Exit] AAB build requires ANDROID_KEYSTORE_PATH/PASS + ANDROID_KEYALIAS/PASS env vars");
                    EditorApplication.Exit(1);
                    return;
                }
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ksPath;
                PlayerSettings.Android.keystorePass = ksPass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = aliasPass;
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false; // debug keystore
            }
            EditorUserBuildSettings.buildAppBundle = bundle;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            string outPath = bundle ? "build/android/ColorMergeExit.aab" : "build/android/ColorMergeExit.apk";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Color Merge Exit] Android {(bundle ? "AAB" : "APK")} build result: {s.result}, size: {s.totalSize} bytes, time: {s.totalTime}");
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Color Merge Exit] Android build FAILED");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log($"[Color Merge Exit] Android artifact at: {outPath}");
                EditorApplication.Exit(0);
            }
        }
    }
}
