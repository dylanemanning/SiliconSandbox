// PluginCheck loads the shipped Windows or macOS library through the same
// LogicEngine wrapper used by Unity. Tests exports, truth tables, error handling,
// whole circuits and stripped binaries. Requires .NET 8 (or a newer target override).
// dotnet run -c Release --project native/SiliconPlugin/tests/PluginCheck

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;

internal static class Program
{
    // Every GateType that Evaluate must accept, with its minimum arity.
    private static readonly Dictionary<GateType, int> EvaluableGates = new Dictionary<GateType, int>
    {
        { GateType.And,    2 },
        { GateType.Or,     2 },
        { GateType.Not,    1 },
        { GateType.Nand,   2 },
        { GateType.Nor,    2 },
        { GateType.Xor,    2 },
        { GateType.Xnor,   2 },
        { GateType.Buffer, 1 },
    };

    // Values Evaluate must reject: reserved zero and the netlist node kinds.
    private static readonly GateType[] NonGates = { GateType.Invalid, GateType.Source, GateType.Output };

    private static int checks;
    private static int failures;

    private static int Main()
    {
        CheckLibraryIsStripped();
        CheckEveryGateTypeIsCovered();

        foreach (KeyValuePair<GateType, int> gate in EvaluableGates)
        {
            // Minimum arity, plus one wider fold for the multi-input gates.
            CheckTruthTable(gate.Key, gate.Value);
            if (gate.Value > 1)
            {
                CheckTruthTable(gate.Key, 3);
            }
        }

        foreach (GateType type in NonGates)
        {
            CheckRejected(type);
        }

        CheckHalfAdder();
        CheckMultiLevel();
        CheckUnwiredPinReadsLow();
        CheckFeedbackLoopReported();
        CheckMultipleDriversRejected();
        CheckStatesLengthEnforced();

        CheckTickRipple();
        CheckTickSrLatch();
        CheckTickOscillator();
        CheckTickErrors();
        LogicEngine.UnloadCircuit();

        Console.WriteLine(new string('-', 40));
        if (failures == 0)
        {
            Console.WriteLine($"All {checks} checks passed.");
            return 0;
        }

        Console.WriteLine($"{failures} of {checks} checks FAILED.");
        return 1;
    }

    private static void CheckLibraryIsStripped()
    {
        bool mac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        string library = mac ? "libSiliconPlugin.dylib" : "SiliconPlugin.dll";
        string path = Path.Combine(AppContext.BaseDirectory, library);
        byte[] bytes = File.ReadAllBytes(path);
        bool hasDebug = IndexOf(bytes, Encoding.ASCII.GetBytes(mac ? "__DWARF" : ".debug_")) >= 0;
        Record(!hasDebug,
               $"{library} is stripped ({bytes.Length / 1024} KB)",
               "contains debug sections; rebuild using the documented platform recipe.");
    }

    private static void CheckEveryGateTypeIsCovered()
    {
        var covered = new HashSet<GateType>(EvaluableGates.Keys.Concat(NonGates));
        var missing = Enum.GetValues(typeof(GateType)).Cast<GateType>().Where(t => !covered.Contains(t)).ToList();
        Record(missing.Count == 0,
               "every GateType is covered by PluginCheck",
               "not covered: " + string.Join(", ", missing) + ". Add them to EvaluableGates or NonGates.");
    }

    private static void CheckTruthTable(GateType gate, int inputCount)
    {
        for (int bits = 0; bits < (1 << inputCount); bits++)
        {
            var inputs = new int[inputCount];
            for (int i = 0; i < inputCount; i++)
            {
                inputs[i] = (bits >> i) & 1;
            }

            string name = $"{gate}({string.Join(", ", inputs)})";
            int expected = Expected(gate, inputs);
            try
            {
                int actual = LogicEngine.Evaluate(gate, inputs);
                Record(actual == expected, $"{name} = {actual}", $"returned {actual}, expected {expected}");
            }
            catch (Exception e)
            {
                Record(false, name, $"threw {e.GetType().Name}: {e.Message}");
            }
        }
    }

    private static void CheckRejected(GateType type)
    {
        try
        {
            LogicEngine.Evaluate(type, new[] { 0, 1 });
            Record(false, $"{type} rejected", "was accepted; it is not an evaluable gate");
        }
        catch (LogicEngineException)
        {
            Record(true, $"{type} rejected", null);
        }
        catch (Exception e)
        {
            Record(false, $"{type} rejected", $"threw {e.GetType().Name} instead of LogicEngineException: {e.Message}");
        }
    }

