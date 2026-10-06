// SimulationDriver.cs — runs the placed circuit through the logic engine and
// shows the result on the blocks (#82).
//
// Once a frame it checks LogicSignalBlock.CircuitVersion. If a block was placed
// or broken, or a source changed, it:
//   1. builds the netlist from every registered block (NetlistBuilder, #18),
//   2. writes each source's value into the state array,
//   3. evaluates the whole circuit in one native call (LogicEngine.EvaluateCircuit, #12),
//   4. pushes the results out: gates and LEDs show their node's value; each wire
//      shows its network's driver value, low when nothing drives it, and the
//      error colour when two outputs drive it.
// While nothing changes it does nothing at all.
//
// This is the stateless demo version: every change re-evaluates the whole
// circuit from the sources. That is exact for combinational circuits. Feedback
// loops (latches) and the clock need #10's fixed-rate Silicon_Tick with state
// kept between ticks; until then a loop is reported and its gates read low.
//
// One driver exists for the whole game. It creates itself before the first scene
// loads and survives scene changes, so no scene has to contain it (and nobody's
// scene file has to change to add it).

using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SimulationDriver : MonoBehaviour
{
    /// <summary>The running driver, or null outside play mode.</summary>
    public static SimulationDriver Instance { get; private set; }

    [Tooltip("Log the netlist builder's warnings (an LED fed twice, a gate left at Invalid...). " +
             "Each distinct set of warnings is logged once, not every rebuild.")]
    [SerializeField] private bool logWarnings = true;

    private int seenVersion = -1;
    private int[] states = Array.Empty<int>();
    private string lastWarnings = string.Empty;
    private bool loopReported;
    private bool pluginUnavailable;

    /// <summary>The netlist from the most recent evaluation, for debugging and tests.</summary>
    public Netlist LastNetlist { get; private set; }

    /// <summary>How the most recent evaluation went.</summary>
    public CircuitResult LastResult { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnStartup()
    {
        if (Instance != null) return;

        var go = new GameObject("Simulation Driver");
        go.AddComponent<SimulationDriver>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Someone also put one in a scene. One is enough.
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // LateUpdate, so blocks placed or toggled in this frame's Update are
    // already registered. A broken block unregisters when Unity actually
    // destroys it at the end of the frame, so it is picked up next frame.
    private void LateUpdate()
    {
        int version = LogicSignalBlock.CircuitVersion;
        if (version == seenVersion) return;

        seenVersion = version;
        Simulate(LogicSignalBlock.All);
    }

    /// <summary>
    /// Builds, evaluates and displays the circuit made of these blocks. Called
    /// automatically when the circuit changes; public so tests can drive it with
    /// blocks created in EditMode (where the grid registry stays empty).
    /// </summary>
    public Netlist Simulate(IEnumerable<LogicSignalBlock> blocks)
    {
        Netlist netlist = NetlistBuilder.FromBlocks(blocks);
        LastNetlist = netlist;
        ReportWarnings(netlist);

        if (states.Length != netlist.NodeCount) states = new int[netlist.NodeCount];
        for (int node = 0; node < netlist.NodeCount; node++)
        {
            states[node] = netlist.NodeBlock(node) is LogicSignalSource source ? source.SignalState : 0;
        }

        LastResult = Evaluate(netlist);
        Apply(netlist);
        return netlist;
    }

    private CircuitResult Evaluate(Netlist netlist)
    {
        // No sources, gates or LEDs: nothing to evaluate, and no reason to load
        // the native plugin. This also keeps the menu and the PlayMode tests on
        // the Linux CI runner (which has no plugin) from touching it.
        if (netlist.NodeCount == 0) return CircuitResult.Complete;

        if (pluginUnavailable)
        {
            Array.Clear(states, 0, states.Length);
            return CircuitResult.Complete;
        }

        try
        {
            CircuitResult result = LogicEngine.EvaluateCircuit(netlist.Nodes, netlist.Connections, states);

            bool hasLoop = result == CircuitResult.HasFeedbackLoop;
            if (hasLoop && !loopReported)
            {
                Debug.LogWarning(
                    "Circuit has a feedback loop (an output wired back into its own inputs). " +
                    "Loops need the clocked simulation that comes after the demo; for now the " +
                    "gates on the loop read low and the rest of the circuit works normally.");
            }
            loopReported = hasLoop;
            return result;
        }
        catch (LogicEngineException e) when (e.InnerException is DllNotFoundException)
        {
            // Expected on any platform without a native build (the Linux CI
            // runner, a Mac). A warning, not an error, so PlayMode tests there
            // are not failed by it. Logged once; the simulation stays off.
            pluginUnavailable = true;
            Debug.LogWarning("Circuit simulation is off: " + e.Message);
        }
        catch (LogicEngineException e)
        {
            // A stale DLL or a netlist the engine rejected. Both are bugs, so this
            // one is an error. Everything shows low rather than stale values.
            Debug.LogError("Circuit could not be evaluated: " + e.Message);
        }

        Array.Clear(states, 0, states.Length);
        return CircuitResult.Complete;
    }

    private void Apply(Netlist netlist)
    {
        for (int node = 0; node < netlist.NodeCount; node++)
        {
            switch (netlist.NodeBlock(node))
            {
                case GateBlock gate:
                    gate.SetDisplayedState(states[node]);
                    break;
                case StateViewerBlock led:
                    led.SetDisplayedState(states[node]);
                    break;
            }
        }

        foreach (WireNetwork network in netlist.Networks)
        {
            int value = network.DriverNode >= 0 ? states[network.DriverNode] : 0;
            foreach (int part in network.WireParts)
            {
                if (netlist.Blocks[part] is LogicWireBlock wire)
                {
                    wire.SetDisplayedState(value, network.HasMultipleDrivers);
                }
            }
        }
    }

    private void ReportWarnings(Netlist netlist)
    {
        string warnings = string.Join("\n", netlist.Warnings);
        if (warnings == lastWarnings) return;

        lastWarnings = warnings;
        if (logWarnings && warnings.Length > 0)
        {
            Debug.LogWarning("Circuit: " + string.Join("\nCircuit: ", netlist.Warnings));
        }
    }
}
