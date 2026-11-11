// SimulationDriverTests.cs — EditMode tests for the simulation driver (#82, #10)
// and the display-only blocks it drives.
//
// The driver is called directly with blocks placed by the test: in EditMode
// OnEnable never runs, so the grid registry is empty and LateUpdate never fires.
// Simulate(blocks) rebuilds and runs the circuit until it settles (the steady
// state a player sees); Rebuild, Step and Advance drive the unit-delay ticks one
// at a time for the timing tests. In play mode LateUpdate does Rebuild whenever
// CircuitVersion moves and Advance every frame.
//
// The first fixture is pure C# and runs everywhere. The second goes through the
// native plugin and, like LogicEngineTests, runs in Windows and macOS editors. Test
// names carry the Verification Test Inventory IDs they provide evidence for.

using NUnit.Framework;
using UnityEngine;

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
    // A source's value is a simulation input, not part of the circuit's
    // structure (#10): toggling it must not rebuild the netlist. The driver reads
    // it every frame instead.
    [Test]
    public void SourceSetState_ChangesTheValue_WithoutRebuildingTheCircuit()
    {
        LogicSignalSource source = Source(0, 0, 0, 0);

        int before = LogicSignalBlock.CircuitVersion;
        source.SetState(1);
        Assert.AreEqual(1, source.SignalState);
        source.ToggleState();
        Assert.AreEqual(0, source.SignalState);
        Assert.AreEqual(before, LogicSignalBlock.CircuitVersion, "no rebuild for a value change");
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

// Blocks -> driver -> engine -> what the blocks show. Windows and macOS editors.
public class SimulationDriverEngineTests : DriverFixture
{
    [OneTimeSetUp]
    public void RequireNativeSimulation()
    {
        if (!SimulationDriver.SimulationAvailable)
        {
            Assert.Ignore(
                $"Native simulation is unsupported on {Application.platform}; Windows and macOS are supported.");
        }
    }

    // TC-3.4.1: a high source lights the wire and the LED at its far end.
    [Test]
    public void TC_3_4_1_HighSource_PowersWiresAndLed()
    {
        Source(0, 0, 0, 1, 90f);
        LogicWireBlock w1 = Wire(1, 0, 0);
        LogicWireBlock w2 = Wire(2, 0, 0);
        WireTopology.Join(w1, w2);
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
        LogicSignalSource source = Source(0, 0, 0, 1, 90f);
        LogicWireBlock wire = Wire(1, 0, 0);
        StateViewerBlock led = Led(2, 0, 0);

        Simulate();
        Assert.AreEqual(1, led.CurrentState, "on");

        source.ToggleState();
        Simulate();
        Assert.AreEqual(0, wire.SignalState, "off: wire");
        Assert.AreEqual(0, led.CurrentState, "off: LED");
    }

    // The demo: two sources into a gate, the gate's output to an LED
    // (PlaceTwoInputCircuit has the layout).
    [Test]
    public void DemoCircuit_GateAndLedShowTheResult()
    {
        TwoInputCircuit c = PlaceTwoInputCircuit(GateType.And, a: 1, b: 0);

        Simulate();
        Assert.AreEqual(0, c.Gate.DisplayedState, "AND(1, 0)");
        Assert.AreEqual(0, c.Led.CurrentState);

        c.B.SetState(1);
        Simulate();
        Assert.AreEqual(1, c.Gate.DisplayedState, "AND(1, 1)");
        Assert.AreEqual(1, c.OutputWire.SignalState, "the output wire shows the gate's value");
        Assert.AreEqual(1, c.Led.CurrentState);
    }

    // TC-3.4.5: two sources on one network shows the error on every wire of
    // it, and the LED on it reads low. Removing one source clears the error.
    [Test]
    public void TC_3_4_5_ShortedNetwork_ShowsErrorUntilFixed()
    {
        Source(0, 0, 0, 1, 90f);
        LogicWireBlock w1 = Wire(1, 0, 0);
        LogicWireBlock w2 = Wire(2, 0, 0);
        WireTopology.Join(w1, w2);
        LogicSignalSource second = Source(3, 0, 0, 1, 270f);
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
        Source(0, 0, 0, 1, 90f);
        LogicWireBlock near = Wire(1, 0, 0);
        LogicWireBlock middle = Wire(2, 0, 0);
        LogicWireBlock far = Wire(3, 0, 0);
        WireTopology.Join(near, middle);
        WireTopology.Join(middle, far);
        StateViewerBlock led = Led(4, 0, 0);

        Simulate();
        Assert.AreEqual(1, led.CurrentState, "connected");

        Object.DestroyImmediate(middle.gameObject);
        Simulate();
        Assert.AreEqual(1, near.SignalState, "still attached to the source");
        Assert.AreEqual(0, far.SignalState, "cut off");
        Assert.AreEqual(0, led.CurrentState, "cut off");
    }

    // A NOT with its output wired round to its own input is the smallest
    // oscillator: with one tick of gate delay it flips every tick, forever.
    // Before #10 the loop was reported and read low.
    [Test]
    public void FeedbackLoop_NotIntoItself_OscillatesEveryTick()
    {
        // NOT facing +X with its output wired round to its own input.
        GateBlock not = Gate(GateType.Not, 0, 0, 0, 90f);
        var wires = new[] { Wire(1, 0, 0), Wire(1, 0, 1), Wire(0, 0, 1), Wire(-1, 0, 1), Wire(-1, 0, 0) };
        for (int i = 1; i < wires.Length; i++) WireTopology.Join(wires[i - 1], wires[i]);

        Driver.Rebuild(AllPlaced());
        Assert.AreEqual(CircuitResult.HasFeedbackLoop, Driver.LastResult, "the loop is still reported");
        Assert.AreEqual(0, not.DisplayedState, "power-on");

        Driver.Step();
        Assert.AreEqual(1, not.DisplayedState, "tick 1");
        Assert.AreEqual(1, wires[0].SignalState, "the loop wire follows the gate");
        Driver.Step();
        Assert.AreEqual(0, not.DisplayedState, "tick 2");
        Driver.Step();
        Assert.AreEqual(1, not.DisplayedState, "tick 3");

        Assert.AreEqual(-1, Driver.Settle(), "an oscillator never settles");
        Assert.IsFalse(Driver.IsSettled);
    }
}

// The unit-delay tick as the player sees it (#10): one tick of delay per gate,
// none for sources, wires and LEDs; the tick rate; settling; and state kept
// across edits. Windows and macOS editors.
public class SimulationTickTests : DriverFixture
{
    [OneTimeSetUp]
    public void RequireNativeSimulation()
    {
        if (!SimulationDriver.SimulationAvailable)
        {
            Assert.Ignore(
                $"Native simulation is unsupported on {Application.platform}; Windows and macOS are supported.");
        }
    }

    // PlaceTwoInputCircuit: A -> BUFFER -> gate pin 0, B -> wire -> gate pin 1,
    // gate -> wire -> LED. So B reaches the LED through one gate, A through two.
    [Test]
    public void EachGateAddsOneTick_WiresAndLedsAddNone()
    {
        TwoInputCircuit c = PlaceTwoInputCircuit(GateType.And, a: 1, b: 0);
        Simulate();
        Assert.AreEqual(0, c.Led.CurrentState, "AND(1, 0)");

        // B rises: one gate between B and the LED.
        c.B.SetState(1);
        Driver.Step();
        Assert.AreEqual(1, c.Gate.DisplayedState, "the gate responds on the first tick");
        Assert.AreEqual(1, c.OutputWire.SignalState, "its output wire with it");
        Assert.AreEqual(1, c.Led.CurrentState, "and the LED with it");
        Assert.AreEqual(0, Driver.Step(), "then nothing more changes");

        // A falls: two gates (the BUFFER, then the AND) between A and the LED.
        c.A.SetState(0);
        Driver.Step();
        Assert.AreEqual(0, c.Buffer.DisplayedState, "tick 1: the BUFFER has fallen");
        Assert.AreEqual(1, c.Led.CurrentState, "tick 1: the AND still sees the BUFFER's old value");
        Driver.Step();
        Assert.AreEqual(0, c.Gate.DisplayedState, "tick 2: the AND falls");
        Assert.AreEqual(0, c.Led.CurrentState, "tick 2: the LED with it");
    }

    [Test]
    public void SourceChange_ShowsOnItsWiresAtOnce_AndReachesGatesOneTickLater()
    {
        LogicSignalSource source = Source(0, 0, 0, 0, 90f);
        LogicWireBlock wire = Wire(1, 0, 0);
        StateViewerBlock directLed = Led(2, 0, 0);
        TwoInputCircuit c = PlaceTwoInputCircuit(GateType.Or, a: 0, b: 0);
        Simulate();
        Driver.TicksPerSecond = 10f;

        source.SetState(1);
        c.B.SetState(1);
        Driver.Advance(0f);
        Assert.AreEqual(1, wire.SignalState, "a wire on a source has no delay");
        Assert.AreEqual(1, directLed.CurrentState, "nor does an LED on a source");
        Assert.AreEqual(0, c.Gate.DisplayedState, "the gate has not ticked yet");

        Driver.Advance(0.05f);
        Assert.AreEqual(0, c.Gate.DisplayedState, "half a tick at 10 ticks/s: still not");
        Driver.Advance(0.06f);
        Assert.AreEqual(1, c.Gate.DisplayedState, "a full tick after the change: OR(0, 1)");
    }

    [Test]
    public void SettledCircuit_StopsTicking_UntilASourceChanges()
    {
        TwoInputCircuit c = PlaceTwoInputCircuit(GateType.And, a: 1, b: 1);
        Simulate();
        Assert.IsTrue(Driver.IsSettled);

        long before = Driver.TickCount;
        Driver.Advance(1f);
        Assert.AreEqual(before, Driver.TickCount, "nothing to do: no ticks");

        c.B.SetState(0);
        Driver.Advance(1f);
        Assert.Greater(Driver.TickCount, before, "a source change wakes it");
        Assert.IsTrue(Driver.IsSettled, "and it settles again");
        Assert.AreEqual(0, c.Led.CurrentState);
    }

    [Test]
    public void LongFrame_RunsAtMostEightTicks()
    {
        GateBlock not = Gate(GateType.Not, 0, 0, 0, 90f);
        var wires = new[] { Wire(1, 0, 0), Wire(1, 0, 1), Wire(0, 0, 1), Wire(-1, 0, 1), Wire(-1, 0, 0) };
        for (int i = 1; i < wires.Length; i++) WireTopology.Join(wires[i - 1], wires[i]);
        Driver.Rebuild(AllPlaced());
        Driver.TicksPerSecond = 20f;

        long before = Driver.TickCount;
        Driver.Advance(1f);
        Assert.AreEqual(8, (int)(Driver.TickCount - before),
            "a one-second frame is 20 ticks due, capped at maxTicksPerFrame (8) so a hitch never snowballs");
    }

    // Editing the circuit rebuilds the netlist, which renumbers every node. Each
    // block must keep its value across that, or every latch in the world would
    // reset whenever the player placed a block anywhere.
    [Test]
    public void Rebuild_KeepsEveryBlocksValue_NewBlocksStartLow()
    {
        TwoInputCircuit c = PlaceTwoInputCircuit(GateType.And, a: 1, b: 1);
        Simulate();
        Assert.AreEqual(1, c.Gate.DisplayedState);

        Wire(30, 0, 30);
        GateBlock newNot = Gate(GateType.Not, 20, 0, 20, 90f);
        Driver.Rebuild(AllPlaced());

        Assert.AreEqual(1, c.Buffer.DisplayedState, "kept without a tick");
        Assert.AreEqual(1, c.Gate.DisplayedState, "kept without a tick");
        Assert.AreEqual(1, c.Led.CurrentState, "kept without a tick");
        Assert.AreEqual(0, newNot.DisplayedState, "a new gate powers on low");

        Driver.Step();
        Assert.AreEqual(1, newNot.DisplayedState, "and responds on its first tick (unwired input reads 0)");
        Assert.AreEqual(1, c.Gate.DisplayedState, "the rest is unchanged");
    }
}
