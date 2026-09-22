// netlist.h — builds an evaluable circuit graph from a flat netlist (#9).
//
// Like gates.h, this is plain C++ with no Unity or DLL awareness. #12 adds the
// extern "C" export in plugin.cpp that receives the netlist from C#; #10 walks
// the resulting graph to propagate signals.
//
// The input is two flat arrays of plain structs rather than anything richer,
// because that is exactly what can cross the DllImport boundary: NetlistNode
// and NetlistConnection are standard-layout int structs, so a C# struct array
// with [StructLayout(LayoutKind.Sequential)] marshals onto them with no copy.
// Do not add pointers, std:: types, or anything non-int to these two structs.

#ifndef SILICON_NETLIST_H
#define SILICON_NETLIST_H

#include <cstddef>
#include <unordered_map>
#include <vector>

namespace silicon {

// ---- Input: the netlist as the game client sends it -----------------------

// One component. id is whatever the game uses to identify the block (a packed
// grid position, an instance ID, ...). It only has to be unique within one
// netlist; it does not have to be dense, positive, or ordered.
struct NetlistNode {
    int id;
    int type;  // a GateType from gates.h
};

// One wire run, already collapsed on the Unity side: however many wire blocks
// sit between two components, they arrive here as a single connection.
//
// A connection always goes from a node's single output to one input pin of
// another node. One output can drive many inputs (fan-out); one input pin can
// be driven by exactly one output.
struct NetlistConnection {
    int fromId;  // node whose output drives the wire
    int toId;    // node whose input the wire feeds
    int toPin;   // which input on toId, starting at 0
};

// ---- Output: the graph the engine evaluates -------------------------------

// Upper bound on input pins for the multi-input gates (AND, OR, NAND, NOR,
// XOR, XNOR). Not a gameplay
// limit so much as a sanity bound: a pin index in the millions means the
// marshaling is broken, and it should be reported rather than used to size a
// vector.
constexpr int kMaxGateInputs = 32;

// Value stored in GraphNode::inputs for a pin with nothing wired to it.
constexpr int kNoDriver = -1;

struct GraphNode {
    int id;    // the original id from the netlist
    int type;  // GateType

    // Driving node index for each input pin, in pin order. inputs[p] is the
    // index (into CircuitGraph::nodes) of the node feeding pin p, or kNoDriver
    // when the pin is not wired.
    //
    // Sized to the gate's minimum arity even when nothing is connected (1 for
    // NOT, BUFFER, OUTPUT; 2 for the multi-input gates; 0 for SOURCE), and
    // grown to the highest wired pin beyond that. So a freshly placed AND has
    // inputs == { kNoDriver, kNoDriver }, and the simulator never has to
    // special-case a gate with too few entries.
    //
    // The parser does not decide what an unwired pin reads as; that is #10's
    // call (reading it as 0 is the natural choice for a player-built circuit).
    std::vector<int> inputs;

    // Indices of every node this node's output feeds. Order follows the order
    // connections appeared in the netlist. A node feeding two pins of the same
    // gate appears twice.
    std::vector<int> fanout;
};

struct CircuitGraph {
    // Dense storage. A node's index here is its position in the input
    // NetlistNode array, which is also the index #12 will use for the array of
    // output states it hands back to C#.
    std::vector<GraphNode> nodes;

    // Original netlist id -> index into nodes.
    std::unordered_map<int, int> indexOf;

    // Number of connections (edges) in the graph.
    int edgeCount = 0;

    // Topological evaluation order for #10: every node appears after all of
    // the nodes that drive it. Only complete when hasCycle is false; when the
    // circuit has a feedback loop, the nodes on (or downstream of) the loop are
    // missing from this list.
    std::vector<int> evalOrder;

    // True when the circuit contains a feedback loop. Not a parse error: loops
    // are how latches and flip-flops work (SR-3.3), so reporting them and
    // leaving the policy to the simulator is deliberate. #10 only has to handle
    // the acyclic case.
    bool hasCycle = false;

    void Clear();
};

// ---- Status codes ---------------------------------------------------------
//
// Numbered from -10 so they can never be mistaken for a GateStatus (-1..-4)
// when #12 surfaces both kinds of failure through the same C# wrapper.
enum NetlistStatus {
    NETLIST_OK                    =   0,
    NETLIST_ERR_NULL_ARRAY        = -10,  // count > 0 but the array pointer was null
    NETLIST_ERR_NEGATIVE_COUNT    = -11,  // nodeCount or connectionCount < 0
    NETLIST_ERR_DUPLICATE_ID      = -12,  // two nodes share an id
    NETLIST_ERR_UNKNOWN_TYPE      = -13,  // node type is not a known GateType
    NETLIST_ERR_UNKNOWN_ID        = -14,  // connection refers to an id with no node
    NETLIST_ERR_NO_OUTPUT         = -15,  // connection is driven from an OUTPUT node
    NETLIST_ERR_PIN_OUT_OF_RANGE  = -16,  // toPin does not exist on the target node
    NETLIST_ERR_MULTIPLE_DRIVERS  = -17,  // two connections feed the same input pin
    NETLIST_ERR_OUT_OF_MEMORY     = -18   // allocation failed while building the graph
};

// Parse a netlist into a circuit graph.
//
//   nodes             array of nodeCount components (may be null if nodeCount is 0)
//   connections       array of connectionCount wires (may be null if 0)
//   out               receives the graph; always cleared first
//
// Valid pins per type:
//   SOURCE                               none
//   NOT, BUFFER, OUTPUT                  pin 0
//   AND, OR, NAND, NOR, XOR, XNOR        pins 0 .. kMaxGateInputs-1
//
// Unwired pins are allowed: players build circuits a block at a time, and a
// half-built circuit still has to parse. They show up as kNoDriver in
// GraphNode::inputs (see above). Wiring a pin that doesn't exist, or wiring
// one pin twice, is still an error.
//
// Returns NETLIST_OK, or a negative NetlistStatus. On failure out is left
// empty. Never throws.
int ParseNetlist(const NetlistNode* nodes, int nodeCount,
                 const NetlistConnection* connections, int connectionCount,
                 CircuitGraph& out);

}  // namespace silicon

#endif  // SILICON_NETLIST_H
