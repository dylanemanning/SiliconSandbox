// LogicWireBlock.cs — a placed wire.
//
// Display only (#82). A wire does not work out its own signal any more: the
// simulation driver evaluates the whole circuit through the logic engine and
// pushes each wire network's value in with SetDisplayedState. The old per-frame
// search that ORed every source it could reach is gone; one driver per network
// is the rule now (decision 1), and two drivers show as an error.
//
// The wire still works out its own junction arms every frame (RefreshConnections),
// since that is purely visual.

using UnityEngine;

public class LogicWireBlock : LogicSignalBlock
{
    [Header("Signal visuals")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material unpoweredMaterial;
    [SerializeField] private Material poweredMaterial;

    [Tooltip("Optional. Shown when two outputs drive this wire's network. " +
             "If empty, the unpowered material is tinted with Error Color instead.")]
    [SerializeField] private Material errorMaterial;
    [SerializeField] private Color errorColor = Color.red;

    [Header("Optional junction visuals")]
    [Tooltip("Child objects for +X, -X, +Y, -Y, +Z, -Z branches.")]
    [SerializeField] private GameObject[] directionVisuals = new GameObject[6];

    private int signalState;
    private bool isError;
    private int connectionMask;
    private MaterialPropertyBlock propertyBlock;

    public override int SignalState => signalState;

    /// <summary>True while this wire's network has more than one driver.</summary>
    public bool IsError => isError;

    public int ConnectionMask => connectionMask;
    public int ConnectionCount => CountBits(connectionMask);
    public bool IsJunction => ConnectionCount >= 3;

    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveRenderer();
        ApplyVisualState();
    }

    protected override void Update()
    {
        base.Update();
        RefreshConnections();
    }

    /// <summary>
    /// Shows a new signal. Called by the simulation driver (#82) with the value
    /// of the network's driver, or 0 for a network with no driver. error is true
    /// when the network has two or more drivers. Does nothing if neither changed,
    /// so the driver can call it freely.
    /// </summary>
    public void SetDisplayedState(int state, bool error = false)
    {
        int next = state == 0 ? 0 : 1;
        if (next == signalState && error == isError) return;

        signalState = next;
        isError = error;
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

        Material nextMaterial;
        if (isError && errorMaterial != null) nextMaterial = errorMaterial;
        else nextMaterial = signalState == 1 && !isError ? poweredMaterial : unpoweredMaterial;

        if (nextMaterial != null)
        {
            targetRenderer.sharedMaterial = nextMaterial;
        }

        // No error material assigned: tint instead. Cleared again as soon as the
        // error goes away, so normal wires never carry a property block.
        if (isError && errorMaterial == null)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor("_BaseColor", errorColor); // URP / HDRP Lit
            propertyBlock.SetColor("_Color", errorColor);     // Built-in Standard
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
        else
        {
            targetRenderer.SetPropertyBlock(null);
        }
    }

    private void RefreshConnections()
    {
        connectionMask = 0;
        Vector3Int position = GridPosition;
        int index = 0;

        foreach (Vector3Int neighborPosition in Neighbors(position))
        {
            LogicSignalBlock neighbor = At(neighborPosition);
            if (neighbor is LogicWireBlock || neighbor is LogicSignalSource)
            {
                connectionMask |= 1 << index;
            }

            if (directionVisuals != null && index < directionVisuals.Length && directionVisuals[index] != null)
            {
                directionVisuals[index].SetActive((connectionMask & (1 << index)) != 0);
            }

            index++;
        }
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }
}
