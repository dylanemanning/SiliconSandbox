using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class WireConnectivityTests : PlacedBlockFixture
{
    internal static int XMask => LogicWireBlock.GetDirectionBit(Vector3Int.right)|LogicWireBlock.GetDirectionBit(Vector3Int.left);
    internal static int ZMask => LogicWireBlock.GetDirectionBit(Vector3Int.forward)|LogicWireBlock.GetDirectionBit(Vector3Int.back);
    internal static WireNetwork Network(Netlist n,LogicWireBlock wire)
    {
        int part=Enumerable.Range(0,n.Blocks.Count).Single(i=>n.Blocks[i]==wire);
        return n.Networks[n.NetworkOfPart[part]];
    }
    [Test]
    public void SourceMustFaceAnExposedWireEndpointAndEndpointEditsDirtyTheCircuit()
    {
        var source=Source(0,0,0,1,90);var wire=Wire(1,0,0);wire.SetEndpointMask(ZMask);
        Assert.That(Network(NetlistBuilder.FromBlocks(AllPlaced()),wire).HasNoDriver,Is.True,"A source beside a wire's insulated side must not drive it.");
        int before=LogicSignalBlock.CircuitVersion;
        wire.AddEndpointTowards(Vector3Int.left);
        Assert.That(LogicSignalBlock.CircuitVersion,Is.GreaterThan(before));
        Assert.That(Network(NetlistBuilder.FromBlocks(AllPlaced()),wire).HasNoDriver,Is.False);
    }
    [Test]
    public void ParallelAndOneSidedArmsStaySeparateUntilTheirGeometryMeets()
    {
        var a=Wire(0,0,0);a.SetEndpointMask(ZMask);
        var b=Wire(1,0,0);b.SetEndpointMask(ZMask);
        Assert.That(NetlistBuilder.FromBlocks(AllPlaced()).Networks.Length,Is.EqualTo(2));
        a.AddEndpointTowards(Vector3Int.right);
        Assert.That(a.ConnectsTo(b,b.GridPosition),Is.False);
        Assert.That(NetlistBuilder.FromBlocks(AllPlaced()).Networks.Length,Is.EqualTo(2));
        b.AddEndpointTowards(Vector3Int.left);
        Assert.That(a.ConnectsTo(b,b.GridPosition),Is.True);
        Assert.That(NetlistBuilder.FromBlocks(AllPlaced()).Networks.Length,Is.EqualTo(1));
        WireTopology.Disconnect(a,b);
        Assert.That(NetlistBuilder.FromBlocks(AllPlaced()).Networks.Length,Is.EqualTo(2));
    }
}

public class WireConnectivitySimulationTests : DriverFixture
{
    [OneTimeSetUp]
    public void RequireWindowsEditor()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor)
            Assert.Ignore("Native signal evaluation requires the committed Windows SiliconPlugin.dll.");
    }

    [TestCase(GateType.And)] [TestCase(GateType.Nand)]
    [TestCase(GateType.Or)] [TestCase(GateType.Nor)]
    [TestCase(GateType.Xor)] [TestCase(GateType.Xnor)]
    public void ParallelInputWiresKeepIndependentSourcesForEveryTruthTable(GateType type)
    {
        var a=Source(3,0,5,0,90);var b=Source(3,0,4,0,90);
        var wa=Wire(4,0,5);wa.SetEndpointMask(WireConnectivityTests.XMask);
        var wb=Wire(4,0,4);wb.SetEndpointMask(WireConnectivityTests.XMask);
        var gate=Gate(type,5,0,5,90);
        var output=Wire(7,0,5);output.SetEndpointMask(WireConnectivityTests.XMask);
        var led=Led(8,0,5);
        Assert.That(wa.ConnectsTo(wb,wb.GridPosition),Is.False);
        for(int bits=0;bits<4;bits++)
        {
            int av=bits>>1,bv=bits&1;a.SetState(av);b.SetState(bv);
            var netlist=Simulate();
            Assert.That(WireConnectivityTests.Network(netlist,wa),Is.Not.SameAs(WireConnectivityTests.Network(netlist,wb)),"Disconnected side-by-side wires must be separate networks.");
            Assert.That(wa.IsError||wb.IsError,Is.False,"Independent sources must not be shorted.");
            Assert.That(wa.SignalState,Is.EqualTo(av));Assert.That(wb.SignalState,Is.EqualTo(bv));
            int expected=LogicEngine.Evaluate(type,new[]{av,bv});
            Assert.That(gate.DisplayedState,Is.EqualTo(expected),type+" inputs "+bits);
            Assert.That(output.SignalState,Is.EqualTo(expected));Assert.That(led.CurrentState,Is.EqualTo(expected));
        }
    }
}
