#!/bin/bash
# Make Google's prebuilt AdMob Unity plugin link into an arm64 *simulator* app.
#
# Two problems with build/ios/Libraries/Plugins/iOS/unity-plugin-library.a:
#   1. Its arm64 objects are tagged LC_BUILD_VERSION platform = iOS (device), so an
#      arm64-simulator link fails with "built for 'iOS'". fix_sim_plugin.py flips the
#      platform field to iOS-simulator (arm64 device == arm64 sim machine code).
#   2. The arm64 GADUInterface.o is missing the ~21 UMP/consent C bridge functions that
#      the x86_64 slice has (an inconsistency in Google's device build). This game never
#      calls the UMP API, so we append no-op stubs (dev/ump_stubs.c) to satisfy the linker.
#
# Run AFTER the Unity iOS build (lib exists) and BEFORE xcodebuild.
set -e
DEV="$(cd "$(dirname "$0")" && pwd)"
LIB="${1:-$DEV/../build/ios/Libraries/Plugins/iOS/unity-plugin-library.a}"

if [ ! -f "$LIB" ]; then
  echo "  (skip fix_admob_sim: no $LIB)"; exit 0
fi

echo "== fix AdMob plugin for simulator =="
# 1. platform re-tag (iOS -> iOS-simulator) on arm64 objects
python3 "$DEV/fix_sim_plugin.py" "$LIB"

# 2. inject UMP stub symbols into the arm64 slice
if lipo -info "$LIB" | grep -q arm64 && ! (lipo "$LIB" -thin arm64 -output /tmp/_ax.a 2>/dev/null && nm /tmp/_ax.a 2>/dev/null | grep -q "T _GADUCreateConsentForm"); then
  SDK=$(xcrun --sdk iphonesimulator --show-sdk-path)
  WORK=$(mktemp -d)
  clang -c -arch arm64 -target arm64-apple-ios15.0-simulator -isysroot "$SDK" \
    "$DEV/ump_stubs.c" -o "$WORK/ump_stubs.o"
  lipo "$LIB" -thin arm64 -output "$WORK/arm.a"
  lipo "$LIB" -thin x86_64 -output "$WORK/x.a" 2>/dev/null || true
  ( cd "$WORK" && ar crs arm.a ump_stubs.o )
  if [ -f "$WORK/x.a" ]; then
    lipo -create "$WORK/arm.a" "$WORK/x.a" -output "$LIB"
  else
    cp "$WORK/arm.a" "$LIB"
  fi
  rm -rf "$WORK"
  echo "  injected UMP no-op stubs into arm64 slice"
else
  echo "  UMP symbols already present (skip stub inject)"
fi
rm -f /tmp/_ax.a