// PluginCheck — verifies the committed SiliconPlugin.dll against LogicEngine.cs.
//
// Same checks as Assets/Tests/Editor/LogicEngineTests.cs, but runnable without
// Unity, so CI can run them on a Windows runner (the Unity job is Linux-only and
// skips those tests). Catches the 20 Sep 2026 failure: a committed binary built
// from older gate source than the C# GateType enum.
//
// Also fails if the DLL still carries debug sections, since an unstripped build
// adds ~460 KB to repo history on every rebuild.
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
