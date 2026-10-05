using System.Collections.Generic;
using UnityEngine;

public class LogicWireBlock : LogicSignalBlock
{
    [Header("Signal visuals")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material unpoweredMaterial;
    [SerializeField] private Material poweredMaterial;
    [SerializeField] private Material erroredMaterial;

    [Header("Optional junction visuals")]
    [Tooltip("Child objects for +X, -X, +Y, -Y, +Z, -Z branches.")]
    [SerializeField] private GameObject[] directionVisuals = new GameObject[6];

    private readonly LineRenderer[] connectionLines = new LineRenderer[6];
    private int signalState;
    private int connectionMask;

    public override int SignalState => signalState;
    public int ConnectionMask => connectionMask;
    public int ConnectionCount => CountBits(connectionMask);
    public bool IsJunction => ConnectionCount >= 3;

    public bool HasEndpointTowards(Vector3Int neighborPosition)
    {
        Vector3Int direction = neighborPosition - GridPosition;
        Vector3Int axis = Vector3Int.RoundToInt(transform.up);
        return direction == axis || direction == -axis;
    }

    public bool ConnectsTo(LogicSignalBlock neighbor)
    {
        if (neighbor == null) return false;

        if (neighbor is LogicWireBlock neighborWire)
        {
            return HasEndpointTowards(neighbor.GridPosition) ||
                   neighborWire.HasEndpointTowards(GridPosition);
        }

        return neighbor is LogicSignalSource && HasEndpointTowards(neighbor.GridPosition);
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
        signalState = ResolveNetworkState();
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
        Material nextMaterial = signalState == 1
            ? poweredMaterial
            : signalState == -1 ? erroredMaterial : unpoweredMaterial;
        if (nextMaterial != null)
        {
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = nextMaterial;
            }

            foreach (LineRenderer line in connectionLines)
            {
                if (line != null) line.sharedMaterial = nextMaterial;
            }
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
            bool connected = ConnectsTo(neighbor);
            if (connected)
            {
                connectionMask |= 1 << index;
            }

            if (directionVisuals != null && index < directionVisuals.Length && directionVisuals[index] != null)
            {
                directionVisuals[index].SetActive((connectionMask & (1 << index)) != 0);
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

    private int ResolveNetworkState()
    {
        var visited = new HashSet<LogicWireBlock>();
        var pending = new Queue<LogicWireBlock>();
        var sourceStates = new List<int>();
        pending.Enqueue(this);

        while (pending.Count > 0)
        {
            LogicWireBlock wire = pending.Dequeue();
            if (!visited.Add(wire)) continue;

            foreach (Vector3Int neighborPosition in Neighbors(wire.GridPosition))
            {
                LogicSignalBlock neighbor = At(neighborPosition);
                if (!wire.ConnectsTo(neighbor)) continue;

                if (neighbor is LogicWireBlock neighborWire)
                {
                    pending.Enqueue(neighborWire);
                }
                else if (neighbor is LogicSignalSource source)
                {
                    sourceStates.Add(source.SignalState);
                }
            }
        }

        return LogicSignalRules.ResolveSources(sourceStates);
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