using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// End to end in the gameplay scene: build a circuit with the real prefabs, select
// it with the packaging wand, create the package in the packaging window, find it
// in the inventory, place a copy and drive it.
public class PackagingGameplayTests
{
    private string libraryFolder;

    [SetUp]
    public void UseTemporaryLibrary()
    {
        libraryFolder = Path.Combine(Application.temporaryCachePath, "PackagingGameplay_" + System.Guid.NewGuid().ToString("N"));
        PackageLibrary.DirectoryOverride = libraryFolder;
        PackageLibrary.Reload();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        WorldSaveSystem.PendingWorldName = null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        PackageLibrary.DirectoryOverride = null;
        PackageLibrary.Reload();
        if (Directory.Exists(libraryFolder)) Directory.Delete(libraryFolder, true);
    }

#if UNITY_EDITOR
    private static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");

    private static GameObject Spawn(string prefab, Vector3Int cell, float yaw = 0)
        => Object.Instantiate(Prefab(prefab), cell, Quaternion.Euler(0, yaw, 0));

    private static void Wire(Vector3Int cell)
    {
        Spawn("Wires", cell).GetComponent<LogicWireBlock>().SetEndpointMask(
            LogicWireBlock.GetDirectionBit(Vector3Int.forward) | LogicWireBlock.GetDirectionBit(Vector3Int.back));
    }

    // Renders a camera (and optionally a canvas switched to that camera) to a PNG for review.
    private static void Capture(Camera camera, string file, Canvas canvas = null)
    {
        if (Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var previousMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
        var target = new RenderTexture(1920, 1080, 24);
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = .5f;
        }
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        string folder = Path.Combine(Application.dataPath, "..", "Logs");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.Destroy(image);
        target.Release();
        if (canvas != null) canvas.renderMode = previousMode;
    }
#endif

    [UnityTest]
    public IEnumerator WandBoxBecomesAnInventoryPackageThatWorksWhenPlaced()
    {
#if UNITY_EDITOR
        WorldSaveSystem.PendingWorldName = "TC_Packaging_" + System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null; yield return null;
        var player = Object.FindFirstObjectByType<Player>();
        var wand = player.GetComponent<PackagingWandController>();

        // A NAND from an AND gate and a NOT gate, with wires to the box edge.
        Spawn("AND Gate", new Vector3Int(40, 1, 40));
        Spawn("NOT Gate", new Vector3Int(40, 1, 42));
        Wire(new Vector3Int(40, 1, 39));
        Wire(new Vector3Int(41, 1, 39));
        yield return null; yield return null;

        wand.SelectCorner(new Vector3Int(40, 1, 39));
        wand.SelectCorner(new Vector3Int(41, 1, 42));
        Assert.That(PackagingWandController.IsConfirmationOpen, Is.True);
        Assert.That(wand.LastDraft.IsValid, Is.True, string.Join("\n", wand.LastDraft.Errors));
        var panel = wand.Panel;
        panel.SetName("My NAND");
        Assert.That(GameObject.Find("Marking field").GetComponent<TMP_InputField>().text, Is.EqualTo("MY NAND"));
        Assert.That(panel.GetComponentsInChildren<TMP_InputField>().Count(f => f.name == "Port name"), Is.EqualTo(3));
        yield return null;
        Capture(player.cameraSettings.camera, "packaging-panel.png", panel.GetComponent<Canvas>());

        var create = GameObject.Find("ConfirmButton").GetComponent<Button>();
        Assert.That(create.interactable, Is.True);
        create.onClick.Invoke();
        Assert.That(PackagingWandController.IsConfirmationOpen, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        var definition = wand.LastCreated;
        Assert.That(definition, Is.Not.Null);
        Assert.That(PackageLibrary.Get(definition.id), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<GateBlock>(FindObjectsSortMode.None).Length, Is.EqualTo(2),
            "The source circuit stays in the world.");

        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        var entry = inventory.GetComponentsInChildren<InventorySlotImage>(true).FirstOrDefault(s => s.Item?.Id == "package:" + definition.id);
        Assert.That(entry, Is.Not.Null, "The package is listed in the inventory.");
        Assert.That(player.hotbar.AssignItem(8, entry.Item), Is.True, "The package can go in the hotbar.");

        // Place a copy and drive it.
        var package = PackageLibrary.CreateInstance(definition, new Vector3(50, 1, 40), Quaternion.identity);
        var a = Spawn("Voltage Source", new Vector3Int(50, 1, 39)).GetComponent<LogicSignalSource>();
        var b = Spawn("Voltage Source", new Vector3Int(51, 1, 39)).GetComponent<LogicSignalSource>();
        var led = Spawn("Viewer Block", new Vector3Int(50, 1, 41)).GetComponent<StateViewerBlock>();
        yield return null; yield return null; yield return null;
        if (SimulationDriver.SimulationAvailable)
        {
            Assert.That(led.SignalState, Is.EqualTo(1), "0 NAND 0 = 1");
            a.SetState(1); b.SetState(1);
            yield return null; yield return null;
            Assert.That(led.SignalState, Is.EqualTo(0), "1 NAND 1 = 0");
            Assert.That(package.PortState(2), Is.EqualTo(0));
        }

        var view = player.cameraSettings.camera;
        player.enabled = false;
        view.transform.SetPositionAndRotation(new Vector3(50.5f, 4f, 36.2f), Quaternion.Euler(45, 0, 0));
        yield return null;
        Capture(view, "packaging-placed.png");
        LogAssert.NoUnexpectedReceived();
#else
        Assert.Ignore("Needs editor prefab access.");
        yield break;
#endif
    }
}
