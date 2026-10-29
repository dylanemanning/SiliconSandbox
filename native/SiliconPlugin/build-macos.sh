#!/bin/bash
set -euo pipefail

# Build both Mac architectures from the same engine sources used by Windows.
script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_dir="$(cd "$script_dir/../.." && pwd)"
build_dir="$repo_dir/Builds/Native/macOS"
plugin_dir="$repo_dir/Assets/Plugins/macOS"
mkdir -p "$build_dir" "$plugin_dir"

xcrun clang++ -std=c++17 -O2 -Wall -Wextra -fvisibility=hidden \
  -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -dynamiclib \
  -Wl,-install_name,@rpath/libSiliconPlugin.dylib \
  -Wl,-exported_symbol,_Silicon_EvaluateGate \
  -Wl,-exported_symbol,_Silicon_EvaluateCircuit \
  "$script_dir/src/plugin.cpp" "$script_dir/src/gates.cpp" \
  "$script_dir/src/netlist.cpp" "$script_dir/src/simulator.cpp" \
  -o "$build_dir/libSiliconPlugin.dylib"
xcrun strip -x "$build_dir/libSiliconPlugin.dylib"
codesign --force --sign - --timestamp=none "$build_dir/libSiliconPlugin.dylib"
xcrun lipo "$build_dir/libSiliconPlugin.dylib" -verify_arch arm64 x86_64
codesign --verify --strict "$build_dir/libSiliconPlugin.dylib"
cp "$build_dir/libSiliconPlugin.dylib" "$plugin_dir/libSiliconPlugin.dylib"
echo "Built universal SiliconPlugin: $plugin_dir/libSiliconPlugin.dylib"
