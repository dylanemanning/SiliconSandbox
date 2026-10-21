using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SourceRoutingTests
{
    private static IEnumerable<TestCaseData> Faces()
    {
        foreach (int yaw in new[]{0,90,180,270})
        foreach (bool clock in new[]{false,true})
        foreach (int side in new[]{-1,1})
            yield return new TestCaseData(yaw,clock,side);
    }

    [TestCaseSource(nameof(Faces))]
    public void RoutingUsesTargetedSourceTerminalEvenWhenPlayerHeadingIsPerpendicular(int yaw,bool clock,int side)
    {
        var playerObject = new GameObject("Routing test player");
        playerObject.SetActive(false);
        var sourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wireObject = new GameObject("Wire prefab test");
        try
        {
            var player = playerObject.AddComponent<Player>();
            sourceObject.transform.SetPositionAndRotation(new Vector3(90,20,90),Quaternion.Euler(0,yaw,0));
            var block = sourceObject.AddComponent<Block>();
            var source = clock ? (LogicSignalSource)sourceObject.AddComponent<LogicClockSource>() : sourceObject.AddComponent<LogicSignalSource>();
            var wirePrefab = wireObject.AddComponent<Block>();
            Vector3 face = sourceObject.transform.forward*side;
            // Camera yaw can be perpendicular when looking down from above a terminal.
            playerObject.transform.forward = sourceObject.transform.right;
            Physics.SyncTransforms();
            Assert.That(sourceObject.GetComponent<Collider>().Raycast(new Ray(sourceObject.transform.position+face*3,-face),out var hit,5),Is.True);
            typeof(Player).GetField("targetBlock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,block);
            typeof(Player).GetField("targetRaycastHit",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,hit);
            object[] args = {wirePrefab,default(Vector3Int),default(Vector3Int),default(Quaternion)};
            bool found = (bool)typeof(Player).GetMethod("TryGetWireTarget",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,args);
            Assert.That(found,Is.True);
            Assert.That((Vector3Int)args[1],Is.EqualTo(source.GridPosition+Vector3Int.RoundToInt(face)),"Wire must attach to the terminal that was hit, regardless of player heading.");
            Assert.That((Vector3Int)args[2],Is.EqualTo(source.GridPosition));
            Assert.That(Vector3.Distance((Quaternion)args[3]*Vector3.up,face),Is.LessThan(.001f));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(wireObject);
        }
    }
}