    // ---- Whole-circuit checks (#12) ------------------------------------------

    // Sum = A XOR B, carry = A AND B: fan-out from both sources into two gates,
    // each with an LED. Nodes: 0 A, 1 B, 2 XOR, 3 AND, 4 sum LED, 5 carry LED.
    private static void CheckHalfAdder()
    {
        NetlistNode[] nodes =
        {
            new NetlistNode(0, GateType.Source), new NetlistNode(1, GateType.Source),
            new NetlistNode(2, GateType.Xor),    new NetlistNode(3, GateType.And),
            new NetlistNode(4, GateType.Output), new NetlistNode(5, GateType.Output),
        };
        NetlistConnection[] connections =
        {
            new NetlistConnection(0, 2, 0), new NetlistConnection(1, 2, 1),
            new NetlistConnection(0, 3, 0), new NetlistConnection(1, 3, 1),
            new NetlistConnection(2, 4, 0), new NetlistConnection(3, 5, 0),
        };

        var states = new int[nodes.Length];
        for (int bits = 0; bits < 4; bits++)
        {
            int a = bits & 1, b = (bits >> 1) & 1;
            states[0] = a;
            states[1] = b;
            string name = $"half adder A={a} B={b}";
            try
            {
                CircuitResult result = LogicEngine.EvaluateCircuit(nodes, connections, states);
                bool ok = result == CircuitResult.Complete && states[4] == (a ^ b) && states[5] == (a & b);
                Record(ok, $"{name} -> sum {states[4]}, carry {states[5]}",
                       $"{result}, states [{string.Join(", ", states)}]");
            }
            catch (Exception e)
            {
                Record(false, name, $"threw {e.GetType().Name}: {e.Message}");
            }
        }
    }

    // LED = (A AND B) OR (NOT C), with the nodes listed downstream-first so the
    // check fails if the plugin evaluates in array order instead of dependency
    // order. Nodes: 0 LED, 1 OR, 2 AND, 3 NOT, 4 A, 5 B, 6 C.
    private static void CheckMultiLevel()
    {
        NetlistNode[] nodes =
        {
            new NetlistNode(0, GateType.Output), new NetlistNode(1, GateType.Or),
            new NetlistNode(2, GateType.And),    new NetlistNode(3, GateType.Not),
            new NetlistNode(4, GateType.Source), new NetlistNode(5, GateType.Source),
            new NetlistNode(6, GateType.Source),
        };
        NetlistConnection[] connections =
        {
            new NetlistConnection(4, 2, 0), new NetlistConnection(5, 2, 1),
            new NetlistConnection(6, 3, 0), new NetlistConnection(2, 1, 0),
            new NetlistConnection(3, 1, 1), new NetlistConnection(1, 0, 0),
        };

        var states = new int[nodes.Length];
        int wrong = 0;
        for (int bits = 0; bits < 8; bits++)
        {
            int a = bits & 1, b = (bits >> 1) & 1, c = (bits >> 2) & 1;
            states[4] = a;
            states[5] = b;
            states[6] = c;
            LogicEngine.EvaluateCircuit(nodes, connections, states);
            int expected = (a & b) | (c == 1 ? 0 : 1);
            if (states[0] != expected) wrong++;
        }
        Record(wrong == 0, "(A AND B) OR (NOT C), all 8 rows, nodes out of order",
               $"{wrong} of 8 rows wrong");
    }

    // A half-built circuit: an AND with only pin 0 wired reads pin 1 as 0.
    private static void CheckUnwiredPinReadsLow()
    {
        NetlistNode[] nodes = { new NetlistNode(0, GateType.Source), new NetlistNode(1, GateType.And) };
        NetlistConnection[] connections = { new NetlistConnection(0, 1, 0) };
        int[] states = { 1, 0 };
        LogicEngine.EvaluateCircuit(nodes, connections, states);
        Record(states[1] == 0, "AND(1, unwired) = 0", $"returned {states[1]}");
    }

