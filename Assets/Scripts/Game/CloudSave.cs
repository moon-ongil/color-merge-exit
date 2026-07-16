using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ColorMergeExit.Game
{
    /// <summary>
    /// No-signup cloud save backed by the platform's built-in key-value store — currently iOS iCloud
    /// KVS (<c>NSUbiquitousKeyValueStore</c>) via the native bridge in Plugins/iOS/iCloudKV.mm. Uses
    /// the user's existing Apple ID, so there is NO account creation / login and thus none of the
    /// associated store-policy burden. Off iOS (editor, Android, standalone) every call is a no-op and
    /// <see cref="Available"/> is false, so callers transparently fall back to local PlayerPrefs.
    ///
    /// NOTE: real cross-device / reinstall sync only happens on a SIGNED device build whose App ID has
    /// the iCloud KVS capability (the entitlement is added for device builds by ICloudEntitlements).
    /// On the simulator / unsigned builds the store works locally but never round-trips to iCloud.
    /// </summary>
    public static class CloudSave
    {
#if UNITY_IOS && !UNITY_EDITOR
        public static bool Available => true;

        [DllImport("__Internal")] private static extern void _iCloudKV_SetLong(string key, long value);
        [DllImport("__Internal")] private static extern long _iCloudKV_GetLong(string key, long defaultValue);
        [DllImport("__Internal")] private static extern void _iCloudKV_SetDouble(string key, double value);
        [DllImport("__Internal")] private static extern double _iCloudKV_GetDouble(string key, double defaultValue);
        [DllImport("__Internal")] private static extern bool _iCloudKV_HasKey(string key);
        [DllImport("__Internal")] private static extern void _iCloudKV_Synchronize();

        public static bool HasKey(string key) => _iCloudKV_HasKey(key);
        public static int GetInt(string key, int def) => (int)_iCloudKV_GetLong(key, def);
        public static void SetInt(string key, int value) => _iCloudKV_SetLong(key, value);
        public static float GetFloat(string key, float def) => (float)_iCloudKV_GetDouble(key, def);
        public static void SetFloat(string key, float value) => _iCloudKV_SetDouble(key, value);
        public static void Flush() => _iCloudKV_Synchronize();
#else
        public static bool Available => false;
        public static bool HasKey(string key) => false;
        public static int GetInt(string key, int def) => def;
        public static void SetInt(string key, int value) { }
        public static float GetFloat(string key, float def) => def;
        public static void SetFloat(string key, float value) { }
        public static void Flush() { }
#endif
    }
}
