// gates.cpp — implementation of the gate evaluator declared in gates.h.

#include "gates.h"

namespace silicon {

namespace {

bool IsEvaluable(int gateType) {
    switch (gateType) {
        case GATE_AND:
        case GATE_OR:
        case GATE_NOT:
        case GATE_NAND:
        case GATE_NOR:
        case GATE_XOR:
        case GATE_XNOR:
        case GATE_BUFFER:
            return true;
        default:
            // Includes GATE_SOURCE and GATE_OUTPUT: netlist nodes, not gates.
            return false;
    }
}

bool IsSingleInputGate(int gateType) {
    return gateType == GATE_NOT || gateType == GATE_BUFFER;
}

}  // namespace

int EvaluateGate(int gateType, const int* inputs, int inputCount) {
    // 1. Type first. Nothing else can be checked meaningfully until we know
    //    which gate we are dealing with, since arity depends on it.
    if (!IsEvaluable(gateType)) {
        return GATE_ERR_UNKNOWN_TYPE;
    }

    // 2. Arity. NOT and BUFFER are the single-input gates; everything else
    //    folds over two or more. Wrong arity is rejected rather than tolerated:
    //    a NOT with two incoming wires is a real mistake a player can make while
    //    building, and silently ignoring the second input would return a
    //    plausible wrong answer that only surfaces later as an unreproducible
    //    propagation bug.
    if (IsSingleInputGate(gateType)) {
        if (inputCount != 1) {
            return GATE_ERR_INPUT_COUNT;
        }
    } else if (inputCount < 2) {
        return GATE_ERR_INPUT_COUNT;
    }

    // 3. The array itself. Every element is validated before any evaluation
    //    happens, so a corrupted value is reported even in cases where the
    //    short-circuit logic below would never have read it. That is the point:
    //    an input outside {0, 1} almost always means the P/Invoke marshaling is
    //    wrong, not that the circuit is wrong, and that is exactly the class of
    //    bug worth catching loudly at the boundary.
    if (inputs == nullptr) {
        return GATE_ERR_NULL_INPUTS;
    }
    for (int i = 0; i < inputCount; i++) {
        if (inputs[i] != 0 && inputs[i] != 1) {
            return GATE_ERR_INPUT_VALUE;
        }
    }

    // 4. Count high inputs once. Every multi-input gate is a function of that
    //    count alone, which keeps the eight gates to one loop and one switch,
    //    and makes each gate's definition read the same as its truth table.
    int high = 0;
    for (int i = 0; i < inputCount; i++) {
        high += inputs[i];
    }

    // 5. Evaluate.
    switch (gateType) {
        case GATE_BUFFER: return high;
        case GATE_NOT:    return high == 0 ? 1 : 0;

        case GATE_AND:    return high == inputCount ? 1 : 0;
        case GATE_NAND:   return high == inputCount ? 0 : 1;

        case GATE_OR:     return high > 0 ? 1 : 0;
        case GATE_NOR:    return high > 0 ? 0 : 1;

        case GATE_XOR:    return high % 2;
        case GATE_XNOR:   return 1 - (high % 2);
    }

    // Unreachable given the check in step 1, but keeps every path returning a
    // value and keeps the compiler quiet.
    return GATE_ERR_UNKNOWN_TYPE;
}

}  // namespace silicon
