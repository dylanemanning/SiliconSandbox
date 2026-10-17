// circuit_eval.cpp — standalone verification for whole-circuit evaluation (#12).
//
// Same shape as truth_table.cpp and graph_parser.cpp: no Unity, no DLL, one
// PASS/FAIL line per check, exit code 1 on any failure.
//
// Calls Silicon_EvaluateCircuit itself rather than silicon::EvaluateCircuit,
// so what is verified here is exactly the function Unity calls: parse and
// evaluate together, including the error paths and the states-zeroed-on-failure
// guarantee.
//
// Build and run from native/SiliconPlugin/:
//     g++ -Wall -Wextra -o circuit_eval.exe tests/circuit_eval.cpp src/plugin.cpp src/netlist.cpp src/simulator.cpp src/gates.cpp
//     ./circuit_eval.exe

#include "../src/gates.h"
#include "../src/netlist.h"
#include "../src/simulator.h"

#include <cstdio>
#include <string>
#include <vector>

using namespace silicon;

// plugin.cpp has no header: it is the DLL surface, and C# declares it with
// DllImport. Declared here the same way the C# side sees it.
extern "C" int Silicon_EvaluateCircuit(const NetlistNode* nodes, int nodeCount,
                                       const NetlistConnection* connections,
                                       int connectionCount,
                                       int* states);

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

int Run(const std::vector<NetlistNode>& nodes, const std::vector<NetlistConnection>& conns,
        std::vector<int>& states) {
    return Silicon_EvaluateCircuit(nodes.data(), static_cast<int>(nodes.size()),
                                   conns.data(), static_cast<int>(conns.size()),
                                   states.data());
}

// ---- Sections --------------------------------------------------------------

// Source -> gate -> LED for every gate type, across its full truth table.
// A reference model written here, independently of gates.cpp.
int Reference(int type, int a, int b) {
    switch (type) {
        case GATE_AND:    return a & b;
        case GATE_OR:     return a | b;
        case GATE_NAND:   return !(a & b);
        case GATE_NOR:    return !(a | b);
        case GATE_XOR:    return a ^ b;
        case GATE_XNOR:   return !(a ^ b);
        case GATE_NOT:    return !a;
        case GATE_BUFFER: return a;
        default:          return -1;
    }
}

void SingleGates() {
    Section("Single gate between sources and an LED");

    struct Gate { int type; const char* name; bool twoInput; };
    const Gate gates[] = {
        { GATE_AND, "AND", true },  { GATE_OR, "OR", true },     { GATE_NAND, "NAND", true },
        { GATE_NOR, "NOR", true },  { GATE_XOR, "XOR", true },   { GATE_XNOR, "XNOR", true },
        { GATE_NOT, "NOT", false }, { GATE_BUFFER, "BUFFER", false },
    };

    for (const Gate& gate : gates) {
        // 0 = source A, 1 = source B, 2 = gate, 3 = LED
        std::vector<NetlistNode> nodes = {
            { 10, GATE_SOURCE }, { 11, GATE_SOURCE }, { 12, gate.type }, { 13, GATE_OUTPUT },
        };
        std::vector<NetlistConnection> conns = { { 10, 12, 0 }, { 12, 13, 0 } };
        if (gate.twoInput) {
            conns.push_back({ 11, 12, 1 });
        }

        const int combos = gate.twoInput ? 4 : 2;
        for (int bits = 0; bits < combos; bits++) {
            const int a = bits & 1;
            const int b = (bits >> 1) & 1;
            std::vector<int> states = { a, b, 0, 0 };
            const int status = Run(nodes, conns, states);
            const int want = Reference(gate.type, a, b);

            std::string label = std::string(gate.name) + "(" + std::to_string(a) +
                                (gate.twoInput ? ", " + std::to_string(b) : "") + ")";
            Check(label + " -> gate and LED = " + std::to_string(want),
                  status == SIM_OK && states[2] == want && states[3] == want,
                  "status " + std::to_string(status) + ", states " + Join(states));
        }
    }
}

