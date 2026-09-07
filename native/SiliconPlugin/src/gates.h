// gates.h — pure logic-gate evaluation for the Silicon Sandbox logic engine.
//
// This header is deliberately free of Unity and DLL concerns. Everything that
// crosses the managed/native boundary lives in plugin.cpp; this file is plain
// C++ that compiles and tests on its own (see tests/truth_table.cpp).

#ifndef SILICON_GATES_H
#define SILICON_GATES_H

namespace silicon {

// Gate type codes.
//
// These values are mirrored by the C# GateType enum in
// Assets/Scripts/LogicEngine/LogicEngine.cs, and will eventually be written into
// saved netlists (#18), which makes them part of the on-disk format:
// APPEND new gate types, never renumber existing ones.
//
// Zero is reserved as invalid on purpose. A zero-initialized struct, or a field
// that failed to deserialize, would otherwise read as a valid AND gate and
// produce plausible-but-wrong output instead of an error.
enum GateType {
    GATE_INVALID = 0,
    GATE_AND     = 1,
    GATE_OR      = 2,
    GATE_NOT     = 3
};

// Failure codes returned by EvaluateGate.
//
// Valid results are 0 and 1. The output domain is boolean and always will be,
// so negative values can never collide with a real result: any negative return
// is unambiguously an error.
enum GateStatus {
    GATE_ERR_UNKNOWN_TYPE = -1,  // gateType is not a recognized GateType
    GATE_ERR_INPUT_COUNT  = -2,  // wrong number of inputs for this gate
    GATE_ERR_NULL_INPUTS  = -3,  // inputs pointer was null
    GATE_ERR_INPUT_VALUE  = -4   // an input was something other than 0 or 1
};

// Evaluate a single logic gate.
//
//   gateType    one of GateType
//   inputs      array of input signals, each strictly 0 or 1
//   inputCount  number of elements in inputs
//
// Arity: NOT takes exactly one input. AND and OR take two or more and fold
// across all of them (AND = every input high, OR = any input high).
//
// Returns 0 or 1 on success, or a negative GateStatus on failure.
int EvaluateGate(int gateType, const int* inputs, int inputCount);

}  // namespace silicon

#endif  // SILICON_GATES_H
