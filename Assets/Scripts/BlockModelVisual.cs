using UnityEngine;

// State visuals share the approved art materials; they never tint a whole body.
public class BlockModelVisual : MonoBehaviour
{
    public GameObject lowModel;
    public GameObject highModel;
    public Renderer outputPin;
    public Material lowMaterial;
    public Material highMaterial;
    private LogicSignalBlock signal;
    private int previous = -1;

    private void Awake() => signal = GetComponent<LogicSignalBlock>();
    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (signal == null) signal = GetComponent<LogicSignalBlock>();
        int state = signal != null ? signal.SignalState : 0;
        if (state == previous) return;
        previous = state;
        if (lowModel != null) lowModel.SetActive(state == 0);
        if (highModel != null) highModel.SetActive(state == 1);
        if (outputPin != null) outputPin.sharedMaterial = state == 0 ? lowMaterial : highMaterial;
    }
}
