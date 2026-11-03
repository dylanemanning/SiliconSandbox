// SimulationDriver.cs — runs the placed circuit through the logic engine and
// shows the result on the blocks (#82), as a unit-delay simulation (#10).
//
// The simulation model (see native/SiliconPlugin/README.md, "Simulation model"):
// every gate has exactly one tick of propagation delay, and sources, LEDs and
// wires have none. That is a Verilog gate-level simulation with #1 on every
// gate, so the game shows what real logic does: signals ripple one gate per
// tick, unequal paths glitch, rings of inverters oscillate, latches hold state,
// and a clock faster than the circuit's longest path breaks it.
//
// What happens when:
//   - A block is placed, broken or rewired (LogicSignalBlock.CircuitVersion
//     moves): rebuild the netlist (#18) and load it into the engine. Every node
//     keeps the value it had in the old circuit, so a latch keeps holding while
//     the player builds next to it; new nodes start low (power-on).
//   - A source changes (the player toggles it, or a clock flips): nothing is
//     rebuilt. The new value is written into the state array, which wires and
//     LEDs on that source show at once (no delay), and the first gate responds
//     one tick later.
//   - Time passes: the engine is ticked ticksPerSecond times a second
//     (LogicEngine.Tick). When a tick changes nothing the circuit has settled
//     and ticking stops until a source or the circuit changes again. A circuit
//     that oscillates keeps ticking.
//   - After all of that, only blocks whose value changed are pushed new values:
//     gates and LEDs show their node, each wire its network's driver (low when
//     nothing drives it, the error colour when two outputs drive it).
// Ticks run on scaled game time, so the simulation stops while the game is
// paused (timeScale 0).
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

    [Tooltip("Simulation ticks per second of game time. One tick is one gate's propagation delay, " +
             "so a signal crosses this many gates per second. Redstone runs at 10.")]
    [SerializeField, Min(1f)] private float ticksPerSecond = 20f;

    [Tooltip("Most ticks run in one frame. After a long frame the simulation drops the rest " +
             "instead of trying to catch up, so a hitch never snowballs.")]
    [SerializeField, Min(1)] private int maxTicksPerFrame = 8;

    [Tooltip("Most ticks Simulate() runs while waiting for the circuit to settle. Only reached by " +
             "circuits that oscillate.")]
    [SerializeField, Min(1)] private int settleTickLimit = 1000;

    [Tooltip("Log the netlist builder's warnings (an LED fed twice, a gate left at Invalid...). " +
             "Each distinct set of warnings is logged once, not every rebuild.")]
    [SerializeField] private bool logWarnings = true;

    private int seenVersion = -1;
    private string lastWarnings = string.Empty;
    private bool pluginUnavailable;

    // The circuit and its state. states is indexed by netlist node and is the
    // only copy of the circuit's signal values; the engine keeps just the graph.
    private Netlist netlist;
    private int[] states = Array.Empty<int>();

    // Per node: the node driving it when it is an LED (Output), else -1.
    private int[] outputDriver = Array.Empty<int>();

    // Source nodes and their blocks, read every frame for value changes.
    private int[] sourceNodes = Array.Empty<int>();
    private LogicSignalSource[] sourceBlocks = Array.Empty<LogicSignalSource>();

    // What each block was last shown, so only changes are pushed. -1 = unknown.
    // Networks store value | (error ? 2 : 0).
    private int[] shownNode = Array.Empty<int>();
    private int[] shownNetwork = Array.Empty<int>();

    private bool engineLoaded;   // this circuit is loaded and can be ticked
    private bool engineHolds;    // the plugin holds a circuit we loaded (to unload)
    private bool settled = true;
    private float tickAccumulator;

    /// <summary>The netlist the driver is simulating, for debugging and tests.</summary>
    public Netlist LastNetlist => netlist;

    /// <summary>
    /// How the last load went. HasFeedbackLoop is information only: loops are
    /// simulated.
    /// </summary>
    public CircuitResult LastResult { get; private set; }

    /// <summary>True when the last tick changed nothing, so no more ticks are needed
    /// until a source or the circuit changes. False while signals are moving or a
    /// circuit oscillates.</summary>
    public bool IsSettled => settled;

    /// <summary>Ticks run since the driver started.</summary>
    public long TickCount { get; private set; }

    /// <summary>Simulation ticks per second of game time (one gate delay each).</summary>
    public float TicksPerSecond
    {
        get => ticksPerSecond;
        set => ticksPerSecond = Mathf.Max(1f, value);
    }

    /// <summary>
    /// True on platforms with a shipped native SiliconPlugin: Windows x86_64
    /// and macOS (Apple Silicon or Intel). Unsupported platforms such as the
    /// Linux Unity CI runner skip native evaluation tests.
    /// </summary>
    public static bool SimulationAvailable =>
        Application.platform == RuntimePlatform.WindowsEditor ||
        Application.platform == RuntimePlatform.WindowsPlayer ||
        Application.platform == RuntimePlatform.OSXEditor ||
        Application.platform == RuntimePlatform.OSXPlayer;

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

        // Without a native build, don't try to load the plugin at all. Deciding
        // here, rather than on the first failed call, keeps the "simulation is
        // off" message out of whichever test happens to build the first circuit
        // (PlayMode tests that call LogAssert.NoUnexpectedReceived would fail on
        // it). Batch mode (CI) stays quiet; an interactive editor gets one note.
        if (!SimulationAvailable)
        {
            pluginUnavailable = true;
            if (!Application.isBatchMode)
            {
                Debug.LogWarning("Circuit simulation is off: SiliconPlugin supports Windows x86_64 and macOS only.");
            }
        }
    }

    private void OnDestroy()
    {
        // The editor keeps native plugins loaded between play sessions, so the
        // loaded circuit would otherwise outlive this one.
        if (engineHolds) LogicEngine.UnloadCircuit();
        engineHolds = false;
        if (Instance == this) Instance = null;
    }

    // LateUpdate, so blocks placed or toggled in this frame's Update are
    // already registered. A broken block unregisters when Unity actually
    // destroys it at the end of the frame, so it is picked up next frame.
    private void LateUpdate()
    {
        int version = LogicSignalBlock.CircuitVersion;
        if (version != seenVersion)
        {
            seenVersion = version;
            Rebuild(LogicSignalBlock.All);
        }
        Advance(Time.deltaTime);
    }

    // ---- Public API (LateUpdate uses these; tests call them directly) --------

    /// <summary>
    /// Builds the circuit made of these blocks, runs it until it settles, and
    /// shows the result: the steady state a player sees once signals have
    /// finished moving. Used by tests with blocks created in EditMode (where the
    /// grid registry stays empty and LateUpdate never runs). A circuit that
    /// oscillates stops after settleTickLimit ticks, with IsSettled false.
    /// </summary>
    public Netlist Simulate(IEnumerable<LogicSignalBlock> blocks)
    {
        Rebuild(blocks);
        Settle();
        return netlist;
    }

    /// <summary>
    /// Rebuilds the netlist from these blocks and loads it into the engine,
    /// carrying every surviving block's value over from the old circuit. Shows
    /// the new circuit without ticking it. Called when the circuit's structure
    /// changes.
    /// </summary>
    public Netlist Rebuild(IEnumerable<LogicSignalBlock> blocks)
    {
        Netlist previous = netlist;
        int[] previousStates = states;

        netlist = NetlistBuilder.FromBlocks(blocks);
        ReportWarnings(netlist);

        int nodeCount = netlist.NodeCount;
        states = new int[nodeCount];
        CarryOver(previous, previousStates);
        IndexNodes();

        shownNode = new int[nodeCount];
        shownNetwork = new int[netlist.Networks.Length];
        Fill(shownNode, -1);
        Fill(shownNetwork, -1);

        LoadIntoEngine();
        ReadSources();
        // LEDs have no delay, so one that was just connected or cut off shows
        // its new net at once, not on the next tick. (Gates keep their carried
        // value until they tick: that is their delay.)
        SyncOutputs();
        Apply();
        return netlist;
    }

    /// <summary>
    /// Runs exactly this many ticks now, regardless of the tick rate, then shows
    /// the result. Source values are read first. Returns how many nodes the last
    /// tick changed (0 = settled), or 0 when there is nothing to tick.
    /// </summary>
    public int Step(int ticks = 1)
    {
        ReadSources();
        int changed = 0;
        for (int i = 0; i < ticks && engineLoaded; i++)
        {
            changed = TickOnce();
        }
        Apply();
        return changed;
    }

    /// <summary>
    /// Ticks until the circuit settles or settleTickLimit is reached, then shows
    /// the result. Returns the number of ticks that changed something (the
    /// circuit's settling time), or -1 if it never settled (it oscillates).
    /// </summary>
    public int Settle()
    {
        ReadSources();
        int ticks = 0;
        int result = 0;
        while (engineLoaded)
        {
            if (TickOnce() == 0) break;
            ticks++;
            if (ticks >= settleTickLimit)
            {
                result = -1;
                break;
            }
        }
        tickAccumulator = 0f;
        Apply();
        return result < 0 ? -1 : ticks;
    }

    /// <summary>
    /// Lets this much game time pass: reads the sources and runs the ticks that
    /// fall due at ticksPerSecond (at most maxTicksPerFrame), then shows the
    /// result. LateUpdate calls it with Time.deltaTime.
    /// </summary>
    public void Advance(float seconds)
    {
        bool inputsChanged = ReadSources();
        if (inputsChanged && settled)
        {
            // The circuit was idle. Start the tick clock now, so the first gate
            // responds a full gate delay after the input changed rather than at
            // whatever moment the old clock would next have ticked.
            settled = false;
            tickAccumulator = 0f;
        }

        if (engineLoaded && !settled)
        {
            tickAccumulator = Mathf.Min(tickAccumulator + seconds * ticksPerSecond, maxTicksPerFrame);
            while (tickAccumulator >= 1f && engineLoaded)
            {
                tickAccumulator -= 1f;
                TickOnce();
                if (settled)
                {
                    tickAccumulator = 0f;
                    break;
                }
            }
        }
        else
        {
            tickAccumulator = 0f;
        }

        Apply();
    }

    // ---- Internals -------------------------------------------------------------

    // Keep the value of every node whose block is still in the circuit, so an
    // edit elsewhere doesn't reset a latch. Node indices change on every
    // rebuild, so the match is by block.
    private void CarryOver(Netlist previous, int[] previousStates)
    {
        if (previous == null || previousStates.Length == 0) return;

        var carried = new Dictionary<LogicSignalBlock, int>();
        int count = Math.Min(previous.NodeCount, previousStates.Length);
        for (int node = 0; node < count; node++)
        {
            LogicSignalBlock block = previous.NodeBlock(node);
            if (block != null) carried[block] = previousStates[node];
        }

        for (int node = 0; node < netlist.NodeCount; node++)
        {
            LogicSignalBlock block = netlist.NodeBlock(node);
            if (block != null && carried.TryGetValue(block, out int value)) states[node] = value;
        }
    }

    private void IndexNodes()
    {
        int nodeCount = netlist.NodeCount;
        outputDriver = new int[nodeCount];
        Fill(outputDriver, -1);
        foreach (NetlistConnection connection in netlist.Connections)
        {
            // A node's id is its index (NetlistBuilder).
            if (netlist.Nodes[connection.ToId].Type == (int)GateType.Output)
            {
                outputDriver[connection.ToId] = connection.FromId;
            }
        }

        var nodes = new List<int>();
        var blocks = new List<LogicSignalSource>();
        for (int node = 0; node < nodeCount; node++)
        {
            if (netlist.NodeBlock(node) is LogicSignalSource source)
            {
                nodes.Add(node);
                blocks.Add(source);
            }
        }
        sourceNodes = nodes.ToArray();
        sourceBlocks = blocks.ToArray();
    }

    private void LoadIntoEngine()
    {
        engineLoaded = false;
        settled = true;
        tickAccumulator = 0f;
        LastResult = CircuitResult.Complete;

        // No sources, gates or LEDs: nothing to simulate, and no reason to load
        // the native plugin. This also keeps the menu and the tests on the Linux
        // CI runner (which has no plugin) from touching it.
        if (netlist.NodeCount == 0 || pluginUnavailable || !SimulationAvailable)
        {
            if (engineHolds)
            {
                LogicEngine.UnloadCircuit();
                engineHolds = false;
            }
            Array.Clear(states, 0, states.Length);
            return;
        }

        try
        {
            LastResult = LogicEngine.LoadCircuit(netlist.Nodes, netlist.Connections);
            engineLoaded = true;
            engineHolds = true;
            settled = false;  // at least one tick to find out
            return;
        }
        catch (LogicEngineException e) when (e.InnerException is DllNotFoundException)
        {
            // A supported platform with a missing plugin is a broken install.
            // Log once and stop calling it; never report this as a passing
            // native simulation test.
            pluginUnavailable = true;
            Debug.LogError("Circuit simulation is off: " + e.Message);
        }
        catch (LogicEngineException e)
        {
            // A stale DLL or a netlist the engine rejected. Both are bugs, so this
            // one is an error. A failed load leaves nothing loaded in the plugin.
            Debug.LogError("Circuit could not be loaded: " + e.Message);
        }

        engineHolds = false;
        Array.Clear(states, 0, states.Length);
    }

    // One engine tick. Returns how many nodes changed and sets settled from it.
    // A failure is logged and stops the simulation until the next rebuild.
    private int TickOnce()
    {
        try
        {
            int changed = LogicEngine.Tick(states);
            TickCount++;
            settled = changed == 0;
            return changed;
        }
        catch (LogicEngineException e)
        {
            Debug.LogError("Circuit simulation stopped: " + e.Message);
            engineLoaded = false;
            settled = true;
            Array.Clear(states, 0, states.Length);
            return 0;
        }
    }

    // Write each source's current value into the state array. Returns whether
    // any changed. LEDs follow at once: they have no delay.
    private bool ReadSources()
    {
        if (!engineLoaded) return false;

        bool changed = false;
        for (int i = 0; i < sourceNodes.Length; i++)
        {
            LogicSignalSource source = sourceBlocks[i];
            if (source == null) continue;  // broken this frame; the rebuild follows

            int value = source.SignalState != 0 ? 1 : 0;
            int node = sourceNodes[i];
            if (states[node] != value)
            {
                states[node] = value;
                changed = true;
            }
        }

        if (changed) SyncOutputs();
        return changed;
    }

    // An LED shows the net it sits on with no delay, exactly as the engine
    // computes it during a tick. Done here too so an LED on a source lights the
    // moment the source flips, before the next tick.
    private void SyncOutputs()
    {
        for (int node = 0; node < outputDriver.Length; node++)
        {
            if (netlist.Nodes[node].Type != (int)GateType.Output) continue;
            int driver = outputDriver[node];
            states[node] = driver >= 0 ? states[driver] : 0;
        }
    }

    private void Apply()
    {
        if (netlist == null) return;

        for (int node = 0; node < netlist.NodeCount; node++)
        {
            int value = states[node];
            if (shownNode[node] == value) continue;

            LogicSignalBlock block = netlist.NodeBlock(node);
            if (block == null) continue;  // destroyed; the rebuild follows
            shownNode[node] = value;

            switch (block)
            {
                case GateBlock gate:
                    gate.SetDisplayedState(value);
                    break;
                case StateViewerBlock led:
                    led.SetDisplayedState(value);
                    break;
            }
        }

        for (int i = 0; i < netlist.Networks.Length; i++)
        {
            WireNetwork network = netlist.Networks[i];
            int value = network.DriverNode >= 0 ? states[network.DriverNode] : 0;
            bool error = network.HasMultipleDrivers;
            int shown = value | (error ? 2 : 0);
            if (shownNetwork[i] == shown) continue;
            shownNetwork[i] = shown;

            foreach (int part in network.WireParts)
            {
                if (netlist.Blocks[part] is LogicWireBlock wire && wire != null)
                {
                    wire.SetDisplayedState(value, error);
                }
            }
        }
    }

    private void ReportWarnings(Netlist built)
    {
        string warnings = string.Join("\n", built.Warnings);
        if (warnings == lastWarnings) return;

        lastWarnings = warnings;
        if (logWarnings && warnings.Length > 0)
        {
            Debug.LogWarning("Circuit: " + string.Join("\nCircuit: ", built.Warnings));
        }
    }

    private static void Fill(int[] array, int value)
    {
        for (int i = 0; i < array.Length; i++) array[i] = value;
    }
}
