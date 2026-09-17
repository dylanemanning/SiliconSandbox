// graph_parser.cpp — standalone verification for the netlist parser (#9).
//
// Same shape as truth_table.cpp: no Unity, no DLL, one PASS/FAIL line per
// check, exit code 1 on any failure.
//
// Build and run from native/SiliconPlugin/:
//     g++ -Wall -Wextra -o graph_parser.exe tests/graph_parser.cpp src/netlist.cpp src/gates.cpp
//     ./graph_parser.exe
//
// Scope: graph construction only — node and edge counts, pin wiring, fan-out,
// unwired pins, evaluation order, cycle detection, and the error contract.
// Signal values belong to #10.

#include "../src/gates.h"
#include "../src/netlist.h"

#include <cstdio>
#include <vector>

using namespace silicon;

namespace {

int checksRun = 0;
int failures = 0;

const char* StatusName(int status) {
    switch (status) {
        case NETLIST_OK:                    return "OK";
        case NETLIST_ERR_NULL_ARRAY:        return "ERR_NULL_ARRAY";
        case NETLIST_ERR_NEGATIVE_COUNT:    return "ERR_NEGATIVE_COUNT";
        case NETLIST_ERR_DUPLICATE_ID:      return "ERR_DUPLICATE_ID";
        case NETLIST_ERR_UNKNOWN_TYPE:      return "ERR_UNKNOWN_TYPE";
        case NETLIST_ERR_UNKNOWN_ID:        return "ERR_UNKNOWN_ID";
        case NETLIST_ERR_NO_OUTPUT:         return "ERR_NO_OUTPUT";
        case NETLIST_ERR_PIN_OUT_OF_RANGE:  return "ERR_PIN_OUT_OF_RANGE";
        case NETLIST_ERR_MULTIPLE_DRIVERS:  return "ERR_MULTIPLE_DRIVERS";
        case NETLIST_ERR_OUT_OF_MEMORY:     return "ERR_OUT_OF_MEMORY";
        default:                            return "?";
    }
}

void Section(const char* title) {
    std::printf("\n%s\n", title);
}

void Check(const char* label, bool passed, int actual, int expected) {
    checksRun++;
    if (passed) {
        std::printf("  PASS  %-44s -> %d\n", label, actual);
    } else {
        std::printf("  FAIL  %-44s -> %d   (expected %d)\n", label, actual, expected);
        failures++;
    }
}

void CheckInt(const char* label, int actual, int expected) {
    Check(label, actual == expected, actual, expected);
}

void CheckStatus(const char* label, int actual, int expected) {
    checksRun++;
    if (actual == expected) {
        std::printf("  PASS  %-44s -> %s\n", label, StatusName(actual));
    } else {
        std::printf("  FAIL  %-44s -> %s   (expected %s)\n",
                    label, StatusName(actual), StatusName(expected));
        failures++;
    }
}

// Parses and checks only the status. Also confirms the graph is left empty on
// failure, since #12 will rely on that.
void ExpectError(const char* label, int expected,
                 const std::vector<NetlistNode>& nodes,
                 const std::vector<NetlistConnection>& conns) {
    CircuitGraph g;
    const int status = ParseNetlist(nodes.data(), static_cast<int>(nodes.size()),
                                    conns.data(), static_cast<int>(conns.size()), g);
    checksRun++;
    const bool empty = g.nodes.empty() && g.indexOf.empty() &&
                       g.evalOrder.empty() && g.edgeCount == 0;
    if (status == expected && (status == NETLIST_OK || empty)) {
        std::printf("  PASS  %-44s -> %s\n", label, StatusName(status));
    } else {
        std::printf("  FAIL  %-44s -> %s%s   (expected %s)\n", label, StatusName(status),
                    empty ? "" : ", graph not cleared", StatusName(expected));
        failures++;
    }
}

// Index of a node by its original id, or -1.
int Idx(const CircuitGraph& g, int id) {
    const auto it = g.indexOf.find(id);
    return it == g.indexOf.end() ? -1 : it->second;
}

// True when evalOrder lists every node exactly once and each node comes after
// all of its drivers. Checked structurally rather than against a fixed list,
// so the tests don't pin down one particular valid order.
bool EvalOrderIsValid(const CircuitGraph& g) {
    if (g.evalOrder.size() != g.nodes.size()) {
        return false;
    }
    std::vector<int> position(g.nodes.size(), -1);
    for (std::size_t i = 0; i < g.evalOrder.size(); i++) {
        const int n = g.evalOrder[i];
        if (n < 0 || n >= static_cast<int>(g.nodes.size()) || position[n] != -1) {
            return false;
        }
        position[n] = static_cast<int>(i);
    }
    for (std::size_t n = 0; n < g.nodes.size(); n++) {
        for (int driver : g.nodes[n].inputs) {
            if (driver == kNoDriver) {
                continue;
            }
            if (position[driver] >= position[n]) {
                return false;
            }
        }
    }
    return true;
}

int Parse(const std::vector<NetlistNode>& nodes,
          const std::vector<NetlistConnection>& conns, CircuitGraph& g) {
    return ParseNetlist(nodes.data(), static_cast<int>(nodes.size()),
                        conns.data(), static_cast<int>(conns.size()), g);
}

}  // namespace

