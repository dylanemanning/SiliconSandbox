// GatePortsTests.cs — EditMode tests for the gate port table and its
// local-to-world rotation (#81).
//
// Why these exist: the netlist builder (#18) decides which wire feeds which gate
// input purely from GatePorts.ToWorld. A wrong sign or a missed rounding there
// would not crash anything; it would quietly wire input A to B or leave a gate
// disconnected, and only show up as a wrong LED in play mode. These pin the
// table and the rotation maths down before anything is built on them.
//
// Pure C#, no native plugin and no scene, so unlike LogicEngineTests they run on
// the Linux CI runner too.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class GatePortsTests
{
    // Every gate type with its number of inputs. Mirrors LogicEngineTests'
    // EvaluableGates: if a gate is added there, add it here too.
    private static readonly Dictionary<GateType, int> Gates = new Dictionary<GateType, int>
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

    private static readonly GateType[] TwoInputGates =
        { GateType.And, GateType.Or, GateType.Nand, GateType.Nor, GateType.Xor, GateType.Xnor };

    private static readonly GateType[] OneInputGates = { GateType.Not, GateType.Buffer };

    private static readonly Vector3Int Anchor = new Vector3Int(5, 1, -3);

    // ---- The table itself -------------------------------------------------

    [Test]
    public void EveryGateHasOneOutputAndTheRightNumberOfInputs()
    {
        foreach (var gate in Gates)
        {
            IReadOnlyList<GatePort> ports = GatePorts.For(gate.Key);

            Assert.AreEqual(1, ports.Count(p => p.Role == PortRole.Output),
                $"{gate.Key} should have exactly one output");
            Assert.AreEqual(-1, ports.Single(p => p.Role == PortRole.Output).InputIndex,
                $"{gate.Key} output should have InputIndex -1");

            // Input indices must be exactly 0..n-1, because they are positions in
            // the inputs array passed to LogicEngine.Evaluate.
            int[] indices = ports.Where(p => p.Role == PortRole.Input)
                                 .Select(p => p.InputIndex)
                                 .OrderBy(i => i)
                                 .ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, gate.Value).ToArray(), indices,
                $"{gate.Key} input indices");
        }
    }

    [Test]
    public void EveryPortIsOnADistinctUnitAxisFace()
    {
        foreach (GateType gate in Gates.Keys)
        {
            IReadOnlyList<GatePort> ports = GatePorts.For(gate);

            foreach (GatePort port in ports)
            {
                Vector3Int f = port.Face;
                int nonZero = (f.x != 0 ? 1 : 0) + (f.y != 0 ? 1 : 0) + (f.z != 0 ? 1 : 0);
                Assert.IsTrue(nonZero == 1 && Math.Abs(f.x + f.y + f.z) == 1,
                    $"{gate} {port} face should be a unit axis vector");
            }

            // Two ports on the same face of the same cell would both connect to the
            // same wire, which makes them indistinguishable.
            int distinct = ports.Select(p => (p.CellOffset, p.Face)).Distinct().Count();
            Assert.AreEqual(ports.Count, distinct, $"{gate} has two ports on the same face");
        }
    }

    [TestCase(GateType.Invalid)]
    [TestCase(GateType.Source)]
    [TestCase(GateType.Output)]
    public void NonGatesHaveNoPortTable(GateType type)
    {
        Assert.Throws<ArgumentException>(() => GatePorts.For(type));
    }

    // ---- Local layout (no rotation) ---------------------------------------

    [Test]
    public void TwoInputGatesUnrotated_AOnLeft_BOnRight_OutputFront()
    {
        foreach (GateType gate in TwoInputGates)
        {
            AssertPortFaces(gate, Quaternion.identity,
                inputA: Vector3Int.left,
                inputB: Vector3Int.right,
                output: Vector3Int.forward);
        }
    }

    [Test]
    public void OneInputGatesUnrotated_InputBack_OutputFront()
    {
        foreach (GateType gate in OneInputGates)
        {
            AssertPortFaces(gate, Quaternion.identity,
                inputA: Vector3Int.back,
                inputB: null,
                output: Vector3Int.forward);
        }
    }

    // ---- Rotation ---------------------------------------------------------

    // Placement (#80) turns a gate to the player's yaw, snapped to 90 degrees, so
    // these are the only four rotations a placed gate can have. Expected faces
    // are written out by hand rather than computed, so a sign error in ToWorld
    // can't cancel itself out. Unity is left-handed, Y up: facing +X, your left
    // is +Z.
    //
    //        yaw   output (front)   input A (left)   input B (right)
    [TestCase(  0f,   0, 0,  1,       -1, 0,  0,        1, 0,  0)]
    [TestCase( 90f,   1, 0,  0,        0, 0,  1,        0, 0, -1)]
    [TestCase(180f,   0, 0, -1,        1, 0,  0,       -1, 0,  0)]
    [TestCase(270f,  -1, 0,  0,        0, 0, -1,        0, 0,  1)]
    public void AndGateRotatedByYaw(float yaw,
        int ox, int oy, int oz, int ax, int ay, int az, int bx, int by, int bz)
    {
        AssertPortFaces(GateType.And, Quaternion.Euler(0f, yaw, 0f),
            inputA: new Vector3Int(ax, ay, az),
            inputB: new Vector3Int(bx, by, bz),
            output: new Vector3Int(ox, oy, oz));
    }

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(180f)]
    [TestCase(270f)]
    public void NotGateInputIsOppositeItsOutput(float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        IReadOnlyList<GatePort> ports = GatePorts.For(GateType.Not);

        Vector3Int input = GatePorts.ToWorld(ports.Single(p => p.Role == PortRole.Input), Anchor, rotation).face;
        Vector3Int output = GatePorts.ToWorld(ports.Single(p => p.Role == PortRole.Output), Anchor, rotation).face;

        Assert.AreEqual(Vector3Int.zero, input + output, $"yaw {yaw}: input {input}, output {output}");
    }

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(180f)]
    [TestCase(270f)]
    public void OneByOneGatePortsAreOnTheAnchorCell(float yaw)
    {
        // Holds while gates are 1x1x1. When they become 2x2x1 (#64) this test is
        // expected to fail and should be replaced with per-cell expectations.
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        foreach (GateType gate in Gates.Keys)
        {
            foreach (GatePort port in GatePorts.For(gate))
            {
                Assert.AreEqual(Anchor, GatePorts.ToWorld(port, Anchor, rotation).cell,
                    $"{gate} {port} at yaw {yaw}");
            }
        }
    }

    // ---- Helpers ----------------------------------------------------------

    private static void AssertPortFaces(GateType gate, Quaternion rotation,
        Vector3Int inputA, Vector3Int? inputB, Vector3Int output)
    {
        IReadOnlyList<GatePort> ports = GatePorts.For(gate);

        Assert.AreEqual(output, WorldFace(ports.Single(p => p.Role == PortRole.Output), rotation),
            $"{gate} output");
        Assert.AreEqual(inputA, WorldFace(ports.Single(p => p.Role == PortRole.Input && p.InputIndex == 0), rotation),
            $"{gate} input A");

        if (inputB.HasValue)
        {
            Assert.AreEqual(inputB.Value, WorldFace(ports.Single(p => p.Role == PortRole.Input && p.InputIndex == 1), rotation),
                $"{gate} input B");
        }
    }

    private static Vector3Int WorldFace(GatePort port, Quaternion rotation)
        => GatePorts.ToWorld(port, Anchor, rotation).face;
}
