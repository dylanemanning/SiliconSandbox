// LogicEngine.cs — managed wrapper over the SiliconPlugin native logic engine.
//
// This is the only file in the project that talks to the native plugin. Gameplay
// code calls LogicEngine.Evaluate (one gate) or LogicEngine.EvaluateCircuit (a
// whole netlist, #12) and never sees a DllImport, a raw return code, or a native
// exception type.
//
// Several types below MIRROR the native headers, and nothing checks that
// correspondence at compile time; a mismatch surfaces as wrong behaviour rather
// than an error, so change both sides in the same commit:
//   GateType, GateStatus                 native/SiliconPlugin/src/gates.h
//   NetlistStatus, NetlistNode,
//   NetlistConnection                    native/SiliconPlugin/src/netlist.h
//   SimulationStatus                     native/SiliconPlugin/src/simulator.h
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
    //
    // Source and Output are netlist node kinds rather than gates (#9). Evaluate
    // rejects them as UnknownType.
    Invalid = 0,
    And     = 1,
    Or      = 2,
    Not     = 3,
    Source  = 4,
    Output  = 5,
    Nand    = 6,
    Nor     = 7,
    Xor     = 8,
    Xnor    = 9,
    Buffer  = 10
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

/// <summary>
/// Failures the netlist parser reports (#9). Mirrors NetlistStatus in netlist.h.
/// Numbered from -10 so they never collide with GateStatus.
/// </summary>
public enum NetlistStatus
{
    NullArray       = -10,
    NegativeCount   = -11,
    DuplicateId     = -12,
    UnknownType     = -13,
    UnknownId       = -14,
    NoOutput        = -15,
    PinOutOfRange   = -16,
    MultipleDrivers = -17,
    OutOfMemory     = -18
}

/// <summary>
/// Codes the whole-circuit evaluator returns (#12). Mirrors SimulateStatus in
/// simulator.h. Ok and HasCycle are successes; the negatives are failures and
/// never collide with GateStatus or NetlistStatus.
/// </summary>
public enum SimulationStatus
{
    Ok         =   0,
    HasCycle   =   1,
    NullStates = -20,
    GateFailed = -21,
    Internal   = -22
}

/// <summary>How a successful EvaluateCircuit call went.</summary>
public enum CircuitResult
{
    /// <summary>Every node was evaluated.</summary>
    Complete,

    /// <summary>
    /// The circuit has a feedback loop. Nodes on the loop, or downstream of it,
    /// read 0. Loops need state between evaluations, which arrives with #10's
    /// tick; until then they are reported rather than simulated.
    /// </summary>
    HasFeedbackLoop
}

/// <summary>
/// One component in a netlist: a source, a gate, or an output (LED).
/// Layout matches silicon::NetlistNode in netlist.h field for field, which is
/// what lets an array of these cross the DllImport boundary with no copy. Keep
/// it two ints, in this order.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetlistNode
{
    /// <summary>
    /// Any value unique within one netlist. Connections refer to nodes by it.
    /// The netlist builder (#18) uses the node's index.
    /// </summary>
    public int Id;

    /// <summary>A GateType, stored as int to keep the layout plain.</summary>
    public int Type;

    public NetlistNode(int id, GateType type)
    {
        Id = id;
        Type = (int)type;
    }

    public override string ToString() => $"Node {Id} ({(GateType)Type})";
}

/// <summary>
/// One wire run from a node's output to one input pin of another node. Layout
/// matches silicon::NetlistConnection in netlist.h; keep it three ints, in this
/// order.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NetlistConnection
{
    /// <summary>Id of the node whose output drives the wire.</summary>
    public int FromId;

    /// <summary>Id of the node whose input the wire feeds.</summary>
    public int ToId;

    /// <summary>Which input on ToId, starting at 0 (the GatePort InputIndex).</summary>
    public int ToPin;

    public NetlistConnection(int fromId, int toId, int toPin)
    {
        FromId = fromId;
        ToId = toId;
        ToPin = toPin;
    }

    public override string ToString() => $"{FromId} -> {ToId} pin {ToPin}";
}

/// <summary>Raised when the native logic engine cannot produce a result.</summary>
public class LogicEngineException : Exception
{
    /// <summary>
    /// The negative code the native call returned, or 0 when the failure did not
    /// come from a return code (plugin missing, for example). Compare against
    /// GateStatus, NetlistStatus or SimulationStatus.
    /// </summary>
    public int StatusCode { get; }

    public LogicEngineException(string message) : base(message) { }
    public LogicEngineException(string message, Exception inner) : base(message, inner) { }
    public LogicEngineException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
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

    // NetlistNode and NetlistConnection are blittable (all-int, sequential), so
    // their arrays are pinned and passed as pointers exactly like int[]. states
    // is [In, Out]: source values go in, every node's value comes back.
    [DllImport(PluginName,
               EntryPoint = "Silicon_EvaluateCircuit",
               CallingConvention = CallingConvention.Cdecl)]
    private static extern int Silicon_EvaluateCircuit(
        [In] NetlistNode[] nodes, int nodeCount,
        [In] NetlistConnection[] connections, int connectionCount,
        [In, Out] int[] states);

