using UnityEngine;

public class LogicSignalSource : LogicSignalBlock
{
    [SerializeField, Range(0, 1)] private int outputState;

    public override int SignalState => outputState;

    /// <summary>
    /// Sets the source's value. Anything non-zero is 1. Re-evaluates the circuit
    /// (via MarkCircuitChanged) only when the value actually changes.
    /// </summary>
    public void SetState(int state)
    {
        int next = state == 0 ? 0 : 1;
        if (next == outputState) return;

        outputState = next;
        MarkCircuitChanged();
    }

    public void ToggleState()
    {
        SetState(outputState == 0 ? 1 : 0);
    }

#if UNITY_EDITOR
    // Changing outputState in the Inspector during play updates the circuit
    // immediately, which is handy for testing a circuit by hand.
    private void OnValidate()
    {
        MarkCircuitChanged();
    }
#endif
}
