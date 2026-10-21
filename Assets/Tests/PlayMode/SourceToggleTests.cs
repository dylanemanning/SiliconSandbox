using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SourceToggleTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        WorldSaveSystem.PendingWorldName = null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShiftRightClickTogglesOnceWithoutPlacingAndRespectsInventory()
    {
        WorldSaveSystem.PendingWorldName = "TC_SourceToggle_"+System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        yield return null;
        var player = Object.FindFirstObjectByType<Player>();
        var body = player.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.position = new Vector3(80,20,80);
        body.linearVelocity = Vector3.zero;
        Set(mouse.delta, Vector2.zero);
        yield return new WaitForFixedUpdate();
        yield return null;
        var camera = player.cameraSettings.camera;
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.transform.position = Vector3Int.RoundToInt(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)).GetPoint(4));
        target.AddComponent<Block>();
        var source = target.AddComponent<LogicSignalSource>();
        source.SetState(0);
        Physics.SyncTransforms();
        yield return null;
        Assert.That(Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)), out var hit, player.reachDistance), Is.True);
        Assert.That(hit.collider.gameObject, Is.EqualTo(target));
        int count = Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length;
        Press(keyboard.leftShiftKey);
        Press(mouse.rightButton);
        yield return null;
        yield return null;
        Assert.That(source.SignalState, Is.EqualTo(1));
        yield return null;
        yield return null;
        Assert.That(source.SignalState, Is.EqualTo(1), "Holding the mouse must not toggle every frame.");
        Assert.That(Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length, Is.EqualTo(count), "Toggling must not also place a block or begin a wire route.");
        Release(mouse.rightButton);
        Release(keyboard.leftShiftKey);
        yield return null;
        Press(keyboard.rightShiftKey);
        Press(mouse.rightButton);
        yield return null;
        yield return null;
        Assert.That(source.SignalState, Is.EqualTo(0), "Either Shift key must toggle the previous state.");
        Assert.That(Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length, Is.EqualTo(count));
        Release(mouse.rightButton);
        yield return null;
        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        inventory.SetOpen(true);
        Press(mouse.rightButton);
        yield return null;
        yield return null;
        Assert.That(source.SignalState, Is.EqualTo(0), "Inventory input must not affect the world.");
        Release(mouse.rightButton);
        Release(keyboard.rightShiftKey);
        Object.Destroy(target);
        LogAssert.NoUnexpectedReceived();
    }
}