    /// <summary>
    /// Evaluates one logic gate.
    /// </summary>
    /// <param name="gateType">The gate to evaluate.</param>
    /// <param name="inputs">Input signals, each strictly 0 or 1. Not and Buffer
    /// take exactly one; And, Or, Nand, Nor, Xor and Xnor take two or more and fold
    /// across all of them (Xor is odd parity, Xnor even parity).</param>
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
            throw PluginMissing(inner);
        }
        catch (EntryPointNotFoundException inner)
        {
            throw ExportMissing("Silicon_EvaluateGate", inner);
        }

        if (result < 0)
        {
            throw new LogicEngineException(Describe((GateStatus)result, gateType, inputs), result);
        }

        return result;
    }

    /// <summary>
    /// Evaluates a whole circuit in one call (#12): parses the netlist, then
    /// computes every node in dependency order. Stateless — call it again
    /// whenever the placed circuit or a source changes.
    /// </summary>
    /// <param name="nodes">Every source, gate and LED in the circuit.</param>
    /// <param name="connections">Every wire run, output to input pin. Null is
    /// treated as no connections.</param>
    /// <param name="states">Exactly one slot per node, indexed like
    /// <paramref name="nodes"/>. In: each Source node's value (non-zero = 1);
    /// other slots are ignored. Out: every node's value, 0 or 1 — a gate's
    /// output, an LED's input. Unwired input pins read 0. Reuse the same array
    /// between calls; nothing needs clearing.</param>
    /// <returns>Complete, or HasFeedbackLoop if part of the circuit could not be
    /// evaluated without state (those nodes read 0).</returns>
    /// <exception cref="ArgumentNullException">nodes or states is null.</exception>
    /// <exception cref="ArgumentException">states is not the same length as nodes.</exception>
    /// <exception cref="LogicEngineException">The netlist was rejected (StatusCode
    /// holds the NetlistStatus), or the plugin is unavailable. On a rejected
    /// netlist every entry of states is 0.</exception>
    public static CircuitResult EvaluateCircuit(NetlistNode[] nodes, NetlistConnection[] connections, int[] states)
    {
        if (nodes == null) throw new ArgumentNullException(nameof(nodes));
        if (states == null) throw new ArgumentNullException(nameof(states));
        if (connections == null) connections = Array.Empty<NetlistConnection>();

        // The one check the native side cannot make: a pointer does not carry
        // its length, so a short states array would be written past its end.
        if (states.Length != nodes.Length)
        {
            throw new ArgumentException(
                $"states has {states.Length} slot(s) but there are {nodes.Length} node(s). " +
                "It needs exactly one slot per node.", nameof(states));
        }

        int status;
        try
        {
            status = Silicon_EvaluateCircuit(nodes, nodes.Length, connections, connections.Length, states);
        }
        catch (DllNotFoundException inner)
        {
            throw PluginMissing(inner);
        }
        catch (EntryPointNotFoundException inner)
        {
            throw ExportMissing("Silicon_EvaluateCircuit", inner);
        }

        switch ((SimulationStatus)status)
        {
            case SimulationStatus.Ok:       return CircuitResult.Complete;
            case SimulationStatus.HasCycle: return CircuitResult.HasFeedbackLoop;
        }

        throw new LogicEngineException(DescribeCircuitFailure(status), status);
    }

    // Most likely cause is running on a platform with no native build. A native
    // library exists for Windows x86_64 only; macOS needs a separately compiled
    // SiliconPlugin.bundle.
    private static LogicEngineException PluginMissing(Exception inner)
        => new LogicEngineException(
            "SiliconPlugin could not be loaded on this platform. The committed " +
            "native library is Windows x86_64 only.", inner);

    // The library loaded but lacks the export. Almost always means the committed
    // binary is older than the C++ sources.
    private static LogicEngineException ExportMissing(string entryPoint, Exception inner)
        => new LogicEngineException(
            $"SiliconPlugin loaded but does not export {entryPoint}. " +
            "The committed binary is probably out of date; rebuild it from " +
            "native/SiliconPlugin/.", inner);

    private static string DescribeCircuitFailure(int status)
    {
        switch (status)
        {
            case (int)NetlistStatus.NullArray:
            case (int)NetlistStatus.NegativeCount:
            case (int)SimulationStatus.NullStates:
                return $"The netlist arrays did not reach the plugin intact (status {status}). " +
                       "This is a marshaling problem, not a circuit problem.";

            case (int)NetlistStatus.DuplicateId:
                return "Two netlist nodes share an id. The netlist builder must give every node a unique id.";

            case (int)NetlistStatus.UnknownType:
                return "A netlist node has a type that is not a GateType (or is Invalid). " +
                       "Usually a gate prefab left at Invalid.";

            case (int)NetlistStatus.UnknownId:
                return "A connection refers to a node id that is not in the node list.";

            case (int)NetlistStatus.NoOutput:
                return "A connection is driven from an Output node (LED). LEDs only receive signals.";

            case (int)NetlistStatus.PinOutOfRange:
                return "A connection targets an input pin the node does not have " +
                       "(a Source has none; Not, Buffer and Output have only pin 0).";

            case (int)NetlistStatus.MultipleDrivers:
                return "Two connections drive the same input pin. The netlist builder should " +
                       "flag a wire network with more than one driver instead of sending it (TC-3.4.5).";

            case (int)NetlistStatus.OutOfMemory:
                return "The plugin ran out of memory building the circuit graph.";

            case (int)SimulationStatus.GateFailed:
                return "The evaluator rejected a gate the parser accepted. gates.cpp and netlist.cpp " +
                       "disagree about a gate's input count; this is a plugin bug.";

            case (int)SimulationStatus.Internal:
                return "The plugin caught an unexpected C++ exception while evaluating the circuit.";

            default:
                return $"Silicon_EvaluateCircuit returned unrecognised status {status}. " +
                       "The native headers and LogicEngine.cs are out of sync.";
        }
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
                return $"Unknown gate type {(int)gateType}. Valid gates are And, Or, Not, Nand, Nor, Xor, Xnor and Buffer.";

            case GateStatus.InputCount:
                return $"{gateType} received {inputs.Length} input(s). " +
                       "Not and Buffer require exactly 1; the other gates require 2 or more.";

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
