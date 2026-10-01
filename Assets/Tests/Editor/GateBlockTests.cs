// GateBlockTests.cs — EditMode tests for GateBlock (#81): that a placed gate
// reports its ports where it actually sits, and that the displayed state the
// simulation driver (#82) pushes in is what other blocks read back.
//
// GatePortsTests already covers the port table and rotation maths. These check
// the wiring between that table and a real GameObject: grid snapping, the
// transform's rotation, and the display-only state.
//
// Note: in EditMode, AddComponent does not run OnEnable for ordinary
// MonoBehaviours, so nothing here touches the LogicSignalBlock grid registry.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class GateBlockTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        created.Clear();
    }

    private GateBlock MakeGate(GateType type, Vector3 position, float yaw)
    {
        var go = new GameObject($"Test {type} Gate");
        created.Add(go);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        GateBlock gate = go.AddComponent<GateBlock>();
        gate.GateType = type;
        return gate;
    }

    private static (Vector3Int cell, Vector3Int face) Port(GateBlock gate, PortRole role, int inputIndex = -1)
    {
        var p = gate.WorldPorts().Single(w => w.port.Role == role && w.port.InputIndex == inputIndex);
        return (p.cell, p.face);
    }

    [Test]
    public void WorldPortsFollowTheGatesPositionAndRotation()
    {
        GateBlock gate = MakeGate(GateType.And, new Vector3(2f, 0f, 3f), 90f);
        var cell = new Vector3Int(2, 0, 3);

        Assert.AreEqual((cell, new Vector3Int(1, 0, 0)), Port(gate, PortRole.Output), "output");
        Assert.AreEqual((cell, new Vector3Int(0, 0, 1)), Port(gate, PortRole.Input, 0), "input A");
        Assert.AreEqual((cell, new Vector3Int(0, 0, -1)), Port(gate, PortRole.Input, 1), "input B");
    }

    [Test]
    public void WorldPortsSnapSlightlyOffGridTransformsToTheGrid()
    {
        // Physics and float maths leave placed blocks fractionally off-grid and
        // fractionally off 90 degrees; ports must still land on exact cells.
        GateBlock gate = MakeGate(GateType.Not, new Vector3(4.0003f, 1.9998f, -6.0001f), 179.9996f);

        var output = Port(gate, PortRole.Output);
        var input = Port(gate, PortRole.Input, 0);

        Assert.AreEqual(new Vector3Int(4, 2, -6), output.cell);
        Assert.AreEqual(new Vector3Int(0, 0, -1), output.face, "output faces -Z at yaw 180");
        Assert.AreEqual(new Vector3Int(0, 0, 1), input.face, "input faces +Z at yaw 180");
    }

    [Test]
    public void WorldPortsCountMatchesTheGateType()
    {
        Assert.AreEqual(3, MakeGate(GateType.Xor, Vector3.zero, 0f).WorldPorts().Count());
        Assert.AreEqual(2, MakeGate(GateType.Buffer, Vector3.zero, 0f).WorldPorts().Count());
    }

    [Test]
    public void GateLeftAtInvalidTypeFailsLoudly()
    {
        GateBlock gate = MakeGate(GateType.Invalid, Vector3.zero, 0f);

        // WorldPorts is lazy, so force enumeration.
        Assert.Throws<ArgumentException>(() => gate.WorldPorts().ToList());
    }

    [Test]
    public void DisplayedStateIsWhatOtherBlocksRead()
    {
        GateBlock gate = MakeGate(GateType.And, Vector3.zero, 0f);
        Assert.AreEqual(0, gate.SignalState, "starts low");

        gate.SetDisplayedState(1);
        Assert.AreEqual(1, gate.DisplayedState);
        Assert.AreEqual(1, gate.SignalState);

        gate.SetDisplayedState(0);
        Assert.AreEqual(0, gate.SignalState);
    }

    [Test]
    public void DisplayedStateIsClampedToBinary()
    {
        GateBlock gate = MakeGate(GateType.Or, Vector3.zero, 0f);

        gate.SetDisplayedState(5);
        Assert.AreEqual(1, gate.SignalState, "any non-zero reads high");

        gate.SetDisplayedState(0);
        Assert.AreEqual(0, gate.SignalState);
    }
}
