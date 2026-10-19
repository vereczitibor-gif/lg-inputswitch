#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -m)" != arm64 ]]; then echo 'Az első verzió Apple Siliconra készül.' >&2; exit 1; fi
mkdir -p work/build outputs
export CLANG_MODULE_CACHE_PATH="$PWD/work/clang-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$PWD/work/swift-cache"
clang -Wall -Wextra -Werror -fmodules -mmacosx-version-min=13.0 -DMAX_DISPLAYS=32 \
  -I third_party/m1ddc/headers third_party/m1ddc/sources/{i2c,ioregistry,m1ddc}.m \
  -framework Foundation -framework IOKit -framework ApplicationServices -framework CoreDisplay -o work/build/m1ddc
swift build --package-path macOS -c release --build-system native --disable-sandbox --cache-path "$PWD/work/swiftpm-cache"
app="outputs/LG InputSwitch.app"
mkdir -p "$app/Contents/"{MacOS,Helpers,Resources/Licenses}
cp macOS/.build/release/LGInputSwitch "$app/Contents/MacOS/LGInputSwitch"
cp work/build/m1ddc "$app/Contents/Helpers/m1ddc"
cp third_party/m1ddc/LICENSE "$app/Contents/Resources/Licenses/m1ddc.txt"
cp third_party/licenses/streamdeck-display-knob.txt "$app/Contents/Resources/Licenses/"
cp LICENSE "$app/Contents/Resources/Licenses/LGInputSwitch.txt"
cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>local.lginputswitch.app</string>
<key>CFBundleName</key><string>LG InputSwitch</string>
<key>CFBundleExecutable</key><string>LGInputSwitch</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>0.1.0</string>
<key>CFBundleVersion</key><string>1</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
codesign --force --sign - "$app/Contents/Helpers/m1ddc"
codesign --force --sign - "$app"
ditto -c -k --sequesterRsrc --keepParent "$app" outputs/LGInputSwitch-macOS-arm64.zip
echo "Elkészült: $app"
