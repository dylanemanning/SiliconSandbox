using System.Collections.Generic;
using UnityEngine;

public abstract class LogicSignalBlock : MonoBehaviour
{
    private static readonly Dictionary<Vector3Int, LogicSignalBlock> blocks =
        new Dictionary<Vector3Int, LogicSignalBlock>();

    private readonly List<Vector3Int> registeredCells = new List<Vector3Int>();
    private Vector3Int lastAnchor, lastFootprint;
    private Quaternion lastRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => blocks.Clear();

    public Vector3Int GridPosition => Vector3Int.RoundToInt(transform.position);

    public virtual int SignalState => 0;

    protected virtual void OnEnable()
    {
        Register();
    }

    protected virtual void OnDisable()
    {
        Unregister();
    }

    protected virtual void Update()
    {
        Register();
    }

    protected void Register()
    {
        var block = GetComponent<Block>();
        var footprint = block != null ? block.footprint : Vector3Int.one;
        if (registeredCells.Count > 0 && lastAnchor == GridPosition && lastFootprint == footprint
            && lastRotation == transform.rotation
            && blocks.TryGetValue(GridPosition,out var registered) && registered == this) return;
        var cells = block != null
            ? new List<Vector3Int>(block.OccupiedCells(GridPosition, transform.rotation))
            : new List<Vector3Int>{GridPosition};
        Unregister();
        foreach (var cell in cells)
            if (blocks.TryGetValue(cell, out var other) && other != this) return;
        foreach (var cell in cells) { blocks[cell] = this; registeredCells.Add(cell); }
        lastAnchor=GridPosition;lastFootprint=footprint;lastRotation=transform.rotation;
    }

    private void Unregister()
    {
        foreach (var cell in registeredCells)
            if (blocks.TryGetValue(cell, out var other) && other == this) blocks.Remove(cell);
        registeredCells.Clear();
    }

    public virtual bool HasConnection(Vector3Int cell, Vector3Int face) => cell == GridPosition;
    public virtual bool CanDrive(Vector3Int cell, Vector3Int face) => HasConnection(cell, face);

    public static LogicSignalBlock At(Vector3Int position)
    {
        blocks.TryGetValue(position, out LogicSignalBlock block);
        return block;
    }

    public static IEnumerable<Vector3Int> Neighbors(Vector3Int position)
    {
        yield return position + Vector3Int.right;
        yield return position + Vector3Int.left;
        yield return position + Vector3Int.up;
        yield return position + Vector3Int.down;
        yield return position + new Vector3Int(0, 0, 1);
        yield return position + new Vector3Int(0, 0, -1);
    }

    public static Vector3Int DirectionToNeighbor(Vector3Int from, Vector3Int neighbor)
    {
        return neighbor - from;
    }
}