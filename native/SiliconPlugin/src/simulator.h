// simulator.h — evaluates a parsed circuit graph in one pass (#12).
//
// Like gates.h and netlist.h, this is plain C++ with no Unity or DLL awareness.
// plugin.cpp's Silicon_EvaluateCircuit export is a thin wrapper that parses the
// netlist and then calls EvaluateCircuit.
//
// This is the STATELESS demo version: every call computes every node from the
// current source values, in the graph's topological order. That is exactly
// right for combinational circuits, which is the demo scope. Feedback loops
// (latches, flip-flops) and the clock need state carried between calls, which
// is what #10's Silicon_LoadCircuit + Silicon_Tick split will add after the
// demo. EvaluateCircuit is written so that split can reuse it as the body of a
// tick.

#ifndef SILICON_SIMULATOR_H
#define SILICON_SIMULATOR_H

#include "netlist.h"

namespace silicon {

// Status codes. 0 and the positive value are successes; negatives are failures.
//
// Numbered from -20 so they never overlap GateStatus (-1..-4) or NetlistStatus
// (-10..-18): Silicon_EvaluateCircuit can return any of the three kinds, and
// the C# wrapper tells them apart by value.
enum SimulateStatus {
    SIM_OK              =   0,  // every node evaluated
    SIM_HAS_CYCLE       =   1,  // evaluated, but the circuit has a feedback loop:
                                // nodes on the loop or downstream of it read 0
    SIM_ERR_NULL_STATES = -20,  // nodeCount > 0 but the states array was null
    SIM_ERR_GATE        = -21,  // EvaluateGate rejected a node the parser accepted.
                                // Means gates.cpp and netlist.cpp disagree about
                                // a gate's arity: a bug, never a player mistake
    SIM_ERR_INTERNAL    = -22   // an unexpected C++ exception was caught at the
                                // export boundary
};

// Evaluate every node of a parsed graph.
//
//   graph   a graph ParseNetlist returned NETLIST_OK for
//   states  one int per graph node, indexed the same way as graph.nodes (which
//           is the order of the NetlistNode array that was parsed)
//
// On entry, only the SOURCE entries of states are read: each is the value the
// player set on that source, and any non-zero value counts as 1. Every other
// entry is ignored and overwritten, so the caller does not need to clear the
// array between calls.
//
// On return, every entry is 0 or 1: sources hold their normalised value, gates
// their output, and OUTPUT nodes (LEDs) the value on their input. An unwired
// input pin reads 0. On SIM_HAS_CYCLE the nodes the topological order could
// not reach are left at 0. On a negative return every entry is 0.
//
// Never throws, never allocates.
int EvaluateCircuit(const CircuitGraph& graph, int* states);

}  // namespace silicon

#endif  // SILICON_SIMULATOR_H
