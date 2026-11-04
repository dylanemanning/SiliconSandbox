using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class HotbarPresentationTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();mouse = InputSystem.AddDevice<Mouse>();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (keyboard != null) InputSystem.ResetDevice(keyboard);
        if (mouse != null) InputSystem.ResetDevice(mouse);

        Time.timeScale = 1;
        WorldSaveSystem.PendingWorldName = null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        yield return null;
    }

    private IEnumerator LoadGameplay()
    {
        WorldSaveSystem.PendingWorldName = "TC_Hotbar_"+System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;yield return null;
        Canvas.ForceUpdateCanvases();
    }

    [UnityTest]
    public IEnumerator BoxedHotbarMatchesInventoryRowAndResolvesInitialImagesWithoutChangingVariants()
    {
        yield return LoadGameplay();
        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        var hotbar = Object.FindFirstObjectByType<Player>().hotbar;
        var row = hotbar.GetComponentInChildren<GridLayoutGroup>();
        var inventoryRow = inventory.GetComponentInChildren<GridLayoutGroup>(true);
        Assert.That(hotbar.SlotCount, Is.EqualTo(9));
        Assert.That(Object.FindObjectsByType<HotbarSlot>(FindObjectsSortMode.None).Length, Is.EqualTo(9), "Remove legacy duplicate slot components.");
        Assert.That(row.cellSize, Is.EqualTo(inventoryRow.cellSize));
        Assert.That(row.spacing, Is.EqualTo(inventoryRow.spacing));
        Assert.That(row.constraintCount, Is.EqualTo(9));
        var panel = row.transform.parent.GetComponent<Image>();
        Assert.That(panel.color, Is.EqualTo(inventoryRow.transform.parent.GetComponent<Image>().color));
        Assert.That(panel.rectTransform.rect.size-row.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(48,48)));
        Assert.That(hotbar.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(inventory.GetComponent<CanvasScaler>().referenceResolution));
        var expected = new[]{"Block","Voltage Source","Ground Source","Wires","Viewer Block","Block","Block","Block","Block"};
        for (int i=0;i<9;i++)
        {
            var slot = hotbar.GetSlot(i);
            Assert.That(slot.BlockPrefab.name, Is.EqualTo(expected[i]));
            Assert.That(slot.Icon.sprite, Is.EqualTo(inventory.Catalog.FindForPrefab(slot.BlockPrefab).Icon));
            Assert.That(slot.Icon.enabled, Is.True);Assert.That(slot.Icon.preserveAspect, Is.True);
            Assert.That(slot.Icon.raycastTarget, Is.False);
            Assert.That(slot.GetComponent<RectTransform>().rect.size, Is.EqualTo(Vector2.one*80));
            Assert.That(slot.GetComponentInChildren<TMP_Text>().text, Is.EqualTo((i+1).ToString()));
        }
        Assert.That(hotbar.GetSlot(2).BlockPrefab.GetComponent<LogicSignalSource>().SignalState, Is.Zero,
            "The low source may share the Constant Signal image, but its placement prefab must stay low.");
        Assert.That(hotbar.GetSlot(0).Frame.color, Is.Not.EqualTo(hotbar.GetSlot(1).Frame.color));
        Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator HoverAssignmentUpdatesImagesImmediatelyAndSelectionHighlightsFollowKeysAndScroll()
    {
        yield return LoadGameplay();
        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        var hotbar = Object.FindFirstObjectByType<Player>().hotbar;
        var initialSelectedColor = hotbar.GetSlot(0).Frame.color;
        var normalColor = hotbar.GetSlot(1).Frame.color;
        inventory.SetOpen(true);Canvas.ForceUpdateCanvases();
        var clock = inventory.GetComponentsInChildren<InventorySlotImage>().Single(s=>s.Item?.Id=="clock");
        var rect = clock.GetComponent<RectTransform>();
        Set(mouse.position,(Vector2)rect.TransformPoint(rect.rect.center));yield return null;yield return null;
        Press(keyboard.digit3Key);yield return null;Release(keyboard.digit3Key);yield return null;
        Assert.That(hotbar.GetSlot(2).Icon.sprite, Is.EqualTo(clock.Item.Icon));
        Assert.That(hotbar.GetSlot(2).BlockPrefab, Is.EqualTo(clock.Item.Prefab));
        Assert.That(hotbar.SelectedSlotIndex, Is.Zero);
        inventory.SetOpen(false);yield return null;
        Press(keyboard.digit3Key);yield return null;Release(keyboard.digit3Key);yield return null;
        Assert.That(hotbar.SelectedSlotIndex, Is.EqualTo(2));
        Assert.That(hotbar.GetSlot(2).Frame.color, Is.EqualTo(initialSelectedColor));
        Assert.That(hotbar.GetSlot(0).Frame.color, Is.EqualTo(normalColor));
        Set(mouse.scroll,new Vector2(0,120));yield return null;Set(mouse.scroll,Vector2.zero);yield return null;
        Assert.That(hotbar.SelectedSlotIndex, Is.EqualTo(1));
        Assert.That(hotbar.GetSlot(1).Frame.color, Is.EqualTo(initialSelectedColor));
        Assert.That(hotbar.GetSlot(2).Frame.color, Is.EqualTo(normalColor));
        var frameSize = hotbar.GetSlot(2).GetComponent<RectTransform>().rect.size;
        hotbar.GetSlot(2).SetItem(null);
        Assert.That(hotbar.GetSlot(2).Icon.enabled, Is.False);
        Assert.That(hotbar.GetSlot(2).Icon.sprite, Is.Null);
        Assert.That(hotbar.GetSlot(2).GetComponent<RectTransform>().rect.size, Is.EqualTo(frameSize));
        foreach (var item in inventory.Catalog.Items)
        {
            Assert.That(hotbar.AssignItem(2,item), Is.True);
            Assert.That(hotbar.GetSlot(2).Icon.sprite, Is.EqualTo(item.Icon));
        }
        LogAssert.NoUnexpectedReceived();
    }
}