int main() {
    std::printf("Netlist parser verification — issue #9\n");

    // ---- The issue's sample: two ANDs into an OR --------------------------
    //
    //   S1 ─┐                      ids are deliberately sparse and out of
    //       AND(10) ─┐             order, the way grid-derived ids will be
    //   S2 ─┘        │
    //                OR(30) ── LED(40)
    //   S3 ─┐        │
    //       AND(20) ─┘
    //   S4 ─┘
    //
    // 4 sources + 3 gates + 1 LED = 8 nodes; 4 + 2 + 1 = 7 connections.
    {
        Section("Sample: (S1 AND S2) OR (S3 AND S4) -> LED");

        const std::vector<NetlistNode> nodes = {
            {40, GATE_OUTPUT}, {30, GATE_OR}, {20, GATE_AND}, {10, GATE_AND},
            {1, GATE_SOURCE},  {2, GATE_SOURCE}, {3, GATE_SOURCE}, {4, GATE_SOURCE},
        };
        const std::vector<NetlistConnection> conns = {
            {30, 40, 0},
            {10, 30, 0}, {20, 30, 1},
            {1, 10, 0}, {2, 10, 1}, {3, 20, 0}, {4, 20, 1},
        };

        CircuitGraph g;
        CheckStatus("parse", Parse(nodes, conns, g), NETLIST_OK);
        CheckInt("node count", static_cast<int>(g.nodes.size()), 8);
        CheckInt("edge count", g.edgeCount, 7);

        const int orIdx = Idx(g, 30), and1 = Idx(g, 10), and2 = Idx(g, 20);
        const int led = Idx(g, 40), s1 = Idx(g, 1), s4 = Idx(g, 4);

        CheckInt("index follows input array order (LED)", led, 0);
        CheckInt("index follows input array order (S4)", s4, 7);
        CheckInt("OR has 2 inputs", static_cast<int>(g.nodes[orIdx].inputs.size()), 2);
        CheckInt("OR pin 0 driven by AND(10)", g.nodes[orIdx].inputs[0], and1);
        CheckInt("OR pin 1 driven by AND(20)", g.nodes[orIdx].inputs[1], and2);
        CheckInt("AND(10) pin 0 driven by S1", g.nodes[and1].inputs[0], s1);
        CheckInt("LED pin 0 driven by OR", g.nodes[led].inputs[0], orIdx);
        CheckInt("source has no inputs", static_cast<int>(g.nodes[s1].inputs.size()), 0);
        CheckInt("S1 fans out to 1 node", static_cast<int>(g.nodes[s1].fanout.size()), 1);
        CheckInt("LED fans out to 0 nodes", static_cast<int>(g.nodes[led].fanout.size()), 0);
        CheckInt("original id kept", g.nodes[orIdx].id, 30);
        CheckInt("type kept", g.nodes[orIdx].type, GATE_OR);
        CheckInt("no cycle", g.hasCycle ? 1 : 0, 0);
        CheckInt("eval order valid", EvalOrderIsValid(g) ? 1 : 0, 1);
    }

    // ---- Fan-out and n-ary gates -----------------------------------------
    {
        Section("Fan-out and three-input gate");

        // One source drives a NOT and two pins of a 3-input AND.
        //   S(1) -> NOT(2) -> AND(3) pin 0
        //   S(1) ------------> AND(3) pin 1
        //   S(1) ------------> AND(3) pin 2
        const std::vector<NetlistNode> nodes = {
            {1, GATE_SOURCE}, {2, GATE_NOT}, {3, GATE_AND},
        };
        const std::vector<NetlistConnection> conns = {
            {1, 3, 2}, {1, 2, 0}, {2, 3, 0}, {1, 3, 1},  // pins arrive out of order
        };

        CircuitGraph g;
        CheckStatus("parse", Parse(nodes, conns, g), NETLIST_OK);
        CheckInt("edge count", g.edgeCount, 4);
        CheckInt("source fans out 3 times", static_cast<int>(g.nodes[0].fanout.size()), 3);
        CheckInt("AND has 3 inputs", static_cast<int>(g.nodes[2].inputs.size()), 3);
        CheckInt("AND pin 0 driven by NOT", g.nodes[2].inputs[0], 1);
        CheckInt("AND pin 2 driven by source", g.nodes[2].inputs[2], 0);
        CheckInt("eval order valid", EvalOrderIsValid(g) ? 1 : 0, 1);
    }

    // ---- Edge shapes ------------------------------------------------------
    {
        Section("Edge shapes");

        CircuitGraph g;
        CheckStatus("empty netlist, null arrays", ParseNetlist(nullptr, 0, nullptr, 0, g), NETLIST_OK);
        CheckInt("empty netlist has 0 nodes", static_cast<int>(g.nodes.size()), 0);

        // A lone source is legal: placed but not wired to anything yet.
        const std::vector<NetlistNode> lone = {{7, GATE_SOURCE}};
        CheckStatus("lone source", Parse(lone, {}, g), NETLIST_OK);
        CheckInt("lone source in eval order", static_cast<int>(g.evalOrder.size()), 1);

        // Negative ids are valid ids.
        const std::vector<NetlistNode> neg = {{-5, GATE_SOURCE}, {-6, GATE_OUTPUT}};
        CheckStatus("negative ids", Parse(neg, {{-5, -6, 0}}, g), NETLIST_OK);

        // Reusing a graph clears the previous parse.
        const std::vector<NetlistNode> big = {{1, GATE_SOURCE}, {2, GATE_OUTPUT}};
        Parse(big, {{1, 2, 0}}, g);
        Parse(lone, {}, g);
        CheckInt("reparse replaces previous graph", static_cast<int>(g.nodes.size()), 1);
    }

    // ---- Unwired pins -----------------------------------------------------
    //
    // Half-built circuits parse. Unwired pins read kNoDriver, and gates start
    // at their minimum arity so the simulator always sees a full pin list.
    {
        Section("Unwired pins (allowed)");

        CircuitGraph g;
        const std::vector<NetlistNode> srcLed = {{1, GATE_SOURCE}, {2, GATE_OUTPUT}};
        CheckStatus("unwired LED parses", Parse(srcLed, {}, g), NETLIST_OK);
        CheckInt("unwired LED has 1 pin", static_cast<int>(g.nodes[1].inputs.size()), 1);
        CheckInt("unwired LED pin 0 is kNoDriver", g.nodes[1].inputs[0], kNoDriver);
        CheckInt("unwired LED is in eval order", static_cast<int>(g.evalOrder.size()), 2);

        const std::vector<NetlistNode> lonelyAnd = {{5, GATE_AND}};
        CheckStatus("unwired AND parses", Parse(lonelyAnd, {}, g), NETLIST_OK);
        CheckInt("unwired AND starts with 2 pins", static_cast<int>(g.nodes[0].inputs.size()), 2);
        CheckInt("unwired AND pin 1 is kNoDriver", g.nodes[0].inputs[1], kNoDriver);

        const std::vector<NetlistNode> srcAnd = {{1, GATE_SOURCE}, {2, GATE_AND}};
        CheckStatus("AND with only pin 1 wired", Parse(srcAnd, {{1, 2, 1}}, g), NETLIST_OK);
        CheckInt("  pin 0 is kNoDriver", g.nodes[1].inputs[0], kNoDriver);
        CheckInt("  pin 1 driven by source", g.nodes[1].inputs[1], 0);
        CheckInt("  eval order valid", EvalOrderIsValid(g) ? 1 : 0, 1);

        const std::vector<NetlistNode> gap = {{1, GATE_SOURCE}, {2, GATE_SOURCE}, {3, GATE_OR}};
        CheckStatus("OR with pins 0 and 2 wired (gap)", Parse(gap, {{1, 3, 0}, {2, 3, 2}}, g), NETLIST_OK);
        CheckInt("  grows to 3 pins", static_cast<int>(g.nodes[2].inputs.size()), 3);
        CheckInt("  pin 1 is kNoDriver", g.nodes[2].inputs[1], kNoDriver);
        CheckInt("  edge count", g.edgeCount, 2);
        CheckInt("  eval order valid", EvalOrderIsValid(g) ? 1 : 0, 1);

        // An unwired gate downstream of wired ones still orders correctly:
        // S -> NOT -> XOR pin 0, XOR pin 1 unwired, XOR -> LED.
        const std::vector<NetlistNode> partial = {
            {4, GATE_OUTPUT}, {3, GATE_XOR}, {2, GATE_NOT}, {1, GATE_SOURCE},
        };
        CheckStatus("partially wired chain", Parse(partial, {{1, 2, 0}, {2, 3, 0}, {3, 4, 0}}, g), NETLIST_OK);
        CheckInt("  no cycle", g.hasCycle ? 1 : 0, 0);
        CheckInt("  eval order valid", EvalOrderIsValid(g) ? 1 : 0, 1);
    }

    // ---- Every node type --------------------------------------------------
    {
        Section("Every node type parses with its minimum pin count");

        struct TypeCase { const char* label; int type; int pins; };
        const TypeCase cases[] = {
            {"SOURCE -> 0 pins", GATE_SOURCE, 0}, {"OUTPUT -> 1 pin", GATE_OUTPUT, 1},
            {"NOT -> 1 pin",     GATE_NOT,    1}, {"BUFFER -> 1 pin", GATE_BUFFER, 1},
            {"AND -> 2 pins",    GATE_AND,    2}, {"OR -> 2 pins",    GATE_OR,     2},
            {"NAND -> 2 pins",   GATE_NAND,   2}, {"NOR -> 2 pins",   GATE_NOR,    2},
            {"XOR -> 2 pins",    GATE_XOR,    2}, {"XNOR -> 2 pins",  GATE_XNOR,   2},
        };
        for (const TypeCase& t : cases) {
            CircuitGraph g;
            const std::vector<NetlistNode> one = {{1, t.type}};
            const int status = Parse(one, {}, g);
            const int pins = status == NETLIST_OK ? static_cast<int>(g.nodes[0].inputs.size()) : status;
            CheckInt(t.label, pins, t.pins);
        }

        // Highest valid pin on a multi-input gate.
        CircuitGraph g;
        const std::vector<NetlistNode> wide = {{1, GATE_SOURCE}, {2, GATE_NAND}};
        CheckStatus("NAND pin kMaxGateInputs-1", Parse(wide, {{1, 2, kMaxGateInputs - 1}}, g), NETLIST_OK);
        CheckInt("  NAND grows to kMaxGateInputs pins", static_cast<int>(g.nodes[1].inputs.size()), kMaxGateInputs);
    }

    // ---- Cycles -----------------------------------------------------------
    {
        Section("Cycles (reported, not rejected)");

        // Two cross-coupled NOTs: the simplest loop, and a valid netlist.
        const std::vector<NetlistNode> ring = {{1, GATE_NOT}, {2, GATE_NOT}};
        CircuitGraph g;
        CheckStatus("NOT ring parses", Parse(ring, {{1, 2, 0}, {2, 1, 0}}, g), NETLIST_OK);
        CheckInt("NOT ring hasCycle", g.hasCycle ? 1 : 0, 1);
        CheckInt("NOT ring eval order empty", static_cast<int>(g.evalOrder.size()), 0);

        // A loop with a clean section upstream: S -> OR <-> NOT, OR -> LED.
        const std::vector<NetlistNode> latch = {
            {1, GATE_SOURCE}, {2, GATE_OR}, {3, GATE_NOT}, {4, GATE_OUTPUT},
        };
        const std::vector<NetlistConnection> latchConns = {
            {1, 2, 0}, {3, 2, 1}, {2, 3, 0}, {2, 4, 0},
        };
        CheckStatus("loop with upstream source parses", Parse(latch, latchConns, g), NETLIST_OK);
        CheckInt("loop hasCycle", g.hasCycle ? 1 : 0, 1);
        CheckInt("only the source is orderable", static_cast<int>(g.evalOrder.size()), 1);

        // A NOT driving itself.
        const std::vector<NetlistNode> self = {{1, GATE_NOT}};
        CheckStatus("self-loop parses", Parse(self, {{1, 1, 0}}, g), NETLIST_OK);
        CheckInt("self-loop hasCycle", g.hasCycle ? 1 : 0, 1);
    }

    // ---- Error contract ---------------------------------------------------
    //
    // Every NetlistStatus except OUT_OF_MEMORY, which can't be triggered
    // honestly from a test. Each case is a valid netlist with exactly one
    // thing wrong, so the check proves which rule fired.
    {
        Section("Error contract");

        const std::vector<NetlistNode> srcLed = {{1, GATE_SOURCE}, {2, GATE_OUTPUT}};
        CircuitGraph g;

        CheckStatus("negative node count",
                    ParseNetlist(srcLed.data(), -1, nullptr, 0, g), NETLIST_ERR_NEGATIVE_COUNT);
        CheckStatus("negative connection count",
                    ParseNetlist(srcLed.data(), 2, nullptr, -1, g), NETLIST_ERR_NEGATIVE_COUNT);
        CheckStatus("null nodes with count 2",
                    ParseNetlist(nullptr, 2, nullptr, 0, g), NETLIST_ERR_NULL_ARRAY);
        CheckStatus("null connections with count 1",
                    ParseNetlist(srcLed.data(), 2, nullptr, 1, g), NETLIST_ERR_NULL_ARRAY);

        ExpectError("duplicate id", NETLIST_ERR_DUPLICATE_ID,
                    {{1, GATE_SOURCE}, {1, GATE_OUTPUT}}, {});
        ExpectError("type GATE_INVALID", NETLIST_ERR_UNKNOWN_TYPE,
                    {{1, GATE_SOURCE}, {2, GATE_INVALID}}, {});
        ExpectError("type 99", NETLIST_ERR_UNKNOWN_TYPE,
                    {{1, 99}}, {});
        ExpectError("connection from unknown id", NETLIST_ERR_UNKNOWN_ID,
                    srcLed, {{9, 2, 0}});
        ExpectError("connection to unknown id", NETLIST_ERR_UNKNOWN_ID,
                    srcLed, {{1, 9, 0}});
        ExpectError("wire out of an LED", NETLIST_ERR_NO_OUTPUT,
                    {{1, GATE_SOURCE}, {2, GATE_OUTPUT}, {3, GATE_OUTPUT}},
                    {{1, 2, 0}, {2, 3, 0}});
        ExpectError("wire into a source", NETLIST_ERR_PIN_OUT_OF_RANGE,
                    {{1, GATE_SOURCE}, {2, GATE_SOURCE}}, {{1, 2, 0}});
        ExpectError("NOT pin 1", NETLIST_ERR_PIN_OUT_OF_RANGE,
                    {{1, GATE_SOURCE}, {2, GATE_NOT}}, {{1, 2, 1}});
        ExpectError("LED pin -1", NETLIST_ERR_PIN_OUT_OF_RANGE,
                    srcLed, {{1, 2, -1}});
        ExpectError("AND pin kMaxGateInputs", NETLIST_ERR_PIN_OUT_OF_RANGE,
                    {{1, GATE_SOURCE}, {2, GATE_AND}}, {{1, 2, kMaxGateInputs}});
        ExpectError("two drivers on LED (TC-3.4.5)", NETLIST_ERR_MULTIPLE_DRIVERS,
                    {{1, GATE_SOURCE}, {2, GATE_SOURCE}, {3, GATE_OUTPUT}},
                    {{1, 3, 0}, {2, 3, 0}});
        ExpectError("same wire listed twice", NETLIST_ERR_MULTIPLE_DRIVERS,
                    {{1, GATE_SOURCE}, {2, GATE_SOURCE}, {3, GATE_AND}},
                    {{1, 3, 0}, {2, 3, 1}, {2, 3, 1}});
        ExpectError("BUFFER pin 1", NETLIST_ERR_PIN_OUT_OF_RANGE,
                    {{1, GATE_SOURCE}, {2, GATE_BUFFER}}, {{1, 2, 1}});
        ExpectError("XNOR pin -1", NETLIST_ERR_PIN_OUT_OF_RANGE,
                    {{1, GATE_SOURCE}, {2, GATE_XNOR}}, {{1, 2, -1}});

        // A failure after a successful parse still leaves the graph empty.
        Parse(srcLed, {{1, 2, 0}}, g);
        ExpectError("failure clears earlier graph", NETLIST_ERR_UNKNOWN_ID, srcLed, {{1, 5, 0}});
    }

    // ---- The evaluator rejects the new node types --------------------------
    {
        Section("EvaluateGate and node-only types");
        const int one[1] = {1};
        CheckInt("EvaluateGate(SOURCE)", EvaluateGate(GATE_SOURCE, one, 1), GATE_ERR_UNKNOWN_TYPE);
        CheckInt("EvaluateGate(OUTPUT)", EvaluateGate(GATE_OUTPUT, one, 1), GATE_ERR_UNKNOWN_TYPE);
    }

    // ---- Summary ----------------------------------------------------------
    std::printf("\n----------------------------------------\n");
    if (failures == 0) {
        std::printf("All %d checks passed.\n", checksRun);
        return 0;
    }

    std::printf("%d of %d checks FAILED.\n", failures, checksRun);
    return 1;
}
