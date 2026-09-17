// truth_table.cpp — standalone verification for the gate evaluator (#7, plus
// the NAND/NOR/XOR/XNOR/BUFFER gates added alongside #9).
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
        std::printf("  PASS  %-24s -> %3d%s\n",
                    label, actual, ErrName(actual));
    } else {
        std::printf("  FAIL  %-24s -> %3d%s   (expected %d%s)\n",
                    label, actual, ErrName(actual), expected, ErrName(expected));
        failures++;
    }
}

}  // namespace

int main() {
    std::printf("Gate evaluator verification — issue #7\n");

    // ---- Two-input gates: exhaustive over every combination ---------------
    //
    // The expected values are written out as literal truth tables rather than
    // computed with && and ||. Deriving them from another expression risks
    // sharing a bug with the implementation; writing them out means the test
    // encodes the specification instead.
    //
    // Index i runs 0..3 and enumerates (a, b) as i >> 1 and i & 1.
    struct TwoInputTable {
        const char* name;
        int type;
        int expected[4];  // (0,0) (0,1) (1,0) (1,1)
    };

    const TwoInputTable twoInput[] = {
        { "AND",  GATE_AND,  { 0, 0, 0, 1 } },
        { "OR",   GATE_OR,   { 0, 1, 1, 1 } },
        { "NAND", GATE_NAND, { 1, 1, 1, 0 } },
        { "NOR",  GATE_NOR,  { 1, 0, 0, 0 } },
        { "XOR",  GATE_XOR,  { 0, 1, 1, 0 } },
        { "XNOR", GATE_XNOR, { 1, 0, 0, 1 } },
    };

    for (const TwoInputTable& t : twoInput) {
        char title[48];
        std::snprintf(title, sizeof title, "%s — exhaustive, two inputs", t.name);
        Section(title);
        for (int i = 0; i < 4; i++) {
            const int inputs[2] = { i >> 1, i & 1 };
            char label[24];
            std::snprintf(label, sizeof label, "%s(%d,%d)", t.name, inputs[0], inputs[1]);
            Check(label, t.expected[i], t.type, inputs, 2);
        }
    }

    // ---- Single-input gates: exhaustive -----------------------------------
    Section("NOT — exhaustive, one input");
    {
        const int zero[1] = { 0 };
        const int one[1]  = { 1 };
        Check("NOT(0)",    1, GATE_NOT,    zero, 1);
        Check("NOT(1)",    0, GATE_NOT,    one,  1);

        Section("BUFFER — exhaustive, one input");
        Check("BUFFER(0)", 0, GATE_BUFFER, zero, 1);
        Check("BUFFER(1)", 1, GATE_BUFFER, one,  1);
    }

    // ---- TC-3.2.9: basic gate + NOT = inverted basic gate ------------------
    //
    // Checks the composed result against the evaluator itself, so it proves
    // NAND/NOR/XNOR agree with NOT applied to AND/OR/XOR on every input.
    Section("TC-3.2.9 — NOT(gate) equals inverted gate, all two-input cases");
    {
        const int pairs[3][2] = {
            { GATE_AND, GATE_NAND }, { GATE_OR, GATE_NOR }, { GATE_XOR, GATE_XNOR },
        };
        const char* names[3] = { "NOT(AND) == NAND", "NOT(OR) == NOR", "NOT(XOR) == XNOR" };
        for (int p = 0; p < 3; p++) {
            for (int i = 0; i < 4; i++) {
                const int inputs[2] = { i >> 1, i & 1 };
                const int basic[1] = { EvaluateGate(pairs[p][0], inputs, 2) };
                char label[32];
                std::snprintf(label, sizeof label, "%s (%d,%d)", names[p], inputs[0], inputs[1]);
                Check(label, EvaluateGate(pairs[p][1], inputs, 2), GATE_NOT, basic, 1);
            }
        }
    }

    // ---- n-ary fold spot checks -------------------------------------------
    //
    // Not exhaustive, but enough to catch a fold that only ever reads the first
    // two elements. In the mixed cases the deciding input is placed LAST on
    // purpose, so a loop that stops early fails here. XOR/XNOR use three highs:
    // parity says XOR(1,1,1) = 1, which a gate chained pairwise from the left
    // also gives, but "exactly one high" (a common wrong reading) gives 0.
    const int all3High[3]  = { 1, 1, 1 };
    const int last3Low[3]  = { 1, 1, 0 };
    const int all3Low[3]   = { 0, 0, 0 };
    const int last3High[3] = { 0, 0, 1 };
    const int four1High[4] = { 0, 1, 1, 1 };

    Section("n-ary fold — three and four inputs, deciding input last");
    Check("AND(1,1,1)",    1, GATE_AND,  all3High,  3);
    Check("AND(1,1,0)",    0, GATE_AND,  last3Low,  3);
    Check("OR(0,0,0)",     0, GATE_OR,   all3Low,   3);
    Check("OR(0,0,1)",     1, GATE_OR,   last3High, 3);
    Check("NAND(1,1,1)",   0, GATE_NAND, all3High,  3);
    Check("NAND(1,1,0)",   1, GATE_NAND, last3Low,  3);
    Check("NOR(0,0,0)",    1, GATE_NOR,  all3Low,   3);
    Check("NOR(0,0,1)",    0, GATE_NOR,  last3High, 3);
    Check("XOR(1,1,1)",    1, GATE_XOR,  all3High,  3);
    Check("XOR(1,1,0)",    0, GATE_XOR,  last3Low,  3);
    Check("XOR(0,1,1,1)",  1, GATE_XOR,  four1High, 4);
    Check("XNOR(1,1,1)",   0, GATE_XNOR, all3High,  3);
    Check("XNOR(1,1,0)",   1, GATE_XNOR, last3Low,  3);

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
    Check("BUFFER arity 2",   GATE_ERR_INPUT_COUNT,  GATE_BUFFER,  pair, 2);
    Check("NAND arity 1",     GATE_ERR_INPUT_COUNT,  GATE_NAND,    pair, 1);
    Check("NOR arity 1",      GATE_ERR_INPUT_COUNT,  GATE_NOR,     pair, 1);
    Check("XOR arity 1",      GATE_ERR_INPUT_COUNT,  GATE_XOR,     pair, 1);
    Check("XNOR arity 1",     GATE_ERR_INPUT_COUNT,  GATE_XNOR,    pair, 1);
    Check("SOURCE not a gate", GATE_ERR_UNKNOWN_TYPE, GATE_SOURCE, pair, 1);
    Check("OUTPUT not a gate", GATE_ERR_UNKNOWN_TYPE, GATE_OUTPUT, pair, 1);
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
