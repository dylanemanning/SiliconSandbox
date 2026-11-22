using UnityEngine;

public class LogicSignalSource : LogicSignalBlock
{
    [SerializeField, Range(0, 1)] private int outputState;

    public override bool HasConnection(Vector3Int cell, Vector3Int face)
    {
        Vector3Int forward = Vector3Int.RoundToInt(transform.forward);
        return cell == GridPosition && (face == forward || face == -forward);
    }

    public override int SignalState => outputState;

    /// <summary>
    /// Sets the source's value. Anything non-zero is 1.
    ///
    /// This does not mark the circuit changed: a source's value is an input to
    /// the running simulation, not part of the circuit's structure. The
    /// SimulationDriver reads every source each frame, so wires and LEDs on the
    /// source follow at once and the gates it feeds respond on the next ticks,
    /// without rebuilding the netlist (#10).
    /// </summary>
    public void SetState(int state)
    {
        outputState = state == 0 ? 0 : 1;
    }

    public void ToggleState()
    {
        SetState(outputState == 0 ? 1 : 0);
    }

    // Changing Output State in the Inspector during play also works: the
    // SimulationDriver reads it like any other source change.
}
