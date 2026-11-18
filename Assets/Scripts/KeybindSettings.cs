using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public struct WireMouseChord : IEquatable<WireMouseChord>
{
    // Key.None selects MouseButton; keyboard actions use Key instead.
    public int MouseButton;
    public Key Key;
    public bool Control, Shift, Alt;

    public static bool ControlHeld => KeybindSettings.IsPressed(UnityEngine.InputSystem.Key.LeftCtrl) || KeybindSettings.IsPressed(UnityEngine.InputSystem.Key.RightCtrl);
    public static bool ShiftHeld => KeybindSettings.IsPressed(UnityEngine.InputSystem.Key.LeftShift) || KeybindSettings.IsPressed(UnityEngine.InputSystem.Key.RightShift);
    public static bool AltHeld => KeybindSettings.IsPressed(UnityEngine.InputSystem.Key.LeftAlt) || KeybindSettings.IsPressed(UnityEngine.InputSystem.Key.RightAlt);
    public bool ModifiersMatch() => Control == ControlHeld && Shift == ShiftHeld && Alt == AltHeld;
    public bool PrimaryIsPressed() => Key == UnityEngine.InputSystem.Key.None
        ? KeybindSettings.IsMouseButtonPressed(MouseButton) : KeybindSettings.IsPressed(Key);
    public bool IsPressed() => ModifiersMatch() && PrimaryIsPressed();
    public bool WasPressedThisFrame() => ModifiersMatch() && (Key == UnityEngine.InputSystem.Key.None
        ? KeybindSettings.WasMouseButtonPressedThisFrame(MouseButton) : KeybindSettings.WasPressedThisFrame(Key));
    public bool WasReleasedThisFrame()
    {
        if (Key != UnityEngine.InputSystem.Key.None)
            return Keyboard.current != null && Keyboard.current[Key].wasReleasedThisFrame;
        if (Mouse.current == null) return false;
        return MouseButton == 0 ? Mouse.current.leftButton.wasReleasedThisFrame
            : MouseButton == 1 ? Mouse.current.rightButton.wasReleasedThisFrame
            : Mouse.current.middleButton.wasReleasedThisFrame;
    }
    public bool Equals(WireMouseChord other) => Key == other.Key &&
        (Key != UnityEngine.InputSystem.Key.None || MouseButton == other.MouseButton) &&
        Control == other.Control && Shift == other.Shift && Alt == other.Alt;
    public override bool Equals(object value) => value is WireMouseChord other && Equals(other);
    public override int GetHashCode() => ((int)Key * 31 + (Key == UnityEngine.InputSystem.Key.None ? MouseButton : 0)) * 8 +
        (Control ? 1 : 0) + (Shift ? 2 : 0) + (Alt ? 4 : 0);
    public override string ToString() => (Control ? "Ctrl + " : "") + (Shift ? "Shift + " : "") + (Alt ? "Alt + " : "") +
        (Key != UnityEngine.InputSystem.Key.None ? Key.ToString() : MouseButton == 0 ? "Left Click" : MouseButton == 1 ? "Right Click" : "Middle Click");
}

public static class KeybindSettings
{
    public const string ManualWirePreference = "Keybind.ManualWire";
    public const string DeleteWireRunPreference = "Keybind.DeleteWireRun";
    private const string MoveForwardKey = "Keybind.MoveForward";
    private const string MoveBackwardKey = "Keybind.MoveBackward";
    private const string MoveLeftKey = "Keybind.MoveLeft";
    private const string MoveRightKey = "Keybind.MoveRight";
    private const string JumpKey = "Keybind.Jump";
    private const string FlyDownKey = "Keybind.FlyDown";
    private const string SprintKey = "Keybind.Sprint";
    private const string PauseKey = "Keybind.Pause";
    public const string InteractBindingPreference = "Keybind.InteractBinding";
    private const string PlaceMouseButtonKey = "Keybind.PlaceMouseButton";
    private const string BreakMouseButtonKey = "Keybind.BreakMouseButton";

