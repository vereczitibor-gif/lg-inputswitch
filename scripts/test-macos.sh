#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
export CLANG_MODULE_CACHE_PATH="$PWD/work/clang-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$PWD/work/swift-cache"
mkdir -p work/build
swift test --package-path macOS --build-system native --disable-sandbox --cache-path "$PWD/work/swiftpm-cache"
clang -Wall -Wextra -Werror -fmodules -I third_party/m1ddc/headers \
 macOS/Tests/PacketTests.m third_party/m1ddc/sources/i2c.m \
 -framework Foundation -framework IOKit -o work/build/PacketTests
work/build/PacketTests
