// NetlistBuilderBlockTests.cs — EditMode tests for building netlists from real
// placed blocks (#18), and for the full round trip: blocks -> netlist ->
// LogicEngine.EvaluateCircuit -> LED values (#11).
//
// NetlistBuilderTests covers the connection rules with hand-written parts. These
// check what only real components can: that each block type becomes the right
// part, that a rotated GateBlock's ports (from the real GatePorts table) land on
// the right pins, and that the lookup table points back at the right blocks.
//
// In EditMode, AddComponent does not run OnEnable, so the LogicSignalBlock grid
// registry stays empty. That is why the builder takes its blocks as an argument.
//
// The first fixture is pure C# and runs everywhere. The second calls the native
// plugin and, like LogicEngineTests, only runs in a Windows editor.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Shared GameObject helpers for both fixtures.
public abstract class PlacedBlockFixture
{
    private readonly List<GameObject> created = new List<GameObject>();
    private readonly List<LogicSignalBlock> placed = new List<LogicSignalBlock>();

    [TearDown]
    public void DestroyCreatedObjects()
    {
        foreach (GameObject go in created)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
        created.Clear();
        placed.Clear();
    }

    // Only the blocks this test placed. Not FindObjectsByType: that would also
    // pick up whatever is in the scene open in the editor.
    protected IEnumerable<LogicSignalBlock> AllPlaced() => placed.Where(b => b != null);

    protected T Place<T>(int x, int y, int z, float yaw = 0f) where T : LogicSignalBlock
    {
        var go = new GameObject($"Test {typeof(T).Name} ({x},{y},{z})");
        created.Add(go);
        go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
        T block = go.AddComponent<T>();
        placed.Add(block);
        return block;
    }

    protected LogicWireBlock Wire(int x, int y, int z) => Place<LogicWireBlock>(x, y, z);

    protected StateViewerBlock Led(int x, int y, int z) => Place<StateViewerBlock>(x, y, z);

    protected LogicSignalSource Source(int x, int y, int z, int state = 0)
    {
        LogicSignalSource source = Place<LogicSignalSource>(x, y, z);
        source.SetState(state);
        return source;
    }

    protected GateBlock Gate(GateType type, int x, int y, int z, float yaw)
    {
        GateBlock gate = Place<GateBlock>(x, y, z, yaw);
        gate.GateType = type;
        return gate;
    }

    // The two-input gate circuit the gate tests share: a gate at (5,0,5) turned
    // to face +X (yaw 90), fed by two sources, its output wired to an LED. Two-
    // input gates are 2x1x2 (GatePorts). Top view, x across, z up the page:
    //
    //   x:    3      4      5      6      7      8
    //   z=5   srcA   BUF>   [A     out>]  wire   LED
    //   z=4   srcB   wire   [B         ]
    //
    // At yaw 90 both inputs are on the gate's -X face: A on the anchor cell,
    // B on the cell at -Z. The two cells facing them are side by side, and wires
    // join on all six faces (until #63), so a wire in each would merge into one
    // shorted network, and a source in A's cell would touch B's wire too. A is
    // fed through a BUFFER instead: gates have no ports on their sides.
    protected struct TwoInputCircuit
    {
        public GateBlock Gate, Buffer;
        public LogicSignalSource A, B;
        public LogicWireBlock OutputWire;
        public StateViewerBlock Led;
    }

    protected TwoInputCircuit PlaceTwoInputCircuit(GateType type, int a = 0, int b = 0)
    {
        var circuit = new TwoInputCircuit
        {
            Gate = Gate(type, 5, 0, 5, 90f),
            A = Source(3, 0, 5, a),
            Buffer = Gate(GateType.Buffer, 4, 0, 5, 90f),
            B = Source(3, 0, 4, b),
        };
        Wire(4, 0, 4);
        circuit.OutputWire = Wire(7, 0, 5);
        circuit.Led = Led(8, 0, 5);
        return circuit;
    }

    protected static int NodeOf(Netlist netlist, LogicSignalBlock block)
    {
        for (int node = 0; node < netlist.NodeCount; node++)
        {
            if (netlist.NodeBlock(node) == block) return node;
        }
        return -1;
    }

    protected static bool HasConnection(Netlist netlist, LogicSignalBlock from, LogicSignalBlock to, int pin)
        => netlist.Connections.Contains(new NetlistConnection(NodeOf(netlist, from), NodeOf(netlist, to), pin));
}

