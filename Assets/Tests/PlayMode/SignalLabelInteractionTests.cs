using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SignalLabelInteractionTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private bool hadManualWireBinding;
    private bool hadDeleteWireBinding;
    private string previousManualWireBinding;
    private string previousDeleteWireBinding;
    private static readonly string[] IntegerBindingPreferences =
    {
        "Keybind.MoveForward", "Keybind.MoveBackward", "Keybind.MoveLeft", "Keybind.MoveRight",
        "Keybind.Jump", "Keybind.FlyDown", "Keybind.Pause", "Keybind.Interact",
        "Keybind.SignalVisibility", "Keybind.PlaceMouseButton", "Keybind.BreakMouseButton"
    };
    private readonly Dictionary<string, int> previousIntegerBindings = new Dictionary<string, int>();
    private readonly HashSet<string> existingIntegerBindings = new HashSet<string>();
    private bool previousSignalLabelVisibility;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        hadManualWireBinding = PlayerPrefs.HasKey(KeybindSettings.ManualWirePreference);
        hadDeleteWireBinding = PlayerPrefs.HasKey(KeybindSettings.DeleteWireRunPreference);
        previousManualWireBinding = PlayerPrefs.GetString(KeybindSettings.ManualWirePreference);
        previousDeleteWireBinding = PlayerPrefs.GetString(KeybindSettings.DeleteWireRunPreference);
        previousIntegerBindings.Clear();
        existingIntegerBindings.Clear();
        foreach (string preference in IntegerBindingPreferences)
        {
            if (!PlayerPrefs.HasKey(preference)) continue;
            existingIntegerBindings.Add(preference);
            previousIntegerBindings[preference] = PlayerPrefs.GetInt(preference);
        }
        KeybindSettings.ResetAllBindings();
        previousSignalLabelVisibility = SignalLabelBlock.AreVisible;
        if (!SignalLabelBlock.AreVisible)
            SignalLabelBlock.ToggleVisibility();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        WorldSaveSystem.PendingWorldName = null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        yield return null;
        if (SignalLabelBlock.AreVisible != previousSignalLabelVisibility)
            SignalLabelBlock.ToggleVisibility();
        KeybindSettings.ResetAllBindings();
        foreach (string preference in IntegerBindingPreferences)
        {
            if (existingIntegerBindings.Contains(preference))
                PlayerPrefs.SetInt(preference, previousIntegerBindings[preference]);
            else
                PlayerPrefs.DeleteKey(preference);
        }
        if (hadManualWireBinding)
            PlayerPrefs.SetString(KeybindSettings.ManualWirePreference, previousManualWireBinding);
        if (hadDeleteWireBinding)
            PlayerPrefs.SetString(KeybindSettings.DeleteWireRunPreference, previousDeleteWireBinding);
        PlayerPrefs.Save();
    }

    private IEnumerator LoadGameplay()
    {
        Time.timeScale = 1f;
        WorldSaveSystem.PendingWorldName = "TC_SignalLabel_" + System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        yield return null;
        if (PauseManager.IsPaused)
            Object.FindFirstObjectByType<PauseManager>().ResumeGame();
        InventoryGrid.Close();
        yield return null;
    }

    [UnityTest]
    public IEnumerator InteractEditsLabelAndEscapeExitsWithoutPausingOrChangingHotbar()
    {
        yield return LoadGameplay();

        var player = Object.FindFirstObjectByType<Player>();
        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        player.GetComponent<Rigidbody>().useGravity = false;
        player.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        Assert.That(player.isActiveAndEnabled, Is.True);
        Assert.That(PauseManager.IsPaused, Is.False);
        Assert.That(InventoryGrid.IsOpen, Is.False);
        var labelPrefab = inventory.Catalog.Items.Single(item => item.Id == "signal_label").Prefab;
        var camera = player.cameraSettings.camera;
        Set(mouse.delta, Vector2.zero);
        var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var label = Object.Instantiate(
            labelPrefab,
            ray.GetPoint(2f),
            Quaternion.LookRotation(-camera.transform.forward, Vector3.up))
            .GetComponent<SignalLabelBlock>();
        Physics.SyncTransforms();
        yield return null;
        Assert.That(Physics.Raycast(ray, out RaycastHit labelHit, player.reachDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide), Is.True);
        Assert.That(labelHit.collider.GetComponentInParent<SignalLabelBlock>(), Is.EqualTo(label));

        int selectedSlot = player.hotbar.SelectedSlotIndex;
        Assert.That(KeybindSettings.Interact, Is.EqualTo(Key.F));
        Press(keyboard.fKey);
        yield return null;
        yield return null;
        Release(keyboard.fKey);
        yield return null;
        Assert.That(Player.IsTypingSignalLabel, Is.True);

        InputSystem.QueueTextEvent(keyboard, 'A');
        InputSystem.QueueTextEvent(keyboard, 'B');
        yield return null;
        Assert.That(label.LabelText, Is.EqualTo("AB"));

        Press(keyboard.digit5Key);
        yield return null;
        Release(keyboard.digit5Key);
        yield return null;
        Assert.That(player.hotbar.SelectedSlotIndex, Is.EqualTo(selectedSlot));

        Press(keyboard.backspaceKey);
        yield return null;
        Release(keyboard.backspaceKey);
        yield return null;
        Assert.That(label.LabelText, Is.EqualTo("A"));

        Press(keyboard.escapeKey);
        yield return null;
        Release(keyboard.escapeKey);
        yield return null;
        Assert.That(Player.IsTypingSignalLabel, Is.False);
        Assert.That(PauseManager.IsPaused, Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator SelectedLabelPlacesBesideAndAttachesToTargetWire()
    {
        yield return LoadGameplay();

        var player = Object.FindFirstObjectByType<Player>();
        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        var labelItem = inventory.Catalog.Items.Single(item => item.Id == "signal_label");
        Assert.That(player.hotbar.AssignItem(0, labelItem), Is.True);

        var camera = player.cameraSettings.camera;
        Set(mouse.delta, Vector2.zero);
        var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var wireObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wireObject.transform.position = ray.GetPoint(2.5f);
        wireObject.AddComponent<Block>();
        var wire = wireObject.AddComponent<LogicWireBlock>();
        Physics.SyncTransforms();
        yield return null;

        var placeButton = TestMouseBindings.GetControl(mouse, KeybindSettings.PlaceMouseButton);
        Press(placeButton);
        yield return null;
        Release(placeButton);
        yield return null;

        var labels = Object.FindObjectsByType<SignalLabelBlock>(FindObjectsSortMode.None);
        Assert.That(labels, Has.Length.EqualTo(1));
        Assert.That(labels[0].HasAttachment, Is.True);
        Assert.That(labels[0].AttachmentCell, Is.EqualTo(wire.GridPosition));
        Assert.That(labels[0].AttachmentFace, Is.Not.EqualTo(Vector3Int.zero));
        Assert.That(labels[0].GetComponent<Block>().occupiesGrid, Is.False);
        Vector3 attachmentPoint = (Vector3)labels[0].AttachmentCell + (Vector3)labels[0].AttachmentFace * 0.5f;
        Assert.That(Vector3.Distance(labels[0].transform.position, attachmentPoint), Is.LessThan(0.75f));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator TabHidesEveryLabelAndDisablesItsInteractionCollider()
    {
        yield return LoadGameplay();

        Assert.That(KeybindSettings.SignalVisibility, Is.EqualTo(Key.Tab));
        var player = Object.FindFirstObjectByType<Player>();
        var inventory = Object.FindFirstObjectByType<InventoryGrid>();
        var labelPrefab = inventory.Catalog.Items.Single(item => item.Id == "signal_label").Prefab;
        var camera = player.cameraSettings.camera;
        Set(mouse.delta, Vector2.zero);
        var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var labels = new[]
        {
            Object.Instantiate(labelPrefab, ray.GetPoint(2f),
                Quaternion.LookRotation(-camera.transform.forward, Vector3.up))
                .GetComponent<SignalLabelBlock>(),
            Object.Instantiate(labelPrefab, ray.GetPoint(3f),
                Quaternion.LookRotation(-camera.transform.forward, Vector3.up))
                .GetComponent<SignalLabelBlock>()
        };
        yield return null;

        var frontText = labels[0].GetComponentsInChildren<TMPro.TMP_Text>(true)
            .Single(text => text.name == "Signal label front text");
        var backText = labels[0].GetComponentsInChildren<TMPro.TMP_Text>(true)
            .Single(text => text.name == "Signal label back text");
        Assert.That(Quaternion.Angle(frontText.transform.localRotation, Quaternion.Euler(0f, 180f, 0f)),
            Is.LessThan(0.01f));
        Assert.That(Quaternion.Angle(backText.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));
        Assert.That(frontText.gameObject.activeSelf, Is.True);
        Assert.That(backText.gameObject.activeSelf, Is.False);

        Press(keyboard.tabKey);
        yield return null;
        Release(keyboard.tabKey);
        yield return null;

        Assert.That(SignalLabelBlock.AreVisible, Is.False);
        foreach (var label in labels)
            Assert.That(label.GetComponentInChildren<Collider>(true).gameObject.activeInHierarchy, Is.False);
        Assert.That(Physics.RaycastAll(ray, player.reachDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide)
            .Any(hit => hit.collider.GetComponentInParent<SignalLabelBlock>() != null), Is.False);

        Press(keyboard.tabKey);
        yield return null;
        Release(keyboard.tabKey);
        yield return null;

        Assert.That(SignalLabelBlock.AreVisible, Is.True);
        foreach (var label in labels)
            Assert.That(label.GetComponentInChildren<Collider>(true).gameObject.activeInHierarchy, Is.True);
        LogAssert.NoUnexpectedReceived();
    }
}
