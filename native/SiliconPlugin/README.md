# SiliconPlugin

The native logic engine for Silicon Sandbox. C++ compiled to a shared library and called from
Unity through `Assets/Scripts/LogicEngine/LogicEngine.cs`.

## Layout

```
src/gates.h            Gate type and status enums, EvaluateGate declaration
src/gates.cpp          The evaluator. Plain C++ — no Unity, no DLL awareness
src/plugin.cpp         The extern "C" export surface. The only DLL-aware file
tests/truth_table.cpp  Standalone verification. Builds and runs without Unity
```

The split is deliberate: `gates.cpp` stays portable and testable on its own, and everything that
crosses the managed/native boundary lives in `plugin.cpp`. Anything added to the exported surface
must stay C-compatible — `extern "C"`, plain types, no C++ classes or `std::` types across the
boundary, no exceptions allowed to escape.

## Building

Run both commands from this directory (`native/SiliconPlugin/`).

**Tests** — 21 checks covering the full truth table for each gate plus the error contract. Exits
non-zero on any failure.

```sh
g++ -Wall -Wextra -o truth_table.exe tests/truth_table.cpp src/gates.cpp
./truth_table.exe
```

**The plugin:**

```sh
g++ -shared -O2 -o SiliconPlugin.dll src/plugin.cpp src/gates.cpp
```

Then copy `SiliconPlugin.dll` into `Assets/Plugins/x86_64/`. Build output left in this directory is
gitignored; the committed binary is the copy under `Assets/`.

### Requirements

A 64-bit toolchain. Check with `g++ -dumpmachine` — it must report `x86_64-...`. Unity 6's editor
and standalone player are x86_64 only, and a 32-bit build fails to load without a useful error
message.

### Gotchas

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
