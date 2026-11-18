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
        Assert.That(entries.Count(s=>s.Item!=null && !s.Item.Id.StartsWith("package:")), Is.EqualTo(14));
        Assert.That(inventory.Catalog.Items.Count, Is.EqualTo(14));
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
    public IEnumerator PackagingWandCanBeAssignedAndSelectedWithoutAPlaceableBlock()
    {
        yield return LoadGameplay();
        yield return Hover(entries[13]);
        Assert.That(entries[13].Item.ItemType, Is.EqualTo(InventoryItemType.PackagingWand));

        yield return Tap(Key.Digit9);
        Assert.That(hotbar.GetSlot(8).Item.Id, Is.EqualTo("packaging_wand"));
        Assert.That(hotbar.GetSlot(8).BlockPrefab, Is.Null);

        inventory.SetOpen(false);
        yield return null;
        yield return Tap(Key.Digit9);
        Assert.That(hotbar.SelectedBlock, Is.Null);
        Assert.That(hotbar.IsPackagingWandSelected, Is.True);
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
        // LogAssert.Expect(LogType.Log,"Pause");
        yield return Tap(Key.Escape);
        Assert.That(PauseManager.IsPaused, Is.True);
        inventory.SetHoveredSlot(entries[0]);
        Assert.That(inventory.AssignHoveredItem(1), Is.False);
        Assert.That(hotbar.GetSlot(1).BlockPrefab, Is.EqualTo(original));
        // LogAssert.Expect(LogType.Log,"Resume");
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
        Assert.That(hotbar.SelectedBlock, Is.EqualTo(entries[9].Item.Prefab), "Clock assignment and selection must precede placement.");
        var body = player.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.position = new Vector3(80,20,80);
        body.linearVelocity = Vector3.zero;
        Set(mouse.delta, Vector2.zero);
        yield return new WaitForFixedUpdate();
        yield return null;
        var camera = player.cameraSettings.camera;
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // Player.Update owns camera rotation; aim along its actual ray rather than
        // assuming world-forward. Keep enough distance for placement beside the target.
        target.transform.position = Vector3Int.RoundToInt(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)).GetPoint(4));
        target.AddComponent<Block>();
        Physics.SyncTransforms();
        yield return null;
        var ray = camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
        Assert.That(Physics.Raycast(ray, out var hit, player.reachDistance), Is.True, "The placement target must be in reach.");
        Assert.That(hit.collider.gameObject, Is.EqualTo(target), "The gameplay ray must hit the test target.");
        var rotation = Quaternion.Euler(0,Mathf.Round(player.transform.eulerAngles.y/90f)*90f,0);
        Assert.That(BlockPlacement.TryGetAnchor(hotbar.SelectedBlock,hit.point,hit.normal,rotation,out var anchor),
            Is.True, "The test must provide an unoccupied placement cell outside the player collider.");
        int count = Object.FindObjectsByType<LogicClockSource>(FindObjectsSortMode.None).Length;
        var placeButton = TestMouseBindings.GetControl(mouse, KeybindSettings.PlaceMouseButton);
        Press(placeButton); yield return null;
        Release(placeButton); yield return null;
        Assert.That(Object.FindObjectsByType<LogicClockSource>(FindObjectsSortMode.None).Length, Is.EqualTo(count+1));
        Assert.That(Object.FindObjectsByType<LogicClockSource>(FindObjectsSortMode.None)
            .Any(clock => clock.transform.position == (Vector3)anchor), Is.True, "The assigned Clock must be placed at the expected cell.");
        Object.Destroy(target);
        LogAssert.NoUnexpectedReceived();
    }

    private IEnumerator Click(UnityEngine.InputSystem.Controls.ButtonControl button)
    {
        // Input events apply on the next frame; LateUpdate then reacts before the following one.
        Press(button); yield return null; yield return null;
        Release(button); yield return null; yield return null;
    }

    private static Vector2 ScreenCenter(Component component)
    {
        var rect = (RectTransform)component.transform;
        return rect.TransformPoint(rect.rect.center);
    }

    [UnityTest]
    public IEnumerator InventoryCoversEightyPercentOfTheScreenAboveTheVisibleHotbar()
    {
        yield return LoadGameplay();
        yield return null;
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        inventory.PanelRect.GetWorldCorners(corners);
        Assert.That((corners[2].x - corners[0].x) / Screen.width, Is.EqualTo(.8f).Within(.02f));
        Assert.That((corners[2].y - corners[0].y) / Screen.height, Is.EqualTo(.8f).Within(.02f));
        var hotbarCorners = new Vector3[4];
        ((RectTransform)hotbar.GetSlot(0).transform).GetWorldCorners(hotbarCorners);
        Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(hotbarCorners[1].y), "The hotbar stays uncovered for drag and drop.");
        Assert.That(hotbar.GetComponentInParent<Canvas>().sortingOrder,
            Is.GreaterThan(inventory.GetComponentInParent<Canvas>().sortingOrder));
        var grid = inventory.GetComponentInChildren<UnityEngine.UI.GridLayoutGroup>();
        Assert.That(grid.cellSize.x, Is.EqualTo(grid.cellSize.y));
        Assert.That(grid.spacing.x, Is.EqualTo(grid.cellSize.x * .1f).Within(.001f));
        var gridCorners = new Vector3[4];
        ((RectTransform)grid.transform).GetWorldCorners(gridCorners);
        Assert.That(gridCorners[0].x, Is.GreaterThanOrEqualTo(corners[0].x));
        Assert.That(gridCorners[2].x, Is.LessThanOrEqualTo(corners[2].x));
        Assert.That(gridCorners[0].y, Is.GreaterThanOrEqualTo(corners[0].y));
        Assert.That(gridCorners[2].y, Is.LessThanOrEqualTo(corners[2].y));
        inventory.SetOpen(false);
        Assert.That(hotbar.GetComponentInParent<Canvas>().sortingOrder, Is.EqualTo(10));
    }

    [UnityTest]
    public IEnumerator ScrollSelectsAHotbarSlotAndDoubleClickFillsIt()
    {
        yield return LoadGameplay();
        Assert.That(hotbar.SelectedSlotIndex, Is.Zero);
        for (int i = 0; i < 4; i++)
        {
            Set(mouse.scroll, new Vector2(0, -120)); yield return null;
            Set(mouse.scroll, Vector2.zero); yield return null;
        }
        Assert.That(hotbar.SelectedSlotIndex, Is.EqualTo(4), "Scrolling in the inventory moves the hotbar selection.");
        Set(mouse.scroll, new Vector2(0, 120)); yield return null;
        Set(mouse.scroll, Vector2.zero); yield return null;
        Assert.That(hotbar.SelectedSlotIndex, Is.EqualTo(3));

        yield return Hover(entries[7]);
        yield return Click(mouse.leftButton);
        Assert.That(hotbar.GetSlot(3).Item.Id, Is.Not.EqualTo("xnor"), "A single click does not assign.");
        yield return Click(mouse.leftButton);
        Assert.That(hotbar.GetSlot(3).Item.Id, Is.EqualTo("xnor"));
        Assert.That(InventoryGrid.IsOpen, Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ShiftClickFillsTheFirstEmptySlotOrReplacesSlotNine()
    {
        yield return LoadGameplay();
        hotbar.GetSlot(2).SetItem(null);
        hotbar.GetSlot(5).SetItem(null);
        yield return Hover(entries[7]);
        Press(keyboard.leftShiftKey); yield return null;
        yield return Click(mouse.leftButton);
        Assert.That(hotbar.GetSlot(2).Item.Id, Is.EqualTo("xnor"));
        Assert.That(hotbar.GetSlot(5).Item, Is.Null);
        yield return Click(mouse.leftButton);
        Assert.That(hotbar.GetSlot(5).Item.Id, Is.EqualTo("xnor"));
        yield return Hover(entries[9]);
        yield return Click(mouse.leftButton);
        Assert.That(hotbar.GetSlot(8).Item.Id, Is.EqualTo("clock"), "A full hotbar replaces slot 9.");
        Release(keyboard.leftShiftKey); yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator HoldingSelectDragsAnItemOntoAnyHotbarSlot()
    {
        yield return LoadGameplay();
        var before = hotbar.GetSlot(6).Item.Id;
        yield return Hover(entries[7]);
        Press(mouse.leftButton); yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        yield return null;
        Assert.That(inventory.IsDragging, Is.True);
        Set(mouse.position, ScreenCenter(hotbar.GetSlot(6))); yield return null; yield return null;
        Assert.That(inventory.HotbarSlotAt(ScreenCenter(hotbar.GetSlot(6))), Is.EqualTo(6));
        Release(mouse.leftButton); yield return null; yield return null;
        Assert.That(inventory.IsDragging, Is.False);
        Assert.That(hotbar.GetSlot(6).Item.Id, Is.EqualTo("xnor"));
        Assert.That(before, Is.Not.EqualTo("xnor"));

        // Dropping away from the hotbar changes nothing.
        yield return Hover(entries[9]);
        Press(mouse.leftButton); yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        yield return null;
        Set(mouse.position, ScreenCenter(inventory.PanelRect)); yield return null; yield return null;
        Release(mouse.leftButton); yield return null; yield return null;
        Assert.That(Enumerable.Range(0, 9).Count(i => hotbar.GetSlot(i).Item?.Id == "clock"), Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }
}
