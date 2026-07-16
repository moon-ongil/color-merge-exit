#!/bin/bash
# Build Color Merge Exit for a REAL iOS device, archive with App Store Connect cloud
# signing, and (optionally) upload to TestFlight. Reuses the ongil team + ASC API-key
# pattern from k-story-trail-app — no local Apple Distribution certificate needed.
#
# Usage:
#   ./dev/build_ios_release.sh            # build + archive + export a signed IPA (no upload)
#   ./dev/build_ios_release.sh --upload   # + upload to TestFlight
#
# ASC credentials (needed for cloud signing + upload) come from env, or are auto-sourced
# from k-story's gitignored .env.production (values are never printed):
#   ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_PATH (default ~/.appstoreconnect/private_keys/AuthKey_<id>.p8)
# Build number: BUILD_NUMBER env, default = timestamp (monotonic → unique per upload).
#
# PREREQUISITE for --upload: an App Store Connect app record for me.ongil.colormergeexit
# must already exist, else altool rejects the build.
set -euo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.0.78f1/Unity.app/Contents/MacOS/Unity"
PROJ="/Users/moon/Developer/work/ongil/color-merge-exit"
TEAM_ID="A8DBT8RJ43"                 # ongil org team (shared across me.ongil.*)
BUNDLE="me.ongil.colormergeexit"
OUT="$PROJ/build/ios-release"
LOGDIR="${LOGDIR:-/tmp}"
KSTORY_ENV="/Users/moon/Developer/work/ongil/k-story-trail-app/scripts/.env.production"

UPLOAD=0
for a in "$@"; do [ "$a" = "--upload" ] && UPLOAD=1; done

# Reuse the ongil ASC key from k-story if not already in the environment (never echoed).
if { [ -z "${ASC_KEY_ID:-}" ] || [ -z "${ASC_ISSUER_ID:-}" ]; } && [ -f "$KSTORY_ENV" ]; then
  set -a; # shellcheck disable=SC1090
  source "$KSTORY_ENV"; set +a
fi
ASC_KEY_PATH="${ASC_KEY_PATH:-$HOME/.appstoreconnect/private_keys/AuthKey_${ASC_KEY_ID:-none}.p8}"

AUTH=(-allowProvisioningUpdates)
if [ -n "${ASC_KEY_ID:-}" ] && [ -n "${ASC_ISSUER_ID:-}" ] && [ -f "$ASC_KEY_PATH" ]; then
  AUTH+=(-authenticationKeyPath "$ASC_KEY_PATH" -authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID")
else
  echo "WARN: ASC key not resolved — cloud signing may fail. Set ASC_KEY_ID/ASC_ISSUER_ID." >&2
fi

echo "== Unity iOS DEVICE build =="
rm -rf "$OUT"
"$UNITY" -batchmode -quit -buildTarget iOS -projectPath "$PROJ" \
  -executeMethod ColorMergeExit.Editor.BuildScript.BuildIOSDevice -logFile "$LOGDIR/unity_ios_release.log"

cd "$OUT"
if [ -f Podfile ] && [ ! -d Pods ]; then
  echo "== pod install (AdMob/Firebase) =="
  pod install >> "$LOGDIR/podinstall_release.log" 2>&1 || true
fi

# Unique, monotonic build number on the generated project.
BN="${BUILD_NUMBER:-$(date +%Y%m%d%H%M)}"
INFO="$OUT/Info.plist"; [ -f "$INFO" ] || INFO="$OUT/Unity-iPhone/Info.plist"
if [ -f "$INFO" ]; then
  /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $BN" "$INFO" 2>/dev/null \
    || /usr/libexec/PlistBuddy -c "Add :CFBundleVersion string $BN" "$INFO"
  echo "== build number: $BN =="
fi

if [ -d Unity-iPhone.xcworkspace ]; then
  BUILD_ARGS=(-workspace Unity-iPhone.xcworkspace)
else
  BUILD_ARGS=(-project Unity-iPhone.xcodeproj)
fi

ARCHIVE="$OUT/Unity-iPhone.xcarchive"
echo "== xcodebuild archive (device, ASC cloud signing, team=$TEAM_ID) =="
rm -rf "$ARCHIVE"
xcodebuild "${BUILD_ARGS[@]}" -scheme Unity-iPhone -configuration Release \
  -sdk iphoneos -archivePath "$ARCHIVE" archive \
  DEVELOPMENT_TEAM="$TEAM_ID" "${AUTH[@]}" > "$LOGDIR/xcarchive_release.log" 2>&1
[ -d "$ARCHIVE" ] || { echo "ERROR: archive failed — see $LOGDIR/xcarchive_release.log"; exit 69; }

EXP="$OUT/export"; rm -rf "$EXP"
OPTS="$OUT/ExportOptions.plist"
cat > "$OPTS" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>app-store-connect</string>
  <key>teamID</key><string>$TEAM_ID</string>
  <key>signingStyle</key><string>automatic</string>
  <key>destination</key><string>export</string>
  <key>uploadSymbols</key><true/>
</dict></plist>
PLIST
echo "== xcodebuild export IPA (app-store-connect) =="
xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportPath "$EXP" \
  -exportOptionsPlist "$OPTS" "${AUTH[@]}" > "$LOGDIR/export_release.log" 2>&1
IPA="$(ls -t "$EXP"/*.ipa 2>/dev/null | head -1 || true)"
[ -n "$IPA" ] || { echo "ERROR: no IPA exported — see $LOGDIR/export_release.log"; exit 70; }
echo "== IPA: $IPA =="

if [ "$UPLOAD" = 1 ]; then
  : "${ASC_KEY_ID:?ASC_KEY_ID required for --upload}"
  : "${ASC_ISSUER_ID:?ASC_ISSUER_ID required for --upload}"
  echo "== upload to TestFlight =="
  xcrun altool --upload-app -f "$IPA" -t ios --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
  echo "== uploaded — App Store Connect processing, then TestFlight =="
fi
echo "DONE"
