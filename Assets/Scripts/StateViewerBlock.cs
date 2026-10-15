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
        ResolveRenderer();
        propertyBlock = new MaterialPropertyBlock();
        ApplyVisualState();
    }

    protected override void Update()
    {
        base.Update();
        ResolveRenderer();
        currentState = ReadAdjacentState();
        ApplyVisualState();
    }

    private void ResolveRenderer()
    {
        if (GetComponent<BlockModelVisual>() != null) return;
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void ApplyVisualState()
    {
        if (GetComponent<BlockModelVisual>() != null || targetRenderer == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        propertyBlock.SetColor("_BaseColor", currentState == 1 ? highColor : lowColor);
        propertyBlock.SetColor("_Color", currentState == 1 ? highColor : lowColor);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private int ReadAdjacentState()
    {
        int state = 0;
        foreach (Vector3Int neighborPosition in Neighbors(GridPosition))
        {
            LogicSignalBlock neighbor = At(neighborPosition);
            if (neighbor != null && neighbor.CanDrive(neighborPosition, GridPosition-neighborPosition)
                && (neighbor is LogicWireBlock || neighbor is LogicSignalSource || neighbor is GateBlock))
            {
                state = Mathf.Max(state, neighbor.SignalState);
            }
        }

        return state;
    }
}