// Thin bridge over NSUbiquitousKeyValueStore (iCloud Key-Value Store) for no-signup cloud save.
// On the simulator / an unsigned build (no iCloud entitlement) these calls operate on a local,
// non-syncing store — safe, they just never round-trip to iCloud. Real cross-device / reinstall
// sync happens only on a signed device build whose App ID has the iCloud KVS capability.
#import <Foundation/Foundation.h>

extern "C" {

    void _iCloudKV_SetLong(const char* key, long long value) {
        if (key == NULL) return;
        NSString* k = [NSString stringWithUTF8String:key];
        [[NSUbiquitousKeyValueStore defaultStore] setLongLong:value forKey:k];
    }

    long long _iCloudKV_GetLong(const char* key, long long defaultValue) {
        if (key == NULL) return defaultValue;
        NSUbiquitousKeyValueStore* s = [NSUbiquitousKeyValueStore defaultStore];
        NSString* k = [NSString stringWithUTF8String:key];
        if ([s objectForKey:k] == nil) return defaultValue;   // KVS has no "hasKey", absence => default
        return [s longLongForKey:k];
    }

    void _iCloudKV_SetDouble(const char* key, double value) {
        if (key == NULL) return;
        NSString* k = [NSString stringWithUTF8String:key];
        [[NSUbiquitousKeyValueStore defaultStore] setDouble:value forKey:k];
    }

    double _iCloudKV_GetDouble(const char* key, double defaultValue) {
        if (key == NULL) return defaultValue;
        NSUbiquitousKeyValueStore* s = [NSUbiquitousKeyValueStore defaultStore];
        NSString* k = [NSString stringWithUTF8String:key];
        if ([s objectForKey:k] == nil) return defaultValue;
        return [s doubleForKey:k];
    }

    bool _iCloudKV_HasKey(const char* key) {
        if (key == NULL) return false;
        NSString* k = [NSString stringWithUTF8String:key];
        return [[NSUbiquitousKeyValueStore defaultStore] objectForKey:k] != nil;
    }

    // Push local changes toward iCloud and pull any newer values into the local cache.
    void _iCloudKV_Synchronize() {
        [[NSUbiquitousKeyValueStore defaultStore] synchronize];
    }
}
