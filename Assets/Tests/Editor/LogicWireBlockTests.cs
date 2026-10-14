using NUnit.Framework;
using UnityEngine;

public class LogicWireBlockTests
{
    [Test]
    public void AdjacentWiresConnectWhenEitherEndFacesTheOtherWire()
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

            Assert.IsTrue(first.ConnectsTo(aligned));
            Assert.IsFalse(first.ConnectsTo(parallel));
            Assert.IsTrue(first.ConnectsTo(junction));
            Assert.IsTrue(junction.ConnectsTo(first));
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

            Assert.IsTrue(first.ConnectsTo(next));
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