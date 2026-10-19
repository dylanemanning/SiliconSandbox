using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class LogicWireBlockTests
{
    [Test]
    public void AdjacentWiresConnectWhenEitherEndpointFacesTheOther()
    {
        var firstObject = new GameObject("First Wire");
        var alignedObject = new GameObject("Aligned Wire");
        var parallelObject = new GameObject("Parallel Wire");
        var junctionObject = new GameObject("Junction Wire");
        try
        {
            Quaternion rotation = Quaternion.Euler(0f, 90f, 90f);
            firstObject.transform.SetPositionAndRotation(Vector3.zero, rotation);
            alignedObject.transform.SetPositionAndRotation(Vector3.forward, rotation);
            parallelObject.transform.SetPositionAndRotation(Vector3.right, rotation);
            junctionObject.transform.SetPositionAndRotation(
                Vector3.right,
                Quaternion.Euler(0f, 0f, 90f));

            LogicWireBlock first = firstObject.AddComponent<LogicWireBlock>();
            LogicWireBlock aligned = alignedObject.AddComponent<LogicWireBlock>();
            LogicWireBlock parallel = parallelObject.AddComponent<LogicWireBlock>();
            LogicWireBlock junction = junctionObject.AddComponent<LogicWireBlock>();
            first.SetEndpointMask(
                LogicWireBlock.GetDirectionBit(Vector3Int.forward) |
                LogicWireBlock.GetDirectionBit(Vector3Int.right));
            aligned.SetEndpointMask(LogicWireBlock.GetDirectionBit(Vector3Int.back));
            parallel.SetEndpointMask(
                LogicWireBlock.GetDirectionBit(Vector3Int.up) |
                LogicWireBlock.GetDirectionBit(Vector3Int.down));

            Assert.IsTrue(first.ConnectsTo(aligned, Vector3Int.forward));
            Assert.IsTrue(first.ConnectsTo(parallel, Vector3Int.right));
            Assert.IsTrue(parallel.ConnectsTo(first, Vector3Int.zero));
            Assert.IsTrue(first.ConnectsTo(junction, Vector3Int.right));
            Assert.IsTrue(junction.ConnectsTo(first, Vector3Int.zero));
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(alignedObject);
            Object.DestroyImmediate(parallelObject);
            Object.DestroyImmediate(junctionObject);
        }
    }

    [TestCase(0f, 0f, 90f, 1, 0, 0)]
    [TestCase(0f, 90f, 90f, 0, 0, 1)]
    public void EndpointsFollowTheWiresRotatedLocalYAxis(
        float xRotation,
        float yRotation,
        float zRotation,
        int axisX,
        int axisY,
        int axisZ)
    {
        var gameObject = new GameObject("Test Wire");
        try
        {
            gameObject.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.Euler(xRotation, yRotation, zRotation));
            LogicWireBlock wire = gameObject.AddComponent<LogicWireBlock>();
            var axis = new Vector3Int(axisX, axisY, axisZ);

            Assert.IsTrue(wire.HasEndpointTowards(axis));
            Assert.IsTrue(wire.HasEndpointTowards(-axis));
            Assert.IsFalse(wire.HasEndpointTowards(Vector3Int.up));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void CustomEndpointMaskAllowsTurnedWireConnections()
    {
        var firstObject = new GameObject("Turn Wire");
        var nextObject = new GameObject("Next Wire");
        try
        {
            firstObject.transform.position = Vector3.zero;
            nextObject.transform.position = Vector3.forward;
            LogicWireBlock first = firstObject.AddComponent<LogicWireBlock>();
            LogicWireBlock next = nextObject.AddComponent<LogicWireBlock>();

            first.SetEndpointMask(
                LogicWireBlock.GetDirectionBit(Vector3Int.right) |
                LogicWireBlock.GetDirectionBit(new Vector3Int(0, 0, 1)));
            next.SetEndpointMask(LogicWireBlock.GetDirectionBit(Vector3Int.back));

            Assert.IsTrue(first.ConnectsTo(next, Vector3Int.forward));
            Assert.IsTrue(first.HasEndpointTowards(Vector3Int.right));
            Assert.IsTrue(first.HasEndpointTowards(Vector3Int.forward));
            Assert.IsFalse(first.HasEndpointTowards(Vector3Int.up));
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(nextObject);
        }
    }

    [Test]
    public void AdjacentParallelWiresConnectOnlyWhenEitherEndpointFacesTheOther()
    {
        Vector3Int firstPosition = new Vector3Int(10000, 10000, 10000);
        Vector3Int nextPosition = firstPosition + Vector3Int.right;
        var firstObject = new GameObject("First Wire");
        var nextObject = new GameObject("Next Wire");
        try
        {
            firstObject.transform.position = firstPosition;
            nextObject.transform.position = nextPosition;
            LogicWireBlock first = firstObject.AddComponent<LogicWireBlock>();
            LogicWireBlock next = nextObject.AddComponent<LogicWireBlock>();
            RegisterBlock(first);
            RegisterBlock(next);
            Assert.That(LogicSignalBlock.At(firstPosition), Is.SameAs(first));
            Assert.That(LogicSignalBlock.At(nextPosition), Is.SameAs(next));
            first.SetEndpointMask(
                LogicWireBlock.GetDirectionBit(Vector3Int.up) |
                LogicWireBlock.GetDirectionBit(Vector3Int.down));
            next.SetEndpointMask(
                LogicWireBlock.GetDirectionBit(Vector3Int.up) |
                LogicWireBlock.GetDirectionBit(Vector3Int.down));

            Assert.IsFalse(first.ConnectsTo(next, firstPosition + Vector3Int.right));
            RefreshConnections(first);
            RefreshConnections(next);
            Assert.AreEqual(0, first.ConnectionCount);
            Assert.AreEqual(0, next.ConnectionCount);

            first.AddEndpointTowards(Vector3Int.right);
            Assert.IsTrue(first.ConnectsTo(next, firstPosition + Vector3Int.right));
            Assert.IsTrue(next.ConnectsTo(first, firstPosition));
            RefreshConnections(first);
            RefreshConnections(next);
            Assert.AreEqual(1, first.ConnectionMask & 1);
            Assert.AreEqual(1 << 1, next.ConnectionMask & (1 << 1));

            first.SetEndpointMask(LogicWireBlock.GetDirectionBit(Vector3Int.up));
            next.SetEndpointMask(LogicWireBlock.GetDirectionBit(Vector3Int.left));
            Assert.IsTrue(next.ConnectsTo(first, firstPosition));
            RefreshConnections(first);
            RefreshConnections(next);
            Assert.AreEqual(1, first.ConnectionMask & 1);
            Assert.AreEqual(1 << 1, next.ConnectionMask & (1 << 1));
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(nextObject);
        }
    }

    private static void RefreshConnections(LogicWireBlock wire)
    {
        typeof(LogicWireBlock)
            .GetMethod("RefreshConnections", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)
            .Invoke(wire, null);
    }

    private static void RegisterBlock(LogicSignalBlock block)
    {
        typeof(LogicSignalBlock)
            .GetMethod("Register", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)
            .Invoke(block, null);
    }

    [Test]
    public void GateTargetUsesTheFacingPortNearestTheHitPoint()
    {
        var gateObject = new GameObject("Gate");
        try
        {
            GateBlock gate = gateObject.AddComponent<GateBlock>();
            gate.GateType = GateType.And;

            Assert.IsTrue(gate.TryGetFacingPort(
                Vector3Int.back,
                new Vector3(1f, 0f, -0.5f),
                out Vector3Int cell,
                out Vector3Int face));

            Assert.AreEqual(Vector3Int.right, cell);
            Assert.AreEqual(Vector3Int.back, face);
        }
        finally
        {
            Object.DestroyImmediate(gateObject);
        }
    }

    [Test]
    public void RoutePreviewVisualUsesTheRouteEndpointMask()
    {
        var wireObject = new GameObject("Preview Wire");
        try
        {
            wireObject.AddComponent<LogicWireBlock>();
            WireModelVisual visual = wireObject.AddComponent<WireModelVisual>();
            visual.straightModel = new GameObject("Straight");
            visual.elbowModel = new GameObject("Elbow");
            visual.straightModel.transform.SetParent(wireObject.transform);
            visual.elbowModel.transform.SetParent(wireObject.transform);
            visual.branches = new GameObject[6];
            for (int index = 0; index < visual.branches.Length; index++)
            {
                visual.branches[index] = new GameObject("Branch " + index);
                visual.branches[index].transform.SetParent(wireObject.transform);
            }

            int mask = LogicWireBlock.GetDirectionBit(Vector3Int.right) |
                       LogicWireBlock.GetDirectionBit(Vector3Int.up) |
                       LogicWireBlock.GetDirectionBit(Vector3Int.forward);
            visual.RefreshForConnections(mask);

            Assert.IsTrue(visual.branches[0].activeSelf);
            Assert.IsTrue(visual.branches[2].activeSelf);
            Assert.IsTrue(visual.branches[4].activeSelf);
            Assert.AreEqual(3, System.Array.FindAll(visual.branches, branch => branch.activeSelf).Length);
        }
        finally
        {
            Object.DestroyImmediate(wireObject);
        }
    }

    [Test]
    public void PathFinderReturnsShortestTurningPath()
    {
        bool found = LogicWirePathfinder.TryFindPath(
            Vector3Int.zero,
            new Vector3Int(1, 0, 1),
            _ => true,
            out var path);

        Assert.IsTrue(found);
        Assert.AreEqual(3, path.Count);
        Assert.AreEqual(Vector3Int.zero, path[0]);
        Assert.AreEqual(new Vector3Int(1, 0, 1), path[path.Count - 1]);
        Assert.AreNotEqual(path[1] - path[0], path[2] - path[1]);
    }

    [Test]
    public void PathFinderFollowsTheLineBetweenEndpointsWithoutUnneededVerticalSteps()
    {
        bool found = LogicWirePathfinder.TryFindPath(
            Vector3Int.zero,
            new Vector3Int(4, 0, 2),
            _ => true,
            out var path);

        Assert.IsTrue(found);
        Assert.AreEqual(Vector3Int.right, path[1] - path[0]);
        Assert.IsTrue(path.TrueForAll(cell => cell.y == 0));
        Assert.AreEqual(new Vector3Int(4, 0, 2), path[path.Count - 1]);
        int turns = 0;
        for (int index = 2; index < path.Count; index++)
            if (path[index] - path[index - 1] != path[index - 1] - path[index - 2]) turns++;
        Assert.AreEqual(1, turns);
    }

    [Test]
    public void PathFinderMinimizesTurnsBeforeDistance()
    {
        var allowed = new HashSet<Vector3Int>
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1),
            new Vector3Int(2, 0, 1),
            new Vector3Int(2, 0, 0),
            new Vector3Int(3, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, 2, 0),
            new Vector3Int(1, 2, 0),
            new Vector3Int(2, 2, 0),
            new Vector3Int(3, 2, 0),
            new Vector3Int(3, 1, 0)
        };

        bool found = LogicWirePathfinder.TryFindPath(
            Vector3Int.zero,
            new Vector3Int(3, 0, 0),
            allowed.Contains,
            out var path);

        Assert.IsTrue(found);
        Assert.AreEqual(8, path.Count, "The two-turn route is longer than the available four-turn route.");
        int turns = 0;
        for (int index = 2; index < path.Count; index++)
            if (path[index] - path[index - 1] != path[index - 1] - path[index - 2]) turns++;
        Assert.AreEqual(2, turns);
    }

    [Test]
    public void PathFinderRoutesAroundOccupiedCells()
    {
        Vector3Int blocked = Vector3Int.right;
        bool found = LogicWirePathfinder.TryFindPath(
            Vector3Int.zero,
            new Vector3Int(2, 0, 0),
            cell => cell.y == 0 && cell.x >= 0 && cell.x <= 2 && cell.z >= -1 && cell.z <= 1 && cell != blocked,
            out var path);

        Assert.IsTrue(found);
        Assert.AreEqual(5, path.Count);
        CollectionAssert.DoesNotContain(path, blocked);
    }

    [Test]
    public void SurfaceHitMapsToTheHitGroundCellInsteadOfItsOrigin()
    {
        Vector3Int firstCell = LogicWirePathfinder.GetCellBeyondSurfaceHit(
            new Vector3(8.2f, 0.05f, -3.4f),
            Vector3.up);
        Vector3Int secondCell = LogicWirePathfinder.GetCellBeyondSurfaceHit(
            new Vector3(12.3f, 0.05f, 5.1f),
            Vector3.up);

        Assert.AreEqual(new Vector3Int(8, 1, -3), firstCell);
        Assert.AreEqual(new Vector3Int(12, 1, 5), secondCell);
        Assert.AreNotEqual(firstCell, secondCell);
    }
}