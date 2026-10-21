using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class WireRouteSolverTests
{
    [Test]
    public void WeightedLengthCanBeatTheRouteWithFewestBends()
    {
        WireRouteResult result = WireRouteSolver.Find(TwoCorridors(5));
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(result.Steps, Is.EqualTo(5));
        Assert.That(result.Bends, Is.EqualTo(3));
        Assert.That(result.Steps + 2 * result.Bends, Is.EqualTo(11),
            "The 13-step, two-bend detour costs 17 and must lose to the short zigzag.");
    }

    [Test]
    public void EqualWeightedCostPrefersFewerBends()
    {
        WireRouteResult result = WireRouteSolver.Find(TwoCorridors(2));
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(result.Steps, Is.EqualTo(7));
        Assert.That(result.Bends, Is.EqualTo(2));
        Assert.That(result.Steps + 2 * result.Bends, Is.EqualTo(11));
    }

    [Test]
    public void EqualCostsPreferLongestFirstRunFromTheInputEnd()
    {
        var request = Open(Vector3Int.zero, new Vector3Int(4, 0, 2));
        WireRouteResult inputAtTarget = WireRouteSolver.Find(request);
        Assert.That(inputAtTarget.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(inputAtTarget.Path[1], Is.EqualTo(Vector3Int.forward));
        Assert.That(inputAtTarget.Path[2], Is.EqualTo(Vector3Int.forward * 2));
        request.PreferTarget = false;
        WireRouteResult inputAtStart = WireRouteSolver.Find(request);
        Assert.That(inputAtStart.Path[1], Is.EqualTo(Vector3Int.right));
        Assert.That(inputAtStart.Path[4], Is.EqualTo(Vector3Int.right * 4));
        Assert.That(inputAtTarget.Bends, Is.EqualTo(1));
        Assert.That(inputAtStart.Bends, Is.EqualTo(1));
    }

    [Test]
    public void ReversingClickOrderPreservesTheInputSideTieBreak()
    {
        var request = Open(Vector3Int.zero, new Vector3Int(4, 0, 2));
        List<Vector3Int> forward = WireRouteSolver.Find(request).Path;
        request.Start = new Vector3Int(4, 0, 2);
        request.Target = Vector3Int.zero;
        request.PreferTarget = false;
        List<Vector3Int> reverse = WireRouteSolver.Find(request).Path;
        reverse.Reverse();
        CollectionAssert.AreEqual(forward, reverse);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void RequiredPinAxesWorkForRotatedEndpoints(int rotation)
    {
        var request = Open(Vector3Int.zero, Rotate(new Vector3Int(3, 0, 3), rotation));
        request.StartDirection = Rotate(Vector3Int.right, rotation);
        request.TargetDirection = Rotate(Vector3Int.forward, rotation);
        var result = WireRouteSolver.Find(request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(result.Path[1] - result.Path[0], Is.EqualTo(request.StartDirection));
        Assert.That(result.Path[result.Path.Count - 1] - result.Path[result.Path.Count - 2],
            Is.EqualTo(request.TargetDirection));
        Assert.That(result.Steps, Is.EqualTo(6));
        Assert.That(result.Bends, Is.EqualTo(1));
    }

    [Test]
    public void BlockedRequiredApproachFailsBeforeSearchBudgetIsSpent()
    {
        var request = Open(Vector3Int.zero, new Vector3Int(3, 0, 3));
        request.StartDirection = Vector3Int.right;
        request.Blocked.Add(Vector3Int.right);
        request.MaxVisited = 1;
        Assert.That(WireRouteSolver.Find(request).Status, Is.EqualTo(WireRouteStatus.Unreachable));
        request.Blocked.Clear();
        request.TargetDirection = Vector3Int.forward;
        request.Blocked.Add(request.Target - Vector3Int.forward);
        Assert.That(WireRouteSolver.Find(request).Status, Is.EqualTo(WireRouteStatus.Unreachable));
    }

    [Test]
    public void ExistingWiresAreBlockedExceptForTheTwoExplicitEndpoints()
    {
        var request = Open(Vector3Int.zero, Vector3Int.right * 2);
        request.Blocked.UnionWith(new[] { request.Start, Vector3Int.right, request.Target });
        var result = WireRouteSolver.Find(request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        CollectionAssert.DoesNotContain(result.Path, Vector3Int.right);
        Assert.That(result.Path.First(), Is.EqualTo(request.Start));
        Assert.That(result.Path.Last(), Is.EqualTo(request.Target));
        Assert.That(result.Steps, Is.EqualTo(4));
    }

    [Test]
    public void EnclosedTargetIsProvenUnreachableWithinFiniteBounds()
    {
        var request = Open(new Vector3Int(-2, 0, 0), Vector3Int.zero);
        request.Min = new Vector3Int(-3, -3, -3);
        request.Max = new Vector3Int(3, 3, 3);
        request.Blocked.UnionWith(new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, Vector3Int.forward, Vector3Int.back });
        var result = WireRouteSolver.Find(request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Unreachable));
        Assert.That(result.Path, Is.Null);
    }

    [Test]
    public void BoundsPreventRoutingAroundAWorldSpanningWall()
    {
        var request = Open(Vector3Int.zero, Vector3Int.right * 2);
        request.Min = Vector3Int.zero;
        request.Max = Vector3Int.right * 2;
        request.Blocked.Add(Vector3Int.right);
        Assert.That(WireRouteSolver.Find(request).Status, Is.EqualTo(WireRouteStatus.Unreachable));
    }

    [Test]
    public void PreCancelledRequestReturnsNoPartialPath()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            var result = WireRouteSolver.Find(Open(Vector3Int.zero, Vector3Int.right), cancellation.Token);
            Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Cancelled));
            Assert.That(result.Path, Is.Null);
        }
    }

    [Test]
    public void SolverRunsOnAWorkerUsingOnlyTheSnapshot()
    {
        var request = Open(Vector3Int.zero, new Vector3Int(4, 2, 3));
        var result = Task.Run(() => WireRouteSolver.Find(request)).GetAwaiter().GetResult();
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(result.Steps, Is.EqualTo(9));
        Assert.That(result.Bends, Is.EqualTo(2));
    }

    [TestCase("visited")] [TestCase("states")] [TestCase("candidates")] [TestCase("length")]
    public void ResourceLimitReturnsAnExplicitFailureWithoutAPartialRoute(string limit)
    {
        var request = Open(Vector3Int.zero, Vector3Int.right * 3);
        if (limit == "visited") request.MaxVisited = 1;
        if (limit == "states") request.MaxStates = 1;
        if (limit == "candidates") request.MaxCandidates = 1;
        if (limit == "length") request.MaxPathLength = 1;
        var result = WireRouteSolver.Find(request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.LimitExceeded));
        Assert.That(result.Path, Is.Null);
    }

    [Test]
    public void PathLengthCapDoesNotPublishAnUnprovenMoreExpensiveRoute()
    {
        var request = TwoCorridors(2);
        request.MaxPathLength = 5;
        // The five-step zigzag fits, but the tied seven-step path with fewer bends is outside
        // the resource limit. Returning the zigzag would violate weighted optimality.
        var result = WireRouteSolver.Find(request);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.LimitExceeded));
        Assert.That(result.Path, Is.Null);
    }

    [Test]
    public void DeterministicRouteDoesNotDependOnHashSetInsertionOrder()
    {
        var request = Open(Vector3Int.zero, Vector3Int.right * 4);
        var obstacles = new[] { Vector3Int.right, Vector3Int.right * 2, Vector3Int.right * 3 };
        request.Blocked.UnionWith(obstacles);
        var expected = WireRouteSolver.Find(request).Path;
        Assert.That(expected, Is.Not.Null);
        for (int repetition = 0; repetition < 8; repetition++)
        {
            request.Blocked = new HashSet<Vector3Int>(obstacles.Reverse());
            CollectionAssert.AreEqual(expected, WireRouteSolver.Find(request).Path);
        }
    }

    [Test]
    public void ExtremeIntegerCoordinatesCannotWrapAroundTheBounds()
    {
        var request = new WireRouteRequest
        {
            Start = new Vector3Int(int.MaxValue - 1, 0, 0), Target = new Vector3Int(int.MaxValue, 0, 0),
            Min = new Vector3Int(int.MaxValue - 1, 0, 0), Max = new Vector3Int(int.MaxValue, 0, 0)
        };
        Assert.That(WireRouteSolver.Find(request).Steps, Is.EqualTo(1));
        request.Start = new Vector3Int(int.MinValue, 0, 0);
        request.Min = request.Start;
        Assert.That(WireRouteSolver.Find(request).Status, Is.EqualTo(WireRouteStatus.LimitExceeded));
    }

    [Test]
    public void CoincidentEndpointsUseOneCellAndZeroSteps()
    {
        var result = WireRouteSolver.Find(Open(Vector3Int.one, Vector3Int.one));
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        Assert.That(result.Path, Has.Count.EqualTo(1));
        Assert.That(result.Steps, Is.Zero);
        Assert.That(result.Bends, Is.Zero);
    }

    [Test]
    public void MatchesExhaustiveSimplePathsOnSmallObstacleMaps()
    {
        var random = new System.Random(90210);
        for (int sample = 0; sample < 80; sample++)
        {
            var request = new WireRouteRequest
            {
                Start = Vector3Int.zero, Target = new Vector3Int(3, 0, 3),
                Min = Vector3Int.zero, Max = new Vector3Int(3, 0, 3),
                PreferTarget = sample % 2 == 0,
                StartDirection = sample % 3 == 0 ? Vector3Int.right : Vector3Int.zero,
                TargetDirection = sample % 5 == 0 ? Vector3Int.forward : Vector3Int.zero
            };
            for (int x = 0; x <= 3; x++)
            for (int z = 0; z <= 3; z++)
            {
                var cell = new Vector3Int(x, 0, z);
                if (cell != request.Start && cell != request.Target && random.Next(4) == 0)
                    request.Blocked.Add(cell);
            }
            List<int> expectedRuns = null;
            int expectedCost = int.MaxValue, expectedBends = int.MaxValue;
            EnumerateSimplePaths(request, new List<Vector3Int> { request.Start },
                ref expectedCost, ref expectedBends, ref expectedRuns);
            WireRouteResult actual = WireRouteSolver.Find(request);
            string context = "Map " + sample;
            Assert.That(actual.Status, Is.EqualTo(expectedRuns == null
                ? WireRouteStatus.Unreachable : WireRouteStatus.Success), context);
            if (expectedRuns == null) continue;
            Assert.That(actual.Steps + 2 * actual.Bends, Is.EqualTo(expectedCost), context);
            Assert.That(actual.Bends, Is.EqualTo(expectedBends), context);
            CollectionAssert.AreEqual(expectedRuns, RunLengths(actual.Path, request.PreferTarget), context);
        }
    }

    private static void EnumerateSimplePaths(WireRouteRequest request, List<Vector3Int> path,
        ref int bestCost, ref int bestBends, ref List<int> bestRuns)
    {
        Vector3Int current = path[path.Count - 1];
        if (current == request.Target)
        {
            var runs = RunLengths(path, request.PreferTarget);
            int bends = runs.Count - 1, cost = path.Count - 1 + 2 * bends;
            bool betterRuns = false;
            if (bestRuns != null && bends == bestBends)
                for (int i = 0; i < runs.Count; i++)
                    if (runs[i] != bestRuns[i]) { betterRuns = runs[i] > bestRuns[i]; break; }
            if (cost < bestCost || cost == bestCost && (bends < bestBends || bends == bestBends && betterRuns))
            {
                bestCost = cost; bestBends = bends; bestRuns = runs;
            }
            return;
        }
        foreach (var direction in new[] { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back })
        {
            Vector3Int next = current + direction;
            if (next.x < 0 || next.x > 3 || next.z < 0 || next.z > 3 ||
                request.Blocked.Contains(next) || path.Contains(next)) continue;
            if (path.Count == 1 && request.StartDirection != Vector3Int.zero && direction != request.StartDirection) continue;
            if (next == request.Target && request.TargetDirection != Vector3Int.zero && direction != request.TargetDirection) continue;
            path.Add(next);
            EnumerateSimplePaths(request, path, ref bestCost, ref bestBends, ref bestRuns);
            path.RemoveAt(path.Count - 1);
        }
    }

    private static List<int> RunLengths(List<Vector3Int> path, bool fromTarget)
    {
        var runs = new List<int>();
        Vector3Int previous = Vector3Int.zero;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3Int direction = path[i] - path[i - 1];
            if (direction != previous) runs.Add(1);
            else runs[runs.Count - 1]++;
            previous = direction;
        }
        if (fromTarget) runs.Reverse();
        return runs;
    }

    [Test]
    public void LegacyPredicateEntryPointHasABoundedSnapshotAndSearch()
    {
        int calls = 0;
        Vector3Int target = Vector3Int.right * 3;
        bool found = LogicWirePathfinder.TryFindPath(Vector3Int.zero, target, cell =>
        {
            calls++;
            Vector3Int delta = cell - target;
            return Math.Abs(delta.x) + Math.Abs(delta.y) + Math.Abs(delta.z) != 1;
        }, out List<Vector3Int> path);
        Assert.That(found, Is.False);
        Assert.That(path, Is.Null);
        Assert.That(calls, Is.LessThanOrEqualTo(262145));
    }

    private static WireRouteRequest Open(Vector3Int start, Vector3Int target)
        => new WireRouteRequest { Start = start, Target = target,
            Min = new Vector3Int(-8, -8, -8), Max = new Vector3Int(8, 8, 8) };

    private static WireRouteRequest TwoCorridors(int detourHeight)
    {
        var request = Open(Vector3Int.zero, Vector3Int.right * 3);
        request.Min = Vector3Int.zero;
        request.Max = new Vector3Int(3, detourHeight, 1);
        var allowed = new HashSet<Vector3Int>
        {
            Vector3Int.zero, Vector3Int.forward, new Vector3Int(1, 0, 1),
            new Vector3Int(2, 0, 1), Vector3Int.right * 2, Vector3Int.right * 3
        };
        for (int y = 0; y <= detourHeight; y++)
        {
            allowed.Add(new Vector3Int(0, y, 0));
            allowed.Add(new Vector3Int(3, y, 0));
        }
        for (int x = 0; x <= 3; x++) allowed.Add(new Vector3Int(x, detourHeight, 0));
        for (int x = 0; x <= 3; x++)
        for (int y = 0; y <= detourHeight; y++)
        for (int z = 0; z <= 1; z++)
        {
            var cell = new Vector3Int(x, y, z);
            if (!allowed.Contains(cell)) request.Blocked.Add(cell);
        }
        return request;
    }

    private static Vector3Int Rotate(Vector3Int v, int rotation)
    {
        switch (rotation)
        {
            case 1: return new Vector3Int(-v.z, v.y, v.x);
            case 2: return new Vector3Int(-v.x, v.y, -v.z);
            case 3: return new Vector3Int(v.z, v.y, -v.x);
            case 4: return new Vector3Int(v.y, -v.x, v.z);
            case 5: return new Vector3Int(-v.y, v.x, v.z);
            default: return v;
        }
    }
}
