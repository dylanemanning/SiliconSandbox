// netlist.cpp — implementation of the netlist parser declared in netlist.h.

#include "netlist.h"

#include "gates.h"

#include <new>
#include <utility>

namespace silicon {

namespace {

// Minimum input pins a node of this type has, or -1 for an unknown type.
// This is also the size GraphNode::inputs starts at.
int MinInputs(int type) {
    switch (type) {
        case GATE_SOURCE:
            return 0;
        case GATE_NOT:
        case GATE_BUFFER:
        case GATE_OUTPUT:
            return 1;
        case GATE_AND:
        case GATE_OR:
        case GATE_NAND:
        case GATE_NOR:
        case GATE_XOR:
        case GATE_XNOR:
            return 2;
        default:
            return -1;
    }
}

// Highest number of input pins a node of this type can have.
int MaxInputs(int type) {
    const int min = MinInputs(type);
    return min == 2 ? kMaxGateInputs : min;
}

// The body of ParseNetlist, split out so the public function can own the
// try/catch and the clear-on-failure guarantee in one place.
int Build(const NetlistNode* nodes, int nodeCount,
          const NetlistConnection* connections, int connectionCount,
          CircuitGraph& g) {
    // 1. The arrays themselves. Mirrors EvaluateGate: a count/pointer mismatch
    //    is a marshaling bug, so it is checked before anything is read.
    if (nodeCount < 0 || connectionCount < 0) {
        return NETLIST_ERR_NEGATIVE_COUNT;
    }
    if ((nodeCount > 0 && nodes == nullptr) ||
        (connectionCount > 0 && connections == nullptr)) {
        return NETLIST_ERR_NULL_ARRAY;
    }

    // 2. Nodes. Assign dense indices in array order and validate each node
    //    before any connection is looked at, so a connection error is never
    //    reported for what is really a bad node.
    g.nodes.reserve(static_cast<std::size_t>(nodeCount));
    g.indexOf.reserve(static_cast<std::size_t>(nodeCount));

    for (int i = 0; i < nodeCount; i++) {
        const NetlistNode& n = nodes[i];

        const int minInputs = MinInputs(n.type);
        if (minInputs < 0) {
            return NETLIST_ERR_UNKNOWN_TYPE;
        }
        if (!g.indexOf.emplace(n.id, i).second) {
            return NETLIST_ERR_DUPLICATE_ID;
        }

        GraphNode node;
        node.id = n.id;
        node.type = n.type;
        // Every pin starts unwired. Multi-input gates grow past their minimum
        // as higher pins are connected.
        node.inputs.assign(static_cast<std::size_t>(minInputs), kNoDriver);
        g.nodes.push_back(std::move(node));
    }

    // 3. Connections. Each one is checked in the order it appears, and only the
    //    driver side of each pin is recorded here. Fan-out lists are built in a
    //    separate pass once every pin is known to be valid.
    for (int c = 0; c < connectionCount; c++) {
        const NetlistConnection& conn = connections[c];

        const auto from = g.indexOf.find(conn.fromId);
        const auto to = g.indexOf.find(conn.toId);
        if (from == g.indexOf.end() || to == g.indexOf.end()) {
            return NETLIST_ERR_UNKNOWN_ID;
        }

        // An LED has nothing to drive with. Checked before the pin so that
        // "wired out of an LED" is reported as that, not as a pin problem on
        // whatever the wire runs into.
        if (g.nodes[from->second].type == GATE_OUTPUT) {
            return NETLIST_ERR_NO_OUTPUT;
        }

        GraphNode& target = g.nodes[to->second];
        const int pin = conn.toPin;

        // Sources have no pins, single-input nodes only pin 0, multi-input
        // gates up to kMaxGateInputs.
        if (pin < 0 || pin >= MaxInputs(target.type)) {
            return NETLIST_ERR_PIN_OUT_OF_RANGE;
        }
        if (pin >= static_cast<int>(target.inputs.size())) {
            target.inputs.resize(static_cast<std::size_t>(pin) + 1, kNoDriver);
        }

        // One driver per input pin. Two outputs on one wire is a short circuit
        // in real hardware and an error here (TC-3.4.5).
        if (target.inputs[pin] != kNoDriver) {
            return NETLIST_ERR_MULTIPLE_DRIVERS;
        }
        target.inputs[pin] = from->second;
    }

    // 4. Fan-out, in connection order. Safe now that every connection passed.
    for (int c = 0; c < connectionCount; c++) {
        const int fromIndex = g.indexOf[connections[c].fromId];
        const int toIndex = g.indexOf[connections[c].toId];
        g.nodes[fromIndex].fanout.push_back(toIndex);
    }
    g.edgeCount = connectionCount;

    // 5. Evaluation order (Kahn's algorithm). A node is ready once every wired
    //    input pin's driver has been placed; unwired pins don't wait on
    //    anything, so a gate with nothing connected is ready immediately. Seeding and processing in index order
    //    keeps the result deterministic for a given netlist, which keeps test
    //    output and bug reports reproducible.
    //
    //    Counting per pin rather than per distinct driver matters: a node that
    //    drives two pins of the same gate appears twice in its fan-out, and is
    //    decremented twice, so the counts stay consistent.
    std::vector<int> remaining(g.nodes.size());
    g.evalOrder.reserve(g.nodes.size());

    for (std::size_t i = 0; i < g.nodes.size(); i++) {
        remaining[i] = 0;
        for (int driver : g.nodes[i].inputs) {
            if (driver != kNoDriver) {
                remaining[i]++;
            }
        }
        if (remaining[i] == 0) {
            g.evalOrder.push_back(static_cast<int>(i));
        }
    }

    // evalOrder doubles as the queue: head walks forward while ready nodes are
    // appended behind it.
    for (std::size_t head = 0; head < g.evalOrder.size(); head++) {
        const GraphNode& node = g.nodes[g.evalOrder[head]];
        for (int next : node.fanout) {
            if (--remaining[next] == 0) {
                g.evalOrder.push_back(next);
            }
        }
    }

    // Anything never reached is on a loop or downstream of one.
    g.hasCycle = g.evalOrder.size() != g.nodes.size();

    return NETLIST_OK;
}

}  // namespace

void CircuitGraph::Clear() {
    nodes.clear();
    indexOf.clear();
    edgeCount = 0;
    evalOrder.clear();
    hasCycle = false;
}

int ParseNetlist(const NetlistNode* nodes, int nodeCount,
                 const NetlistConnection* connections, int connectionCount,
                 CircuitGraph& out) {
    out.Clear();

    // #12 calls this from an extern "C" export, and an exception unwinding
    // across that boundary crashes the Unity editor. The only thing in here
    // that can throw is allocation, so that is caught and reported.
    int status;
    try {
        status = Build(nodes, nodeCount, connections, connectionCount, out);
    } catch (const std::bad_alloc&) {
        status = NETLIST_ERR_OUT_OF_MEMORY;
    }

    if (status != NETLIST_OK) {
        out.Clear();
    }
    return status;
}

}  // namespace silicon
