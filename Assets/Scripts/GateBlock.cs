// GateBlock.cs — a placed logic gate in the world (#81).
//
// Display only. A gate does NOT compute its own output from its neighbours:
// the simulation driver (#82) evaluates the whole circuit through the logic
// engine and pushes each gate's result in with SetDisplayedState. Computing
// it here as well would give two sources of truth that can disagree.
//
// Where the gate's inputs and output are comes from GatePorts, never from this
// file. WorldPorts() is the one place that combines that table with the block's
// placed position and rotation; the netlist builder (#18) calls it to find
// which wire network touches which port.
//
// Prefab notes:
//   - Keep the prefab ROOT at identity rotation and unit scale. Placement sets
//     the root's rotation, and WorldPorts reads it. Put any model or marker
//     offsets on child objects.
//   - Assign outputIndicator to the output marker's renderer so only that
//     marker changes colour, not the whole body.

using System.Collections.Generic;
using UnityEngine;

public class GateBlock : LogicSignalBlock
{
    [Header("Logic")]
    [SerializeField] private GateType gateType = GateType.Invalid;

    [Header("Visual output")]
    [Tooltip("Renderer recoloured to show the gate's output. Defaults to this object's own renderer.")]
    [SerializeField] private Renderer outputIndicator;
    [SerializeField] private Color lowColor = Color.black;
    [SerializeField] private Color highColor = Color.green;

    private int displayedState;
    private MaterialPropertyBlock propertyBlock;

    /// <summary>
    /// The gate's type. Settable so tests and the netlist builder's tests (#18)
    /// can build gates in code; in the game it comes from the prefab.
    /// </summary>
    public GateType GateType
    {
        get => gateType;
        set => gateType = value;
    }

    /// <summary>The output state last pushed in by the simulation driver.</summary>
    public int DisplayedState => displayedState;

    public override int SignalState => displayedState;

    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveRenderer();
        ApplyVisualState();
    }

    /// <summary>
    /// Shows a new output state. Called by the simulation driver (#82); does
    /// nothing if the state hasn't changed, so the driver can call it freely.
    /// </summary>
    public void SetDisplayedState(int state)
    {
        int next = state == 0 ? 0 : 1;
        if (next == displayedState) return;

        displayedState = next;
        ApplyVisualState();
    }

    /// <summary>
    /// This gate's ports in world grid space. For each port: the grid cell it is
    /// on and the direction its face points. A wire connects to the port if it
    /// occupies <c>cell + face</c>.
    /// </summary>
    public IEnumerable<(GatePort port, Vector3Int cell, Vector3Int face)> WorldPorts()
    {
        Vector3Int anchor = GridPosition;
        Quaternion rotation = transform.rotation;

        foreach (GatePort port in GatePorts.For(gateType))
        {
            (Vector3Int cell, Vector3Int face) = GatePorts.ToWorld(port, anchor, rotation);
            yield return (port, cell, face);
        }
    }

    private void ResolveRenderer()
    {
        if (outputIndicator != null) return;

        // Own renderer first: GetComponentInChildren would also do that, but
        // being explicit keeps a marker child from being picked by accident.
        outputIndicator = GetComponent<Renderer>();
        if (outputIndicator == null)
        {
            outputIndicator = GetComponentInChildren<Renderer>(true);
        }
    }

    private void ApplyVisualState()
    {
        if (outputIndicator == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        Color color = displayedState == 1 ? highColor : lowColor;
        propertyBlock.SetColor("_BaseColor", color); // URP / HDRP Lit
        propertyBlock.SetColor("_Color", color);     // Built-in Standard
        outputIndicator.SetPropertyBlock(propertyBlock);
    }

#if UNITY_EDITOR
    // Catches a gate prefab left at Invalid, or set to Source/Output, when it is
    // edited rather than when someone first tries to wire it up.
    private void OnValidate()
    {
        if (gateType == GateType.Invalid || gateType == GateType.Source || gateType == GateType.Output)
        {
            Debug.LogWarning($"{name}: GateBlock.gateType is {gateType}, which is not a gate. Pick a gate type.", this);
        }
    }
#endif
}
