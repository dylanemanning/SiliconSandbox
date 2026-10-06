// SimulationDriverTests.cs — EditMode tests for the simulation driver (#82) and
// the display-only blocks it drives.
//
// The driver's Simulate(blocks) is called directly with blocks placed by the
// test: in EditMode OnEnable never runs, so the grid registry is empty and
// LateUpdate never fires. In play mode the driver does exactly this with
// LogicSignalBlock.All whenever CircuitVersion moves.
//
// The first fixture is pure C# and runs everywhere. The second goes through the
// native plugin and, like LogicEngineTests, only runs in a Windows editor. Test
// names carry the Verification Test Inventory IDs they provide evidence for.

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public abstract class DriverFixture : PlacedBlockFixture
{
    private GameObject driverObject;

    protected SimulationDriver Driver { get; private set; }

    [SetUp]
    public void CreateDriver()
    {
        driverObject = new GameObject("Test Simulation Driver");
        Driver = driverObject.AddComponent<SimulationDriver>();
    }

    [TearDown]
    public void DestroyDriver()
    {
        if (driverObject != null) Object.DestroyImmediate(driverObject);
    }

    protected Netlist Simulate() => Driver.Simulate(AllPlaced());
}

// Runs everywhere: change tracking and the display-only setters.
public class SimulationDisplayTests : DriverFixture
{
    [Test]
    public void SourceSetState_MarksTheCircuitChanged_OnlyWhenTheValueChanges()
    {
        LogicSignalSource source = Source(0, 0, 0, 0);

        int before = LogicSignalBlock.CircuitVersion;
        source.SetState(0);
        Assert.AreEqual(before, LogicSignalBlock.CircuitVersion, "same value: no change");

        source.SetState(1);
        Assert.AreEqual(before + 1, LogicSignalBlock.CircuitVersion, "0 -> 1");

        source.ToggleState();
        Assert.AreEqual(before + 2, LogicSignalBlock.CircuitVersion, "toggle");
        Assert.AreEqual(0, source.SignalState);
    }

    [Test]
    public void SourceSetState_ClampsToBinary()
    {
        LogicSignalSource source = Source(0, 0, 0, 0);
        source.SetState(7);
        Assert.AreEqual(1, source.SignalState);
    }

    [Test]
    public void Wire_ShowsWhatTheDriverPushes()
    {
        LogicWireBlock wire = Wire(0, 0, 0);
        Assert.AreEqual(0, wire.SignalState, "starts low");
        Assert.IsFalse(wire.IsError);

        wire.SetDisplayedState(1);
        Assert.AreEqual(1, wire.SignalState);

        wire.SetDisplayedState(0, error: true);
        Assert.AreEqual(0, wire.SignalState);
        Assert.IsTrue(wire.IsError);

        wire.SetDisplayedState(1, error: false);
        Assert.IsFalse(wire.IsError, "error clears when the short is fixed");
    }

    [Test]
    public void Led_ShowsWhatTheDriverPushes()
    {
        StateViewerBlock led = Led(0, 0, 0);
        Assert.AreEqual(0, led.CurrentState);

        led.SetDisplayedState(5);
        Assert.AreEqual(1, led.CurrentState, "any non-zero reads high");
        Assert.AreEqual(1, led.SignalState);
    }

    // Wires on their own make no nodes, so the driver never calls the native
    // plugin. This is what keeps the menu and the Linux CI runner off it.
    [Test]
    public void WiresOnly_SimulatesWithoutTheEngine()
    {
        LogicWireBlock a = Wire(0, 0, 0);
        LogicWireBlock b = Wire(1, 0, 0);
        a.SetDisplayedState(1);

        Netlist netlist = null;
        Assert.DoesNotThrow(() => netlist = Simulate());

        Assert.AreEqual(0, netlist.NodeCount);
        Assert.AreEqual(0, a.SignalState, "a wire with no driver is pushed low");
        Assert.AreEqual(0, b.SignalState);
    }
}

