// plugin.cpp — the native plugin's exported surface.
//
// This is the only file in SiliconPlugin that knows it is a DLL. Everything
// here is C-compatible on purpose; everything in gates.cpp stays plain,
// portable, testable C++. Keeping the boundary in one small file is what makes
// tests/truth_table.cpp able to exercise the logic without Unity, and what
// keeps #12's netlist marshaling a matter of adding exports rather than
// untangling Unity concerns from the engine.
//
// Rules for anything added to this file:
//   - extern "C" only. C++ name mangling is compiler-specific; DllImport
//     resolves symbols by plain name.
//   - Plain types across the boundary: int, pointers to int, sizes. No C++
//     classes, no std:: types, no references, no exceptions allowed to escape.
//   - Memory crossing the boundary is allocated and owned by the caller (here,
//     Unity's marshaler owns the inputs array).
//
// Build the DLL from native/SiliconPlugin/:
//     g++ -shared -O2 -o SiliconPlugin.dll src/plugin.cpp src/gates.cpp
//
// Then copy SiliconPlugin.dll into Assets/Plugins/x86_64/. Close the Unity
// editor before rebuilding — it holds a lock on loaded native libraries, and
// the copy will fail silently while it is open.

#include "gates.h"

// Export decoration. MinGW and MSVC use __declspec; the GCC/Clang attribute is
// here so a future macOS or Linux player build exports correctly without this
// file changing shape. As exports accumulate in #12, they all use this macro.
#if defined(_WIN32)
    #define SILICON_API __declspec(dllexport)
#else
    #define SILICON_API __attribute__((visibility("default")))
#endif

extern "C" {

// Evaluate a single logic gate. Thin forwarding wrapper over
// silicon::EvaluateGate — no logic lives here, deliberately, so that what the
// truth-table test verifies is exactly what Unity calls.
//
// Exported under a prefixed name because a DLL's exports share one flat
// namespace inside the host process. "EvaluateGate" is generic enough to
// collide with another native plugin Unity has loaded; "Silicon_EvaluateGate"
// is not. The C# side names it explicitly via EntryPoint, so the managed API
// can still be called LogicEngine.Evaluate().
//
//   gateType    silicon::GateType — AND = 1, OR = 2, NOT = 3
//   inputs      caller-owned array of signals, each strictly 0 or 1
//   inputCount  number of elements in inputs
//
// Returns 0 or 1 on success, or a negative silicon::GateStatus on failure:
//   -1 unknown gate type   -2 wrong input count
//   -3 null inputs         -4 input outside {0, 1}
//
// Matching C# declaration (keep in sync — Assets/Scripts/LogicEngine/LogicEngine.cs):
//
//     [DllImport("SiliconPlugin", EntryPoint = "Silicon_EvaluateGate")]
//     private static extern int EvaluateGate(int gateType, int[] inputs, int inputCount);
//
SILICON_API int Silicon_EvaluateGate(int gateType, const int* inputs, int inputCount) {
    return silicon::EvaluateGate(gateType, inputs, inputCount);
}

}  // extern "C"
