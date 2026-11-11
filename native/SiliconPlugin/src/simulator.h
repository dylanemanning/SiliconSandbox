// simulator.h — evaluates a parsed circuit graph (#12) and steps it in time (#10).
//
// Like gates.h and netlist.h, this is plain C++ with no Unity or DLL awareness.
// plugin.cpp's exports are thin wrappers over the two functions here.
//
// Two ways to run a circuit:
//
//   EvaluateCircuit  stateless, zero-delay, one pass in topological order (#12).
//                    Exact for combinational circuits; a feedback loop reads 0.
//                    Kept for the parse-and-evaluate export and its tests.
//
//   TickCircuit      one step of a unit-delay simulation (#10). This is what the
//                    game runs. Every gate has a propagation delay of exactly one
//                    tick: its output at tick t+1 is computed from its inputs at
//                    tick t. That is the same model as a Verilog/SystemVerilog
//                    gate-level simulation with #1 on every gate primitive (and
//                    the same as Minecraft redstone), so the game shows what real
//                    logic does: signals ripple through a circuit one gate per
//                    tick, unequal paths produce glitches, a ring of inverters
//                    oscillates, latches hold state, and a clock that is faster
//                    than the circuit's longest path breaks it.
//
// Sources and LEDs have no delay. A source is the player's switch; an LED is
// just a lamp on the net its driver feeds, so it shows that net's value on the
// same tick the driving gate changes. Wires are already collapsed into
// connections by the Unity side and have no delay either.

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
    SIM_ERR_INTERNAL    = -22,  // an unexpected C++ exception was caught at the
                                // export boundary
    SIM_ERR_NOT_LOADED  = -23,  // Silicon_Tick was called with no circuit loaded
                                // (never loaded, unloaded, or the last load failed)
    SIM_ERR_NODE_COUNT  = -24   // Silicon_Tick's nodeCount does not match the
                                // loaded circuit. states is not touched: its real
                                // length is unknown, so writing to it is unsafe
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

// Advance a unit-delay simulation by one tick.
//
//   graph   a graph ParseNetlist returned NETLIST_OK for (loops are fine)
//   states  one int per graph node, indexed like graph.nodes: the value of
//           every node at the current tick. The caller owns it and keeps it
//           between ticks; it IS the circuit's state. Before each call the
//           caller writes the current value of every SOURCE into it (any
//           non-zero value counts as 1).
//   next    scratch space for graph.nodes.size() ints. Contents on entry are
//           ignored. Needed because every gate must read the old values, not
//           ones already updated this tick.
//
// For every node, the value at the next tick is:
//   SOURCE  its own entry, normalised to 0/1 (the player's switch, no delay)
//   gate    the gate applied to its drivers' CURRENT values (one tick of delay).
//           An unwired input pin reads 0
//   OUTPUT  its driver's NEXT value (an LED has no delay), or 0 when unwired
//
// On return states holds the next tick's values, every entry 0 or 1.
//
// Returns the number of nodes whose value changed (>= 0). Zero means the
// circuit has settled: with the sources unchanged, every later tick would also
// change nothing, so the caller can stop ticking until a source or the circuit
// changes. A circuit that oscillates (a NOT wired to itself, a ring of
// inverters, an SR latch released from S = R = 1) never returns 0, exactly as
// it would in a real gate-level simulator.
//
// Returns SIM_ERR_NULL_STATES if the graph has nodes and either array is null,
// or SIM_ERR_GATE (with states zeroed) if a gate is rejected, which is a bug.
// Never throws, never allocates.
int TickCircuit(const CircuitGraph& graph, int* states, int* next);

}  // namespace silicon

#endif  // SILICON_SIMULATOR_H
