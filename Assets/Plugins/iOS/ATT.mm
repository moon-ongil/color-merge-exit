// App Tracking Transparency bridge. Shows the iOS 14+ "Allow tracking?" prompt so AdMob may use the
// IDFA for personalized ads. Requires NSUserTrackingUsageDescription in Info.plist (added by
// IosTrackingPlist.cs at build time) — without it the request silently no-ops and no prompt appears.
#import <Foundation/Foundation.h>
#if __has_include(<AppTrackingTransparency/AppTrackingTransparency.h>)
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#endif

typedef void (*CMEAttCallback)(int status);

extern "C" void _cmeRequestATT(CMEAttCallback cb) {
#if __has_include(<AppTrackingTransparency/AppTrackingTransparency.h>)
    if (@available(iOS 14, *)) {
        // If status is already determined, the completion fires immediately with no prompt.
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            if (cb) cb((int)status);
        }];
        return;
    }
#endif
    if (cb) cb(-1); // pre-iOS 14 / framework unavailable: nothing to ask
}

extern "C" int _cmeATTStatus() {
#if __has_include(<AppTrackingTransparency/AppTrackingTransparency.h>)
    if (@available(iOS 14, *)) return (int)[ATTrackingManager trackingAuthorizationStatus];
#endif
    return -1;
}
