// GatePorts.cs — where each gate type's inputs and output sit on its block (#81).
//
// Ports are data, not code. The netlist builder (#18) and the simulation driver
// (#82) only ever ask this table where a gate's ports are, so changing a gate's
// footprint (1x1x1 now, 2x2x1 once #64 supports multi-cell blocks) means editing
// the table below and nothing else.
//
// Everything in the table is in the gate's LOCAL frame, before placement
// rotation: +Z is the gate's front (the output side), +X its right, -X its left.
// ToWorld turns a local port into grid coordinates using the placed block's
// grid position and rotation.
//
// Static and free of MonoBehaviour state so it can be unit tested without a scene.

using System;
using System.Collections.Generic;
using UnityEngine;

public enum PortRole
{
    Input,
    Output
}

/// <summary>One connection point on a gate, in the gate's local frame.</summary>
public readonly struct GatePort
{
    /// <summary>
    /// Cell the port is on, relative to the gate's anchor cell. Always zero while
    /// gates are 1x1x1; this is the field that changes for 2x2x1.
    /// </summary>
    public readonly Vector3Int CellOffset;

    /// <summary>Unit vector pointing out of the face the port is on.</summary>
    public readonly Vector3Int Face;

    public readonly PortRole Role;

    /// <summary>
    /// Position in the inputs array LogicEngine.Evaluate expects (A = 0, B = 1).
    /// -1 for the output.
    /// </summary>
    public readonly int InputIndex;

    private GatePort(Vector3Int cellOffset, Vector3Int face, PortRole role, int inputIndex)
    {
        CellOffset = cellOffset;
        Face = face;
        Role = role;
        InputIndex = inputIndex;
    }

    public static GatePort Input(int inputIndex, Vector3Int face, Vector3Int cellOffset = default)
        => new GatePort(cellOffset, face, PortRole.Input, inputIndex);

    public static GatePort Output(Vector3Int face, Vector3Int cellOffset = default)
        => new GatePort(cellOffset, face, PortRole.Output, -1);

    public override string ToString()
        => Role == PortRole.Output
            ? $"Output (cell {CellOffset}, face {Face})"
            : $"Input {InputIndex} (cell {CellOffset}, face {Face})";
}

public static class GatePorts
{
    // A on the left, B on the right, output out the front.
    private static readonly GatePort[] TwoInput =
    {
        GatePort.Input(0, Vector3Int.left),
        GatePort.Input(1, Vector3Int.right),
        GatePort.Output(Vector3Int.forward),
    };

    // Input on the back, output out the front.
    private static readonly GatePort[] OneInput =
    {
        GatePort.Input(0, Vector3Int.back),
        GatePort.Output(Vector3Int.forward),
    };

    /// <summary>
    /// The ports of a gate type, in its local frame. Throws for Source, Output
    /// and Invalid: those are netlist node kinds, not gates, and a gate block
    /// configured with one is a prefab mistake that should fail loudly.
    /// </summary>
    public static IReadOnlyList<GatePort> For(GateType type)
    {
        switch (type)
        {
            case GateType.And:
            case GateType.Or:
            case GateType.Nand:
            case GateType.Nor:
            case GateType.Xor:
            case GateType.Xnor:
                return TwoInput;

            case GateType.Not:
            case GateType.Buffer:
                return OneInput;

            default:
                throw new ArgumentException($"{type} is not a gate type and has no ports.", nameof(type));
        }
    }

    /// <summary>
    /// Converts a local port to world grid space: the grid cell the port is on and
    /// the direction its face points. The neighbouring cell a wire must occupy to
    /// connect is <c>cell + face</c>.
    /// </summary>
    public static (Vector3Int cell, Vector3Int face) ToWorld(GatePort port, Vector3Int anchorCell, Quaternion rotation)
    {
        Vector3Int cell = anchorCell + Rotate(port.CellOffset, rotation);
        Vector3Int face = Rotate(port.Face, rotation);
        return (cell, face);
    }

    // Rounding matters: a 90-degree rotation yields components like 0.9999999 or
    // -4.371139E-08, which would never compare equal to grid coordinates.
    private static Vector3Int Rotate(Vector3Int v, Quaternion rotation)
        => Vector3Int.RoundToInt(rotation * (Vector3)v);
}
