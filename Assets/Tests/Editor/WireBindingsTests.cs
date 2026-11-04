using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public class WireBindingsTests
{
    private string manual, deletion;
    private bool hadManual, hadDeletion;

    [SetUp]
    public void SetUp()
    {
        hadManual = PlayerPrefs.HasKey(KeybindSettings.ManualWirePreference);
        hadDeletion = PlayerPrefs.HasKey(KeybindSettings.DeleteWireRunPreference);
        manual = PlayerPrefs.GetString(KeybindSettings.ManualWirePreference);
        deletion = PlayerPrefs.GetString(KeybindSettings.DeleteWireRunPreference);
        KeybindSettings.ResetWireBindings();
    }

    [TearDown]
    public void TearDown()
    {
        KeybindSettings.ResetWireBindings();
        if (hadManual) PlayerPrefs.SetString(KeybindSettings.ManualWirePreference, manual);
        if (hadDeletion) PlayerPrefs.SetString(KeybindSettings.DeleteWireRunPreference, deletion);
        PlayerPrefs.Save();
    }

    [Test]
    public void DefaultsUseTemporarySingleKeyControls()
    {
        Assert.That(KeybindSettings.ManualWireBinding.Equals(new WireMouseChord { Key = Key.R }), Is.True);
        Assert.That(KeybindSettings.DeleteWireRunBinding.Equals(new WireMouseChord { Key = Key.T }), Is.True);
    }

    [Test]
    public void KeyboardBindingPersistsAndResetRestoresDefaults()
    {
        var binding = new WireMouseChord { Key = Key.F10, Alt = true };
        Assert.That(KeybindSettings.TrySetWireBinding(true, binding, out _), Is.True);
        Assert.That(PlayerPrefs.HasKey(KeybindSettings.ManualWirePreference), Is.True);
        Assert.That(KeybindSettings.ManualWireBinding.Equals(binding), Is.True);
        KeybindSettings.ResetWireBindings();
        Assert.That(KeybindSettings.ManualWireBinding.Equals(KeybindSettings.DefaultManualWire), Is.True);
    }

    [Test]
    public void DuplicateActionRejectedWithoutChangingEitherBinding()
    {
        Assert.That(KeybindSettings.TrySetWireBinding(true, KeybindSettings.DeleteWireRunBinding, out string error), Is.False);
        Assert.That(error, Does.Contain("different"));
        Assert.That(KeybindSettings.ManualWireBinding.Equals(KeybindSettings.DefaultManualWire), Is.True);
    }

    [Test]
    public void InteractKeyIsReservedAndOldSourceMouseChordIsAvailable()
    {
        bool hadInteract = PlayerPrefs.HasKey("Keybind.Interact");
        int previous = PlayerPrefs.GetInt("Keybind.Interact");
        try
        {
            KeybindSettings.SetKey("Keybind.Interact", UnityEngine.InputSystem.Key.F9);
            Assert.That(KeybindSettings.TrySetWireBinding(true, new WireMouseChord { Key = UnityEngine.InputSystem.Key.F9 }, out _), Is.False);
            Assert.That(KeybindSettings.TrySetWireBinding(true, new WireMouseChord { MouseButton = 1, Shift = true }, out _), Is.True);
        }
        finally
        {
            if (hadInteract) PlayerPrefs.SetInt("Keybind.Interact", previous);
            else PlayerPrefs.DeleteKey("Keybind.Interact");
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void ExistingControlsAndBareModifiersAreReserved()
    {
        var reserved = new[] {
            new WireMouseChord { MouseButton = KeybindSettings.PlaceMouseButton },
            new WireMouseChord { MouseButton = KeybindSettings.BreakMouseButton },
            new WireMouseChord { Key = KeybindSettings.MoveForward, Control = true },
            new WireMouseChord { Key = Key.E },
            new WireMouseChord { Key = Key.Escape },
            new WireMouseChord { Key = Key.Digit1 },
            new WireMouseChord { Key = Key.LeftCtrl },
            new WireMouseChord { Key = (Key)9999 },
            new WireMouseChord { MouseButton = 9 }
        };
        foreach (var binding in reserved)
            Assert.That(KeybindSettings.TrySetWireBinding(true, binding, out _), Is.False, binding.ToString());
    }

    [Test]
    public void MalformedSavedBindingFallsBackWithoutBreakingControls()
    {
        PlayerPrefs.SetString(KeybindSettings.ManualWirePreference, "not json");
        Assert.That(KeybindSettings.ManualWireBinding.Equals(KeybindSettings.DefaultManualWire), Is.True);
    }

    [Test]
    public void RebindingBaseMouseActionCannotSilentlyInvalidateWireShortcut()
    {
        int oldPlace = KeybindSettings.PlaceMouseButton, oldBreak = KeybindSettings.BreakMouseButton;
        try
        {
            KeybindSettings.SetMouseButton("Keybind.PlaceMouseButton", 1);
            KeybindSettings.SetMouseButton("Keybind.BreakMouseButton", 0);
            var middle = new WireMouseChord { MouseButton = 2 };
            Assert.That(KeybindSettings.TrySetWireBinding(true, middle, out _), Is.True);
            Assert.That(KeybindSettings.TrySetMouseButton("Keybind.PlaceMouseButton", 2, out string error), Is.False);
            Assert.That(error, Does.Contain("wire action"));
            Assert.That(KeybindSettings.PlaceMouseButton, Is.EqualTo(1));
            Assert.That(KeybindSettings.ManualWireBinding.Equals(middle), Is.True);
            Assert.That(KeybindSettings.TrySetMouseButton("Keybind.PlaceMouseButton", 0, out _), Is.False);
        }
        finally
        {
            KeybindSettings.SetMouseButton("Keybind.PlaceMouseButton", oldPlace);
            KeybindSettings.SetMouseButton("Keybind.BreakMouseButton", oldBreak);
        }
    }
}
