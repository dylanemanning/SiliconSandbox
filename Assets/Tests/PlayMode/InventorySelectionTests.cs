using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class InventorySelectionTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private InventoryGrid inventory;
    private HotbarManager hotbar;
    private InventorySlotImage[] entries;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        PlayerPrefs.DeleteKey("Keybind.Pause");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        WorldSaveSystem.PendingWorldName = null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        yield return null;
    }

    private IEnumerator LoadGameplay()
    {
        WorldSaveSystem.PendingWorldName = "TC_InventorySelection_"+System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        yield return null;
        inventory = Object.FindFirstObjectByType<InventoryGrid>();
        hotbar = Object.FindFirstObjectByType<Player>().hotbar;
        entries = inventory.GetComponentsInChildren<InventorySlotImage>(true);
        inventory.SetOpen(true);
        Canvas.ForceUpdateCanvases();
    }

    // Exercise the real EventSystem raycast and InputSystem UI module, not just the handler.
    private IEnumerator Hover(InventorySlotImage entry)
    {
        var rect = entry.GetComponent<RectTransform>();
        Set(mouse.position, (Vector2)rect.TransformPoint(rect.rect.center));
        yield return null;
        yield return null;
    }

    private IEnumerator Tap(Key key)
    {
        Press(keyboard[key]); yield return null;
        Release(keyboard[key]); yield return null;
    }

    [UnityTest]
    public IEnumerator HoverAndNumberKeysReplaceEveryDestinationWithoutConsumingCatalogItems()
    {
        yield return LoadGameplay();
        var original = Enumerable.Range(0,9).Select(i=>hotbar.GetSlot(i).BlockPrefab).ToArray();
        int selected = hotbar.SelectedSlotIndex;
        yield return Hover(entries[7]);
        Assert.That(inventory.HoveredSlot, Is.EqualTo(entries[7]));
        Assert.That(inventory.AssignmentHint, Does.Contain("XNOR"));
        Assert.That(entries[7].GetComponent<UnityEngine.UI.Image>().color,
            Is.Not.EqualTo(entries[6].GetComponent<UnityEngine.UI.Image>().color));
        for (int i = 0; i < 9; i++)
        {
            yield return Tap((Key)((int)Key.Digit1+i));
            Assert.That(hotbar.GetSlot(i).BlockPrefab, Is.EqualTo(entries[7].Item.Prefab));
            Assert.That(hotbar.GetSlot(i).Item.Id, Is.EqualTo("xnor"));
            Assert.That(inventory.AssignmentHint, Does.Contain("slot "+(i+1)));
            for (int untouched = i+1; untouched < 9; untouched++)
                Assert.That(hotbar.GetSlot(untouched).BlockPrefab, Is.EqualTo(original[untouched]));
        }
        Assert.That(hotbar.SelectedSlotIndex, Is.EqualTo(selected));
        Assert.That(entries.Count(s=>s.Item!=null), Is.EqualTo(13));
        Assert.That(inventory.Catalog.Items.Count, Is.EqualTo(13));
        yield return Hover(entries[9]);
        yield return Tap(Key.Digit9);
        Assert.That(hotbar.GetSlot(8).Item.Id, Is.EqualTo("clock"));
        Assert.That(hotbar.GetSlot(7).Item.Id, Is.EqualTo("xnor"));
        inventory.SetOpen(false);
        yield return null;
        yield return Tap(Key.Digit9);
        Assert.That(hotbar.SelectedBlock, Is.EqualTo(entries[9].Item.Prefab));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator EmptyHoverExitCloseAndPauseCannotAssignStaleEntries()
    {
        yield return LoadGameplay();
        var original = hotbar.GetSlot(1).BlockPrefab;
        yield return Hover(entries[0]);
        var emptyRect = entries[20].GetComponent<RectTransform>();
        Set(mouse.position, (Vector2)emptyRect.TransformPoint(emptyRect.rect.center));
        Press(keyboard.digit2Key); yield return null;
        Release(keyboard.digit2Key); yield return null;
        Assert.That(inventory.HoveredSlot, Is.Null);
        yield return Tap(Key.Digit2);
        Assert.That(hotbar.GetSlot(1).BlockPrefab, Is.EqualTo(original));
        yield return Hover(entries[0]);
        Set(mouse.position, Vector2.zero);
        yield return null; yield return null;
        Assert.That(inventory.HoveredSlot, Is.Null);
        yield return Tap(Key.Digit2);
        Assert.That(hotbar.GetSlot(1).BlockPrefab, Is.EqualTo(original));
        yield return Hover(entries[0]);
        inventory.SetOpen(false);
        Assert.That(inventory.HoveredSlot, Is.Null);
        Assert.That(inventory.AssignHoveredItem(1), Is.False);
        yield return null;
        LogAssert.Expect(LogType.Log,"Pause");
        yield return Tap(Key.Escape);
        Assert.That(PauseManager.IsPaused, Is.True);
        inventory.SetHoveredSlot(entries[0]);
        Assert.That(inventory.AssignHoveredItem(1), Is.False);
        Assert.That(hotbar.GetSlot(1).BlockPrefab, Is.EqualTo(original));
        LogAssert.Expect(LogType.Log,"Resume");
        Object.FindFirstObjectByType<PauseManager>().ResumeGame();
        inventory.SetOpen(true);
        Assert.That(inventory.HoveredSlot, Is.Null);
        Assert.That(inventory.AssignHoveredItem(-1), Is.False);
        Assert.That(hotbar.AssignItem(9,entries[0].Item), Is.False);
        Assert.That(hotbar.AssignItem(0,null), Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator AssignedPrefabIsPlacedByTheNormalGameplayMouseAction()
    {
        yield return LoadGameplay();
        yield return Hover(entries[9]);
        yield return Tap(Key.Digit3);
        inventory.SetOpen(false);
        yield return null;
        yield return Tap(Key.Digit3);
        var player = Object.FindFirstObjectByType<Player>();
        var body = player.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.position = new Vector3(80,20,80);
        body.linearVelocity = Vector3.zero;
        player.transform.rotation = Quaternion.identity;
        player.cameraSettings.camera.transform.localRotation = Quaternion.identity;
        yield return null;
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.transform.position = Vector3Int.RoundToInt(player.cameraSettings.camera.transform.position+Vector3.forward*3);
        target.AddComponent<Block>();
        Physics.SyncTransforms();
        yield return null;
        int count = Object.FindObjectsByType<LogicClockSource>(FindObjectsSortMode.None).Length;
        Press(mouse.rightButton); yield return null;
        Release(mouse.rightButton); yield return null;
        Assert.That(Object.FindObjectsByType<LogicClockSource>(FindObjectsSortMode.None).Length, Is.EqualTo(count+1));
        Object.Destroy(target);
        LogAssert.NoUnexpectedReceived();
    }
}
