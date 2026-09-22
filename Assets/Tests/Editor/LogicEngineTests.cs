// LogicEngineTests.cs — EditMode regression tests for the SiliconPlugin native
// logic engine, called through LogicEngine.Evaluate exactly as gameplay code does.
//
// Why these exist: on 20 Sep 2026 the committed SiliconPlugin.dll turned out to
// have been built from three-gate source (And/Or/Not) while GateType and gates.h
// had eight. Nothing failed, because the export name never changed. These tests
// call every GateType through the real P/Invoke path, so a stale or mismatched
// binary shows up as a failing test instead of as wrong behaviour in play mode.
//
// Platform note: the only committed native library is Windows x86_64. On any
// other editor (including the Linux runner CI uses) the plugin cannot load, so
// the fixture is ignored there rather than failed. See OneTimeSetUp.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class LogicEngineTests
{
    // Every GateType that Evaluate must accept, with its arity.
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

    // GateType values Evaluate must reject: the reserved zero and the two
    // netlist node kinds (#9), which are not gates.
    private static readonly GateType[] NonGates =
    {
        GateType.Invalid,
        GateType.Source,
        GateType.Output,
    };

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

    // Guards the lists above. If someone appends a GateType to the enum without
    // deciding here whether it is a gate, this fails instead of the new type
    // silently going untested.
    [Test]
    public void EveryGateType_IsCoveredByThisFixture()
    {
        var covered = new HashSet<GateType>(EvaluableGates.Keys.Concat(NonGates));
        var missing = Enum.GetValues(typeof(GateType)).Cast<GateType>()
                          .Where(t => !covered.Contains(t))
                          .ToList();

        Assert.IsEmpty(missing,
            "GateType values not covered by LogicEngineTests: " + string.Join(", ", missing) +
            ". Add them to EvaluableGates or NonGates.");
    }

    private static IEnumerable<GateType> EvaluableGateCases() => EvaluableGates.Keys;

    // The core regression test: every real gate evaluates, for every input
    // combination, without throwing. A binary older than the C# enum returns
    // UnknownType for the missing gates, which LogicEngine turns into a
    // LogicEngineException, which fails here.
    [TestCaseSource(nameof(EvaluableGateCases))]
    public void Evaluate_EveryGate_DoesNotThrow(GateType gate)
    {
        foreach (int[] inputs in AllInputCombinations(EvaluableGates[gate]))
        {
            Assert.DoesNotThrow(
                () => LogicEngine.Evaluate(gate, inputs),
                $"{gate}({string.Join(", ", inputs)}) threw");
        }
    }

    // Same calls, checked against the expected truth table. Catches a binary
    // that exports the right gates but computes the wrong answer (e.g. values
    // renumbered on one side only).
    [TestCaseSource(nameof(EvaluableGateCases))]
    public void Evaluate_EveryGate_MatchesTruthTable(GateType gate)
    {
        foreach (int[] inputs in AllInputCombinations(EvaluableGates[gate]))
        {
            Assert.AreEqual(
                Expected(gate, inputs),
                LogicEngine.Evaluate(gate, inputs),
                $"{gate}({string.Join(", ", inputs)})");
        }
    }

    private static IEnumerable<GateType> NonGateCases() => NonGates;

    [TestCaseSource(nameof(NonGateCases))]
    public void Evaluate_NonGate_ThrowsLogicEngineException(GateType type)
    {
        Assert.Throws<LogicEngineException>(() => LogicEngine.Evaluate(type, new[] { 0, 1 }));
    }

    // All 2^n combinations of n binary inputs.
    private static IEnumerable<int[]> AllInputCombinations(int n)
    {
        for (int bits = 0; bits < (1 << n); bits++)
        {
            var inputs = new int[n];
            for (int i = 0; i < n; i++)
            {
                inputs[i] = (bits >> i) & 1;
            }
            yield return inputs;
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
            default:
                throw new ArgumentOutOfRangeException(nameof(gate), gate, "Not an evaluable gate");
        }
    }
}