    // Two NOTs feeding each other. Reported, not simulated, until #10.
    private static void CheckFeedbackLoopReported()
    {
        NetlistNode[] nodes = { new NetlistNode(0, GateType.Not), new NetlistNode(1, GateType.Not) };
        NetlistConnection[] connections = { new NetlistConnection(0, 1, 0), new NetlistConnection(1, 0, 0) };
        var states = new int[2];
        CircuitResult result = LogicEngine.EvaluateCircuit(nodes, connections, states);
        Record(result == CircuitResult.HasFeedbackLoop && states[0] == 0 && states[1] == 0,
               "feedback loop reported as HasFeedbackLoop, loop nodes read 0",
               $"{result}, states [{string.Join(", ", states)}]");
    }

    // Two sources on one AND pin (TC-3.4.5) must come back as a typed failure.
    private static void CheckMultipleDriversRejected()
    {
        NetlistNode[] nodes =
        {
            new NetlistNode(0, GateType.Source), new NetlistNode(1, GateType.Source), new NetlistNode(2, GateType.And),
        };
        NetlistConnection[] connections = { new NetlistConnection(0, 2, 0), new NetlistConnection(1, 2, 0) };
        int[] states = { 1, 1, 1 };
        try
        {
            LogicEngine.EvaluateCircuit(nodes, connections, states);
            Record(false, "multiple drivers rejected", "was accepted");
        }
        catch (LogicEngineException e)
        {
            bool ok = e.StatusCode == (int)NetlistStatus.MultipleDrivers && states.All(s => s == 0);
            Record(ok, "multiple drivers rejected with NetlistStatus.MultipleDrivers, states zeroed",
                   $"status {e.StatusCode}, states [{string.Join(", ", states)}]");
        }
        catch (Exception e)
        {
            Record(false, "multiple drivers rejected", $"threw {e.GetType().Name}: {e.Message}");
        }
    }

    // A states array the wrong length would be written past its end natively,
    // so the wrapper must refuse it before calling.
    private static void CheckStatesLengthEnforced()
    {
        NetlistNode[] nodes = { new NetlistNode(0, GateType.Source), new NetlistNode(1, GateType.Output) };
        try
        {
            LogicEngine.EvaluateCircuit(nodes, null, new int[1]);
            Record(false, "short states array refused", "was accepted");
        }
        catch (ArgumentException)
        {
            Record(true, "short states array refused with ArgumentException", null);
        }
        catch (Exception e)
        {
            Record(false, "short states array refused", $"threw {e.GetType().Name}: {e.Message}");
        }
    }

    // Reference truth table, written independently of the native code.
    private static int Expected(GateType gate, int[] inputs)
    {
        int high = inputs.Sum();
        bool all = high == inputs.Length;
        bool any = high > 0;
        bool odd = high % 2 == 1;

        switch (gate)
        {
            case GateType.And:    return all ? 1 : 0;
            case GateType.Nand:   return all ? 0 : 1;
            case GateType.Or:     return any ? 1 : 0;
            case GateType.Nor:    return any ? 0 : 1;
            case GateType.Xor:    return odd ? 1 : 0;
            case GateType.Xnor:   return odd ? 0 : 1;
            case GateType.Not:    return inputs[0] == 1 ? 0 : 1;
            case GateType.Buffer: return inputs[0];
            default: throw new ArgumentOutOfRangeException(nameof(gate), gate, "Not an evaluable gate");
        }
    }

    // ---- Unit-delay tick checks (#10) -------------------------------------------

    // Source -> BUFFER -> BUFFER -> LED: the LED rises exactly two ticks after
    // the source, one per gate. Nodes: 0 source, 1 and 2 buffers, 3 LED.
    private static void CheckTickRipple()
    {
        NetlistNode[] nodes =
        {
            new NetlistNode(0, GateType.Source), new NetlistNode(1, GateType.Buffer),
            new NetlistNode(2, GateType.Buffer), new NetlistNode(3, GateType.Output),
        };
        NetlistConnection[] connections =
        {
            new NetlistConnection(0, 1, 0), new NetlistConnection(1, 2, 0), new NetlistConnection(2, 3, 0),
        };
        try
        {
            CircuitResult loaded = LogicEngine.LoadCircuit(nodes, connections);
            var states = new[] { 1, 0, 0, 0 };
            int first = LogicEngine.Tick(states);
            int led1 = states[3];
            int second = LogicEngine.Tick(states);
            int led2 = states[3];
            int third = LogicEngine.Tick(states);
            bool ok = loaded == CircuitResult.Complete && led1 == 0 && led2 == 1 &&
                      first == 1 && second == 2 && third == 0;
            Record(ok, "tick: two buffers delay the LED by two ticks, then it settles",
                   $"load {loaded}, LED {led1} then {led2}, changed {first}/{second}/{third}");
        }
        catch (Exception e)
        {
            Record(false, "tick: ripple", $"threw {e.GetType().Name}: {e.Message}");
        }
    }

