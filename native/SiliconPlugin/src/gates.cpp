// gates.cpp — implementation of the gate evaluator declared in gates.h.

#include "gates.h"

namespace silicon {

int EvaluateGate(int gateType, const int* inputs, int inputCount) {
    // 1. Type first. Nothing else can be checked meaningfully until we know
    //    which gate we are dealing with, since arity depends on it.
    if (gateType != GATE_AND && gateType != GATE_OR && gateType != GATE_NOT) {
        return GATE_ERR_UNKNOWN_TYPE;
    }

    // 2. Arity. NOT is the only single-input gate; AND and OR fold over two or
    //    more. Wrong arity is rejected rather than tolerated: a NOT with two
    //    incoming wires is a real mistake a player can make while building, and
    //    silently ignoring the second input would return a plausible wrong
    //    answer that only surfaces later as an unreproducible propagation bug.
    if (gateType == GATE_NOT) {
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

    // 4. Evaluate.
    switch (gateType) {
        case GATE_NOT:
            return inputs[0] == 0 ? 1 : 0;

        case GATE_AND:
            // One low input settles it.
            for (int i = 0; i < inputCount; i++) {
                if (inputs[i] == 0) {
                    return 0;
                }
            }
            return 1;

        case GATE_OR:
            // One high input settles it.
            for (int i = 0; i < inputCount; i++) {
                if (inputs[i] == 1) {
                    return 1;
                }
            }
            return 0;
    }

    // Unreachable given the check in step 1, but keeps every path returning a
    // value and keeps the compiler quiet.
    return GATE_ERR_UNKNOWN_TYPE;
}

}  // namespace silicon
