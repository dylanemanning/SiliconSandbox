// simulator.cpp — implementation of EvaluateCircuit (#12) and TickCircuit (#10), declared in simulator.h.

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

int TickCircuit(const CircuitGraph& graph, int* states, int* next) {
    const int nodeCount = static_cast<int>(graph.nodes.size());
    if (nodeCount > 0 && (states == nullptr || next == nullptr)) {
        return SIM_ERR_NULL_STATES;
    }

    int inputs[kMaxGateInputs];

    // 1. Sources and gates. Every read is from states (this tick) and every
    //    write is to next (the next tick), so the order nodes are visited in
    //    makes no difference: no gate ever sees another gate's new value. That
    //    is what gives every gate exactly one tick of delay, and what lets a
    //    feedback loop work without any special handling.
    for (int i = 0; i < nodeCount; i++) {
        const GraphNode& node = graph.nodes[i];
        if (node.type == GATE_SOURCE) {
            next[i] = states[i] != 0 ? 1 : 0;
            continue;
        }
        if (node.type == GATE_OUTPUT) {
            continue;  // step 2, once its driver's next value is known
        }

        const int inputCount = static_cast<int>(node.inputs.size());
        if (inputCount > kMaxGateInputs) {
            ClearAll(states, nodeCount);
            return SIM_ERR_GATE;
        }
        for (int pin = 0; pin < inputCount; pin++) {
            const int driver = node.inputs[pin];
            inputs[pin] = driver == kNoDriver ? 0 : (states[driver] != 0 ? 1 : 0);
        }

        const int result = EvaluateGate(node.type, inputs, inputCount);
        if (result < 0) {
            ClearAll(states, nodeCount);
            return SIM_ERR_GATE;
        }
        next[i] = result;
    }

    // 2. LEDs show the net they sit on, with no delay of their own. The parser
    //    rejects wires out of an OUTPUT, so a driver is always a source or a
    //    gate and its next value was set in step 1.
    for (int i = 0; i < nodeCount; i++) {
        const GraphNode& node = graph.nodes[i];
        if (node.type == GATE_OUTPUT) {
            const int driver = node.inputs[0];
            next[i] = driver == kNoDriver ? 0 : next[driver];
        }
    }

    // 3. Commit, counting what changed.
    int changed = 0;
    for (int i = 0; i < nodeCount; i++) {
        if (states[i] != next[i]) {
            changed++;
            states[i] = next[i];
        }
    }
    return changed;
}

}  // namespace silicon
