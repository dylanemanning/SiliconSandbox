using System.Collections.Generic;
using UnityEngine;

public abstract class LogicSignalBlock : MonoBehaviour
{
    private static readonly Dictionary<Vector3Int, LogicSignalBlock> blocks =
        new Dictionary<Vector3Int, LogicSignalBlock>();

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
        Vector3Int position = GridPosition;
        if (blocks.TryGetValue(position, out LogicSignalBlock existing) && existing != this)
        {
            return;
        }

        blocks[position] = this;
    }

    private void Unregister()
    {
        Vector3Int position = GridPosition;
        if (blocks.TryGetValue(position, out LogicSignalBlock existing) && existing == this)
        {
            blocks.Remove(position);
        }
    }

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