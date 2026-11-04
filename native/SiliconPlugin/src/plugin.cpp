// plugin.cpp — the native plugin's exported surface.
//
// This is the only file in SiliconPlugin that knows it is a DLL. Everything
// here is C-compatible on purpose; everything in gates.cpp stays plain,
// portable, testable C++. Keeping the boundary in one small file is what makes
// tests/truth_table.cpp able to exercise the logic without Unity, and what
// keeps #12's netlist marshaling a matter of adding exports rather than
// untangling Unity concerns from the engine.
//
// Exports:
//   Silicon_EvaluateGate     one gate (#7)
//   Silicon_EvaluateCircuit  a whole netlist in one pass (#12, stateless demo
//                            version; #10 splits it into load + tick)
//
// Rules for anything added to this file:
//   - extern "C" only. C++ name mangling is compiler-specific; DllImport
//     resolves symbols by plain name.
//   - Plain types across the boundary: int, pointers to int, sizes, and
//     pointers to the all-int NetlistNode / NetlistConnection structs from
//     netlist.h. No C++ classes, no std:: types, no references, no exceptions
//     allowed to escape.
//   - Memory crossing the boundary is allocated and owned by the caller (here,
//     Unity's marshaler owns the inputs array).
//   - Nothing is kept between calls. Each export builds what it needs on the
//     stack and frees it before returning.
//
// Build the DLL from native/SiliconPlugin/:
//     g++ -shared -O2 -static -s -o SiliconPlugin.dll src/plugin.cpp src/gates.cpp src/netlist.cpp src/simulator.cpp
//
// -static matters: netlist.cpp uses the C++ standard library, and without it
// the DLL depends on libstdc++-6.dll, which Unity can't find.
//
// -s strips debug info and symbols. Without it MinGW's static libstdc++ adds
// roughly 460 KB of debug sections to a binary that is committed to git.
//
// Then copy SiliconPlugin.dll into Assets/Plugins/x86_64/. Close the Unity
// editor before rebuilding — it holds a lock on loaded native libraries, and
// the copy will fail silently while it is open.

#include "gates.h"
#include "netlist.h"
#include "simulator.h"

#include <new>

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
//   gateType    silicon::GateType — any gate (AND, OR, NOT, NAND, NOR, XOR,
//               XNOR, BUFFER); SOURCE and OUTPUT are rejected as unknown
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

// Parse a netlist and evaluate every node in one call (#12).
//
// Stateless: the graph is built, evaluated and thrown away inside this call.
// C# calls it again whenever the placed circuit or a source changes. Parsing a
// demo-sized circuit is microseconds, so caching the graph in the DLL is not
// worth the lifetime questions yet; #10's Silicon_LoadCircuit is where that
// happens.
//
//   nodes            caller-owned array of nodeCount silicon::NetlistNode
//   connections      caller-owned array of connectionCount silicon::NetlistConnection
//   states           caller-owned array of nodeCount ints, indexed like nodes.
//                    In: the value of each SOURCE node (non-zero = 1); other
//                    entries are ignored. Out: every node's value, 0 or 1. See
//                    silicon::EvaluateCircuit in simulator.h for the details.
//
// Returns:
//    0  SIM_OK
//    1  SIM_HAS_CYCLE — evaluated, but nodes on or after a feedback loop read 0
//   -10..-18  a NetlistStatus from the parser (netlist.h); states all 0
//   -20..-22  a SimulateStatus failure (simulator.h); states all 0
//
// The caller must make states exactly nodeCount long. A pointer cannot carry
// its length, so the C# wrapper checks this before calling.
//
// Matching C# declaration (keep in sync — Assets/Scripts/LogicEngine/LogicEngine.cs):
//
//     [DllImport("SiliconPlugin", EntryPoint = "Silicon_EvaluateCircuit")]
//     private static extern int Silicon_EvaluateCircuit(
//         NetlistNode[] nodes, int nodeCount,
//         NetlistConnection[] connections, int connectionCount,
//         int[] states);
//
SILICON_API int Silicon_EvaluateCircuit(const silicon::NetlistNode* nodes, int nodeCount,
                                        const silicon::NetlistConnection* connections,
                                        int connectionCount,
                                        int* states) {
    if (nodeCount > 0 && states == nullptr) {
        return silicon::SIM_ERR_NULL_STATES;
    }

    // ParseNetlist and EvaluateCircuit never throw, but building the empty
    // CircuitGraph and destroying the full one sit outside their guards. An
    // exception escaping an extern "C" function takes the Unity editor down
    // with it, so nothing is allowed through.
    try {
        silicon::CircuitGraph graph;
        const int parsed = silicon::ParseNetlist(nodes, nodeCount, connections, connectionCount, graph);
        if (parsed != silicon::NETLIST_OK) {
            for (int i = 0; i < nodeCount; i++) {
                states[i] = 0;
            }
            return parsed;
        }
        return silicon::EvaluateCircuit(graph, states);
    } catch (const std::bad_alloc&) {
        return silicon::NETLIST_ERR_OUT_OF_MEMORY;
    } catch (...) {
        return silicon::SIM_ERR_INTERNAL;
    }
}

}  // extern "C"
