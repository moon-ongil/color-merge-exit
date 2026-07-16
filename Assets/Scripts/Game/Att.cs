using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

namespace ColorMergeExit.Game
{
    /// <summary>
    /// iOS App Tracking Transparency. <see cref="Request"/> shows the system "Allow tracking?" prompt
    /// (once — iOS remembers the choice) so AdMob can use the IDFA for personalized ads. No-op off iOS.
    /// The prompt only appears if NSUserTrackingUsageDescription is in Info.plist (see IosTrackingPlist).
    /// </summary>
    public static class Att
    {
#if UNITY_IOS && !UNITY_EDITOR
        private delegate void AttCb(int status);
        [DllImport("__Internal")] private static extern void _cmeRequestATT(AttCb cb);
        [DllImport("__Internal")] private static extern int _cmeATTStatus();

        private static Action _onDone;

        [MonoPInvokeCallback(typeof(AttCb))]
        private static void OnAtt(int status)
        {
            // status: 0=notDetermined 1=restricted 2=denied 3=authorized
            UnityEngine.Debug.Log($"[ATT] answered status={status}");
            var cb = _onDone; _onDone = null;
            cb?.Invoke();
        }
#endif

        /// <summary>Show the ATT prompt; <paramref name="onDone"/> fires when the user answers (or
        /// immediately if already answered / not iOS).</summary>
        public static void Request(Action onDone)
        {
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.Debug.Log($"[ATT] requesting (status before={_cmeATTStatus()})");
            _onDone = onDone;
            try { _cmeRequestATT(OnAtt); }
            catch (Exception e) { UnityEngine.Debug.LogWarning($"[ATT] request failed: {e.Message}"); _onDone = null; onDone?.Invoke(); }
#else
            onDone?.Invoke();
#endif
        }
    }
}
