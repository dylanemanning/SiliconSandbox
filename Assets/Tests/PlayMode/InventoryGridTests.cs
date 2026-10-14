using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class InventoryGridTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    public override void Setup()
    {
        base.Setup();
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
        PlayerPrefs.DeleteKey("Keybind.Pause");
    }
    [UnityTearDown]
    public IEnumerator UnloadGameplay()
    {
        Time.timeScale=1;
        yield return SceneManager.LoadSceneAsync("EmptyScene");yield return null;
    }
    private IEnumerator LoadGameplay()
    {
        WorldSaveSystem.PendingWorldName="TC_InventoryGrid";
        yield return SceneManager.LoadSceneAsync("SampleScene");yield return null;yield return null;
    }

    [UnityTest]
    public IEnumerator EShows36SquareSlotsWithTenPercentGapsAndClosesAgain()
    {
        yield return LoadGameplay();
        Assert.That(InventoryGrid.IsOpen,Is.False);
        var inventory=Object.FindFirstObjectByType<InventoryGrid>();
        Assert.That(inventory.Panel.activeSelf,Is.False);
        Press(keyboard.eKey);yield return null;Release(keyboard.eKey);yield return null;
        Assert.That(InventoryGrid.IsOpen,Is.True);
        var grid=inventory.GetComponentInChildren<GridLayoutGroup>();
        Assert.That(grid.transform.childCount,Is.EqualTo(36));
        Assert.That(grid.constraintCount,Is.EqualTo(9));
        Assert.That(grid.cellSize.x,Is.EqualTo(grid.cellSize.y));
        Assert.That(grid.spacing.x,Is.EqualTo(grid.cellSize.x*.1f).Within(.001f));
        Assert.That(grid.spacing.y,Is.EqualTo(grid.cellSize.y*.1f).Within(.001f));
        Assert.That(grid.GetComponentsInChildren<HotbarSlot>().Length,Is.Zero);
        Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible,Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(1),"Opening the inventory should not change simulation time.");
        Press(keyboard.eKey);yield return null;Release(keyboard.eKey);yield return null;
        Assert.That(InventoryGrid.IsOpen,Is.False);
        // A batchmode Editor without Game-view focus releases the OS cursor lock.
        if (!Application.isBatchMode) Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.Locked));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator EscapeClosesInventoryBeforeOpeningPauseMenu()
    {
        yield return LoadGameplay();
        Press(keyboard.eKey);yield return null;Release(keyboard.eKey);yield return null;
        Press(keyboard.escapeKey);yield return null;Release(keyboard.escapeKey);yield return null;
        Assert.That(InventoryGrid.IsOpen,Is.False);
        Assert.That(PauseManager.IsPaused,Is.False);
        Press(keyboard.escapeKey);yield return null;Release(keyboard.escapeKey);yield return null;
        Assert.That(PauseManager.IsPaused,Is.True);
        Press(keyboard.eKey);yield return null;Release(keyboard.eKey);yield return null;
        Assert.That(InventoryGrid.IsOpen,Is.False);
        Object.FindFirstObjectByType<PauseManager>().ResumeGame();
    }

    [UnityTest]
    public IEnumerator InventoryStopsMovementLookHotbarChangesAndWorldClicks()
    {
        yield return LoadGameplay();
        var player=Object.FindFirstObjectByType<Player>();
        var body=player.GetComponent<Rigidbody>();
        var slotField=typeof(HotbarManager).GetField("selectedSlot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        int initialSelected=(int)slotField.GetValue(player.hotbar);
        var target=GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.transform.position=player.cameraSettings.camera.transform.position+player.cameraSettings.camera.transform.forward*3;
        target.AddComponent<Block>().durabilitySeconds=-1;
        Physics.SyncTransforms();
        yield return null; // Acquire a breakable target before opening the modal.
        body.linearVelocity=new Vector3(4,0,3);
        Object.FindFirstObjectByType<InventoryGrid>().SetOpen(true);
        Assert.That(body.linearVelocity.x,Is.Zero);Assert.That(body.linearVelocity.z,Is.Zero);
        var rotation=player.transform.rotation;var cameraRotation=player.cameraSettings.camera.transform.localRotation;
        int count=Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length;
        Press(keyboard.wKey);Press(keyboard.digit2Key);Press(mouse.leftButton);Press(mouse.rightButton);
        Set(mouse.delta,new Vector2(120,90));
        yield return null;yield return null;
        Assert.That(body.linearVelocity.x,Is.Zero);Assert.That(body.linearVelocity.z,Is.Zero);
        Assert.That(player.transform.rotation,Is.EqualTo(rotation));
        Assert.That(player.cameraSettings.camera.transform.localRotation,Is.EqualTo(cameraRotation));
        Assert.That((int)slotField.GetValue(player.hotbar),Is.EqualTo(initialSelected));
        Assert.That(Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length,Is.EqualTo(count));
        Release(keyboard.wKey);Release(keyboard.digit2Key);Release(mouse.leftButton);Release(mouse.rightButton);
        Object.FindFirstObjectByType<InventoryGrid>().SetOpen(false);
        Object.Destroy(target);
    }
}
