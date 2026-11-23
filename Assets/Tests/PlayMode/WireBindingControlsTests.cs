using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class WireBindingControlsTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private string manual, deletion;
    private bool hadManual, hadDeletion;
    private static readonly string[] IntegerBindingPreferences =
    {
        "Keybind.MoveForward", "Keybind.MoveBackward", "Keybind.MoveLeft", "Keybind.MoveRight",
        "Keybind.Jump", "Keybind.FlyDown", "Keybind.Pause", "Keybind.Interact",
        "Keybind.SignalVisibility", "Keybind.PlaceMouseButton", "Keybind.BreakMouseButton"
    };
    private readonly Dictionary<string, int> previousIntegerBindings = new Dictionary<string, int>();
    private readonly HashSet<string> existingIntegerBindings = new HashSet<string>();

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        hadManual = PlayerPrefs.HasKey(KeybindSettings.ManualWirePreference);
        hadDeletion = PlayerPrefs.HasKey(KeybindSettings.DeleteWireRunPreference);
        manual = PlayerPrefs.GetString(KeybindSettings.ManualWirePreference);
        deletion = PlayerPrefs.GetString(KeybindSettings.DeleteWireRunPreference);
        previousIntegerBindings.Clear();
        existingIntegerBindings.Clear();
        foreach (string preference in IntegerBindingPreferences)
        {
            if (!PlayerPrefs.HasKey(preference)) continue;
            existingIntegerBindings.Add(preference);
            previousIntegerBindings[preference] = PlayerPrefs.GetInt(preference);
        }
        KeybindSettings.ResetAllBindings();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        KeybindSettings.ResetAllBindings();
        foreach (string preference in IntegerBindingPreferences)
        {
            if (existingIntegerBindings.Contains(preference))
                PlayerPrefs.SetInt(preference, previousIntegerBindings[preference]);
            else
                PlayerPrefs.DeleteKey(preference);
        }
        if (hadManual) PlayerPrefs.SetString(KeybindSettings.ManualWirePreference, manual);
        if (hadDeletion) PlayerPrefs.SetString(KeybindSettings.DeleteWireRunPreference, deletion);
        PlayerPrefs.Save();
        yield return SceneManager.LoadSceneAsync("EmptyScene");
    }

    [Test]
    public void ReleasingModifierDoesNotReleaseTheGesturePrimary()
    {
        Assert.That(KeybindSettings.TrySetWireBinding(true, new WireMouseChord { MouseButton = 1, Control = true }, out _), Is.True);
        Press(keyboard.leftCtrlKey);
        Press(mouse.rightButton);
        var held = KeybindSettings.ManualWireBinding;
        Assert.That(held.IsPressed(), Is.True);
        Release(keyboard.leftCtrlKey);
        Assert.That(held.IsPressed(), Is.False);
        Assert.That(held.PrimaryIsPressed(), Is.True);
        Release(mouse.rightButton);
        Assert.That(held.PrimaryIsPressed(), Is.False);
    }

    [UnityTest]
    public IEnumerator SettingsExposeWireBindingsCaptureAndCancelAndRestore()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return null;
        var traversal = Object.FindFirstObjectByType<MenuTraversal>();
        traversal.openSettings();
        yield return null;
        var controls = Object.FindFirstObjectByType<ControlsKeybindsMenu>();
        Assert.That(controls, Is.Not.Null);
        Button Find(string name) => controls.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        Find("ManualWireButton").onClick.Invoke();
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.F10));
        yield return null;
        Release(keyboard.f10Key);
        Release(keyboard.leftCtrlKey);
        Assert.That(KeybindSettings.ManualWireBinding.Key, Is.EqualTo(Key.F10));
        Assert.That(KeybindSettings.ManualWireBinding.Control, Is.True, "Capture the primary key when modifier and key arrive together.");
        Find("SignalVisibilityButton").onClick.Invoke();
        yield return null;
        Press(keyboard.f9Key);
        yield return null;
        Release(keyboard.f9Key);
        Assert.That(KeybindSettings.SignalVisibility, Is.EqualTo(Key.F9));
        Find("DeleteWireRunButton").onClick.Invoke();
        yield return null;
        Press(keyboard.escapeKey);
        yield return null;
        Release(keyboard.escapeKey);
        Assert.That(KeybindSettings.DeleteWireRunBinding.Equals(KeybindSettings.DefaultDeleteWireRun), Is.True);
        Find("ResetWireBindingsButton").onClick.Invoke();
        Assert.That(KeybindSettings.ManualWireBinding.Equals(KeybindSettings.DefaultManualWire), Is.True);
        KeybindSettings.SetKey("Keybind.MoveForward", Key.UpArrow);
        KeybindSettings.SetKey("Keybind.Interact", Key.G);
        KeybindSettings.SetKey("Keybind.SignalVisibility", Key.F9);
        KeybindSettings.SetMouseButton("Keybind.PlaceMouseButton", 2);
        KeybindSettings.SetMouseButton("Keybind.BreakMouseButton", 1);
        Assert.That(KeybindSettings.TrySetWireBinding(true, new WireMouseChord { Key = Key.F10 }, out _), Is.True);
        Assert.That(KeybindSettings.TrySetWireBinding(false, new WireMouseChord { Key = Key.F11 }, out _), Is.True);
        Button restoreDefaultsButton = Find("RestoreDefaultsButton");
        Assert.That(restoreDefaultsButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Restore defaults"));
        restoreDefaultsButton.onClick.Invoke();
        Assert.That(KeybindSettings.MoveForward, Is.EqualTo(Key.W));
        Assert.That(KeybindSettings.Interact, Is.EqualTo(Key.F));
        Assert.That(KeybindSettings.SignalVisibility, Is.EqualTo(Key.Tab));
        Assert.That(KeybindSettings.PlaceMouseButton, Is.EqualTo(1));
        Assert.That(KeybindSettings.BreakMouseButton, Is.EqualTo(0));
        Assert.That(KeybindSettings.ManualWireBinding.Equals(KeybindSettings.DefaultManualWire), Is.True);
        Assert.That(KeybindSettings.DeleteWireRunBinding.Equals(KeybindSettings.DefaultDeleteWireRun), Is.True);
        Assert.That(Find("InteractButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("F"));
        Assert.That(Find("SignalVisibilityButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("TAB"));
        Canvas.ForceUpdateCanvases();
        foreach (string name in new[] { "InteractButton", "ManualWireButton", "DeleteWireRunButton",
            "SignalVisibilityButton", "ResetWireBindingsButton" })
        {
            var rect = (RectTransform)Find(name).transform;
            Assert.That(rect.rect.height, Is.GreaterThan(20), name + " must remain usable.");
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(0));
            Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height));
            var caption = rect.parent.GetComponentsInChildren<TMP_Text>()
                .Single(label => !label.transform.IsChildOf(rect));
            caption.ForceMeshUpdate();
            Assert.That(caption.textInfo.lineCount, Is.EqualTo(1), name + " caption must not overlap adjacent rows.");
            Assert.That(caption.isTextOverflowing, Is.False, name + " caption must fit its row.");
        }
        Directory.CreateDirectory("Builds/WireRouting");
        ScreenCapture.CaptureScreenshot("Builds/WireRouting/wire-controls.png");
        yield return null;
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }
}
