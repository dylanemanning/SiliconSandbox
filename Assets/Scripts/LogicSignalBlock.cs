using System;
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

public static class LogicWirePathfinder
{
    public static Vector3Int GetCellBeyondSurfaceHit(Vector3 hitPoint, Vector3 cardinalNormal)
    {
        return Vector3Int.RoundToInt(hitPoint + cardinalNormal * 0.5f);
    }

    public static bool TryFindPath(
        Vector3Int start,
        Vector3Int target,
        Func<Vector3Int, bool> canTraverse,
        out List<Vector3Int> path)
    {
        path = null;
        if (start == target)
        {
            path = new List<Vector3Int> { start };
            return true;
        }
        if (canTraverse == null || !canTraverse(target)) return false;

        var pending = new Queue<Vector3Int>();
        var previous = new Dictionary<Vector3Int, Vector3Int>();
        var visited = new HashSet<Vector3Int> { start };
        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            Vector3Int current = pending.Dequeue();
            foreach (Vector3Int neighbor in LogicSignalBlock.Neighbors(current))
            {
                if (!visited.Add(neighbor) || !canTraverse(neighbor)) continue;

                previous[neighbor] = current;
                if (neighbor == target)
                {
                    path = ReconstructPath(start, target, previous);
                    return true;
                }

                pending.Enqueue(neighbor);
            }
        }

        return false;
    }

    private static List<Vector3Int> ReconstructPath(
        Vector3Int start,
        Vector3Int target,
        Dictionary<Vector3Int, Vector3Int> previous)
    {
        var path = new List<Vector3Int> { target };
        Vector3Int current = target;
        while (current != start)
        {
            current = previous[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}