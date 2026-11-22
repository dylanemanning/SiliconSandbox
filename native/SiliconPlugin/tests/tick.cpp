// tick.cpp — standalone verification for the unit-delay tick (#10).
//
// Same shape as circuit_eval.cpp: no Unity, no DLL, one PASS/FAIL line per
// check, exit code 1 on any failure.
//
// Calls Silicon_LoadCircuit / Silicon_Tick / Silicon_UnloadCircuit themselves,
// so what is verified is exactly what Unity calls, including the loaded-circuit
// lifetime and the error paths.
//
// The model under test (simulator.h): every gate has one tick of delay, sources
// and LEDs have none. The expected values below are worked out by hand from that
// rule, tick by tick, the way a Verilog gate-level simulation with #1 on every
// gate would produce them.
//
// Build and run from native/SiliconPlugin/:
//     g++ -Wall -Wextra -o tick.exe tests/tick.cpp src/plugin.cpp src/netlist.cpp src/simulator.cpp src/gates.cpp
//     ./tick.exe

#include "../src/gates.h"
#include "../src/netlist.h"
#include "../src/simulator.h"

#include <cstdio>
#include <string>
#include <vector>

using namespace silicon;

// plugin.cpp has no header; declared the way the C# side sees it.
extern "C" int Silicon_EvaluateCircuit(const NetlistNode* nodes, int nodeCount,
                                       const NetlistConnection* connections,
                                       int connectionCount,
                                       int* states);
extern "C" int Silicon_LoadCircuit(const NetlistNode* nodes, int nodeCount,
                                   const NetlistConnection* connections,
                                   int connectionCount);
extern "C" int Silicon_Tick(int* states, int nodeCount);
extern "C" void Silicon_UnloadCircuit();

