#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
APP_DIR="$ROOT_DIR/build/MEMO.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
MODULE_CACHE_DIR="$ROOT_DIR/.build/module-cache"
DEVELOPER_DIR="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
SWIFTC="$DEVELOPER_DIR/Toolchains/XcodeDefault.xctoolchain/usr/bin/swiftc"
SDKROOT="$DEVELOPER_DIR/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk"
ARCH="$(uname -m)"

rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR"
mkdir -p "$RESOURCES_DIR"
mkdir -p "$MODULE_CACHE_DIR"
python3 "$ROOT_DIR/scripts/generate_icon.py" "$ROOT_DIR/Resources/AppIcon.iconset" "$ROOT_DIR/Resources/AppIcon.icns"
cp "$ROOT_DIR/Resources/Info.plist" "$CONTENTS_DIR/Info.plist"
cp "$ROOT_DIR/Resources/AppIcon.icns" "$RESOURCES_DIR/AppIcon.icns"
printf "APPL????" > "$CONTENTS_DIR/PkgInfo"

if [ ! -x "$SWIFTC" ]; then
  SWIFTC="$(xcrun --find swiftc)"
  SDKROOT="$(xcrun --sdk macosx --show-sdk-path)"
fi

"$SWIFTC" \
  -O \
  -whole-module-optimization \
  -sdk "$SDKROOT" \
  -target "$ARCH-apple-macos13.0" \
  -module-cache-path "$MODULE_CACHE_DIR" \
  -framework AppKit \
  "$ROOT_DIR"/Sources/MEMO/*.swift \
  -o "$MACOS_DIR/MEMO"

xattr -cr "$APP_DIR" 2>/dev/null || true
codesign --force --sign - "$APP_DIR" >/dev/null

echo "Built: $APP_DIR"