    public static Key MoveForward => GetKey(MoveForwardKey, Key.W);
    public static Key MoveBackward => GetKey(MoveBackwardKey, Key.S);
    public static Key MoveLeft => GetKey(MoveLeftKey, Key.A);
    public static Key MoveRight => GetKey(MoveRightKey, Key.D);
    public static Key Jump => GetKey(JumpKey, Key.Space);
    /// <summary>Crouch while walking; descend while flying.</summary>
    public static Key FlyDown => GetKey(FlyDownKey, Key.LeftShift);
    public static Key Sprint => GetKey(SprintKey, Key.LeftCtrl);
    public static Key Pause => GetKey(PauseKey, Key.Escape);
    /// <summary>
    /// Interact (toggle a Constant Signal). A key or a mouse button; right click by
    /// default, like opening a block in Minecraft. Holding Crouch skips interaction
    /// so the same click places the selected block instead.
    /// </summary>
    public static WireMouseChord InteractBinding => LoadBinding(InteractBindingPreference, DefaultInteract, IsValidInteractBinding);
    public static WireMouseChord DefaultInteract => new WireMouseChord { MouseButton = 1 };
    public static bool InteractPressedThisFrame() => InteractBinding.Key == Key.None
        ? WasMouseButtonPressedThisFrame(InteractBinding.MouseButton) : WasPressedThisFrame(InteractBinding.Key);
    public static bool CrouchHeld => IsPressed(FlyDown);
    public static int PlaceMouseButton => PlayerPrefs.GetInt(PlaceMouseButtonKey, 1);
    public static int BreakMouseButton => PlayerPrefs.GetInt(BreakMouseButtonKey, 0);
    public static WireMouseChord ManualWireBinding => LoadWireBinding(ManualWirePreference, DefaultManualWire);
    public static WireMouseChord DeleteWireRunBinding => LoadWireBinding(DeleteWireRunPreference, DefaultDeleteWireRun);
    // Temporary single-key defaults until the combination-binding UI is improved.
    public static WireMouseChord DefaultManualWire => new WireMouseChord { Key = Key.R };
    public static WireMouseChord DefaultDeleteWireRun => new WireMouseChord { Key = Key.T };

    public static bool IsModifier(Key key) => key == Key.LeftCtrl || key == Key.RightCtrl ||
        key == Key.LeftShift || key == Key.RightShift || key == Key.LeftAlt || key == Key.RightAlt ||
        key == Key.LeftMeta || key == Key.RightMeta;

    public static bool TrySetWireBinding(bool manual, WireMouseChord binding, out string error)
    {
        if (!IsValidWireBinding(binding))
        {
            error = "That control is reserved. Choose another key or modified mouse button.";
            return false;
        }
        if (binding.Equals(manual ? DeleteWireRunBinding : ManualWireBinding))
        {
            error = "The two wire actions need different bindings.";
            return false;
        }
        PlayerPrefs.SetString(manual ? ManualWirePreference : DeleteWireRunPreference, JsonUtility.ToJson(binding));
        PlayerPrefs.Save();
        error = null;
        return true;
    }

    public static void ResetWireBindings()
    {
        PlayerPrefs.DeleteKey(ManualWirePreference);
        PlayerPrefs.DeleteKey(DeleteWireRunPreference);
        PlayerPrefs.Save();
    }

    public static bool IsWirePrimaryKey(Key key) => ManualWireBinding.Key == key || DeleteWireRunBinding.Key == key;

    private static bool IsValidWireBinding(WireMouseChord binding)
    {
        if (!Enum.IsDefined(typeof(Key), binding.Key) || IsModifier(binding.Key)) return false;
        if (binding.Key != Key.None)
        {
            Key key = binding.Key;
            return key != MoveForward && key != MoveBackward && key != MoveLeft && key != MoveRight &&
                key != Jump && key != FlyDown && key != Sprint && key != Pause && key != InteractBinding.Key && key != Key.Escape && key != Key.E &&
                key != Key.Enter && key != Key.NumpadEnter && !(key >= Key.Digit1 && key <= Key.Digit9);
        }
        if (binding.MouseButton < 0 || binding.MouseButton > 2) return false;
        if ((binding.MouseButton == PlaceMouseButton || binding.MouseButton == BreakMouseButton) &&
            !binding.Control && !binding.Shift && !binding.Alt) return false;
        // Crouch (Shift) + the place button places a block against interactive blocks.
        bool crouchIsShift = FlyDown == Key.LeftShift || FlyDown == Key.RightShift;
        if (crouchIsShift && binding.Shift && binding.MouseButton == PlaceMouseButton) return false;
        return true;
    }

    private static WireMouseChord LoadWireBinding(string preference, WireMouseChord fallback)
        => LoadBinding(preference, fallback, IsValidWireBinding);

    private static WireMouseChord LoadBinding(string preference, WireMouseChord fallback, Func<WireMouseChord, bool> isValid)
    {
        string json = PlayerPrefs.GetString(preference, "");
        if (string.IsNullOrEmpty(json)) return fallback;
        try
        {
            WireMouseChord binding = JsonUtility.FromJson<WireMouseChord>(json);
            return isValid(binding) ? binding : fallback;
        }
        catch (ArgumentException) { return fallback; }
    }

