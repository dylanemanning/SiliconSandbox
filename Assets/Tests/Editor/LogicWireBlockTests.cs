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
}