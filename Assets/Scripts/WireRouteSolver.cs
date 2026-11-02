using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>A value-only world snapshot. Do not mutate it while Find is running.</summary>
public sealed class WireRouteRequest
{
    public HashSet<Vector3Int> Blocked = new HashSet<Vector3Int>();
    public Vector3Int Start, Target;
    // Zero is unrestricted. Otherwise these are the signed first/last path steps.
    public Vector3Int StartDirection, TargetDirection;
    public Vector3Int Min, Max; // Inclusive world/search bounds.
    public bool PreferTarget = true;
    public int MaxVisited = 100000;
    public int MaxStates = 262144;
    public int MaxCandidates = 524288;
    public int MaxPathLength = 4096; // Maximum number of steps, excluding the start cell.
}

public enum WireRouteStatus { Success, Unreachable, Cancelled, LimitExceeded }

public sealed class WireRouteResult
{
    public WireRouteStatus Status { get; }
    public List<Vector3Int> Path { get; }
    public int Steps { get; }
    public int Bends { get; }

    internal WireRouteResult(WireRouteStatus status, List<Vector3Int> path = null, int bends = 0)
    {
        Status = status;
        Path = path;
        Steps = path == null ? 0 : path.Count - 1;
        Bends = bends;
    }
}

/// <summary>
/// Bounded weighted A* over (cell, arrival direction). It uses only managed data
/// and Vector3Int values, so Find can run on a worker without touching Unity objects.
/// </summary>
public static class WireRouteSolver
{
    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up,
        Vector3Int.down, Vector3Int.forward, Vector3Int.back
    };
    // Callers may lower budgets but cannot accidentally request unbounded storage/work.
    private const int HardMaxVisited = 100000;
    private const int HardMaxStates = 262144;
    private const int HardMaxCandidates = 524288;
    private const int HardMaxPathLength = 4096;

    private readonly struct State : IEquatable<State>
    {
        public readonly Vector3Int Cell;
        public readonly int Direction;
        public State(Vector3Int cell, int direction) { Cell = cell; Direction = direction; }
        public bool Equals(State other) => Cell == other.Cell && Direction == other.Direction;
        public override bool Equals(object obj) => obj is State other && Equals(other);
        public override int GetHashCode() => unchecked(Cell.GetHashCode() * 397 ^ Direction);
    }

    // A persistent run list avoids copying an entire path/length array for every label.
    private sealed class Run
    {
        public readonly Run Previous;
        public readonly int Length;
        public readonly int Count;
        public Run(Run previous, int length)
        {
            Previous = previous;
            Length = length;
            Count = (previous == null ? 0 : previous.Count) + 1;
        }
    }

    private sealed class Label : IComparable<Label>
    {
        public readonly State State;
        public readonly Label Previous;
        public readonly Run Runs;
        public readonly int Steps, Bends;
        public readonly long Estimate, Sequence;
        public int Cost => Steps + 2 * Bends;

        public Label(State state, Label previous, Run runs, int steps, int bends,
            long estimate, long sequence)
        {
            State = state; Previous = previous; Runs = runs;
            Steps = steps; Bends = bends; Estimate = estimate; Sequence = sequence;
        }

        public int CompareTo(Label other)
        {
            int order = Estimate.CompareTo(other.Estimate);
            if (order == 0) order = Bends.CompareTo(other.Bends);
            if (order == 0) order = Steps.CompareTo(other.Steps);
            return order == 0 ? Sequence.CompareTo(other.Sequence) : order;
        }
    }

    public static WireRouteResult Find(WireRouteRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (cancellationToken.IsCancellationRequested) return Failure(WireRouteStatus.Cancelled);
        int startDirection = DirectionIndex(request.StartDirection);
        int targetDirection = DirectionIndex(request.TargetDirection);
        if (startDirection == -2 || targetDirection == -2 ||
            !Inside(request.Start, request.Min, request.Max) || !Inside(request.Target, request.Min, request.Max))
            return Failure(WireRouteStatus.Unreachable);
        if (request.MaxVisited <= 0 || request.MaxStates <= 0 ||
            request.MaxCandidates <= 0 || request.MaxPathLength < 0)
            return Failure(WireRouteStatus.LimitExceeded);
        if (request.Start == request.Target)
            return new WireRouteResult(WireRouteStatus.Success, new List<Vector3Int> { request.Start });

        int maxVisited = Math.Min(request.MaxVisited, HardMaxVisited);
        int maxStates = Math.Min(request.MaxStates, HardMaxStates);
        int maxCandidates = Math.Min(request.MaxCandidates, HardMaxCandidates);
        int maxPathLength = Math.Min(request.MaxPathLength, HardMaxPathLength);
        if (Distance(request.Start, request.Target) > maxPathLength)
            return Failure(WireRouteStatus.LimitExceeded);

        // Solve outward from the preferred/input end, so tied labels can compare
        // their run prefixes. Reverse the result back to the caller's click order.
        Vector3Int start = request.PreferTarget ? request.Target : request.Start;
        Vector3Int target = request.PreferTarget ? request.Start : request.Target;
        int firstDirection = request.PreferTarget ? Opposite(targetDirection) : startDirection;
        int lastDirection = request.PreferTarget ? Opposite(startDirection) : targetDirection;
        if (!ApproachIsClear(start, firstDirection, request) ||
            !ApproachIsClear(target, Opposite(lastDirection), request))
            return Failure(WireRouteStatus.Unreachable);

        var pending = new SortedSet<Label>();
        var best = new Dictionary<State, Label>();
        var initial = new Label(new State(start, -1), null, null, 0, 0, Distance(start, target), 0);
        pending.Add(initial);
        best.Add(initial.State, initial);
        int visited = 0, candidates = 1;
        long sequence = 1;
        long limitedEstimate = long.MaxValue;
        Label winner = null;
        var leftRuns = new int[maxPathLength + 1];
        var rightRuns = new int[maxPathLength + 1];

        while (pending.Count != 0)
        {
            if (cancellationToken.IsCancellationRequested) return Failure(WireRouteStatus.Cancelled);
            Label current = pending.Min;
            // A Manhattan lower bound cannot discard a cheaper or tied route.
            // Finish all equal-cost possibilities to prove the complete tie-break.
            if (winner != null && current.Estimate > winner.Cost) break;
            if (visited >= maxVisited) return Failure(WireRouteStatus.LimitExceeded);
            visited++;
            pending.Remove(current);
            if (current.State.Cell == target)
            {
                if (winner == null || IsBetter(current, winner, leftRuns, rightRuns)) winner = current;
                continue;
            }

            for (int direction = 0; direction < Directions.Length; direction++)
            {
                if (cancellationToken.IsCancellationRequested) return Failure(WireRouteStatus.Cancelled);
                if (current.Previous == null && firstDirection >= 0 && direction != firstDirection) continue;
                if (current.State.Direction >= 0 && direction == Opposite(current.State.Direction)) continue;
                if (!TryStep(current.State.Cell, direction, request.Min, request.Max, out Vector3Int next)) continue;
                if (next == start || IsBlocked(next, request)) continue;
                if (next == target && lastDirection >= 0 && direction != lastDirection) continue;
                int steps = current.Steps + 1;
                int bends = current.Bends + (current.State.Direction >= 0 && current.State.Direction != direction ? 1 : 0);
                int cost = steps + 2 * bends;
                long estimate = cost + Distance(next, target);
                if (winner != null && estimate > winner.Cost) continue;
                if (steps + Distance(next, target) > maxPathLength)
                {
                    limitedEstimate = Math.Min(limitedEstimate, estimate);
                    continue;
                }
                var state = new State(next, direction);
                best.TryGetValue(state, out Label known);
                if (known != null && (cost > known.Cost || cost == known.Cost && bends > known.Bends)) continue;
                Run runs = current.State.Direction == direction
                    ? new Run(current.Runs.Previous, current.Runs.Length + 1)
                    : new Run(current.Runs, 1);
                if (known != null && cost == known.Cost && bends == known.Bends &&
                    CompareRuns(runs, known.Runs, leftRuns, rightRuns) <= 0) continue;
                if (known == null && best.Count >= maxStates || candidates >= maxCandidates)
                    return Failure(WireRouteStatus.LimitExceeded);
                candidates++;
                var candidate = new Label(state, current, runs, steps, bends, estimate, sequence++);
                // Only one queued entry per directional state. Old immutable labels
                // remain valid ancestors of routes already generated from them.
                if (known != null) pending.Remove(known);
                best[state] = candidate;
                pending.Add(candidate);
            }
        }

        if (cancellationToken.IsCancellationRequested) return Failure(WireRouteStatus.Cancelled);
        if (winner == null)
            return Failure(limitedEstimate != long.MaxValue ? WireRouteStatus.LimitExceeded : WireRouteStatus.Unreachable);
        if (limitedEstimate <= winner.Cost) return Failure(WireRouteStatus.LimitExceeded);
        var path = new List<Vector3Int>(winner.Steps + 1);
        for (Label label = winner; label != null; label = label.Previous) path.Add(label.State.Cell);
        if (!request.PreferTarget) path.Reverse();
        return new WireRouteResult(WireRouteStatus.Success, path, winner.Bends);
    }

    private static bool IsBetter(Label left, Label right, int[] leftRuns, int[] rightRuns)
        => left.Cost < right.Cost || left.Cost == right.Cost &&
            (left.Bends < right.Bends || left.Bends == right.Bends &&
             CompareRuns(left.Runs, right.Runs, leftRuns, rightRuns) > 0);

    private static int CompareRuns(Run left, Run right, int[] leftBuffer, int[] rightBuffer)
    {
        int count = left == null ? 0 : left.Count;
        int rightCount = right == null ? 0 : right.Count;
        for (Run run = left; run != null; run = run.Previous) leftBuffer[run.Count - 1] = run.Length;
        for (Run run = right; run != null; run = run.Previous) rightBuffer[run.Count - 1] = run.Length;
        for (int i = 0; i < Math.Min(count, rightCount); i++)
        {
            int order = leftBuffer[i].CompareTo(rightBuffer[i]);
            if (order != 0) return order;
        }
        return count.CompareTo(rightCount);
    }

    private static bool ApproachIsClear(Vector3Int endpoint, int direction, WireRouteRequest request)
        => direction < 0 || TryStep(endpoint, direction, request.Min, request.Max, out Vector3Int cell) && !IsBlocked(cell, request);

    private static bool IsBlocked(Vector3Int cell, WireRouteRequest request)
        => cell != request.Start && cell != request.Target && request.Blocked != null && request.Blocked.Contains(cell);

    private static bool Inside(Vector3Int cell, Vector3Int min, Vector3Int max)
        => cell.x >= min.x && cell.x <= max.x && cell.y >= min.y && cell.y <= max.y && cell.z >= min.z && cell.z <= max.z;

    private static bool TryStep(Vector3Int cell, int direction, Vector3Int min, Vector3Int max, out Vector3Int next)
    {
        Vector3Int delta = Directions[direction];
        long x = (long)cell.x + delta.x, y = (long)cell.y + delta.y, z = (long)cell.z + delta.z;
        if (x < min.x || x > max.x || y < min.y || y > max.y || z < min.z || z > max.z)
        {
            next = default;
            return false;
        }
        next = new Vector3Int((int)x, (int)y, (int)z);
        return true;
    }

    private static long Distance(Vector3Int a, Vector3Int b)
        => Math.Abs((long)a.x - b.x) + Math.Abs((long)a.y - b.y) + Math.Abs((long)a.z - b.z);

    private static int Opposite(int direction) => direction < 0 ? direction : direction ^ 1;

    private static int DirectionIndex(Vector3Int direction)
    {
        if (direction == Vector3Int.zero) return -1;
        for (int i = 0; i < Directions.Length; i++) if (Directions[i] == direction) return i;
        return -2;
    }

    private static WireRouteResult Failure(WireRouteStatus status) => new WireRouteResult(status);
}