// Blocks -> driver -> engine -> what the blocks show. Windows editor only.
public class SimulationDriverEngineTests : DriverFixture
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

    // TC-3.4.1: a high source lights the wire and the LED at its far end.
    [Test]
    public void TC_3_4_1_HighSource_PowersWiresAndLed()
    {
        Source(0, 0, 0, 1);
        LogicWireBlock w1 = Wire(1, 0, 0);
        LogicWireBlock w2 = Wire(2, 0, 0);
        StateViewerBlock led = Led(3, 0, 0);

        Simulate();

        Assert.AreEqual(1, w1.SignalState);
        Assert.AreEqual(1, w2.SignalState);
        Assert.IsFalse(w1.IsError);
        Assert.AreEqual(1, led.CurrentState);
        Assert.AreEqual(CircuitResult.Complete, Driver.LastResult);
    }

    // TC-3.4.4: toggling the source and re-simulating toggles wires and LED.
    [Test]
    public void TC_3_4_4_TogglingTheSource_TogglesWiresAndLed()
    {
        LogicSignalSource source = Source(0, 0, 0, 1);
        LogicWireBlock wire = Wire(1, 0, 0);
        StateViewerBlock led = Led(2, 0, 0);

        Simulate();
        Assert.AreEqual(1, led.CurrentState, "on");

        source.ToggleState();
        Simulate();
        Assert.AreEqual(0, wire.SignalState, "off: wire");
        Assert.AreEqual(0, led.CurrentState, "off: LED");
    }

    // The demo: two sources into a gate, the gate's output to an LED.
    [Test]
    public void DemoCircuit_GateAndLedShowTheResult()
    {
        GateBlock gate = Gate(GateType.And, 5, 0, 5, 90f);     // faces +X; A on +Z, B on -Z
        LogicSignalSource a = Source(5, 0, 7, 1);
        Wire(5, 0, 6);
        LogicSignalSource b = Source(5, 0, 3, 0);
        Wire(5, 0, 4);
        LogicWireBlock output = Wire(6, 0, 5);
        StateViewerBlock led = Led(7, 0, 5);

        Simulate();
        Assert.AreEqual(0, gate.DisplayedState, "AND(1, 0)");
        Assert.AreEqual(0, led.CurrentState);

        b.SetState(1);
        Simulate();
        Assert.AreEqual(1, gate.DisplayedState, "AND(1, 1)");
        Assert.AreEqual(1, output.SignalState, "the output wire shows the gate's value");
        Assert.AreEqual(1, led.CurrentState);
    }

    // TC-3.4.5: two sources on one network shows the error on every wire of
    // it, and the LED on it reads low. Removing one source clears the error.
    [Test]
    public void TC_3_4_5_ShortedNetwork_ShowsErrorUntilFixed()
    {
        Source(0, 0, 0, 1);
        LogicWireBlock w1 = Wire(1, 0, 0);
        LogicWireBlock w2 = Wire(2, 0, 0);
        LogicSignalSource second = Source(3, 0, 0, 1);
        StateViewerBlock led = Led(1, 0, 1);

        Simulate();
        Assert.IsTrue(w1.IsError);
        Assert.IsTrue(w2.IsError);
        Assert.AreEqual(0, led.CurrentState);

        Object.DestroyImmediate(second.gameObject);
        Simulate();
        Assert.IsFalse(w1.IsError, "error clears once only one source is left");
        Assert.IsFalse(w2.IsError);
        Assert.AreEqual(1, led.CurrentState);
    }

    // TC-3.1.4 in the game: breaking a wire cuts the LED off, and the part of
    // the wire still attached to the source stays powered.
    [Test]
    public void TC_3_1_4_BreakingAWire_CutsOffTheRest()
    {
        Source(0, 0, 0, 1);
        LogicWireBlock near = Wire(1, 0, 0);
        LogicWireBlock middle = Wire(2, 0, 0);
        LogicWireBlock far = Wire(3, 0, 0);
        StateViewerBlock led = Led(4, 0, 0);

        Simulate();
        Assert.AreEqual(1, led.CurrentState, "connected");

        Object.DestroyImmediate(middle.gameObject);
        Simulate();
        Assert.AreEqual(1, near.SignalState, "still attached to the source");
        Assert.AreEqual(0, far.SignalState, "cut off");
        Assert.AreEqual(0, led.CurrentState, "cut off");
    }

    [Test]
    public void FeedbackLoop_IsReportedAndReadsLow()
    {
        // NOT facing +X with its output wired round to its own input.
        GateBlock not = Gate(GateType.Not, 0, 0, 0, 90f);
        Wire(1, 0, 0); Wire(1, 0, 1); Wire(0, 0, 1); Wire(-1, 0, 1); Wire(-1, 0, 0);

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("feedback loop"));
        Simulate();

        Assert.AreEqual(CircuitResult.HasFeedbackLoop, Driver.LastResult);
        Assert.AreEqual(0, not.DisplayedState);
    }
}
