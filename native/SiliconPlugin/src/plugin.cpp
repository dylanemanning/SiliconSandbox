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
//   Silicon_EvaluateCircuit  a whole netlist in one zero-delay pass (#12)
//   Silicon_LoadCircuit      parse a netlist and keep it for ticking (#10)
//   Silicon_Tick             advance the loaded circuit by one unit-delay tick (#10)
//   Silicon_UnloadCircuit    free the loaded circuit (#10)
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
//   - The only state kept between calls is the one loaded circuit (#10): its
//     parsed graph and a scratch buffer, both owned by this file. The circuit's
//     signal values are NOT kept here; they live in the caller's states array,
//     so the caller can save, reset or inspect them without asking the DLL.
//   - Main thread only. Unity calls these from the SimulationDriver on the main
//     thread; nothing here is locked.
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
#include <vector>

// Export decoration. MinGW and MSVC use __declspec; the GCC/Clang attribute is
// here so a future macOS or Linux player build exports correctly without this
// file changing shape. As exports accumulate in #12, they all use this macro.
#if defined(_WIN32)
    #define SILICON_API __declspec(dllexport)
#else
    #define SILICON_API __attribute__((visibility("default")))
#endif

namespace {

// The circuit Silicon_LoadCircuit parsed, ticked by Silicon_Tick.
//
// One slot is enough: the game simulates one world at a time. Loading again
// replaces it. Note the Unity editor keeps native plugins loaded between play
// sessions, so this outlives a play session unless the SimulationDriver calls
// Silicon_UnloadCircuit when it is destroyed.
struct LoadedCircuit {
    silicon::CircuitGraph graph;
    std::vector<int> next;  // TickCircuit's scratch buffer, one int per node
    bool loaded = false;

    void Reset() {
        loaded = false;
        graph.Clear();
        next.clear();
    }
};

LoadedCircuit g_circuit;

}  // namespace

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

// Parse a netlist and keep it as the circuit Silicon_Tick advances (#10).
//
// Call this whenever the circuit's structure changes: a block placed, broken
// or rotated. NOT when a source is toggled; a source's value is just written
// into the states array before the next Silicon_Tick.
//
// Takes the same nodes and connections as Silicon_EvaluateCircuit. Replaces
// whatever was loaded before. Does not touch any states array: the caller
// decides what the new circuit starts at (all 0 is power-on; carrying values
// over from the old circuit keeps latches holding across an edit).
//
// Returns:
//    0  SIM_OK
//    1  SIM_HAS_CYCLE — loaded; the circuit has a feedback loop. Informational
//       only: loops are what latches and oscillators are made of, and the
//       unit-delay tick handles them
//   -10..-18  a NetlistStatus from the parser; nothing is loaded afterwards
//   -18 (NETLIST_ERR_OUT_OF_MEMORY) or -22 (SIM_ERR_INTERNAL) on an exception;
//       nothing is loaded afterwards
//
// Matching C# declaration (keep in sync — Assets/Scripts/LogicEngine/LogicEngine.cs):
//
//     [DllImport("SiliconPlugin", EntryPoint = "Silicon_LoadCircuit")]
//     private static extern int Silicon_LoadCircuit(
//         NetlistNode[] nodes, int nodeCount,
//         NetlistConnection[] connections, int connectionCount);
//
SILICON_API int Silicon_LoadCircuit(const silicon::NetlistNode* nodes, int nodeCount,
                                    const silicon::NetlistConnection* connections,
                                    int connectionCount) {
    try {
        g_circuit.Reset();
        const int parsed = silicon::ParseNetlist(nodes, nodeCount, connections, connectionCount,
                                                 g_circuit.graph);
        if (parsed != silicon::NETLIST_OK) {
            g_circuit.Reset();
            return parsed;
        }
        g_circuit.next.assign(g_circuit.graph.nodes.size(), 0);
        g_circuit.loaded = true;
        return g_circuit.graph.hasCycle ? silicon::SIM_HAS_CYCLE : silicon::SIM_OK;
    } catch (const std::bad_alloc&) {
        g_circuit.Reset();
        return silicon::NETLIST_ERR_OUT_OF_MEMORY;
    } catch (...) {
        g_circuit.Reset();
        return silicon::SIM_ERR_INTERNAL;
    }
}

// Advance the loaded circuit by one tick (#10). See silicon::TickCircuit in
// simulator.h for the unit-delay model.
//
//   states     caller-owned array of nodeCount ints, indexed like the nodes
//              array given to Silicon_LoadCircuit. In: every node's value at
//              this tick, with each SOURCE set to the player's current value.
//              Out: every node's value at the next tick, 0 or 1.
//   nodeCount  must equal the loaded circuit's node count
//
// Returns:
//   >= 0  the number of nodes whose value changed; 0 means the circuit has
//         settled and needs no more ticks until a source or the circuit changes
//   -20   SIM_ERR_NULL_STATES (nodeCount > 0 and states is null)
//   -21   SIM_ERR_GATE (a bug; states zeroed)
//   -23   SIM_ERR_NOT_LOADED (states untouched)
//   -24   SIM_ERR_NODE_COUNT (states untouched)
//
// Matching C# declaration (keep in sync — Assets/Scripts/LogicEngine/LogicEngine.cs):
//
//     [DllImport("SiliconPlugin", EntryPoint = "Silicon_Tick")]
//     private static extern int Silicon_Tick(int[] states, int nodeCount);
//
SILICON_API int Silicon_Tick(int* states, int nodeCount) {
    if (!g_circuit.loaded) {
        return silicon::SIM_ERR_NOT_LOADED;
    }
    if (nodeCount != static_cast<int>(g_circuit.graph.nodes.size())) {
        return silicon::SIM_ERR_NODE_COUNT;
    }
    // TickCircuit never throws or allocates, so there is nothing to catch.
    return silicon::TickCircuit(g_circuit.graph, states, g_circuit.next.data());
}

// Free the loaded circuit. Safe to call when nothing is loaded. Afterwards
// Silicon_Tick returns SIM_ERR_NOT_LOADED until the next Silicon_LoadCircuit.
//
// Matching C# declaration (keep in sync — Assets/Scripts/LogicEngine/LogicEngine.cs):
//
//     [DllImport("SiliconPlugin", EntryPoint = "Silicon_UnloadCircuit")]
//     private static extern void Silicon_UnloadCircuit();
//
SILICON_API void Silicon_UnloadCircuit() {
    g_circuit.Reset();
}

}  // extern "C"
