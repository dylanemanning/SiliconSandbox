using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SourceToggleTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private LogicSignalSource source;
    private bool hadInteractBinding;
    private string previousInteractBinding;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        hadInteractBinding = PlayerPrefs.HasKey(KeybindSettings.InteractBindingPreference);
        previousInteractBinding = PlayerPrefs.GetString(KeybindSettings.InteractBindingPreference);
        PlayerPrefs.DeleteKey(KeybindSettings.InteractBindingPreference);
    }

    public override void TearDown()
    {
        if (hadInteractBinding) PlayerPrefs.SetString(KeybindSettings.InteractBindingPreference, previousInteractBinding);
        else PlayerPrefs.DeleteKey(KeybindSettings.InteractBindingPreference);
        PlayerPrefs.Save();
        base.TearDown();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        WorldSaveSystem.PendingWorldName = null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        yield return null;
    }

    private IEnumerator LoadTargetedSource()
    {
        WorldSaveSystem.PendingWorldName = "TC_SourceToggle_" + System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        yield return null;
        var player = Object.FindFirstObjectByType<Player>();
        var body = player.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.position = new Vector3(80, 20, 80);
        body.linearVelocity = Vector3.zero;
        Set(mouse.delta, Vector2.zero);
        yield return new WaitForFixedUpdate();
        yield return null;
        var camera = player.cameraSettings.camera;
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        target.transform.position = Vector3Int.RoundToInt(ray.GetPoint(4));
        target.AddComponent<Block>();
        source = target.AddComponent<LogicSignalSource>();
        source.SetState(0);
        Physics.SyncTransforms();
        yield return null;
        Assert.That(Physics.Raycast(ray, out var hit, player.reachDistance), Is.True);
        Assert.That(hit.collider.gameObject, Is.EqualTo(target));
    }

    private IEnumerator Tap(Key key)
    {
        Press(keyboard[key]);
        yield return null;
        yield return null;
        Release(keyboard[key]);
        yield return null;
    }

    private static Button ButtonNamed(ControlsKeybindsMenu menu, string name)
    {
        return menu.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
    }

    private IEnumerator Click(ButtonControl button)
    {
        Press(button);
        yield return null;
        yield return null;
        Release(button);
        yield return null;
    }

    [UnityTest]
    public IEnumerator InteractDefaultsToRightClickAndTogglesOnceWithoutPlacementOrUiInputLeakage()
    {
        Assert.That(KeybindSettings.InteractBinding, Is.EqualTo(KeybindSettings.DefaultInteract));
        Assert.That(KeybindSettings.InteractBinding.ToString(), Is.EqualTo("Right Click"));
        yield return LoadTargetedSource();
        int count = Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length;
        Press(mouse.rightButton);
        yield return null;
        yield return null;
        Assert.That(source.SignalState, Is.EqualTo(1));
        yield return null;
        yield return null;
        Assert.That(source.SignalState, Is.EqualTo(1), "Holding Interact must not toggle every frame.");
        Assert.That(Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length, Is.EqualTo(count),
            "Interacting must not also place the selected block.");
        Release(mouse.rightButton);
        yield return null;
        yield return Click(mouse.rightButton);
        Assert.That(source.SignalState, Is.EqualTo(0));

        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        inventory.SetOpen(true);
        yield return Click(mouse.rightButton);
        Assert.That(source.SignalState, Is.EqualTo(0), "Inventory input must not affect the world.");
        inventory.SetOpen(false);
        yield return null;
        yield return Tap(KeybindSettings.Pause);
        Assert.That(PauseManager.IsPaused, Is.True);
        yield return Click(mouse.rightButton);
        Assert.That(source.SignalState, Is.EqualTo(0), "Pause input must not affect the world.");
        Object.FindFirstObjectByType<PauseManager>().ResumeGame();
        yield return null;

        // Keep the view still while crouching so the click aims at the same source.
        Object.FindFirstObjectByType<Player>().crouchEyeHeightScale = 1f;
        Press(keyboard.leftShiftKey);
        yield return null;
        yield return Click(mouse.rightButton);
        Assert.That(source.SignalState, Is.EqualTo(0), "Shift + right click must not toggle a source.");
        Assert.That(Object.FindObjectsByType<Block>(FindObjectsSortMode.None).Length, Is.EqualTo(count + 1),
            "Shift + right click places the selected block against the source instead.");
        Release(keyboard.leftShiftKey);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ControlsMenuRebindsInteractPersistsAndGameplayUsesTheNewKey()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return null;
        yield return null;
        var traversal = Object.FindFirstObjectByType<MenuTraversal>();
        traversal.openSettings();
        var controls = traversal.settingsControlsPanel.GetComponent<ControlsKeybindsMenu>();
        var button = ButtonNamed(controls, "InteractButton");
        Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Right Click"));
        Assert.That(button.transform.parent.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Interact"), Is.True);
        Canvas.ForceUpdateCanvases();
        // The extra row must fit within the existing layout instead of overlapping the footer.
        var row = (RectTransform)button.transform.parent;
        var container = (RectTransform)row.parent;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(container, row);
        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(container.rect.yMin - 1));
        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(container.rect.yMax + 1));

        button.onClick.Invoke();
        yield return null;
        yield return null;
        yield return Tap(Key.G);
        Assert.That(KeybindSettings.InteractBinding.Key, Is.EqualTo(Key.G));
        Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("G"));
        ButtonNamed(controls, "SaveButton").onClick.Invoke();
        traversal.closeSettings();
        traversal.openSettings();
        controls.Initialize();
        Assert.That(controls.GetComponentsInChildren<Button>(true).Count(b => b.name == "InteractButton"), Is.EqualTo(1));

        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return null;
        yield return null;
        traversal = Object.FindFirstObjectByType<MenuTraversal>();
        traversal.openSettings();
        controls = traversal.settingsControlsPanel.GetComponent<ControlsKeybindsMenu>();
        Assert.That(ButtonNamed(controls, "InteractButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("G"));
        Assert.That(KeybindSettings.InteractBinding.Key, Is.EqualTo(Key.G));

        yield return LoadTargetedSource();
        // Empty the selected slot so the old right click cannot place a block in front of the source.
        var hotbar = Object.FindFirstObjectByType<Player>().hotbar;
        hotbar.GetSlot(hotbar.SelectedSlotIndex).SetItem(null);
        yield return Click(mouse.rightButton);
        Assert.That(source.SignalState, Is.EqualTo(0), "The previous binding must stop interacting after a rebind.");
        yield return Tap(Key.G);
        Assert.That(source.SignalState, Is.EqualTo(1));
        yield return Tap(Key.G);
        Assert.That(source.SignalState, Is.EqualTo(0));
        LogAssert.NoUnexpectedReceived();
    }
}
