// PluginCheck — verifies the committed SiliconPlugin.dll against LogicEngine.cs.
//
// Same checks as Assets/Tests/Editor/LogicEngineTests.cs, but runnable without
// Unity, so CI can run them on a Windows runner (the Unity job is Linux-only and
// skips those tests). Catches the 20 Sep 2026 failure: a committed binary built
// from older gate source than the C# GateType enum.
//
// Also checks whole-circuit evaluation through LogicEngine.EvaluateCircuit
// (#12), and fails if the DLL still carries debug sections, since an unstripped
// build adds ~460 KB to repo history on every rebuild.
//
// Run locally (Windows, from the repo root, needs the .NET 8 SDK):
//     dotnet run -c Release --project native/SiliconPlugin/tests/PluginCheck
// Exits 0 when every check passes, 1 otherwise.

using System;
using System.Collections.Generic;
using System.IO;
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
        CheckDllIsStripped();
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

        Console.WriteLine(new string('-', 40));
        if (failures == 0)
        {
            Console.WriteLine($"All {checks} checks passed.");
            return 0;
        }

        Console.WriteLine($"{failures} of {checks} checks FAILED.");
        return 1;
    }

    private static void CheckDllIsStripped()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "SiliconPlugin.dll");
        byte[] bytes = File.ReadAllBytes(path);
        bool hasDebug = IndexOf(bytes, Encoding.ASCII.GetBytes(".debug_")) >= 0;
        Record(!hasDebug,
               $"SiliconPlugin.dll is stripped ({bytes.Length / 1024} KB)",
               "contains .debug_* sections. Rebuild with -s, or run: strip --strip-unneeded SiliconPlugin.dll");
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
