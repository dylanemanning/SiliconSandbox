using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameplayModelIntegrationTests
{
    [UnityTearDown]
    public IEnumerator UnloadGameplayBeforeInputFixturesResetDevices()
    {
        Time.timeScale=1;
        WorldSaveSystem.PendingWorldName=null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        yield return null;
    }
#if UNITY_EDITOR
    GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab");
#endif

    [UnityTest]
    public IEnumerator MainGameplayUsesApprovedLightingAndPostProcessing()
    {
        WorldSaveSystem.PendingWorldName="TC_GameplayArt_"+System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;
        Assert.That(Object.FindFirstObjectByType<GameplayArtLighting>(),Is.Not.Null);
        Assert.That(Camera.main.allowHDR,Is.True);
        Assert.That(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing,Is.True);
        var volume=Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Single(v=>v.isGlobal);
        Assert.That(volume.sharedProfile.TryGet<ColorAdjustments>(out var grade),Is.True);
        Assert.That(grade.postExposure.value,Is.EqualTo(.4155f).Within(.0001f));
        Assert.That(grade.saturation.value,Is.EqualTo(-25));
        Assert.That(volume.sharedProfile.TryGet<Tonemapping>(out var tone),Is.True);
        Assert.That(tone.mode.value,Is.EqualTo(TonemappingMode.ACES));
        Assert.That(RenderSettings.ambientMode,Is.EqualTo(AmbientMode.Flat));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator SourceWireAndLedUseApprovedStateModelsAndDisconnectedFacesStayLow()
    {
#if UNITY_EDITOR
        var source=Object.Instantiate(Prefab("Voltage Source"),new Vector3(30,1,30),Quaternion.identity);
        var wire=Object.Instantiate(Prefab("Wires"),new Vector3(30,1,31),Quaternion.identity);
        var led=Object.Instantiate(Prefab("Viewer Block"),new Vector3(30,1,32),Quaternion.identity);
        try
        {
            yield return null;yield return null;yield return null;
            Assert.That(wire.GetComponent<LogicWireBlock>().SignalState,Is.EqualTo(1));
            Assert.That(led.GetComponent<StateViewerBlock>().SignalState,Is.EqualTo(1));
            Assert.That(led.GetComponent<BlockModelVisual>().highModel.activeSelf,Is.True);
            Assert.That(wire.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.name=="SS_one"),Is.True);
            source.GetComponent<LogicSignalSource>().SetState(0);
            yield return null;yield return null;yield return null;
            Assert.That(source.GetComponent<BlockModelVisual>().lowModel.activeSelf,Is.True);
            Assert.That(led.GetComponent<BlockModelVisual>().lowModel.activeSelf,Is.True);
            source.GetComponent<LogicSignalSource>().SetState(1);
            wire.transform.position=new Vector3(31,1,30);
            yield return null;yield return null;yield return null;
            Assert.That(wire.GetComponent<LogicWireBlock>().SignalState,Is.EqualTo(0),"No source pin exists on the +X face.");
        }
        finally { Object.Destroy(source);Object.Destroy(wire);Object.Destroy(led); }
#else
        Assert.Ignore("Prefab integration requires Editor asset access.");yield break;
#endif
    }

    [UnityTest]
    public IEnumerator MultiCellGateRegistersAndClearsAllCellsAndOnlyOutputDrivesWires()
    {
#if UNITY_EDITOR
        var gate=Object.Instantiate(Prefab("AND Gate"),new Vector3(40,1,40),Quaternion.identity);
        var outputWire=Object.Instantiate(Prefab("Wires"),new Vector3(40,1,42),Quaternion.identity);
        var blankFaceWire=Object.Instantiate(Prefab("Wires"),new Vector3(41,1,42),Quaternion.identity);
        try
        {
            gate.GetComponent<GateBlock>().SetDisplayedState(1);
            yield return null;yield return null;
            foreach(var cell in gate.GetComponent<Block>().OccupiedCells(new Vector3Int(40,1,40),Quaternion.identity))
                Assert.That(LogicSignalBlock.At(cell),Is.EqualTo(gate.GetComponent<GateBlock>()));
            // Remove the other wire's wire-to-wire link before checking the unpinned face.
            outputWire.SetActive(false);
            yield return null;yield return null;
            Assert.That(blankFaceWire.GetComponent<LogicWireBlock>().ConnectionCount,Is.EqualTo(0));
            Assert.That(blankFaceWire.GetComponent<LogicWireBlock>().SignalState,Is.EqualTo(0));
            outputWire.SetActive(true);blankFaceWire.SetActive(false);
            yield return null;yield return null;
            Assert.That(outputWire.GetComponent<LogicWireBlock>().SignalState,Is.EqualTo(1));
            gate.SetActive(false);
            foreach(var cell in gate.GetComponent<Block>().OccupiedCells(new Vector3Int(40,1,40),Quaternion.identity))
                Assert.That(LogicSignalBlock.At(cell),Is.Null);
        }
        finally { Object.Destroy(gate);Object.Destroy(outputWire);Object.Destroy(blankFaceWire); }
#else
        Assert.Ignore("Prefab integration requires Editor asset access.");yield break;
#endif
    }
    [UnityTest]
    public IEnumerator WireElbowsAndJunctionBranchesFollowTheirConnectedFaces()
    {
#if UNITY_EDITOR
        var center=Object.Instantiate(Prefab("Wires"),new Vector3(50,1,50),Quaternion.identity);
        var right=Object.Instantiate(Prefab("Wires"),new Vector3(51,1,50),Quaternion.identity);
        var forward=Object.Instantiate(Prefab("Wires"),new Vector3(50,1,51),Quaternion.identity);
        GameObject above=null;
        try
        {
            yield return null;yield return null;
            var visual=center.GetComponent<WireModelVisual>();
            Assert.That(visual.elbowModel.activeSelf,Is.True);
            // Authored elbow endpoints (-Z,+X) must point along the two connections.
            var rotation=visual.elbowModel.transform.rotation;
            var ends=new[]{Vector3Int.RoundToInt(rotation*Vector3.back),Vector3Int.RoundToInt(rotation*Vector3.right)};
            CollectionAssert.AreEquivalent(new[]{Vector3Int.right,Vector3Int.forward},ends);
            above=Object.Instantiate(Prefab("Wires"),new Vector3(50,2,50),Quaternion.identity);
            yield return null;yield return null;
            Assert.That(center.GetComponent<LogicWireBlock>().IsJunction,Is.True);
            Assert.That(visual.branches.Count(b=>b.activeSelf),Is.EqualTo(3));
            foreach(var branch in visual.branches.Where(b=>b.activeSelf))
            {
                var endpoint=branch.transform.TransformPoint(Vector3.forward*.5f);
                var direction=Vector3Int.RoundToInt((endpoint-center.transform.position)*2);
                Assert.That(new[]{Vector3Int.right,Vector3Int.forward,Vector3Int.up},Does.Contain(direction));
            }
        }
        finally { Object.Destroy(center);Object.Destroy(right);Object.Destroy(forward);if(above!=null)Object.Destroy(above); }
#else
        Assert.Ignore("Prefab integration requires Editor asset access.");yield break;
#endif
    }

}
