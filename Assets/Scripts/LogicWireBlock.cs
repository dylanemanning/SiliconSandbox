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
        if (targetRenderer == null) return;

        Material nextMaterial = signalState == 1 ? poweredMaterial : unpoweredMaterial;
        if (nextMaterial != null)
        {
            targetRenderer.sharedMaterial = nextMaterial;
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
            if (ConnectsTo(neighbor))
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