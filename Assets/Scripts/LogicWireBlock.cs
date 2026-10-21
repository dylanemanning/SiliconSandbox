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
    [SerializeField] private Material erroredMaterial;

    [Tooltip("Optional. Shown when two outputs drive this wire's network. " +
             "If empty, the unpowered material is tinted with Error Color instead.")]
    [SerializeField] private Material errorMaterial;
    [SerializeField] private Color errorColor = Color.red;

    [Header("Optional junction visuals")]
    [Tooltip("Child objects for +X, -X, +Y, -Y, +Z, -Z branches.")]
    [SerializeField] private GameObject[] directionVisuals = new GameObject[6];

    private readonly LineRenderer[] connectionLines = new LineRenderer[6];
    private int signalState;
    private bool isError;
    private int connectionMask;
    [SerializeField] private int endpointMask;
    [SerializeField] private bool hasCustomEndpointMask;
    [SerializeField] private int joinMask;
    private MaterialPropertyBlock propertyBlock;

    public override int SignalState => signalState;

    /// <summary>True while this wire's network has more than one driver.</summary>
    public bool IsError => isError;

    public int ConnectionMask => connectionMask;
    public int ConnectionCount => CountBits(connectionMask);
    public bool IsJunction => ConnectionCount >= 3;
    public int EndpointMask => hasCustomEndpointMask ? endpointMask : GetAxisEndpointMask();
    /// <summary>Intentional wire and component-pin joins, in world-space face order.</summary>
    public int JoinMask => joinMask;

    public bool HasJoinTowards(Vector3Int neighborPosition)
    {
        int bit = GetDirectionBit(neighborPosition - GridPosition);
        return bit != 0 && (joinMask & bit) != 0;
    }

    public void SetJoinMask(int mask)
    {
        int next = mask & 0x3f;
        if (joinMask == next) return;
        joinMask = next;
        MarkCircuitChanged();
    }

    public bool HasEndpointTowards(Vector3Int neighborPosition)
    {
        Vector3Int direction = neighborPosition - GridPosition;
        int directionBit = GetDirectionBit(direction);
        return directionBit != 0 && (EndpointMask & directionBit) != 0;
    }

    public void SetEndpointMask(int mask)
    {
        int previous = EndpointMask;
        endpointMask = mask & 0x3f;
        hasCustomEndpointMask = true;
        ApplyEndpointVisuals(endpointMask);
        if (EndpointMask != previous) MarkCircuitChanged();
    }

    public void AddEndpointTowards(Vector3Int direction)
    {
        SetEndpointMask(EndpointMask | GetDirectionBit(direction));
    }

    public static int GetDirectionBit(Vector3Int direction)
    {
        if (direction == Vector3Int.right) return 1 << 0;
        if (direction == Vector3Int.left) return 1 << 1;
        if (direction == Vector3Int.up) return 1 << 2;
        if (direction == Vector3Int.down) return 1 << 3;
        if (direction == new Vector3Int(0, 0, 1)) return 1 << 4;
        if (direction == new Vector3Int(0, 0, -1)) return 1 << 5;
        return 0;
    }

    private int GetAxisEndpointMask()
    {
        Vector3Int axis = Vector3Int.RoundToInt(transform.up);
        return GetDirectionBit(axis) | GetDirectionBit(-axis);
    }

    private void ApplyEndpointVisuals(int mask)
    {
        if (directionVisuals == null) return;

        for (int index = 0; index < directionVisuals.Length && index < 6; index++)
        {
            if (directionVisuals[index] != null)
            {
                directionVisuals[index].SetActive((mask & (1 << index)) != 0);
            }
        }
    }

    public bool ConnectsTo(LogicSignalBlock neighbor, Vector3Int neighborCell)
    {
        if (neighbor == null) return false;

        if (neighbor is LogicWireBlock neighborWire)
        {
            return neighborWire.GridPosition == neighborCell &&
                   HasJoinTowards(neighborCell) && neighborWire.HasJoinTowards(GridPosition);
        }

        if (!HasEndpointTowards(neighborCell) || !HasJoinTowards(neighborCell)) return false;
        return neighbor.HasConnection(neighborCell, GridPosition - neighborCell);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveRenderer();
        EnsureConnectionLines();
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
        // A prefab with the new wire model draws itself (WireModelVisual).
        if (GetComponent<WireModelVisual>() != null) return;
        ResolveRenderer();
        if (targetRenderer == null) return;

        Material nextMaterial;
        if (isError)
        {
            nextMaterial = errorMaterial != null
                ? errorMaterial
                : erroredMaterial != null ? erroredMaterial : unpoweredMaterial;
        }
        else
        {
            nextMaterial = signalState == 1 ? poweredMaterial : unpoweredMaterial;
        }

        if (nextMaterial != null)
        {
            targetRenderer.sharedMaterial = nextMaterial;

            foreach (LineRenderer line in connectionLines)
            {
                if (line != null) line.sharedMaterial = nextMaterial;
            }
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

    private void EnsureConnectionLines()
    {
        for (int index = 0; index < connectionLines.Length; index++)
        {
            Transform existing = transform.Find($"Wire Connection {index}");
            GameObject lineObject = existing != null
                ? existing.gameObject
                : new GameObject($"Wire Connection {index}");

            if (existing == null)
            {
                lineObject.transform.SetParent(transform, false);
            }

            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null) line = lineObject.AddComponent<LineRenderer>();

            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.18f;
            line.endWidth = 0.18f;
            line.enabled = false;
            connectionLines[index] = line;
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
            bool connected = ConnectsTo(neighbor, neighborPosition);
            if (connected)
            {
                connectionMask |= 1 << index;
            }

            if (directionVisuals != null && index < directionVisuals.Length && directionVisuals[index] != null)
            {
                directionVisuals[index].SetActive(((EndpointMask | connectionMask) & (1 << index)) != 0);
            }

            UpdateConnectionLine(index, position, neighborPosition, neighbor, connected);
            index++;
        }
    }

    private void UpdateConnectionLine(
        int index,
        Vector3Int position,
        Vector3Int neighborPosition,
        LogicSignalBlock neighbor,
        bool connected)
    {
        LineRenderer line = connectionLines[index];
        if (line == null) return;

        LogicWireBlock neighborWire = neighbor as LogicWireBlock;
        bool wireConnected = connected && neighborWire != null;
        int oppositeIndex = index ^ 1;
        bool hasAuthoredVisual = directionVisuals != null && index < directionVisuals.Length && directionVisuals[index] != null;
        if (neighborWire != null && neighborWire.directionVisuals != null &&
            oppositeIndex < neighborWire.directionVisuals.Length && neighborWire.directionVisuals[oppositeIndex] != null)
        {
            hasAuthoredVisual = true;
        }

        bool drawLine = wireConnected && !hasAuthoredVisual && IsBefore(position, neighborPosition);
        line.enabled = drawLine;
        if (!drawLine) return;

        line.SetPosition(0, transform.position);
        line.SetPosition(1, neighborWire.transform.position);
    }

    private static bool IsBefore(Vector3Int first, Vector3Int second)
    {
        if (first.x != second.x) return first.x < second.x;
        if (first.y != second.y) return first.y < second.y;
        return first.z < second.z;
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