// Two levels of logic, so evaluation order matters: (A AND B) OR (NOT C).
// Nodes are listed with the OR first, so an evaluator that walked array order
// instead of evalOrder would read the AND and NOT before computing them.
void MultiLevel() {
    Section("Multi-level circuit: LED = (A AND B) OR (NOT C), nodes listed out of order");

    // 0 = LED, 1 = OR, 2 = AND, 3 = NOT, 4..6 = sources A, B, C
    const std::vector<NetlistNode> nodes = {
        { 0, GATE_OUTPUT }, { 1, GATE_OR }, { 2, GATE_AND }, { 3, GATE_NOT },
        { 4, GATE_SOURCE }, { 5, GATE_SOURCE }, { 6, GATE_SOURCE },
    };
    const std::vector<NetlistConnection> conns = {
        { 4, 2, 0 }, { 5, 2, 1 }, { 6, 3, 0 }, { 2, 1, 0 }, { 3, 1, 1 }, { 1, 0, 0 },
    };

    for (int bits = 0; bits < 8; bits++) {
        const int a = bits & 1, b = (bits >> 1) & 1, c = (bits >> 2) & 1;
        std::vector<int> states = { 0, 0, 0, 0, a, b, c };
        const int status = Run(nodes, conns, states);
        const int want = (a & b) | !c;
        Check("A=" + std::to_string(a) + " B=" + std::to_string(b) + " C=" + std::to_string(c) +
                  " -> LED " + std::to_string(want),
              status == SIM_OK && states[0] == want,
              "status " + std::to_string(status) + ", states " + Join(states));
    }
}

// Half adder: one source pair fans out to an XOR and an AND, each with an LED.
void FanOut() {
    Section("Fan-out: half adder (sum = A XOR B, carry = A AND B)");

    // 0 = A, 1 = B, 2 = XOR, 3 = AND, 4 = sum LED, 5 = carry LED
    const std::vector<NetlistNode> nodes = {
        { 0, GATE_SOURCE }, { 1, GATE_SOURCE }, { 2, GATE_XOR }, { 3, GATE_AND },
        { 4, GATE_OUTPUT }, { 5, GATE_OUTPUT },
    };
    const std::vector<NetlistConnection> conns = {
        { 0, 2, 0 }, { 1, 2, 1 }, { 0, 3, 0 }, { 1, 3, 1 }, { 2, 4, 0 }, { 3, 5, 0 },
    };

    for (int bits = 0; bits < 4; bits++) {
        const int a = bits & 1, b = (bits >> 1) & 1;
        std::vector<int> states = { a, b, 0, 0, 0, 0 };
        Run(nodes, conns, states);
        CheckStates("A=" + std::to_string(a) + " B=" + std::to_string(b), states,
                    { a, b, a ^ b, a & b, a ^ b, a & b });
    }
}

// What a half-built circuit reads as.
void UnwiredAndInputs() {
    Section("Unwired pins and the states array contract");

    {
        // An AND with only pin 0 wired: pin 1 floats and reads 0.
        const std::vector<NetlistNode> nodes = { { 0, GATE_SOURCE }, { 1, GATE_AND } };
        const std::vector<NetlistConnection> conns = { { 0, 1, 0 } };
        std::vector<int> states = { 1, 0 };
        Run(nodes, conns, states);
        CheckInt("AND(1, unwired) = 0", states[1], 0);
    }
    {
        // A NOT with nothing wired reads its input as 0, so it outputs 1.
        const std::vector<NetlistNode> nodes = { { 0, GATE_NOT } };
        std::vector<int> states = { 0 };
        Run(nodes, {}, states);
        CheckInt("NOT(unwired) = 1", states[0], 1);
    }
    {
        // An LED with nothing wired to it.
        const std::vector<NetlistNode> nodes = { { 0, GATE_OUTPUT } };
        std::vector<int> states = { 1 };
        Run(nodes, {}, states);
        CheckInt("unwired LED = 0", states[0], 0);
    }
    {
        // Any non-zero source value counts as 1, and is written back as 1.
        const std::vector<NetlistNode> nodes = { { 0, GATE_SOURCE }, { 1, GATE_OUTPUT } };
        const std::vector<NetlistConnection> conns = { { 0, 1, 0 } };
        std::vector<int> states = { 7, 0 };
        Run(nodes, conns, states);
        CheckStates("source value 7 normalised to 1", states, { 1, 1 });
    }
    {
        // Whatever is left in non-source slots (from the previous call, say)
        // is ignored and overwritten.
        const std::vector<NetlistNode> nodes = { { 0, GATE_SOURCE }, { 1, GATE_BUFFER } };
        const std::vector<NetlistConnection> conns = { { 0, 1, 0 } };
        std::vector<int> states = { 0, 5 };
        Run(nodes, conns, states);
        CheckStates("stale value in a gate slot is overwritten", states, { 0, 0 });
    }
    {
        // ids are sparse and negative; states are indexed by array position.
        const std::vector<NetlistNode> nodes = {
            { 900, GATE_OUTPUT }, { -4, GATE_NOT }, { 77, GATE_SOURCE },
        };
        const std::vector<NetlistConnection> conns = { { 77, -4, 0 }, { -4, 900, 0 } };
        std::vector<int> states = { 0, 0, 0 };
        Run(nodes, conns, states);
        CheckStates("sparse ids: states follow array order", states, { 1, 1, 0 });
    }
    {
        // Calling again with new source values gives the new answer: no state
        // is carried between calls.
        const std::vector<NetlistNode> nodes = { { 0, GATE_SOURCE }, { 1, GATE_NOT } };
        const std::vector<NetlistConnection> conns = { { 0, 1, 0 } };
        std::vector<int> states = { 0, 0 };
        Run(nodes, conns, states);
        states[0] = 1;
        Run(nodes, conns, states);
        CheckStates("re-evaluating after a source flips", states, { 1, 0 });
    }
    {
        // A wide AND (pins 0..2) folds across all three.
        const std::vector<NetlistNode> nodes = {
            { 0, GATE_SOURCE }, { 1, GATE_SOURCE }, { 2, GATE_SOURCE }, { 3, GATE_AND },
        };
        const std::vector<NetlistConnection> conns = { { 0, 3, 0 }, { 1, 3, 1 }, { 2, 3, 2 } };
        std::vector<int> states = { 1, 1, 0, 0 };
        Run(nodes, conns, states);
        CheckInt("3-input AND(1, 1, 0) = 0", states[3], 0);
        states = { 1, 1, 1, 0 };
        Run(nodes, conns, states);
        CheckInt("3-input AND(1, 1, 1) = 1", states[3], 1);
    }
}