namespace {

int checksRun = 0;
int failures = 0;

void Section(const char* title) {
    std::printf("\n%s\n", title);
}

void Check(const std::string& label, bool passed, const std::string& detail) {
    checksRun++;
    if (passed) {
        std::printf("  PASS  %s\n", label.c_str());
    } else {
        std::printf("  FAIL  %s   (%s)\n", label.c_str(), detail.c_str());
        failures++;
    }
}

void CheckInt(const std::string& label, int actual, int expected) {
    Check(label, actual == expected,
          "got " + std::to_string(actual) + ", expected " + std::to_string(expected));
}

std::string Join(const std::vector<int>& values) {
    std::string out = "[";
    for (std::size_t i = 0; i < values.size(); i++) {
        if (i > 0) out += ", ";
        out += std::to_string(values[i]);
    }
    return out + "]";
}

void CheckStates(const std::string& label, const std::vector<int>& actual,
                 const std::vector<int>& expected) {
    Check(label, actual == expected, "got " + Join(actual) + ", expected " + Join(expected));
}

int Load(const std::vector<NetlistNode>& nodes, const std::vector<NetlistConnection>& conns) {
    return Silicon_LoadCircuit(nodes.data(), static_cast<int>(nodes.size()),
                               conns.data(), static_cast<int>(conns.size()));
}

int Tick(std::vector<int>& states) {
    return Silicon_Tick(states.data(), static_cast<int>(states.size()));
}

// Tick until a tick changes nothing. Returns how many ticks changed something
// (the circuit's settling time), or -1 if it never settled within maxTicks.
int Settle(std::vector<int>& states, int maxTicks = 64) {
    for (int t = 0; t < maxTicks; t++) {
        const int changed = Tick(states);
        if (changed < 0) return changed;
        if (changed == 0) return t;
    }
    return -1;
}

// Tick n times and record one node's value after each tick.
std::vector<int> Trace(std::vector<int>& states, int node, int ticks) {
    std::vector<int> trace;
    for (int t = 0; t < ticks; t++) {
        Tick(states);
        trace.push_back(states[node]);
    }
    return trace;
}

// ---- Sections --------------------------------------------------------------

void Lifetime() {
    Section("Loaded-circuit lifetime and argument checks");

    Silicon_UnloadCircuit();
    std::vector<int> states = { 1, 1 };
    CheckInt("tick before any load -> SIM_ERR_NOT_LOADED", Tick(states), SIM_ERR_NOT_LOADED);
    CheckStates("states untouched when nothing is loaded", states, { 1, 1 });

    const std::vector<NetlistNode> two = { { 10, GATE_SOURCE }, { 20, GATE_OUTPUT } };
    const std::vector<NetlistConnection> wire = { { 10, 20, 0 } };
    CheckInt("load source -> LED -> SIM_OK", Load(two, wire), SIM_OK);

    std::vector<int> wrong = { 1, 1, 1 };
    CheckInt("node count mismatch -> SIM_ERR_NODE_COUNT",
             Silicon_Tick(wrong.data(), 3), SIM_ERR_NODE_COUNT);
    CheckStates("states untouched on a count mismatch", wrong, { 1, 1, 1 });
    CheckInt("null states -> SIM_ERR_NULL_STATES", Silicon_Tick(nullptr, 2), SIM_ERR_NULL_STATES);

    // A reload replaces the circuit, including its node count.
    const std::vector<NetlistNode> three = {
        { 1, GATE_SOURCE }, { 2, GATE_NOT }, { 3, GATE_OUTPUT },
    };
    const std::vector<NetlistConnection> chain = { { 1, 2, 0 }, { 2, 3, 0 } };
    CheckInt("reload with a different circuit -> SIM_OK", Load(three, chain), SIM_OK);
    std::vector<int> old = { 0, 0 };
    CheckInt("old node count rejected after a reload", Tick(old), SIM_ERR_NODE_COUNT);
    std::vector<int> fresh = { 0, 0, 0 };
    CheckInt("new node count accepted after a reload", Tick(fresh), 2);
    CheckStates("NOT of a low source goes high after one tick, LED with it", fresh, { 0, 1, 1 });

    // A failed load leaves nothing loaded, so a stale circuit can never tick.
    const std::vector<NetlistConnection> bad = { { 1, 99, 0 } };
    CheckInt("load with an unknown id -> NETLIST_ERR_UNKNOWN_ID", Load(three, bad),
             NETLIST_ERR_UNKNOWN_ID);
    CheckInt("tick after a failed load -> SIM_ERR_NOT_LOADED", Tick(fresh), SIM_ERR_NOT_LOADED);

    CheckInt("load again -> SIM_OK", Load(three, chain), SIM_OK);
    Silicon_UnloadCircuit();
    CheckInt("tick after unload -> SIM_ERR_NOT_LOADED", Tick(fresh), SIM_ERR_NOT_LOADED);
    Silicon_UnloadCircuit();
    Check("unload twice is harmless", true, "");

    CheckInt("empty circuit loads", Load({}, {}), SIM_OK);
    CheckInt("empty circuit ticks with nothing changed", Silicon_Tick(nullptr, 0), 0);
}

void IssueCircuit() {
    Section("Issue #10's 3-gate circuit: LED = (A AND B) OR (C AND D), every input combination");

    // Nodes listed out of order on purpose: the tick must not depend on order.
    const std::vector<NetlistNode> nodes = {
        { 7, GATE_OUTPUT }, { 6, GATE_OR }, { 4, GATE_AND }, { 5, GATE_AND },
        { 0, GATE_SOURCE }, { 1, GATE_SOURCE }, { 2, GATE_SOURCE }, { 3, GATE_SOURCE },
    };
    const std::vector<NetlistConnection> conns = {
        { 0, 4, 0 }, { 1, 4, 1 }, { 2, 5, 0 }, { 3, 5, 1 },
        { 4, 6, 0 }, { 5, 6, 1 }, { 6, 7, 0 },
    };
    // Indices: 0 LED, 1 OR, 2 AND(A,B), 3 AND(C,D), 4..7 sources A..D.

    bool allMatch = true;
    bool allWithinTwo = true;
    std::string mismatch;
    for (int bits = 0; bits < 16; bits++) {
        std::vector<int> reference = { 0, 0, 0, 0, bits & 1, (bits >> 1) & 1, (bits >> 2) & 1, (bits >> 3) & 1 };
        Silicon_EvaluateCircuit(nodes.data(), 8, conns.data(), 7, reference.data());

        Load(nodes, conns);
        std::vector<int> states = { 0, 0, 0, 0, bits & 1, (bits >> 1) & 1, (bits >> 2) & 1, (bits >> 3) & 1 };
        const int ticks = Settle(states);
        if (states != reference) {
            allMatch = false;
            mismatch = "inputs " + std::to_string(bits) + ": got " + Join(states) +
                       ", expected " + Join(reference);
        }
        if (ticks < 0 || ticks > 2) allWithinTwo = false;
    }
    Check("settled tick result equals the zero-delay evaluation for all 16 inputs", allMatch, mismatch);
    Check("two gate levels settle within two ticks", allWithinTwo, "a combination took longer");

    // The ripple itself, for A = B = 1, C = D = 0.
    Load(nodes, conns);
    std::vector<int> states = { 0, 0, 0, 0, 1, 1, 0, 0 };
    CheckInt("tick 1 changes one node", Tick(states), 1);
    CheckStates("tick 1: AND(A,B) high, OR and LED not yet", states, { 0, 0, 1, 0, 1, 1, 0, 0 });
    CheckInt("tick 2 changes two nodes", Tick(states), 2);
    CheckStates("tick 2: OR high, LED with it", states, { 1, 1, 1, 0, 1, 1, 0, 0 });
    CheckInt("tick 3 changes nothing (settled)", Tick(states), 0);
}

void Delays() {
    Section("Propagation delay: one tick per gate, none for sources, LEDs or wires");

    {
        // Source -> LED: no gate, no delay.
        const std::vector<NetlistNode> nodes = { { 0, GATE_SOURCE }, { 1, GATE_OUTPUT } };
        Load(nodes, { { 0, 1, 0 } });
        std::vector<int> states = { 1, 0 };
        Tick(states);
        CheckStates("LED on a source lights on the first tick", states, { 1, 1 });
    }
    {
        // Source -> BUF -> BUF -> BUF -> LED, plus a second LED tapped after
        // the first buffer (fan-out).
        const std::vector<NetlistNode> nodes = {
            { 0, GATE_SOURCE }, { 1, GATE_BUFFER }, { 2, GATE_BUFFER }, { 3, GATE_BUFFER },
            { 4, GATE_OUTPUT }, { 5, GATE_OUTPUT },
        };
        const std::vector<NetlistConnection> conns = {
            { 0, 1, 0 }, { 1, 2, 0 }, { 2, 3, 0 }, { 3, 4, 0 }, { 1, 5, 0 },
        };
        Load(nodes, conns);
        std::vector<int> states = { 1, 0, 0, 0, 0, 0 };
        CheckStates("end LED of a 3-buffer chain: low, low, high", Trace(states, 4, 3), { 0, 0, 1 });

        Load(nodes, conns);
        states = { 1, 0, 0, 0, 0, 0 };
        CheckStates("LED tapped after buffer 1: high from tick 1", Trace(states, 5, 3), { 1, 1, 1 });

        // Turning the source off ripples the same way.
        states[0] = 0;
        CheckStates("source off: end LED goes low on tick 3", Trace(states, 4, 4), { 1, 1, 0, 0 });
    }
    {
        // Source normalisation: any non-zero source value counts as 1.
        const std::vector<NetlistNode> nodes = { { 0, GATE_SOURCE }, { 1, GATE_NOT } };
        Load(nodes, { { 0, 1, 0 } });
        std::vector<int> states = { 7, 1 };
        Tick(states);
        CheckStates("source value 7 reads and normalises as 1", states, { 1, 0 });
    }
}

void Unwired() {
    Section("Unwired pins read 0");

    const std::vector<NetlistNode> nodes = {
        { 0, GATE_AND }, { 1, GATE_NOT }, { 2, GATE_NOR }, { 3, GATE_OUTPUT },
    };
    Load(nodes, {});
    std::vector<int> states = { 1, 0, 0, 1 };
    CheckInt("tick changes all four nodes", Tick(states), 4);
    CheckStates("AND low, NOT high, NOR high, unwired LED low", states, { 0, 1, 1, 0 });
    CheckInt("then settled", Tick(states), 0);
}

void Glitch() {
    Section("Hazard: LED = A AND (NOT A) glitches for one tick when A rises");

    // A static-1 hazard is the textbook proof that delays are modelled: with
    // zero delay this LED could never light, but in real hardware (and here)
    // the NOT is one gate slower than the direct path.
    const std::vector<NetlistNode> nodes = {
        { 0, GATE_SOURCE }, { 1, GATE_NOT }, { 2, GATE_AND }, { 3, GATE_OUTPUT },
    };
    const std::vector<NetlistConnection> conns = {
        { 0, 1, 0 }, { 0, 2, 0 }, { 1, 2, 1 }, { 2, 3, 0 },
    };
    Load(nodes, conns);
    std::vector<int> states = { 0, 0, 0, 0 };
    Settle(states);
    CheckStates("settled with A low: NOT high, AND low", states, { 0, 1, 0, 0 });

    states[0] = 1;
    CheckStates("A rises: LED reads 1 for exactly one tick", Trace(states, 3, 4), { 1, 0, 0, 0 });

    states[0] = 0;
    CheckStates("A falls: no glitch (AND sees A low at once)", Trace(states, 3, 4), { 0, 0, 0, 0 });
}

void Oscillators() {
    Section("Oscillators: feedback loops with an odd number of inversions");

    {
        // A NOT wired to its own input: the smallest clock, period 2 ticks.
        const std::vector<NetlistNode> nodes = { { 0, GATE_NOT }, { 1, GATE_OUTPUT } };
        CheckInt("NOT -> itself loads with SIM_HAS_CYCLE", Load(nodes, { { 0, 0, 0 }, { 0, 1, 0 } }),
                 SIM_HAS_CYCLE);
        std::vector<int> states = { 0, 0 };
        CheckStates("NOT -> itself toggles every tick", Trace(states, 1, 6), { 1, 0, 1, 0, 1, 0 });
        Check("it never settles", Settle(states, 20) == -1, "Settle reported a fixpoint");
    }
    {
        // Ring of three inverters: period 2 x 3 = 6 ticks, 3 high and 3 low.
        //
        // Start state matters. A player builds the ring a gate at a time, so
        // before the last wire closes the loop the chain has already settled to
        // NOT0 = 1 (unwired input), NOT1 = 0, NOT2 = 1, and that start gives the
        // real ring-oscillator period. From an all-zero power-on, equal delays
        // keep the three gates switching in phase with period 2 instead. That
        // is a known artifact of any equal-delay simulation (a Verilog
        // simulation shows X forever there); real hardware's noise breaks the
        // symmetry. Documented, not tested as desired behaviour.
        const std::vector<NetlistNode> nodes = {
            { 0, GATE_NOT }, { 1, GATE_NOT }, { 2, GATE_NOT }, { 3, GATE_OUTPUT },
        };
        const std::vector<NetlistConnection> conns = {
            { 0, 1, 0 }, { 1, 2, 0 }, { 2, 0, 0 }, { 2, 3, 0 },
        };
        CheckInt("3-inverter ring loads with SIM_HAS_CYCLE", Load(nodes, conns), SIM_HAS_CYCLE);
        std::vector<int> states = { 1, 0, 1, 1 };
        CheckStates("3-inverter ring: period 6, three ticks high then three low",
                    Trace(states, 3, 12), { 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1 });
    }
}

void Latches() {
    Section("Latches: feedback loops that hold state (SR-3.3)");

    // NOR SR latch: Q = NOR(R, Qn), Qn = NOR(S, Q).
    const std::vector<NetlistNode> nodes = {
        { 0, GATE_SOURCE },  // S
        { 1, GATE_SOURCE },  // R
        { 2, GATE_NOR },     // Q
        { 3, GATE_NOR },     // Qn
        { 4, GATE_OUTPUT },  // LED on Q
    };
    const std::vector<NetlistConnection> conns = {
        { 1, 2, 0 }, { 3, 2, 1 }, { 0, 3, 0 }, { 2, 3, 1 }, { 2, 4, 0 },
    };
    const int S = 0, R = 1, Q = 2, QN = 3, LED = 4;

    CheckInt("NOR SR latch loads with SIM_HAS_CYCLE", Load(nodes, conns), SIM_HAS_CYCLE);
    std::vector<int> states = { 0, 1, 0, 1, 0 };
    Settle(states);
    CheckStates("reset: Q low, Qn high", { states[Q], states[QN], states[LED] }, { 0, 1, 0 });
    states[R] = 0;
    CheckInt("release R: nothing changes", Tick(states), 0);

    states[S] = 1;
    const int setTicks = Settle(states);
    CheckStates("set (S=1): Q high, Qn low", { states[Q], states[QN], states[LED] }, { 1, 0, 1 });
    CheckInt("set takes two gate delays", setTicks, 2);

    states[S] = 0;
    CheckInt("release S: nothing changes", Tick(states), 0);
    Trace(states, Q, 10);
    CheckStates("Q holds high with S = R = 0", { states[Q], states[QN] }, { 1, 0 });

    states[R] = 1;
    Settle(states);
    CheckStates("reset (R=1): Q low, Qn high", { states[Q], states[QN], states[LED] }, { 0, 1, 0 });

    states[R] = 0;
    Trace(states, Q, 10);
    CheckStates("Q holds low with S = R = 0", { states[Q], states[QN] }, { 0, 1 });

    // The forbidden input. S = R = 1 forces Q = Qn = 0; releasing both on the
    // same tick leaves two perfectly matched NORs racing each other, and with
    // identical delays they oscillate forever. A real latch goes metastable and
    // falls one way at random; a gate-level Verilog simulation with equal
    // delays oscillates exactly like this. The game shows the real hazard
    // instead of quietly picking a winner.
    states[S] = 1;
    states[R] = 1;
    Settle(states);
    CheckStates("S = R = 1: Q and Qn both low", { states[Q], states[QN] }, { 0, 0 });
    states[S] = 0;
    states[R] = 0;
    CheckStates("releasing S and R together races: Q oscillates", Trace(states, Q, 4), { 1, 0, 1, 0 });
}

void DLatch() {
    Section("Gated D latch from four NANDs and a NOT");

    // S' = NAND(D, E), R' = NAND(NOT D, E), Q = NAND(S', Qn), Qn = NAND(R', Q).
    const std::vector<NetlistNode> nodes = {
        { 0, GATE_SOURCE },  // D
        { 1, GATE_SOURCE },  // E (enable)
        { 2, GATE_NOT },     // NOT D
        { 3, GATE_NAND },    // S'
        { 4, GATE_NAND },    // R'
        { 5, GATE_NAND },    // Q
        { 6, GATE_NAND },    // Qn
    };
    const std::vector<NetlistConnection> conns = {
        { 0, 2, 0 },
        { 0, 3, 0 }, { 1, 3, 1 },
        { 2, 4, 0 }, { 1, 4, 1 },
        { 3, 5, 0 }, { 6, 5, 1 },
        { 4, 6, 0 }, { 5, 6, 1 },
    };
    const int D = 0, E = 1, Q = 5, QN = 6;

    Load(nodes, conns);
    std::vector<int> states(7, 0);
    states[E] = 1;
    Settle(states);
    CheckStates("E=1, D=0: Q low", { states[Q], states[QN] }, { 0, 1 });

    states[D] = 1;
    Settle(states);
    CheckStates("E=1, D=1: Q follows D high", { states[Q], states[QN] }, { 1, 0 });

    states[E] = 0;
    Settle(states);
    states[D] = 0;
    const int ticks = Settle(states);
    CheckStates("E=0 then D=0: Q holds high", { states[Q], states[QN] }, { 1, 0 });
    Check("holding settles", ticks >= 0, "never settled");

    states[E] = 1;
    Settle(states);
    CheckStates("E=1 again: Q follows D low", { states[Q], states[QN] }, { 0, 1 });
}

}  // namespace

int main() {
    std::printf("Silicon_Tick verification (#10, unit-delay model)\n");

    Lifetime();
    IssueCircuit();
    Delays();
    Unwired();
    Glitch();
    Oscillators();
    Latches();
    DLatch();

    Silicon_UnloadCircuit();
    std::printf("\n%d checks, %d failed\n", checksRun, failures);
    return failures == 0 ? 0 : 1;
}
