using UnityEngine;

public class LogicSignalSource : LogicSignalBlock
{
    [SerializeField, Range(0, 1)] private int outputState;

    public override int SignalState => outputState;

    public void SetState(int state)
    {
        outputState = state == 0 ? 0 : 1;
    }

    public void ToggleState()
    {
        outputState = outputState == 0 ? 1 : 0;
    }
}