void Cycles() {
    Section("Feedback loops (left for #10)");

    // 0, 1 = two NOTs feeding each other; 2 = source, 3 = buffer, 4 = LED off
    // the buffer. The loop must not stop the rest of the circuit evaluating.
    const std::vector<NetlistNode> nodes = {
        { 0, GATE_NOT }, { 1, GATE_NOT }, { 2, GATE_SOURCE }, { 3, GATE_BUFFER }, { 4, GATE_OUTPUT },
    };
    const std::vector<NetlistConnection> conns = {
        { 0, 1, 0 }, { 1, 0, 0 }, { 2, 3, 0 }, { 3, 4, 0 },
    };
    std::vector<int> states = { 1, 1, 1, 0, 0 };
    const int status = Run(nodes, conns, states);
    CheckInt("returns SIM_HAS_CYCLE", status, SIM_HAS_CYCLE);
    CheckStates("loop nodes read 0, the rest evaluates", states, { 0, 0, 1, 1, 1 });
}

void Errors() {
    Section("Errors: status passed through, states zeroed");

    {
        // Two sources into one AND pin: the short circuit TC-3.4.5 describes.
        // #18's netlist builder should never send this, but if it does the
        // parser's error must come back unchanged.
        const std::vector<NetlistNode> nodes = {
            { 0, GATE_SOURCE }, { 1, GATE_SOURCE }, { 2, GATE_AND },
        };
        const std::vector<NetlistConnection> conns = { { 0, 2, 0 }, { 1, 2, 0 } };
        std::vector<int> states = { 1, 1, 1 };
        const int status = Run(nodes, conns, states);
        CheckInt("multiple drivers -> NETLIST_ERR_MULTIPLE_DRIVERS", status, NETLIST_ERR_MULTIPLE_DRIVERS);
        CheckStates("states zeroed after a parse error", states, { 0, 0, 0 });
    }
    {
        const std::vector<NetlistNode> nodes = { { 0, GATE_SOURCE }, { 0, GATE_OUTPUT } };
        std::vector<int> states = { 1, 1 };
        CheckInt("duplicate id -> NETLIST_ERR_DUPLICATE_ID", Run(nodes, {}, states), NETLIST_ERR_DUPLICATE_ID);
    }
    {
        const std::vector<NetlistNode> nodes = { { 0, GATE_INVALID } };
        std::vector<int> states = { 0 };
        CheckInt("type 0 -> NETLIST_ERR_UNKNOWN_TYPE", Run(nodes, {}, states), NETLIST_ERR_UNKNOWN_TYPE);
    }
    {
        const NetlistNode node = { 0, GATE_SOURCE };
        CheckInt("null states -> SIM_ERR_NULL_STATES",
                 Silicon_EvaluateCircuit(&node, 1, nullptr, 0, nullptr), SIM_ERR_NULL_STATES);
    }
    {
        CheckInt("empty circuit -> SIM_OK",
                 Silicon_EvaluateCircuit(nullptr, 0, nullptr, 0, nullptr), SIM_OK);
    }
}

}  // namespace

int main() {
    std::printf("Silicon_EvaluateCircuit verification (#12)\n");

    SingleGates();
    MultiLevel();
    FanOut();
    UnwiredAndInputs();
    Cycles();
    Errors();

    std::printf("\n%d checks, %d failed\n", checksRun, failures);
    return failures == 0 ? 0 : 1;
}
