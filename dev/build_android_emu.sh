#!/bin/bash
# Build Color Merge Exit as a debug-signed APK, install on the Android emulator, and launch it.
# Mirrors dev/build_ios_sim.sh for Android smoke tests.
#
# Usage: ./dev/build_android_emu.sh [avd-name]   # default AVD: kstory_test
set -euo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.0.78f1/Unity.app/Contents/MacOS/Unity"
PROJ="/Users/moon/Developer/work/ongil/color-merge-exit"
SDK="$HOME/Library/Android/sdk"
ADB="$SDK/platform-tools/adb"
EMU="$SDK/emulator/emulator"
AVD="${1:-kstory_test}"
APK="$PROJ/build/android/ColorMergeExit.apk"
PKG="me.ongil.colormergeexit"
LOGDIR="${LOGDIR:-/tmp}"

echo "== Unity Android APK build =="
"$UNITY" -batchmode -quit -buildTarget Android -projectPath "$PROJ" \
  -executeMethod ColorMergeExit.Editor.BuildScript.BuildAndroidApk \
  -logFile "$LOGDIR/unity_android_apk.log"
[ -f "$APK" ] || { echo "ERROR: APK not produced — see $LOGDIR/unity_android_apk.log"; exit 65; }
echo "== APK: $APK ($(du -h "$APK" | cut -f1)) =="

# Boot the emulator if nothing is connected.
if ! "$ADB" get-state >/dev/null 2>&1; then
  echo "== starting emulator: $AVD =="
  nohup "$EMU" -avd "$AVD" -netdelay none -netspeed full > "$LOGDIR/emulator.log" 2>&1 &
  "$ADB" wait-for-device
fi
# Wait for full boot (sys.boot_completed).
until [ "$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = "1" ]; do sleep 2; done
echo "== emulator ready =="

echo "== install + launch =="
"$ADB" install -r "$APK"
"$ADB" shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null
sleep 8
"$ADB" exec-out screencap -p > "$LOGDIR/cme_android_launch.png"
echo "== screenshot: $LOGDIR/cme_android_launch.png =="
echo "DONE"