    // NOR SR latch: set, hold, reset, hold. Nodes: 0 S, 1 R, 2 Q, 3 Qn.
    private static void CheckTickSrLatch()
    {
        NetlistNode[] nodes =
        {
            new NetlistNode(0, GateType.Source), new NetlistNode(1, GateType.Source),
            new NetlistNode(2, GateType.Nor),    new NetlistNode(3, GateType.Nor),
        };
        NetlistConnection[] connections =
        {
            new NetlistConnection(1, 2, 0), new NetlistConnection(3, 2, 1),
            new NetlistConnection(0, 3, 0), new NetlistConnection(2, 3, 1),
        };
        try
        {
            CircuitResult loaded = LogicEngine.LoadCircuit(nodes, connections);
            var states = new int[4];
            string trace = "";
            foreach (var (s, r) in new[] { (1, 0), (0, 0), (0, 1), (0, 0) })
            {
                states[0] = s;
                states[1] = r;
                for (int t = 0; t < 8 && LogicEngine.Tick(states) != 0; t++) { }
                trace += states[2];
            }
            Record(loaded == CircuitResult.HasFeedbackLoop && trace == "1100",
                   "tick: NOR SR latch sets, holds, resets and holds",
                   $"load {loaded}, Q after set/hold/reset/hold = {trace}");
        }
        catch (Exception e)
        {
            Record(false, "tick: SR latch", $"threw {e.GetType().Name}: {e.Message}");
        }
    }

    // A NOT wired to its own input flips every tick and never settles.
    private static void CheckTickOscillator()
    {
        NetlistNode[] nodes = { new NetlistNode(0, GateType.Not) };
        NetlistConnection[] connections = { new NetlistConnection(0, 0, 0) };
        try
        {
            LogicEngine.LoadCircuit(nodes, connections);
            var states = new int[1];
            string trace = "";
            bool alwaysChanged = true;
            for (int t = 0; t < 4; t++)
            {
                alwaysChanged &= LogicEngine.Tick(states) == 1;
                trace += states[0];
            }
            Record(trace == "1010" && alwaysChanged, "tick: NOT into itself oscillates every tick",
                   $"trace {trace}, changed every tick: {alwaysChanged}");
        }
        catch (Exception e)
        {
            Record(false, "tick: oscillator", $"threw {e.GetType().Name}: {e.Message}");
        }
    }

    // Wrong length and nothing loaded are refused with the right codes, and the
    // states array is not written.
    private static void CheckTickErrors()
    {
        NetlistNode[] nodes = { new NetlistNode(0, GateType.Source), new NetlistNode(1, GateType.Output) };
        NetlistConnection[] connections = { new NetlistConnection(0, 1, 0) };
        LogicEngine.LoadCircuit(nodes, connections);
        CheckTickFails("tick: wrong-length states rejected", new[] { 7, 7, 7 }, SimulationStatus.NodeCount);
        LogicEngine.UnloadCircuit();
        CheckTickFails("tick: nothing loaded rejected", new[] { 7, 7 }, SimulationStatus.NotLoaded);
    }

    private static void CheckTickFails(string name, int[] states, SimulationStatus expected)
    {
        int[] before = (int[])states.Clone();
        try
        {
            LogicEngine.Tick(states);
            Record(false, name, "was accepted");
        }
        catch (LogicEngineException e)
        {
            bool untouched = string.Join(",", states) == string.Join(",", before);
            Record(e.StatusCode == (int)expected && untouched, name,
                   $"status {e.StatusCode}, states untouched: {untouched}");
        }
        catch (Exception e)
        {
            Record(false, name, $"threw {e.GetType().Name}: {e.Message}");
        }
    }

    private static void Record(bool passed, string what, string why)
    {
        checks++;
        if (passed)
        {
            Console.WriteLine($"PASS  {what}");
        }
        else
        {
            failures++;
            Console.WriteLine($"FAIL  {what}: {why}");
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j])
            {
                j++;
            }
            if (j == needle.Length)
            {
                return i;
            }
        }
        return -1;
    }
}
