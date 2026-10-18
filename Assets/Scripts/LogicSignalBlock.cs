using System;
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
            if (TryGetRegisteredBlock(cell, out var other) && other != this) return;
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
        TryGetRegisteredBlock(position, out LogicSignalBlock block);
        return block;
    }

    private static bool TryGetRegisteredBlock(Vector3Int position, out LogicSignalBlock block)
    {
        if (!blocks.TryGetValue(position, out block)) return false;
        if (block != null) return true;

        blocks.Remove(position);
        block = null;
        return false;
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
    private readonly struct SearchState : System.IEquatable<SearchState>
    {
        public readonly Vector3Int Position;
        public readonly Vector3Int Direction;

        public SearchState(Vector3Int position, Vector3Int direction)
        {
            Position = position;
            Direction = direction;
        }

        public bool Equals(SearchState other)
            => Position == other.Position && Direction == other.Direction;

        public override bool Equals(object obj)
            => obj is SearchState other && Equals(other);

        public override int GetHashCode()
            => unchecked(Position.GetHashCode() * 397 ^ Direction.GetHashCode());
    }

    private readonly struct SearchCost : System.IComparable<SearchCost>, System.IEquatable<SearchCost>
    {
        public readonly int Turns;
        public readonly int Steps;

        public SearchCost(int turns, int steps)
        {
            Turns = turns;
            Steps = steps;
        }

        public int CompareTo(SearchCost other)
        {
            int turnOrder = Turns.CompareTo(other.Turns);
            return turnOrder != 0 ? turnOrder : Steps.CompareTo(other.Steps);
        }

        public bool Equals(SearchCost other) => Turns == other.Turns && Steps == other.Steps;

        public override bool Equals(object obj) => obj is SearchCost other && Equals(other);

        public override int GetHashCode() => unchecked(Turns * 397 ^ Steps);
    }

    private sealed class SearchEntry : System.IComparable<SearchEntry>
    {
        public readonly SearchState State;
        public readonly SearchCost Cost;
        public readonly SearchCost EstimatedCost;
        private readonly long sequence;

        public SearchEntry(SearchState state, SearchCost cost, SearchCost estimatedCost, long sequence)
        {
            State = state;
            Cost = cost;
            EstimatedCost = estimatedCost;
            this.sequence = sequence;
        }

        public int CompareTo(SearchEntry other)
        {
            int estimateOrder = EstimatedCost.CompareTo(other.EstimatedCost);
            if (estimateOrder != 0) return estimateOrder;

            int costOrder = Cost.CompareTo(other.Cost);
            return costOrder != 0 ? costOrder : sequence.CompareTo(other.sequence);
        }
    }

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

        var startState = new SearchState(start, Vector3Int.zero);
        var pending = new SortedSet<SearchEntry>();
        var previous = new Dictionary<SearchState, SearchState>();
        var bestCosts = new Dictionary<SearchState, SearchCost> { [startState] = new SearchCost(0, 0) };
        long sequence = 0;
        pending.Add(new SearchEntry(
            startState,
            new SearchCost(0, 0),
            new SearchCost(MinimumRemainingTurns(start, target, Vector3Int.zero), ManhattanDistance(start, target)),
            sequence++));

        while (pending.Count > 0)
        {
            SearchEntry current = pending.Min;
            pending.Remove(current);
            if (!bestCosts.TryGetValue(current.State, out SearchCost currentBest) || !current.Cost.Equals(currentBest))
                continue;

            if (current.State.Position == target)
            {
                path = ReconstructPath(startState, current.State, previous);
                return true;
            }

            foreach (Vector3Int neighbor in LogicSignalBlock.Neighbors(current.State.Position))
            {
                if (!canTraverse(neighbor)) continue;

                Vector3Int direction = neighbor - current.State.Position;
                int turnCost = current.State.Direction != Vector3Int.zero && current.State.Direction != direction ? 1 : 0;
                var nextCost = new SearchCost(current.Cost.Turns + turnCost, current.Cost.Steps + 1);
                var nextState = new SearchState(neighbor, direction);
                if (bestCosts.TryGetValue(nextState, out SearchCost knownCost) && knownCost.CompareTo(nextCost) <= 0)
                    continue;

                bestCosts[nextState] = nextCost;
                previous[nextState] = current.State;
                pending.Add(new SearchEntry(
                    nextState,
                    nextCost,
                    new SearchCost(
                        nextCost.Turns + MinimumRemainingTurns(neighbor, target, direction),
                        nextCost.Steps + ManhattanDistance(neighbor, target)),
                    sequence++));
            }
        }

        return false;
    }

    private static int ManhattanDistance(Vector3Int first, Vector3Int second)
    {
        Vector3Int difference = first - second;
        return Mathf.Abs(difference.x) + Mathf.Abs(difference.y) + Mathf.Abs(difference.z);
    }

    private static int MinimumRemainingTurns(Vector3Int position, Vector3Int target, Vector3Int direction)
    {
        Vector3Int delta = target - position;
        int axes = (delta.x != 0 ? 1 : 0) + (delta.y != 0 ? 1 : 0) + (delta.z != 0 ? 1 : 0);
        bool continuesTowardTarget =
            delta.x != 0 && direction == new Vector3Int(System.Math.Sign(delta.x), 0, 0) ||
            delta.y != 0 && direction == new Vector3Int(0, System.Math.Sign(delta.y), 0) ||
            delta.z != 0 && direction == new Vector3Int(0, 0, System.Math.Sign(delta.z));

        if (axes == 0 || direction == Vector3Int.zero) return Mathf.Max(0, axes - 1);
        if (axes == 1) return continuesTowardTarget ? 0 : 1;
        return continuesTowardTarget ? axes - 1 : axes;
    }

    private static List<Vector3Int> ReconstructPath(
        SearchState start,
        SearchState target,
        Dictionary<SearchState, SearchState> previous)
    {
        var path = new List<Vector3Int>();
        SearchState current = target;
        while (!current.Equals(start))
        {
            path.Add(current.Position);
            current = previous[current];
        }

        path.Add(start.Position);
        path.Reverse();
        return path;
    }
}