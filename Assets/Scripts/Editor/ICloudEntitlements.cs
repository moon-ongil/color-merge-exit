#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Post-build: add the iCloud Key-Value Store entitlement so <c>NSUbiquitousKeyValueStore</c>
    /// (see Plugins/iOS/iCloudKV.mm + CloudSave.cs) actually syncs on device.
    ///
    /// Applied ONLY to DEVICE builds — the unsigned simulator build we develop with never gets it, so
    /// it can't disturb the sim workflow. For it to work on device the App ID must ALSO have the iCloud
    /// (Key-Value storage) capability enabled in the Apple Developer portal, with matching provisioning.
    /// </summary>
    public static class ICloudEntitlements
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;
            if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.DeviceSDK)
            {
                Debug.Log("[ICloudEntitlements] Simulator build — skipping iCloud KVS entitlement.");
                return;
            }

            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string targetGuid = proj.GetUnityMainTargetGuid();

            const string entName = "ColorMergeExit.entitlements";
            string entPath = Path.Combine(pathToBuiltProject, entName);
            var ent = new PlistDocument();
            if (File.Exists(entPath)) ent.ReadFromFile(entPath);
            // Standard KVS identifier: the SDK expands this to <team>.<bundle-id> at sign time.
            ent.root.SetString("com.apple.developer.ubiquity-kvstore-identifier",
                "$(TeamIdentifierPrefix)$(CFBundleIdentifier)");
            ent.WriteToFile(entPath);

            proj.AddFile(entName, entName);
            proj.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entName);
            proj.WriteToFile(projPath);
            Debug.Log("[ICloudEntitlements] Added iCloud KVS entitlement for device build.");
        }
    }
}
#endif
