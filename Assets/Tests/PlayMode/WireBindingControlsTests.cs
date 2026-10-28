using System.Collections;
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

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        hadManual = PlayerPrefs.HasKey(KeybindSettings.ManualWirePreference);
        hadDeletion = PlayerPrefs.HasKey(KeybindSettings.DeleteWireRunPreference);
        manual = PlayerPrefs.GetString(KeybindSettings.ManualWirePreference);
        deletion = PlayerPrefs.GetString(KeybindSettings.DeleteWireRunPreference);
        KeybindSettings.ResetWireBindings();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        KeybindSettings.ResetWireBindings();
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
        Find("DeleteWireRunButton").onClick.Invoke();
        yield return null;
        Press(keyboard.escapeKey);
        yield return null;
        Release(keyboard.escapeKey);
        Assert.That(KeybindSettings.DeleteWireRunBinding.Equals(KeybindSettings.DefaultDeleteWireRun), Is.True);
        Find("ResetWireBindingsButton").onClick.Invoke();
        Assert.That(KeybindSettings.ManualWireBinding.Equals(KeybindSettings.DefaultManualWire), Is.True);
        Canvas.ForceUpdateCanvases();
        foreach (string name in new[] { "InteractButton", "ManualWireButton", "DeleteWireRunButton", "ResetWireBindingsButton" })
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
