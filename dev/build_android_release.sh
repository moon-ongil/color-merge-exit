#!/bin/bash
# Build the Play-Store AAB for Color Merge Exit, signed with the upload keystore.
# Keystore credentials auto-sourced from android-keystore/credentials.txt (gitignored).
# PRODUCTION_ADS=1 is set so AdManager serves real ad units (store build).
#
# Usage: ./dev/build_android_release.sh
# Output: build/android/ColorMergeExit.aab  → Play Console에 업로드
set -euo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.0.78f1/Unity.app/Contents/MacOS/Unity"
PROJ="/Users/moon/Developer/work/ongil/color-merge-exit"
CREDS="$PROJ/android-keystore/credentials.txt"
LOGDIR="${LOGDIR:-/tmp}"

[ -f "$CREDS" ] || { echo "ERROR: $CREDS 없음 — 키스토어 크리덴셜 필요"; exit 64; }
set -a; # shellcheck disable=SC1090
source <(grep -v '^#' "$CREDS"); set +a

echo "== Unity Android AAB build (PRODUCTION_ADS=1) =="
PRODUCTION_ADS=1 "$UNITY" -batchmode -quit -buildTarget Android -projectPath "$PROJ" \
  -executeMethod ColorMergeExit.Editor.BuildScript.BuildAndroidAab \
  -logFile "$LOGDIR/unity_android_aab.log"

AAB="$PROJ/build/android/ColorMergeExit.aab"
[ -f "$AAB" ] || { echo "ERROR: AAB not produced — see $LOGDIR/unity_android_aab.log"; exit 65; }
echo "== AAB: $AAB ($(du -h "$AAB" | cut -f1)) =="
echo "DONE — Play Console > 프로덕션 > 새 버전 만들기에 업로드하세요"
