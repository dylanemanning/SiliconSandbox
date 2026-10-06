// NetlistBuilderTests.cs — EditMode tests for the netlist builder's rules (#18),
// using hand-written CircuitParts.
//
// No scene, no GameObjects and no native plugin, so these run on the Linux CI
// runner as well as in the editor. NetlistBuilderBlockTests covers the adapter
// from real blocks (and the real GatePorts table), and the round trip through
// the engine.
//
// Gate ports here come from a small helper that mirrors the GatePorts layout
// (inputs A left and B right, or one input behind; output in front), so a test
// reads as "AND facing +Z at (2,0,0)". Test names carry the Verification Test
// Inventory IDs they provide evidence for.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class NetlistBuilderTests
{
    // ---- Part helpers ------------------------------------------------------------

    private static readonly Vector3Int PlusX = new Vector3Int(1, 0, 0);
    private static readonly Vector3Int PlusZ = new Vector3Int(0, 0, 1);

    private static Vector3Int C(int x, int y, int z) => new Vector3Int(x, y, z);

    private static CircuitPart Wire(int x, int y, int z) => new CircuitPart { Kind = PartKind.Wire, Cell = C(x, y, z) };

    private static CircuitPart Source(int x, int y, int z)
    {
        Vector3Int cell = C(x, y, z);
        return new CircuitPart
        {
            Kind = PartKind.Source,
            Cell = cell,
            Ports = NetlistBuilder.AllFaces.Select(f => PartPort.Output(cell, f)).ToArray(),
        };
    }

    private static CircuitPart Led(int x, int y, int z)
    {
        Vector3Int cell = C(x, y, z);
        return new CircuitPart
        {
            Kind = PartKind.Output,
            Cell = cell,
            Ports = NetlistBuilder.AllFaces.Select(f => PartPort.Input(cell, f, 0)).ToArray(),
        };
    }

    // A gate whose output faces `front` (a horizontal unit vector). Two-input
    // gates: A on the left, B on the right. One-input gates: input behind.
    private static CircuitPart Gate(GateType type, Vector3Int cell, Vector3Int front)
    {
        var right = new Vector3Int(front.z, 0, -front.x);
        var left = new Vector3Int(-right.x, 0, -right.z);
        var back = new Vector3Int(-front.x, 0, -front.z);

        bool oneInput = type == GateType.Not || type == GateType.Buffer;
        PartPort[] ports = oneInput
            ? new[] { PartPort.Input(cell, back, 0), PartPort.Output(cell, front) }
            : new[] { PartPort.Input(cell, left, 0), PartPort.Input(cell, right, 1), PartPort.Output(cell, front) };

        return new CircuitPart { Kind = PartKind.Gate, Cell = cell, GateType = type, Ports = ports };
    }

    private static Netlist Build(params CircuitPart[] parts) => NetlistBuilder.FromParts(parts);

    // The node index for the part at a given position in the parts array.
    private static int NodeOf(Netlist netlist, int partIndex) => System.Array.IndexOf(netlist.NodeParts, partIndex);

    private static bool HasConnection(Netlist netlist, int fromPart, int toPart, int pin)
        => netlist.Connections.Contains(new NetlistConnection(NodeOf(netlist, fromPart), NodeOf(netlist, toPart), pin));

    // ---- Basics --------------------------------------------------------------------

    [Test]
    public void EmptyWorld_GivesAnEmptyNetlist()
    {
        Netlist netlist = Build();
        Assert.AreEqual(0, netlist.Nodes.Length);
        Assert.AreEqual(0, netlist.Connections.Length);
        Assert.AreEqual(0, netlist.Networks.Length);
        Assert.IsEmpty(netlist.Warnings);
    }

    // TC-3.1.2: a user-made circuit becomes the right nodes and connections.
    // Source -> two wires -> LED.
    [Test]
    public void TC_3_1_2_SourceWireLed_OneConnection()
    {
        Netlist netlist = Build(Source(0, 0, 0), Wire(1, 0, 0), Wire(2, 0, 0), Led(3, 0, 0));

        Assert.AreEqual(2, netlist.Nodes.Length, "source and LED are nodes; wires are not");
        Assert.AreEqual(GateType.Source, (GateType)netlist.Nodes[NodeOf(netlist, 0)].Type);
        Assert.AreEqual(GateType.Output, (GateType)netlist.Nodes[NodeOf(netlist, 3)].Type);

        Assert.AreEqual(1, netlist.Connections.Length);
        Assert.IsTrue(HasConnection(netlist, 0, 3, 0), "source drives LED pin 0");

        Assert.AreEqual(1, netlist.Networks.Length);
        CollectionAssert.AreEqual(new[] { 1, 2 }, netlist.Networks[0].WireParts);
        Assert.AreEqual(NodeOf(netlist, 0), netlist.Networks[0].DriverNode);
        Assert.AreEqual(0, netlist.NetworkOfPart[1]);
        Assert.AreEqual(-1, netlist.NetworkOfPart[0], "sources are not on a network as wires");
    }

    // TC-3.1.2 with a gate: inputs land on the right pins, output reaches the LED.
    //
    //   z=1:  srcA  wire  [AND->+Z]  wire  srcB      (x = 0..4)
    //   z=2:              wire
    //   z=3:              LED
    [Test]
    public void TC_3_1_2_TwoSourcesThroughAnAnd_PinsMatchSides()
    {
        Netlist netlist = Build(
            Source(0, 0, 1),                    // 0  A, on the gate's left (-X)
            Wire(1, 0, 1),                      // 1
            Gate(GateType.And, C(2, 0, 1), PlusZ), // 2
            Wire(3, 0, 1),                      // 3
            Source(4, 0, 1),                    // 4  B, on the gate's right (+X)
            Wire(2, 0, 2),                      // 5
            Led(2, 0, 3));                      // 6

        Assert.AreEqual(4, netlist.Nodes.Length);
        Assert.AreEqual(3, netlist.Connections.Length);
        Assert.IsTrue(HasConnection(netlist, 0, 2, 0), "left source -> pin 0 (A)");
        Assert.IsTrue(HasConnection(netlist, 4, 2, 1), "right source -> pin 1 (B)");
        Assert.IsTrue(HasConnection(netlist, 2, 6, 0), "gate output -> LED");
        Assert.IsEmpty(netlist.Warnings);
    }

    // A wire beside a gate face that has no port is not connected.
    [Test]
    public void WireOnAGateSideWithNoPort_IsNotConnected()
    {
        // NOT facing +Z: input behind (-Z), output in front (+Z). Its left side
        // (-X) has no port.
        Netlist netlist = Build(Source(-2, 0, 0), Wire(-1, 0, 0), Gate(GateType.Not, C(0, 0, 0), PlusZ));

        Assert.AreEqual(0, netlist.Connections.Length);
        Assert.AreEqual(1, netlist.Networks[0].DriverNodes.Length, "the source still drives its own wire");
    }

    [Test]
    public void WiresJoinVertically()
    {
        Netlist netlist = Build(Source(0, 0, 0), Wire(1, 0, 0), Wire(1, 1, 0), Wire(1, 2, 0), Led(1, 3, 0));

        Assert.AreEqual(1, netlist.Networks.Length);
        Assert.IsTrue(HasConnection(netlist, 0, 4, 0));
    }

    [Test]
    public void WiresWithAGap_AreSeparateNetworks()
    {
        Netlist netlist = Build(Source(0, 0, 0), Wire(1, 0, 0), Wire(3, 0, 0), Led(4, 0, 0));

        Assert.AreEqual(2, netlist.Networks.Length);
        Assert.AreEqual(0, netlist.Connections.Length);
        Assert.IsTrue(netlist.Networks[1].HasNoDriver);
    }

    // ---- Drivers -------------------------------------------------------------------

    // TC-3.4.5: two outputs on one wire network is a short circuit. The network
    // is flagged and nothing is connected from it.
    [Test]
    public void TC_3_4_5_TwoSourcesOnOneNetwork_IsFlaggedAndNotConnected()
    {
        Netlist netlist = Build(Source(0, 0, 0), Wire(1, 0, 0), Wire(2, 0, 0), Source(3, 0, 0), Led(1, 0, 1));

        WireNetwork network = netlist.Networks.Single();
        Assert.IsTrue(network.HasMultipleDrivers);
        Assert.AreEqual(-1, network.DriverNode);
        Assert.AreEqual(2, network.DriverNodes.Length);
        Assert.AreEqual(0, netlist.Connections.Length, "the LED on the shorted network is not fed");
    }

    // Two gate outputs on one network is the same short.
    [Test]
    public void TC_3_4_5_TwoGateOutputsOnOneNetwork_IsFlagged()
    {
        // Two BUFFERs facing each other across a wire.
        Netlist netlist = Build(
            Gate(GateType.Buffer, C(0, 0, 0), PlusX),
            Wire(1, 0, 0),
            Gate(GateType.Buffer, C(2, 0, 0), new Vector3Int(-1, 0, 0)));

        Assert.IsTrue(netlist.Networks.Single().HasMultipleDrivers);
    }

    [Test]
    public void NetworkWithNoDriver_FloatsAndConnectsNothing()
    {
        Netlist netlist = Build(Wire(0, 0, 0), Wire(1, 0, 0), Led(2, 0, 0));

        Assert.IsTrue(netlist.Networks.Single().HasNoDriver);
        Assert.AreEqual(-1, netlist.Networks[0].DriverNode);
        Assert.AreEqual(0, netlist.Connections.Length);
    }

    // A source wrapped by an L of wire touches the same network on two faces.
    // That is one driver, not a short.
    [Test]
    public void SourceTouchingOneNetworkOnTwoFaces_IsOneDriver()
    {
        Netlist netlist = Build(Source(0, 0, 0), Wire(1, 0, 0), Wire(1, 0, 1), Wire(0, 0, 1), Led(2, 0, 0));

        WireNetwork network = netlist.Networks.Single();
        Assert.IsFalse(network.HasMultipleDrivers);
        Assert.AreEqual(1, netlist.Connections.Length);
    }

    // Same for an LED touching one network on two faces: one connection.
    [Test]
    public void LedTouchingOneNetworkOnTwoFaces_IsOneConnection()
    {
        Netlist netlist = Build(Source(-1, 0, 1), Wire(0, 0, 1), Wire(1, 0, 1), Wire(1, 0, 0), Led(0, 0, 0));

        Assert.AreEqual(1, netlist.Connections.Length);
        Assert.IsEmpty(netlist.Warnings);
    }

    [Test]
    public void FanOut_OneSourceFeedsTwoLeds()
    {
        Netlist netlist = Build(Source(0, 0, 0), Wire(1, 0, 0), Led(2, 0, 0), Led(1, 0, 1));

        Assert.AreEqual(2, netlist.Connections.Length);
        Assert.IsTrue(HasConnection(netlist, 0, 2, 0));
        Assert.IsTrue(HasConnection(netlist, 0, 3, 0));
    }

    // ---- Direct contact ------------------------------------------------------------

    [Test]
    public void SourceRightNextToAnLed_IsConnected()
    {
        Netlist netlist = Build(Source(0, 0, 0), Led(1, 0, 0));

        Assert.AreEqual(1, netlist.Connections.Length);
        Assert.IsTrue(HasConnection(netlist, 0, 1, 0));
    }

    [Test]
    public void LedAgainstAGatesOutput_IsConnected()
    {
        Netlist netlist = Build(Gate(GateType.Not, C(0, 0, 0), PlusZ), Led(0, 0, 1));

        Assert.IsTrue(HasConnection(netlist, 0, 1, 0));
    }

    [Test]
    public void SourceAgainstAGatesInput_IsConnected()
    {
        // NOT facing +Z has its input on -Z.
        Netlist netlist = Build(Source(0, 0, -1), Gate(GateType.Not, C(0, 0, 0), PlusZ));

        Assert.IsTrue(HasConnection(netlist, 0, 1, 0));
    }

    [Test]
    public void TwoGatesButtedTogether_AreConnected()
    {
        // NOT -> NOT, both facing +X.
        Netlist netlist = Build(Gate(GateType.Not, C(0, 0, 0), PlusX), Gate(GateType.Not, C(1, 0, 0), PlusX));

        Assert.AreEqual(1, netlist.Connections.Length);
        Assert.IsTrue(HasConnection(netlist, 0, 1, 0));
    }

    [Test]
    public void LedAgainstAGateSideWithNoPort_IsNotConnected()
    {
        Netlist netlist = Build(Gate(GateType.Not, C(0, 0, 0), PlusZ), Led(1, 0, 0));
        Assert.AreEqual(0, netlist.Connections.Length);
    }

    [Test]
    public void TwoSourcesSideBySide_ConnectNothing()
    {
        Netlist netlist = Build(Source(0, 0, 0), Source(1, 0, 0));
        Assert.AreEqual(0, netlist.Connections.Length);
    }

    // ---- Rules that resolve conflicts instead of failing ---------------------------

    // An LED between two separately driven networks would give its only pin two
    // drivers, which the engine rejects outright. The first is kept and the
    // conflict is reported.
    [Test]
    public void LedFedByTwoDrivenNetworks_KeepsOneAndWarns()
    {
        Netlist netlist = Build(Source(0, 0, 0), Wire(1, 0, 0), Led(2, 0, 0), Wire(3, 0, 0), Source(4, 0, 0));

        Assert.AreEqual(2, netlist.Networks.Length);
        Assert.AreEqual(1, netlist.Connections.Length, "only one driver reaches the LED's single pin");
        Assert.AreEqual(1, netlist.Warnings.Count);
        StringAssert.Contains("input 0", netlist.Warnings[0]);
    }

    // The same driver reaching one pin by two routes is one signal, not a
    // conflict: one connection, and no warning to spam the console.
    [Test]
    public void SameDriverByTwoRoutes_IsOneConnectionAndNoWarning()
    {
        // LED touches the source directly (+X) and through the source's wire.
        Netlist netlist = Build(Source(0, 0, 0), Led(1, 0, 0), Wire(0, 0, 1), Wire(1, 0, 1));

        Assert.AreEqual(1, netlist.Connections.Length);
        Assert.IsEmpty(netlist.Warnings);
    }

    [Test]
    public void PartWithNullPorts_DoesNotThrow()
    {
        CircuitPart led = Led(1, 0, 0);
        led.Ports = null;

        Netlist netlist = null;
        Assert.DoesNotThrow(() => netlist = Build(Source(0, 0, 0), led));
        Assert.AreEqual(0, netlist.Connections.Length, "a part with no ports connects to nothing");
    }

    [Test]
    public void TwoPartsOnOneCell_SecondIsIgnoredWithAWarning()
    {
        Netlist netlist = Build(Source(0, 0, 0), Led(0, 0, 0));

        Assert.AreEqual(1, netlist.Nodes.Length);
        Assert.AreEqual(1, netlist.Warnings.Count);
    }

    [Test]
    public void GateWithInvalidType_IsSkippedWithAWarning()
    {
        CircuitPart broken = Gate(GateType.And, C(0, 0, 0), PlusZ);
        broken.GateType = GateType.Invalid;

        Netlist netlist = null;
        Assert.DoesNotThrow(() => netlist = Build(Source(-1, 0, 0), broken));
        Assert.AreEqual(1, netlist.Nodes.Length, "only the source");
        Assert.AreEqual(1, netlist.Warnings.Count);
    }

    // A gate wired back into itself is passed through as a connection. The
    // engine reports the loop (CircuitResult.HasFeedbackLoop); the builder does
    // not second-guess it, since latches (SR-3.3) are built exactly like this.
    [Test]
    public void GateOutputWiredToItsOwnInput_IsPassedThrough()
    {
        // NOT facing +X at origin; wire loops from its front (+X) round to its back (-X).
        Netlist netlist = Build(
            Gate(GateType.Not, C(0, 0, 0), PlusX),
            Wire(1, 0, 0), Wire(1, 0, 1), Wire(0, 0, 1), Wire(-1, 0, 1), Wire(-1, 0, 0));

        Assert.AreEqual(1, netlist.Connections.Length);
        Assert.IsTrue(HasConnection(netlist, 0, 0, 0));
    }

    // TC-3.1.4: the netlist follows the circuit when it changes. Rebuilding
    // after a wire is broken drops the connection; putting it back restores it.
    [Test]
    public void TC_3_1_4_RebuildingAfterAChange_FollowsTheCircuit()
    {
        var parts = new List<CircuitPart> { Source(0, 0, 0), Wire(1, 0, 0), Wire(2, 0, 0), Led(3, 0, 0) };
        Assert.AreEqual(1, NetlistBuilder.FromParts(parts).Connections.Length, "complete");

        CircuitPart removed = parts[2];
        parts.RemoveAt(2);
        Assert.AreEqual(0, NetlistBuilder.FromParts(parts).Connections.Length, "wire broken");

        parts.Insert(2, removed);
        Assert.AreEqual(1, NetlistBuilder.FromParts(parts).Connections.Length, "wire replaced");
    }

    // Every connection the builder emits must be one the engine's parser
    // accepts: known ids, in-range pins, never two drivers on one pin.
    [Test]
    public void Connections_AreAlwaysValidForTheEngine()
    {
        Netlist netlist = Build(
            Source(0, 0, 0), Wire(1, 0, 0), Led(2, 0, 0), Wire(3, 0, 0), Source(4, 0, 0),
            Gate(GateType.Xor, C(1, 0, 2), PlusZ), Wire(0, 0, 2), Wire(0, 0, 1),
            Led(1, 0, 3), Source(2, 0, 2));

        var pins = new HashSet<(int, int)>();
        foreach (NetlistConnection c in netlist.Connections)
        {
            Assert.That(c.FromId, Is.InRange(0, netlist.Nodes.Length - 1));
            Assert.That(c.ToId, Is.InRange(0, netlist.Nodes.Length - 1));
            Assert.AreNotEqual(GateType.Output, (GateType)netlist.Nodes[c.FromId].Type, "LEDs never drive");
            Assert.AreNotEqual(GateType.Source, (GateType)netlist.Nodes[c.ToId].Type, "sources have no inputs");
            Assert.IsTrue(pins.Add((c.ToId, c.ToPin)), $"pin {c.ToPin} of node {c.ToId} fed twice");
        }
    }
}
