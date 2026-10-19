// CircuitEvaluationTests.cs — EditMode tests for whole-circuit evaluation (#12),
// called through LogicEngine.EvaluateCircuit exactly as the simulation driver
// (#82) will call it.
//
// Netlists are built by hand here, so these tests cover the engine alone. The
// netlist builder (#18) gets its own tests with placed blocks; the end-to-end
// tests (#11) cover the two together.
//
// Test names carry the Verification Test Inventory IDs they provide evidence for.
//
// Platform note: same as LogicEngineTests — the only committed native library is
// Windows x86_64, so the fixture is ignored on any other editor (including the
// Linux CI runner). The same checks run on Windows in CI through PluginCheck.

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class CircuitEvaluationTests
{
    [OneTimeSetUp]
    public void RequireWindowsEditor()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor)
        {
            Assert.Ignore(
                "SiliconPlugin is only built for Windows x86_64, so it cannot be " +
                $"loaded on {Application.platform}. These tests only run in a Windows editor.");
        }
    }

    // ---- Helpers ---------------------------------------------------------------

    private static NetlistNode Node(int id, GateType type) => new NetlistNode(id, type);

    private static NetlistConnection Wire(int fromId, int toId, int toPin = 0) => new NetlistConnection(fromId, toId, toPin);

    // Source A (0), source B (1), the gate (2), an LED on its output (3).
    // One-input gates leave B unconnected.
    private static NetlistNode[] SingleGateNodes(GateType gate) => new[]
    {
        Node(0, GateType.Source), Node(1, GateType.Source), Node(2, gate), Node(3, GateType.Output),
    };

    private static NetlistConnection[] SingleGateWires(GateType gate) =>
        IsOneInput(gate)
            ? new[] { Wire(0, 2), Wire(2, 3) }
            : new[] { Wire(0, 2, 0), Wire(1, 2, 1), Wire(2, 3) };

    private static bool IsOneInput(GateType gate) => gate == GateType.Not || gate == GateType.Buffer;

    private static readonly GateType[] AllGates =
    {
        GateType.And, GateType.Or, GateType.Nand, GateType.Nor,
        GateType.Xor, GateType.Xnor, GateType.Not, GateType.Buffer,
    };

    // Reference truth table, written independently of the native code.
    private static int Expected(GateType gate, int a, int b)
    {
        switch (gate)
        {
            case GateType.And:    return a & b;
            case GateType.Or:     return a | b;
            case GateType.Nand:   return 1 - (a & b);
            case GateType.Nor:    return 1 - (a | b);
            case GateType.Xor:    return a ^ b;
            case GateType.Xnor:   return 1 - (a ^ b);
            case GateType.Not:    return 1 - a;
            case GateType.Buffer: return a;
            default: throw new ArgumentOutOfRangeException(nameof(gate), gate, "Not an evaluable gate");
        }
    }

    // ---- Tests -------------------------------------------------------------------

    // Every gate wired source -> gate -> LED, every input combination. The LED
    // must show what the gate outputs.
    [TestCaseSource(nameof(AllGates))]
    public void SingleGate_LedShowsGateOutput(GateType gate)
    {
        NetlistNode[] nodes = SingleGateNodes(gate);
        NetlistConnection[] wires = SingleGateWires(gate);
        var states = new int[nodes.Length];

        for (int bits = 0; bits < 4; bits++)
        {
            int a = bits & 1, b = (bits >> 1) & 1;
            states[0] = a;
            states[1] = b;

            CircuitResult result = LogicEngine.EvaluateCircuit(nodes, wires, states);

            int expected = Expected(gate, a, b);
            string row = $"{gate}(A={a}, B={b})";
            Assert.AreEqual(CircuitResult.Complete, result, row);
            Assert.AreEqual(expected, states[2], row + " gate output");
            Assert.AreEqual(expected, states[3], row + " LED");
        }
    }

    // TC-3.2.9: a basic gate followed by NOT behaves as the inverted gate.
    [TestCase(GateType.And, GateType.Nand)]
    [TestCase(GateType.Or, GateType.Nor)]
    [TestCase(GateType.Xor, GateType.Xnor)]
    [TestCase(GateType.Buffer, GateType.Not)]
    public void TC_3_2_9_GateThenNot_EqualsInvertedGate(GateType gate, GateType inverted)
    {
        // 0 A, 1 B, 2 gate, 3 NOT, 4 LED
        NetlistNode[] nodes =
        {
            Node(0, GateType.Source), Node(1, GateType.Source), Node(2, gate),
            Node(3, GateType.Not), Node(4, GateType.Output),
        };
        var wires = IsOneInput(gate)
            ? new[] { Wire(0, 2), Wire(2, 3), Wire(3, 4) }
            : new[] { Wire(0, 2, 0), Wire(1, 2, 1), Wire(2, 3), Wire(3, 4) };
        var states = new int[nodes.Length];

        for (int bits = 0; bits < 4; bits++)
        {
            int a = bits & 1, b = (bits >> 1) & 1;
            states[0] = a;
            states[1] = b;
            LogicEngine.EvaluateCircuit(nodes, wires, states);
            Assert.AreEqual(Expected(inverted, a, b), states[4], $"NOT({gate}(A={a}, B={b}))");
        }
    }

    // TC-3.4.3: changing a gate's input changes its output, on the next
    // evaluation, with the same states array reused (as the driver will).
    [Test]
    public void TC_3_4_3_ChangingAnInput_ChangesTheOutput()
    {
        NetlistNode[] nodes = SingleGateNodes(GateType.And);
        NetlistConnection[] wires = SingleGateWires(GateType.And);
        int[] states = { 1, 0, 0, 0 };

        LogicEngine.EvaluateCircuit(nodes, wires, states);
        Assert.AreEqual(0, states[3], "AND(1, 0)");

        states[1] = 1;
        LogicEngine.EvaluateCircuit(nodes, wires, states);
        Assert.AreEqual(1, states[3], "AND(1, 1) after flipping B");
    }

    // TC-3.4.5: two outputs driving one input is an error, not a value. The
    // netlist builder (#18) is meant to catch this per wire network before it
    // gets here; this pins down what the engine does if it ever does arrive.
    [Test]
    public void TC_3_4_5_TwoDriversOnOnePin_IsRejected()
    {
        NetlistNode[] nodes = { Node(0, GateType.Source), Node(1, GateType.Source), Node(2, GateType.Output) };
        NetlistConnection[] wires = { Wire(0, 2), Wire(1, 2) };
        int[] states = { 1, 0, 1 };

        var e = Assert.Throws<LogicEngineException>(() => LogicEngine.EvaluateCircuit(nodes, wires, states));
        Assert.AreEqual((int)NetlistStatus.MultipleDrivers, e.StatusCode);
        Assert.IsTrue(states.All(s => s == 0), "states are zeroed after a rejected netlist");
    }

    // TC-3.4.6: a gate's output does not feed back into its inputs. Here the
    // gate's output slot is pre-loaded with 1 before evaluating; the result must
    // depend only on the sources wired to its inputs, and the sources must be
    // left as they were.
    [Test]
    public void TC_3_4_6_GateOutput_DoesNotAffectItsInputs()
    {
        NetlistNode[] nodes = SingleGateNodes(GateType.Or);
        NetlistConnection[] wires = SingleGateWires(GateType.Or);
        int[] states = { 0, 0, 1, 1 };

        LogicEngine.EvaluateCircuit(nodes, wires, states);

        Assert.AreEqual(0, states[0], "source A unchanged");
        Assert.AreEqual(0, states[1], "source B unchanged");
        Assert.AreEqual(0, states[2], "OR(0, 0) output");
    }

    // Two levels of logic with the nodes listed downstream-first: the engine
    // must evaluate in dependency order, not array order.
    [Test]
    public void MultiLevelCircuit_EvaluatesInDependencyOrder()
    {
        // LED = (A AND B) OR (NOT C). 0 LED, 1 OR, 2 AND, 3 NOT, 4 A, 5 B, 6 C
        NetlistNode[] nodes =
        {
            Node(0, GateType.Output), Node(1, GateType.Or), Node(2, GateType.And), Node(3, GateType.Not),
            Node(4, GateType.Source), Node(5, GateType.Source), Node(6, GateType.Source),
        };
        NetlistConnection[] wires =
        {
            Wire(4, 2, 0), Wire(5, 2, 1), Wire(6, 3), Wire(2, 1, 0), Wire(3, 1, 1), Wire(1, 0),
        };
        var states = new int[nodes.Length];

        for (int bits = 0; bits < 8; bits++)
        {
            int a = bits & 1, b = (bits >> 1) & 1, c = (bits >> 2) & 1;
            states[4] = a;
            states[5] = b;
            states[6] = c;
            LogicEngine.EvaluateCircuit(nodes, wires, states);
            Assert.AreEqual((a & b) | (1 - c), states[0], $"A={a} B={b} C={c}");
        }
    }

    // A half-built circuit: unwired pins read 0 and nothing throws.
    [Test]
    public void UnwiredPins_ReadLow()
    {
        NetlistNode[] nodes = { Node(0, GateType.Source), Node(1, GateType.And), Node(2, GateType.Not), Node(3, GateType.Output) };
        NetlistConnection[] wires = { Wire(0, 1, 0) };
        int[] states = { 1, 0, 0, 1 };

        LogicEngine.EvaluateCircuit(nodes, wires, states);

        Assert.AreEqual(0, states[1], "AND(1, unwired)");
        Assert.AreEqual(1, states[2], "NOT(unwired)");
        Assert.AreEqual(0, states[3], "unwired LED");
    }

    // Loops need #10's tick. Until then they are reported and read 0, and the
    // rest of the circuit still evaluates.
    [Test]
    public void FeedbackLoop_IsReported_AndTheRestStillEvaluates()
    {
        // 0, 1 NOTs in a loop; 2 source -> 3 LED alongside
        NetlistNode[] nodes = { Node(0, GateType.Not), Node(1, GateType.Not), Node(2, GateType.Source), Node(3, GateType.Output) };
        NetlistConnection[] wires = { Wire(0, 1), Wire(1, 0), Wire(2, 3) };
        int[] states = { 0, 0, 1, 0 };

        CircuitResult result = LogicEngine.EvaluateCircuit(nodes, wires, states);

        Assert.AreEqual(CircuitResult.HasFeedbackLoop, result);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, states);
    }

    [Test]
    public void StatesArrayOfTheWrongLength_IsRefused()
    {
        NetlistNode[] nodes = SingleGateNodes(GateType.And);
        Assert.Throws<ArgumentException>(() => LogicEngine.EvaluateCircuit(nodes, null, new int[nodes.Length - 1]));
    }
}