public class NetlistBuilderBlockTests : PlacedBlockFixture
{
    [Test]
    public void EachBlockType_BecomesTheRightNode()
    {
        LogicSignalSource source = Source(0, 0, 0);
        Wire(1, 0, 0);
        GateBlock gate = Gate(GateType.Nand, 5, 0, 5, 0f);
        StateViewerBlock led = Led(9, 0, 9);

        Netlist netlist = NetlistBuilder.FromBlocks(AllPlaced());

        Assert.AreEqual(3, netlist.NodeCount, "wires are not nodes");
        Assert.AreEqual(GateType.Source, (GateType)netlist.Nodes[NodeOf(netlist, source)].Type);
        Assert.AreEqual(GateType.Nand, (GateType)netlist.Nodes[NodeOf(netlist, gate)].Type);
        Assert.AreEqual(GateType.Output, (GateType)netlist.Nodes[NodeOf(netlist, led)].Type);
    }

    // The real GatePorts table on a rotated gate (PlaceTwoInputCircuit). At yaw
    // 90 the AND faces +X; input A is on the anchor cell's -X face, input B on
    // the -Z cell's -X face, and the output on the +X cell's +X face (see
    // GateBlockTests).
    [Test]
    public void RotatedGate_InputsLandOnTheRightPins()
    {
        TwoInputCircuit c = PlaceTwoInputCircuit(GateType.And);

        Netlist netlist = NetlistBuilder.FromBlocks(AllPlaced());

        Assert.IsTrue(HasConnection(netlist, c.A, c.Buffer, 0), "source A feeds the buffer directly");
        Assert.IsTrue(HasConnection(netlist, c.Buffer, c.Gate, 0), "buffer on the anchor cell feeds pin 0 (A)");
        Assert.IsTrue(HasConnection(netlist, c.B, c.Gate, 1), "wire on the -Z cell feeds pin 1 (B)");
        Assert.IsTrue(HasConnection(netlist, c.Gate, c.Led, 0), "output on the +X cell reaches the LED");
        Assert.AreEqual(4, netlist.Connections.Length);
        Assert.IsEmpty(netlist.Warnings);
    }

    // The lookup table the driver uses: each network lists its wire blocks, and
    // its driver is the source's node.
    [Test]
    public void LookupTable_PointsBackAtTheBlocks()
    {
        LogicSignalSource source = Source(0, 0, 0);
        LogicWireBlock w1 = Wire(1, 0, 0);
        LogicWireBlock w2 = Wire(2, 0, 0);
        Led(3, 0, 0);

        Netlist netlist = NetlistBuilder.FromBlocks(AllPlaced());

        WireNetwork network = netlist.Networks.Single();
        CollectionAssert.AreEquivalent(new LogicSignalBlock[] { w1, w2 }, network.WireParts.Select(p => netlist.Blocks[p]));
        Assert.AreSame(source, netlist.NodeBlock(network.DriverNode));
    }

    // The same world gives the same netlist whatever order the blocks arrive in
    // (the registry is a Dictionary, so its order is not something to rely on).
    [Test]
    public void BlockOrder_DoesNotChangeTheNetlist()
    {
        Source(0, 0, 0);
        Wire(1, 0, 0);
        Gate(GateType.Not, 2, 0, 0, 90f);
        Wire(3, 0, 0);
        Led(4, 0, 0);

        List<LogicSignalBlock> blocks = AllPlaced().ToList();
        Netlist forward = NetlistBuilder.FromBlocks(blocks);
        blocks.Reverse();
        Netlist backward = NetlistBuilder.FromBlocks(blocks);

        CollectionAssert.AreEqual(forward.Nodes, backward.Nodes);
        CollectionAssert.AreEqual(forward.Connections, backward.Connections);
    }

    [Test]
    public void GatePrefabLeftAtInvalid_IsSkippedNotThrown()
    {
        Source(0, 0, 0);
        Gate(GateType.Invalid, 1, 0, 0, 0f);

        Netlist netlist = null;
        Assert.DoesNotThrow(() => netlist = NetlistBuilder.FromBlocks(AllPlaced()));
        Assert.AreEqual(1, netlist.NodeCount);
        Assert.AreEqual(1, netlist.Warnings.Count);
        StringAssert.Contains("Invalid", netlist.Warnings[0]);
    }

