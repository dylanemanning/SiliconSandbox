// truth_table.cpp — standalone verification for the gate evaluator (#7).
//
// Builds and runs without Unity, without the DLL, and without the Unity Test
// Framework. This is deliberately the fast loop: the logic gets proven here
// before plugin.cpp and the P/Invoke boundary exist, so a failure at this stage
// is always a logic bug and never a marshaling bug.
//
// Build and run from native/SiliconPlugin/:
//     g++ -Wall -Wextra -o truth_table.exe tests/truth_table.cpp src/gates.cpp
//     ./truth_table.exe
//
// Every check prints a PASS or FAIL line so the run doubles as a readable
// record of what is covered. Exits 0 when every case passes and 1 otherwise,
// so it satisfies the "logic engine unit tests pass locally" line on the PR
// checklist as a single command.
//
// Scope: this covers gate evaluation only. The multi-gate circuit cases and the
// Unity Test Framework suite belong to #11.

#include "../src/gates.h"

#include <cstdio>

using namespace silicon;

namespace {

int checksRun = 0;
int failures = 0;

// Renders the symbolic name of a negative return code, or an empty string for
// the ordinary 0/1 results. Returning string literals rather than filling a
// shared buffer means two calls can safely appear in one printf.
const char* ErrName(int value) {
    switch (value) {
        case GATE_ERR_UNKNOWN_TYPE: return " (ERR_UNKNOWN_TYPE)";
        case GATE_ERR_INPUT_COUNT:  return " (ERR_INPUT_COUNT)";
        case GATE_ERR_NULL_INPUTS:  return " (ERR_NULL_INPUTS)";
        case GATE_ERR_INPUT_VALUE:  return " (ERR_INPUT_VALUE)";
        default:                    return "";
    }
}

void Section(const char* title) {
    std::printf("\n%s\n", title);
}

// Runs one EvaluateGate call, compares it against an expected result, and
// prints the outcome either way.
//
// Failures are counted rather than aborted on, so a single run reports every
// broken case instead of stopping at the first. That is also why this is a
// plain function and not <cassert>: assert aborts immediately, and it compiles
// out entirely under NDEBUG, which would silently turn this whole file into a
// program that always succeeds.
void Check(const char* label, int expected, int gateType,
           const int* inputs, int inputCount) {
    checksRun++;
    const int actual = EvaluateGate(gateType, inputs, inputCount);

    if (actual == expected) {
        std::printf("  PASS  %-18s -> %3d%s\n",
                    label, actual, ErrName(actual));
    } else {
        std::printf("  FAIL  %-18s -> %3d%s   (expected %d%s)\n",
                    label, actual, ErrName(actual), expected, ErrName(expected));
        failures++;
    }
}

}  // namespace

int main() {
    std::printf("Gate evaluator verification — issue #7\n");

    // ---- AND and OR: exhaustive over every two-input combination ----------
    //
    // The expected values are written out as literal truth tables rather than
    // computed with && and ||. Deriving them from another expression risks
    // sharing a bug with the implementation; writing them out means the test
    // encodes the specification instead.
    //
    // Index i runs 0..3 and enumerates (a, b) as i >> 1 and i & 1.
    //
    //                            (0,0) (0,1) (1,0) (1,1)
    const int expectedAnd[4] = {      0,    0,    0,    1 };
    const int expectedOr[4]  = {      0,    1,    1,    1 };

    const char* andLabels[4] = { "AND(0,0)", "AND(0,1)", "AND(1,0)", "AND(1,1)" };
    const char* orLabels[4]  = { "OR(0,0)",  "OR(0,1)",  "OR(1,0)",  "OR(1,1)"  };

    Section("AND — exhaustive, two inputs");
    for (int i = 0; i < 4; i++) {
        const int inputs[2] = { i >> 1, i & 1 };
        Check(andLabels[i], expectedAnd[i], GATE_AND, inputs, 2);
    }

    Section("OR — exhaustive, two inputs");
    for (int i = 0; i < 4; i++) {
        const int inputs[2] = { i >> 1, i & 1 };
        Check(orLabels[i], expectedOr[i], GATE_OR, inputs, 2);
    }

    // ---- NOT: exhaustive over its single input ----------------------------
    const int expectedNot[2] = { 1, 0 };
    const char* notLabels[2] = { "NOT(0)", "NOT(1)" };

    Section("NOT — exhaustive, one input");
    for (int i = 0; i < 2; i++) {
        const int inputs[1] = { i };
        Check(notLabels[i], expectedNot[i], GATE_NOT, inputs, 1);
    }

    // ---- n-ary fold spot checks -------------------------------------------
    //
    // Not exhaustive — the issue's completion condition is about the two-input
    // truth tables — but enough to catch a fold that only ever reads the first
    // two elements. In both mixed cases the deciding input is placed LAST on
    // purpose, so a loop that stops early fails here.
    const int and3AllHigh[3] = { 1, 1, 1 };
    const int and3LastLow[3] = { 1, 1, 0 };
    const int or3AllLow[3]   = { 0, 0, 0 };
    const int or3LastHigh[3] = { 0, 0, 1 };

    Section("n-ary fold — three inputs, deciding input last");
    Check("AND(1,1,1)", 1, GATE_AND, and3AllHigh, 3);
    Check("AND(1,1,0)", 0, GATE_AND, and3LastLow, 3);
    Check("OR(0,0,0)",  0, GATE_OR,  or3AllLow,   3);
    Check("OR(0,0,1)",  1, GATE_OR,  or3LastHigh, 3);

    // ---- Error contract ---------------------------------------------------
    //
    // The negative return codes are part of the interface #9, #10 and #12 will
    // branch on, so they are verified as deliberately as the truth tables.
    const int pair[2] = { 1, 0 };

    Section("Error contract");
    Check("unknown type 99",  GATE_ERR_UNKNOWN_TYPE, 99,           pair, 2);
    Check("GATE_INVALID",     GATE_ERR_UNKNOWN_TYPE, GATE_INVALID, pair, 2);
    Check("NOT arity 2",      GATE_ERR_INPUT_COUNT,  GATE_NOT,     pair, 2);
    Check("AND arity 1",      GATE_ERR_INPUT_COUNT,  GATE_AND,     pair, 1);
    Check("OR arity 0",       GATE_ERR_INPUT_COUNT,  GATE_OR,      pair, 0);
    Check("AND null inputs",  GATE_ERR_NULL_INPUTS,  GATE_AND,     nullptr, 2);

    // The out-of-range value sits at index 1 while index 0 is low, so a
    // short-circuiting AND would return 0 without ever reading the 7. Expecting
    // GATE_ERR_INPUT_VALUE here is what pins down the ordering in gates.cpp:
    // the whole array is validated before any evaluation happens.
    const int outOfRange[2] = { 0, 7 };
    Check("AND value 7",      GATE_ERR_INPUT_VALUE,  GATE_AND,     outOfRange, 2);

    // ---- Summary ----------------------------------------------------------
    std::printf("\n----------------------------------------\n");
    if (failures == 0) {
        std::printf("All %d checks passed.\n", checksRun);
        return 0;
    }

    std::printf("%d of %d checks FAILED.\n", failures, checksRun);
    return 1;
}
