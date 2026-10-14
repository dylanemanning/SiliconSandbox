using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GameplayIntegrationTests
{
    [TestCase(0f)] [TestCase(90f)] [TestCase(180f)] [TestCase(270f)]
    public void GatePinsAlignWithPortFaceCellsAtEveryPlacementYaw(float yaw)
    {
        foreach(string name in new[]{"AND","OR","NOT","NAND","NOR","XOR","XNOR","BUFFER"})
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+" Gate.prefab"));
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(3,2,-4),Quaternion.Euler(0,yaw,0));
                var cells=root.GetComponent<Block>().OccupiedCells(new Vector3Int(3,2,-4),root.transform.rotation).ToArray();
                Assert.That(cells.Distinct().Count(),Is.EqualTo(name=="NOT" || name=="BUFFER"?1:4));
                foreach(var port in root.GetComponent<GateBlock>().WorldPorts())
                {
                    string suffix=port.port.Role==PortRole.Output?"Y":port.port.InputIndex==0?"A":"B";
                    var pin=root.GetComponentsInChildren<Renderer>().Single(r=>r.name.Contains("__Pin_"+suffix));
                    Assert.That(Vector3.Distance(pin.bounds.center,port.cell+(Vector3)port.face*.52125f),Is.LessThan(.002f));
                    Assert.That(cells,Does.Contain(port.cell));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    [Test]
    public void MultiCellPlacementRejectsAnObstacleInTheFarCell()
    {
        var gate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AND Gate.prefab").GetComponent<Block>();
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            obstacle.transform.position=new Vector3(21,1,21);
            Assert.That(BlockPlacement.TryGetAnchor(gate,new Vector3(20,.5f,20),Vector3.up,Quaternion.identity,out _),Is.False);
            obstacle.transform.position=new Vector3(24,1,24);
            Assert.That(BlockPlacement.TryGetAnchor(gate,new Vector3(20,.5f,20),Vector3.up,Quaternion.identity,out var anchor),Is.True);
            Assert.That(anchor,Is.EqualTo(new Vector3Int(20,1,20)));
        }
        finally { Object.DestroyImmediate(obstacle); }
    }

    [Test]
    public void HotbarChoicesRemainTheExistingNineSlots()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var manager = new SerializedObject(Object.FindFirstObjectByType<HotbarManager>());
        var configured = manager.FindProperty("slots");
        var slots = Enumerable.Range(0,configured.arraySize)
            .Select(i=>(HotbarSlot)configured.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        Assert.That(slots.Length,Is.EqualTo(9));
        Assert.That(slots.Count(s=>s.BlockPrefab.name=="Block"),Is.EqualTo(5));
        foreach(string name in new[]{"Voltage Source","Ground Source","Wires","Viewer Block"})
            Assert.That(slots.Count(s=>s.BlockPrefab.name==name),Is.EqualTo(1));
    }
}