    [Test]
    public void NullAndDestroyedBlocks_AreSkipped()
    {
        LogicSignalSource source = Source(0, 0, 0);
        StateViewerBlock led = Led(1, 0, 0);
        LogicWireBlock destroyed = Wire(5, 0, 0);
        Object.DestroyImmediate(destroyed.gameObject);

        Netlist netlist = NetlistBuilder.FromBlocks(new LogicSignalBlock[] { source, null, destroyed, led });

        Assert.AreEqual(2, netlist.NodeCount);
        Assert.AreEqual(0, netlist.Networks.Length);
        Assert.IsTrue(HasConnection(netlist, source, led, 0));
    }
}

// Blocks -> netlist -> native engine -> values, as the simulation driver (#82)
// will do it. Windows editor only.
public class NetlistToEngineTests : PlacedBlockFixture
{
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

    // Evaluates the placed world and returns each block's value.
    private Dictionary<LogicSignalBlock, int> Evaluate(out Netlist netlist, out CircuitResult result)
    {
        netlist = NetlistBuilder.FromBlocks(AllPlaced());

        var states = new int[netlist.NodeCount];
        for (int node = 0; node < netlist.NodeCount; node++)
        {
            if (netlist.NodeBlock(node) is LogicSignalSource source) states[node] = source.SignalState;
        }

        result = LogicEngine.EvaluateCircuit(netlist.Nodes, netlist.Connections, states);

        var values = new Dictionary<LogicSignalBlock, int>();
        for (int node = 0; node < netlist.NodeCount; node++) values[netlist.NodeBlock(node)] = states[node];
        return values;
    }

    private Dictionary<LogicSignalBlock, int> Evaluate() => Evaluate(out _, out _);

    // TC-3.4.1 / TC-3.4.2: a high (or low) source at one end of a wire reaches
    // the LED at the other end.
    [TestCase(1, TestName = "TC_3_4_1_HighSourceThroughWire_LightsLed")]
    [TestCase(0, TestName = "TC_3_4_2_LowSourceThroughWire_LeavesLedOff")]
    public void SourceThroughWire_ReachesLed(int sourceState)
    {
        Source(0, 0, 0, sourceState);
        Wire(1, 0, 0);
        Wire(2, 0, 0);
        StateViewerBlock led = Led(3, 0, 0);

        Assert.AreEqual(sourceState, Evaluate()[led]);
    }

    // TC-3.4.4: toggling the source and re-evaluating toggles the LED.
    [Test]
    public void TC_3_4_4_TogglingTheSource_TogglesTheLed()
    {
        LogicSignalSource source = Source(0, 0, 0, 0);
        Wire(1, 0, 0);
        StateViewerBlock led = Led(2, 0, 0);

        Assert.AreEqual(0, Evaluate()[led], "before");
        source.ToggleState();
        Assert.AreEqual(1, Evaluate()[led], "after one toggle");
        source.ToggleState();
        Assert.AreEqual(0, Evaluate()[led], "after two toggles");
    }

    // The demo circuit: two sources into a gate, the gate wired to an LED,
    // across every input combination, for a rotated gate (PlaceTwoInputCircuit).
    [TestCase(GateType.And)]
    [TestCase(GateType.Or)]
    [TestCase(GateType.Xor)]
    [TestCase(GateType.Nand)]
    public void DemoCircuit_TwoSourcesGateLed_MatchesTruthTable(GateType type)
    {
        TwoInputCircuit c = PlaceTwoInputCircuit(type);

        for (int bits = 0; bits < 4; bits++)
        {
            int av = bits & 1, bv = (bits >> 1) & 1;
            c.A.SetState(av);
            c.B.SetState(bv);

            Dictionary<LogicSignalBlock, int> values = Evaluate();

            int expected = LogicEngine.Evaluate(type, new[] { av, bv });
            Assert.AreEqual(expected, values[c.Gate], $"{type}({av}, {bv}) gate");
            Assert.AreEqual(expected, values[c.Led], $"{type}({av}, {bv}) LED");
        }
    }

    // TC-3.4.5 end to end: a shorted network is flagged and the engine is never
    // handed the bad connection, so evaluation still succeeds.
    [Test]
    public void TC_3_4_5_ShortedNetwork_IsFlaggedAndEvaluationStillRuns()
    {
        Source(0, 0, 0, 1);
        Wire(1, 0, 0);
        Source(2, 0, 0, 0);
        StateViewerBlock led = Led(1, 0, 1);

        Dictionary<LogicSignalBlock, int> values = null;
        Netlist netlist = null;
        Assert.DoesNotThrow(() => values = Evaluate(out netlist, out _));

        Assert.IsTrue(netlist.Networks.Single().HasMultipleDrivers);
        Assert.AreEqual(0, values[led], "an LED on a shorted network reads 0");
    }
}