#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Post-build: add NSUserTrackingUsageDescription to Info.plist so the iOS App Tracking Transparency
    /// prompt (see Plugins/iOS/ATT.mm + Att.cs) can actually appear. Applied to BOTH simulator and device
    /// builds so ATT is testable in the simulator too — the string is required or the request silently
    /// no-ops with no prompt. Also sets ITSAppUsesNonExemptEncryption=false (this game uses only standard
    /// HTTPS = exempt) so every upload auto-answers App Store Connect's export-compliance question and
    /// isn't held back from TestFlight/review as "Missing Compliance".
    /// </summary>
    public static class IosTrackingPlist
    {
        [PostProcessBuild(90)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription",
                "This lets us show you more relevant ads. Your info is only used to personalize the ads you see.");
            // Export compliance: only standard/exempt encryption (HTTPS) → answer "No" up front so the
            // build skips the "Missing Compliance" hold and reaches testers immediately.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);

            // Weak-link AppTrackingTransparency (iOS 14+) so ATT.mm's ATTrackingManager resolves. It
            // compiles into the UnityFramework target, so add the framework there. Weak = safe on <14.
            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
            proj.WriteToFile(projPath);
            Debug.Log("[IosTrackingPlist] Added NSUserTrackingUsageDescription + AppTrackingTransparency.framework.");
        }
    }
}
#endif
