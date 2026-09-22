# SiliconPlugin

The native logic engine for Silicon Sandbox. C++ compiled to a shared library and called from
Unity through `Assets/Scripts/LogicEngine/LogicEngine.cs`.

## Layout

```
src/gates.h            Gate type and status enums, EvaluateGate declaration
src/gates.cpp          The evaluator. Plain C++ — no Unity, no DLL awareness
src/netlist.h          Netlist input structs, CircuitGraph, NetlistStatus, ParseNetlist declaration
src/netlist.cpp        The netlist parser (#9). Plain C++, same rules as gates.cpp
src/plugin.cpp         The extern "C" export surface. The only DLL-aware file
tests/truth_table.cpp  Gate evaluator verification. Builds and runs without Unity
tests/graph_parser.cpp Netlist parser verification. Builds and runs without Unity
tests/PluginCheck/     Loads the committed DLL through LogicEngine.cs (.NET, Windows). Run in CI
```

The split is deliberate: `gates.cpp` stays portable and testable on its own, and everything that
crosses the managed/native boundary lives in `plugin.cpp`. Anything added to the exported surface
must stay C-compatible — `extern "C"`, plain types, no C++ classes or `std::` types across the
boundary, no exceptions allowed to escape.

## Building

Run these commands from this directory (`native/SiliconPlugin/`).

**Gate tests** — the full truth table for every gate (AND, OR, NOT, NAND, NOR, XOR, XNOR, BUFFER),
multi-input folds, TC-3.2.9 (NOT of a gate equals its inverted gate), and the error contract. Exits
non-zero on any failure.

```sh
g++ -Wall -Wextra -o truth_table.exe tests/truth_table.cpp src/gates.cpp
./truth_table.exe
```

**Parser tests** — graph construction for the sample netlists, unwired pins, every node type,
cycle detection, and every `NetlistStatus` error code. Exits non-zero on any failure.

```sh
g++ -Wall -Wextra -o graph_parser.exe tests/graph_parser.cpp src/netlist.cpp src/gates.cpp
./graph_parser.exe
```

**PluginCheck** — loads the *committed* `Assets/Plugins/x86_64/SiliconPlugin.dll` through the real
`LogicEngine.cs`, checks every `GateType` against its truth table, and fails if the DLL still has debug
sections. This is what the `native-plugin` CI job runs on `windows-latest`, since the Unity test job
runs on Linux and can't load the DLL. Needs the .NET 8 SDK; run from the repo root:

```sh
dotnet run -c Release --project native/SiliconPlugin/tests/PluginCheck
```

**The plugin:**

```sh
g++ -shared -O2 -static -s -o SiliconPlugin.dll src/plugin.cpp src/gates.cpp src/netlist.cpp
```

`-static` is required now that `netlist.cpp` uses the C++ standard library. Without it, a MinGW build
depends on `libstdc++-6.dll` and `libgcc_s_seh-1.dll`, which Unity cannot find, and the plugin fails
to load with a `DllNotFoundException` even though `SiliconPlugin.dll` is sitting right there. Check
with `objdump -p SiliconPlugin.dll | grep "DLL Name"` — only system DLLs (KERNEL32, msvcrt or
api-ms-win-crt-*) should be listed.

`-s` strips debug sections and symbols. The committed DLL is a binary in git with no LFS, so every
rebuild adds its full size to the repo history permanently. Unstripped, the static MinGW build is
~700 KB; stripped it is ~230 KB. Check with `objdump -h SiliconPlugin.dll` — there should be no
`.debug_*` sections. If you forget, `strip --strip-unneeded SiliconPlugin.dll` fixes an existing
build without touching the code.

`netlist.cpp` has no exports until #12, so including it does not change the DLL's surface yet;
it is listed so the command does not need to change again when #12 lands.

Then copy `SiliconPlugin.dll` into `Assets/Plugins/x86_64/`. Build output left in this directory is
gitignored; the committed binary is the copy under `Assets/`.

### Requirements

A 64-bit toolchain. Check with `g++ -dumpmachine` — it must report `x86_64-...`. Unity 6's editor
and standalone player are x86_64 only, and a 32-bit build fails to load without a useful error
message.

### Gotchas

- **Link statically.** See the `-static` note above — a missing runtime DLL looks exactly like a
  missing plugin.
- **Strip before committing.** See the `-s` note above.
- **Close the Unity editor before rebuilding.** Unity holds a lock on loaded native libraries, so
  copying over the DLL fails silently while it is open and you end up debugging stale code.
- The DLL's platform settings live in `Assets/Plugins/x86_64/SiliconPlugin.dll.meta`, which is
  committed. Editor must be enabled with `OS: Windows` — Play mode loads the *Editor* entry, not
  Standalone.

## Other platforms

Only a Windows x86_64 binary is committed. `plugin.cpp` already handles non-Windows export
decoration via the `SILICON_API` macro, so a macOS or Linux build needs no source changes — just a
separately compiled library committed alongside the DLL with its own platform settings.

## Adding to the exported surface

`gates.h` and `LogicEngine.cs` hold mirrored copies of the gate type and status enums. Nothing
verifies the correspondence at compile time, so change both in the same commit. Gate type values
are written into saved netlists, which makes them part of the on-disk format: append new types,
never renumber existing ones.
