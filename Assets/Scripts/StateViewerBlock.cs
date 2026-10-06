// StateViewerBlock.cs — an LED.
//
// Display only (#82). The LED no longer reads its neighbours every frame: the
// simulation driver evaluates the whole circuit through the logic engine and
// pushes the LED's value in with SetDisplayedState.

using UnityEngine;

public class StateViewerBlock : LogicSignalBlock
{
    [Header("Visual output")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color lowColor = Color.black;
    [SerializeField] private Color highColor = Color.green;

    private int currentState;
    private MaterialPropertyBlock propertyBlock;

    public int CurrentState => currentState;
    public override int SignalState => currentState;

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplyVisualState();
    }

    /// <summary>
    /// Shows a new value. Called by the simulation driver (#82); does nothing if
    /// the value hasn't changed, so the driver can call it freely.
    /// </summary>
    public void SetDisplayedState(int state)
    {
        int next = state == 0 ? 0 : 1;
        if (next == currentState) return;

        currentState = next;
        ApplyVisualState();
    }

    private void ResolveRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void ApplyVisualState()
    {
        ResolveRenderer();
        if (targetRenderer == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        propertyBlock.SetColor("_BaseColor", currentState == 1 ? highColor : lowColor);
        propertyBlock.SetColor("_Color", currentState == 1 ? highColor : lowColor);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}
