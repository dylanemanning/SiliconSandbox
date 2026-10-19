// Netlist.cs — what the netlist builder (#18) produces, and what it reads.
//
// The builder turns placed blocks into the two flat arrays LogicEngine.
// EvaluateCircuit takes (nodes and connections), plus a lookup table that maps
// the engine's results back onto the world: which block each node is, and which
// wire blocks make up each wire network and who drives it. The simulation
// driver (#82) uses that table to update LEDs, gates and wires.
//
// Everything here is plain data with no MonoBehaviour behaviour, so the builder
// can be tested from hand-written parts without a scene.

using System.Collections.Generic;
using UnityEngine;

/// <summary>What a placed part is, as far as the circuit is concerned.</summary>
public enum PartKind
{
    /// <summary>A wire block. Wires join into networks; they are never nodes.</summary>
    Wire,

    /// <summary>A signal source (switch / voltage / ground). Every face is an output.</summary>
    Source,

    /// <summary>A logic gate. Ports come from the GatePorts table.</summary>
    Gate,

    /// <summary>An LED (StateViewerBlock). Every face is input pin 0.</summary>
    Output
}

/// <summary>
/// One connection point on a part, already in world grid space.
/// </summary>
public readonly struct PartPort
{
    /// <summary>The part's own cell the port sits on.</summary>
    public readonly Vector3Int OnCell;

    /// <summary>
    /// The neighbouring cell the port faces. A wire in this cell is connected to
    /// the port; another part in this cell is connected if it has a port facing
    /// back (see NetlistBuilder).
    /// </summary>
    public readonly Vector3Int FacingCell;

    /// <summary>Input pin number (0 = A, 1 = B), or -1 for an output.</summary>
    public readonly int Pin;

    public bool IsOutput => Pin < 0;

    public PartPort(Vector3Int onCell, Vector3Int facingCell, int pin)
    {
        OnCell = onCell;
        FacingCell = facingCell;
        Pin = pin;
    }

    public static PartPort Output(Vector3Int onCell, Vector3Int face) => new PartPort(onCell, onCell + face, -1);

    public static PartPort Input(Vector3Int onCell, Vector3Int face, int pin) => new PartPort(onCell, onCell + face, pin);

    public override string ToString()
        => IsOutput ? $"output {OnCell}->{FacingCell}" : $"input {Pin} {OnCell}->{FacingCell}";
}

/// <summary>One placed part, as the builder sees it.</summary>
public sealed class CircuitPart
{
    public PartKind Kind;

    /// <summary>
    /// The grid cell the part occupies. One cell per part while every block is
    /// 1x1x1; multi-cell gates (#64) will need a list here.
    /// </summary>
    public Vector3Int Cell;

    /// <summary>The gate type for Gate parts. Ignored for the other kinds.</summary>
    public GateType GateType;

    /// <summary>The part's ports in world space. Empty for wires.</summary>
    public IReadOnlyList<PartPort> Ports = System.Array.Empty<PartPort>();

    public override string ToString()
        => Kind == PartKind.Gate ? $"{GateType} gate at {Cell}" : $"{Kind} at {Cell}";
}

/// <summary>
/// A set of wire blocks joined face to face, and every part that touches it.
/// </summary>
public sealed class WireNetwork
{
    /// <summary>Indices (into the part list) of the wire blocks in this network.</summary>
    public int[] WireParts;

    /// <summary>
    /// Node indices of every output that touches this network. One driver is the
    /// normal case. None means the network floats and reads 0. Two or more is a
    /// short circuit (TC-3.4.5): the builder connects nothing from it, and the
    /// driver draws its wires in the error colour.
    /// </summary>
    public int[] DriverNodes;

    /// <summary>The single driving node, or -1 if there are none or several.</summary>
    public int DriverNode => DriverNodes.Length == 1 ? DriverNodes[0] : -1;

    public bool HasMultipleDrivers => DriverNodes.Length > 1;

    public bool HasNoDriver => DriverNodes.Length == 0;
}

/// <summary>The builder's output: engine input plus the lookup table back to the world.</summary>
public sealed class Netlist
{
    /// <summary>For LogicEngine.EvaluateCircuit. A node's id is its index.</summary>
    public NetlistNode[] Nodes;

    /// <summary>For LogicEngine.EvaluateCircuit.</summary>
    public NetlistConnection[] Connections;

    /// <summary>Node index -> index into the part list it came from.</summary>
    public int[] NodeParts;

    /// <summary>Every wire network, including ones that touch nothing.</summary>
    public WireNetwork[] Networks;

    /// <summary>
    /// Part index -> its network index, or -1 for parts that are not wires.
    /// </summary>
    public int[] NetworkOfPart;

    /// <summary>
    /// Part index -> the block it came from. Set by NetlistBuilder.FromBlocks;
    /// null when the netlist was built from hand-written parts.
    /// </summary>
    public IReadOnlyList<LogicSignalBlock> Blocks;

    /// <summary>
    /// Things the builder skipped or resolved by a rule rather than failing on:
    /// a gate prefab left at Invalid, an LED touching two driven networks, two
    /// blocks registered on one cell. Worth logging; never fatal.
    /// </summary>
    public List<string> Warnings = new List<string>();

    public int NodeCount => Nodes.Length;

    /// <summary>The block behind a node. Only valid when built from blocks.</summary>
    public LogicSignalBlock NodeBlock(int node) => Blocks[NodeParts[node]];
}
