#!/bin/bash
# Build the game for the iOS Simulator and deploy to the booted simulator.
# Works around a Unity quirk where the simulator build copies x86_64-only runtime
# libs; we replace them with the universal (x64+arm64) variants before linking.
set -e

UNITY="/Applications/Unity/Hub/Editor/6000.0.78f1/Unity.app/Contents/MacOS/Unity"
PROJ="/Users/moon/Developer/work/ongil/color-merge-exit"
LIBS="/Applications/Unity/Hub/Editor/6000.0.78f1/PlaybackEngines/iOSSupport/Trampoline/Libraries"
LOGDIR="${1:-/tmp}"
BUNDLE="me.ongil.colormergeexit"

echo "== Unity iOS build =="
rm -rf "$PROJ/build/ios"
"$UNITY" -batchmode -quit -buildTarget iOS -projectPath "$PROJ" \
  -executeMethod ColorMergeExit.Editor.BuildScript.BuildIOSSimulator -logFile "$LOGDIR/unity_ib.log"

echo "== fix simulator libs (x86_64 -> universal) =="
cp "$LIBS/libiPhone-lib-sim-x64arm64.dylib" "$PROJ/build/ios/Libraries/libiPhone-lib.dylib"
cp "$LIBS/baselib-sim-x64arm64.a" "$PROJ/build/ios/Libraries/baselib.a"

# AdMob (EDM4U) generates a CocoaPods workspace; ensure pods are installed, then build the WORKSPACE.
cd "$PROJ/build/ios"
if [ -f Podfile ] && [ ! -d Pods ]; then
  echo "== pod install =="
  pod install >> "$LOGDIR/podinstall.log" 2>&1 || true
fi

# AdMob's prebuilt arm64 plugin is device-tagged and misses UMP symbols; patch for the simulator.
bash "$PROJ/dev/fix_admob_sim.sh"

# Firebase's prebuilt C++ static libs (libFirebaseCpp*.a) are arm64 device-tagged too; re-tag for sim.
FBDIR="$PROJ/build/ios/Libraries/Plugins/iOS/Firebase"
if [ -d "$FBDIR" ]; then
  echo "== fix Firebase libs for simulator =="
  python3 "$PROJ/dev/fix_sim_plugin.py" "$FBDIR"/*.a
fi

echo "== xcodebuild (simulator, arm64) =="
if [ -d Unity-iPhone.xcworkspace ]; then
  BUILD_ARGS=(-workspace Unity-iPhone.xcworkspace)
else
  BUILD_ARGS=(-project Unity-iPhone.xcodeproj)
fi
xcodebuild "${BUILD_ARGS[@]}" -scheme Unity-iPhone -configuration Debug \
  -sdk iphonesimulator -arch arm64 -derivedDataPath ./DD \
  CODE_SIGNING_ALLOWED=NO ONLY_ACTIVE_ARCH=YES build > "$LOGDIR/xcodebuild.log" 2>&1

APP=$(find "$PROJ/build/ios/DD/Build/Products/Debug-iphonesimulator" -maxdepth 1 -iname "*.app" | head -1)
echo "== deploy: $APP =="
xcrun simctl terminate booted "$BUNDLE" 2>/dev/null || true
xcrun simctl uninstall booted "$BUNDLE" 2>/dev/null || true
xcrun simctl install booted "$APP"
xcrun simctl launch booted "$BUNDLE"
echo "DONE"
