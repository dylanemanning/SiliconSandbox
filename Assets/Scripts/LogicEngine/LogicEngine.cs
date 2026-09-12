// LogicEngine.cs — managed wrapper over the SiliconPlugin native logic engine.
//
// This is the only file in the project that talks to the native plugin. Gameplay
// code calls LogicEngine.Evaluate and never sees a DllImport, a raw return code,
// or a native exception type.
//
// The two enums below MIRROR native/SiliconPlugin/src/gates.h. Nothing checks
// that correspondence at compile time, and a mismatch surfaces as wrong
// behaviour rather than an error, so add gate types and status codes to both
// files in the same commit.
//
// Deliberately free of UnityEngine references: errors are raised as exceptions
// rather than logged, which keeps this file compilable and testable outside the
// editor.

using System;
using System.Runtime.InteropServices;

/// <summary>Logic gate kinds understood by the native evaluator.</summary>
public enum GateType
{
    // These values are written into saved netlists (#18), which makes them part
    // of the on-disk format: APPEND new gate types, never renumber existing ones.
    //
    // Zero is reserved as invalid on purpose. A default-initialised field, or one
    // that failed to deserialise, would otherwise read as a valid And and produce
    // plausible-but-wrong output instead of an error.
    Invalid = 0,
    And     = 1,
    Or      = 2,
    Not     = 3
}

/// <summary>
/// Negative codes the native evaluator returns in place of a result. Valid
/// results are 0 and 1, so any negative return is unambiguously a failure.
/// </summary>
public enum GateStatus
{
    UnknownType = -1,
    InputCount  = -2,
    NullInputs  = -3,
    InputValue  = -4
}

/// <summary>Raised when the native logic engine cannot produce a result.</summary>
public class LogicEngineException : Exception
{
    public LogicEngineException(string message) : base(message) { }
    public LogicEngineException(string message, Exception inner) : base(message, inner) { }
}

public static class LogicEngine
{
    private const string PluginName = "SiliconPlugin";

    // EntryPoint names the exported symbol, which is prefixed to avoid colliding
    // with other native plugins in the same process. The managed API keeps the
    // short name.
    //
    // Cdecl is the convention a C export uses. On x64 Windows there is only one
    // convention so this is currently cosmetic, but it is the correct declaration
    // and it matters if a macOS or Linux build is ever added.
    //
    // int[] is blittable, so the marshaler pins the array and passes a pointer to
    // its first element with no copy. [In] documents that the native side only
    // reads it.
    [DllImport(PluginName,
               EntryPoint = "Silicon_EvaluateGate",
               CallingConvention = CallingConvention.Cdecl)]
    private static extern int Silicon_EvaluateGate(int gateType, [In] int[] inputs, int inputCount);

    /// <summary>
    /// Evaluates one logic gate.
    /// </summary>
    /// <param name="gateType">The gate to evaluate.</param>
    /// <param name="inputs">Input signals, each strictly 0 or 1. Not takes exactly
    /// one; And and Or take two or more and fold across all of them.</param>
    /// <returns>0 or 1.</returns>
    /// <exception cref="LogicEngineException">
    /// The gate could not be evaluated, or the native plugin is unavailable.
    /// </exception>
    public static int Evaluate(GateType gateType, int[] inputs)
    {
        if (inputs == null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        int result;
        try
        {
            result = Silicon_EvaluateGate((int)gateType, inputs, inputs.Length);
        }
        catch (DllNotFoundException inner)
        {
            // Most likely cause is running on a platform with no native build.
            // A native library exists for Windows x86_64 only; macOS needs a
            // separately compiled SiliconPlugin.bundle.
            throw new LogicEngineException(
                "SiliconPlugin could not be loaded on this platform. The committed " +
                "native library is Windows x86_64 only.", inner);
        }
        catch (EntryPointNotFoundException inner)
        {
            // The library loaded but has no Silicon_EvaluateGate. Almost always
            // means the committed binary is older than the C++ sources.
            throw new LogicEngineException(
                "SiliconPlugin loaded but does not export Silicon_EvaluateGate. " +
                "The committed binary is probably out of date; rebuild it from " +
                "native/SiliconPlugin/.", inner);
        }

        if (result < 0)
        {
            throw new LogicEngineException(Describe((GateStatus)result, gateType, inputs));
        }

        return result;
    }

    // Turns a native status code into a message that names the actual problem.
    // Worth the few lines: by the time #10's propagation tick is calling this
    // thousands of times per second, "node returned -2" is not a useful thing to
    // read in the console.
    private static string Describe(GateStatus status, GateType gateType, int[] inputs)
    {
        switch (status)
        {
            case GateStatus.UnknownType:
                return $"Unknown gate type {(int)gateType}. Valid types are And, Or and Not.";

            case GateStatus.InputCount:
                return $"{gateType} received {inputs.Length} input(s). " +
                       "Not requires exactly 1; And and Or require 2 or more.";

            case GateStatus.NullInputs:
                return $"{gateType} received a null input array.";

            case GateStatus.InputValue:
                return $"{gateType} received an input outside {{0, 1}}: " +
                       $"[{string.Join(", ", inputs)}]. Inputs always come from a " +
                       "previous gate output or a source block, so this usually " +
                       "indicates a marshaling problem rather than a bad circuit.";

            default:
                // An unrecognised negative means gates.h gained a status code that
                // GateStatus above does not have.
                return $"{gateType} returned unrecognised status {(int)status}. " +
                       "gates.h and LogicEngine.cs are out of sync.";
        }
    }
}