    private static bool IsValidInteractBinding(WireMouseChord binding)
    {
        if (binding.Control || binding.Shift || binding.Alt) return false;
        if (!Enum.IsDefined(typeof(Key), binding.Key) || IsModifier(binding.Key)) return false;
        if (binding.Key == Key.None)
            return binding.MouseButton >= 0 && binding.MouseButton <= 2 && binding.MouseButton != BreakMouseButton;
        Key key = binding.Key;
        return key != MoveForward && key != MoveBackward && key != MoveLeft && key != MoveRight &&
            key != Jump && key != FlyDown && key != Sprint && key != Pause && key != Key.Escape && key != Key.E &&
            key != Key.Enter && key != Key.NumpadEnter && !(key >= Key.Digit1 && key <= Key.Digit9);
    }

    public static bool TrySetInteractBinding(WireMouseChord binding, out string error)
    {
        binding.Control = binding.Shift = binding.Alt = false;
        // Wire keys are checked here, not in IsValidInteractBinding, because wire
        // validation reads the Interact binding (loading either must not recurse).
        if (!IsValidInteractBinding(binding) || binding.Key != Key.None && IsWirePrimaryKey(binding.Key))
        {
            error = "That control is reserved. Choose another key or mouse button.";
            return false;
        }
        PlayerPrefs.SetString(InteractBindingPreference, JsonUtility.ToJson(binding));
        PlayerPrefs.Save();
        error = null;
        return true;
    }

    /// <summary>Holding Ctrl turns the scroll wheel into the flight-speed control.</summary>
    public static bool FlightSpeedModifierHeld => WireMouseChord.ControlHeld;

    /// <summary>
    /// Vertical scroll. macOS reports Shift + wheel as horizontal scrolling, so a
    /// horizontal value stands in when there is no vertical one (descending in
    /// flight holds Shift).
    /// </summary>
    public static float ReadScroll()
    {
        if (Mouse.current == null) return 0f;
        Vector2 scroll = Mouse.current.scroll.ReadValue();
        return scroll.y != 0f ? scroll.y : scroll.x;
    }

    /// <summary>Scroll intended for the hotbar: none while Ctrl adjusts flight speed.</summary>
    public static float ReadHotbarScroll() => FlightSpeedModifierHeld ? 0f : ReadScroll();

    public static bool IsPressed(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].isPressed;
    }

    public static bool WasPressedThisFrame(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }

    public static bool IsMouseButtonPressed(int button)
    {
        if (Mouse.current == null) return false;
        if (button == 0) return Mouse.current.leftButton.isPressed;
        if (button == 1) return Mouse.current.rightButton.isPressed;
        return Mouse.current.middleButton.isPressed;
    }

    public static bool WasMouseButtonPressedThisFrame(int button)
    {
        if (Mouse.current == null) return false;
        if (button == 0) return Mouse.current.leftButton.wasPressedThisFrame;
        if (button == 1) return Mouse.current.rightButton.wasPressedThisFrame;
        return Mouse.current.middleButton.wasPressedThisFrame;
    }

    public static void SetKey(string settingKey, Key key)
    {
        PlayerPrefs.SetInt(settingKey, (int)key);
        PlayerPrefs.Save();
    }

    public static void SetMouseButton(string settingKey, int button)
    {
        PlayerPrefs.SetInt(settingKey, button);
        PlayerPrefs.Save();
    }

    public static bool TrySetMouseButton(string settingKey, int button, out string error)
    {
        error = null;
        if (button < 0 || button > 2 || (settingKey != PlaceMouseButtonKey && settingKey != BreakMouseButtonKey))
        { error = "Choose a supported mouse button."; return false; }
        int other = settingKey == PlaceMouseButtonKey ? BreakMouseButton : PlaceMouseButton;
        if (button == other)
        { error = "Place and Break need different mouse buttons."; return false; }
        foreach (var binding in new[] { ManualWireBinding, DeleteWireRunBinding })
        {
            if (binding.Key == Key.None && binding.MouseButton == button && !binding.Control && !binding.Shift && !binding.Alt)
            { error = "That button is assigned to a wire action."; return false; }
        }
        SetMouseButton(settingKey, button);
        return true;
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static Key GetKey(string settingKey, Key defaultKey)
    {
        return (Key)PlayerPrefs.GetInt(settingKey, (int)defaultKey);
    }
}
