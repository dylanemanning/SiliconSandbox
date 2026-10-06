// simulator.cpp — implementation of the one-pass evaluator declared in simulator.h.

#include "simulator.h"

#include "gates.h"

namespace silicon {

namespace {

void ClearAll(int* states, int count) {
    for (int i = 0; i < count; i++) {
        states[i] = 0;
    }
}

}  // namespace

int EvaluateCircuit(const CircuitGraph& graph, int* states) {
    const int nodeCount = static_cast<int>(graph.nodes.size());
    if (nodeCount > 0 && states == nullptr) {
        return SIM_ERR_NULL_STATES;
    }

    // 1. Normalise sources and clear everything else. Clearing first is what
    //    leaves nodes on a feedback loop at 0: evalOrder never reaches them, so
    //    nothing below writes to them.
    for (int i = 0; i < nodeCount; i++) {
        if (graph.nodes[i].type == GATE_SOURCE) {
            states[i] = states[i] != 0 ? 1 : 0;
        } else {
            states[i] = 0;
        }
    }

    // 2. Walk the topological order. Every node's drivers come before it, so
    //    each input read here is already final.
    //
    //    The parser caps input pins at kMaxGateInputs, so a fixed buffer is
    //    enough and the evaluator never allocates.
    int inputs[kMaxGateInputs];

    for (int index : graph.evalOrder) {
        const GraphNode& node = graph.nodes[index];
        if (node.type == GATE_SOURCE) {
            continue;  // already set in step 1
        }

        const int inputCount = static_cast<int>(node.inputs.size());
        if (inputCount > kMaxGateInputs) {
            ClearAll(states, nodeCount);
            return SIM_ERR_GATE;
        }

        for (int pin = 0; pin < inputCount; pin++) {
            const int driver = node.inputs[pin];
            // Unwired pins read low. A player-built circuit is half finished
            // most of the time, and low is what a floating input should show.
            inputs[pin] = driver == kNoDriver ? 0 : states[driver];
        }

        if (node.type == GATE_OUTPUT) {
            // An LED shows its single input. MinInputs gives OUTPUT exactly
            // one pin, so inputs[0] always exists.
            states[index] = inputs[0];
            continue;
        }

        const int result = EvaluateGate(node.type, inputs, inputCount);
        if (result < 0) {
            ClearAll(states, nodeCount);
            return SIM_ERR_GATE;
        }
        states[index] = result;
    }

    return graph.hasCycle ? SIM_HAS_CYCLE : SIM_OK;
}

}  // namespace silicon
