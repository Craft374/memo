#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$ROOT_DIR/build/windows"
ICON_PATH="$ROOT_DIR/Resources/AppIcon.ico"

mkdir -p "$OUT_DIR"
python3 "$ROOT_DIR/scripts/generate_windows_icon.py" "$ROOT_DIR/Resources/AppIcon.iconset/icon_256x256.png" "$ICON_PATH"

mcs \
  -target:winexe \
  -r:System.Windows.Forms.dll \
  -r:System.Drawing.dll \
  -win32icon:"$ICON_PATH" \
  -out:"$OUT_DIR/MEMO-Windows.exe" \
  "$ROOT_DIR/Windows/MEMO/Program.cs"

echo "Built: $OUT_DIR/MEMO-Windows.exe"
