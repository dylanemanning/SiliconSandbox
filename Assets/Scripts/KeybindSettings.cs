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
    private const string PauseKey = "Keybind.Pause";
    private const string PlaceMouseButtonKey = "Keybind.PlaceMouseButton";
    private const string BreakMouseButtonKey = "Keybind.BreakMouseButton";

    public static Key MoveForward => GetKey(MoveForwardKey, Key.W);
    public static Key MoveBackward => GetKey(MoveBackwardKey, Key.S);
    public static Key MoveLeft => GetKey(MoveLeftKey, Key.A);
    public static Key MoveRight => GetKey(MoveRightKey, Key.D);
    public static Key Jump => GetKey(JumpKey, Key.Space);
    public static Key FlyDown => GetKey(FlyDownKey, Key.LeftShift);
    public static Key Pause => GetKey(PauseKey, Key.Escape);
    public static int PlaceMouseButton => PlayerPrefs.GetInt(PlaceMouseButtonKey, 1);
    public static int BreakMouseButton => PlayerPrefs.GetInt(BreakMouseButtonKey, 0);
    public static WireMouseChord ManualWireBinding => LoadWireBinding(ManualWirePreference, DefaultManualWire);
    public static WireMouseChord DeleteWireRunBinding => LoadWireBinding(DeleteWireRunPreference, DefaultDeleteWireRun);
    public static WireMouseChord DefaultManualWire => new WireMouseChord { MouseButton = 1, Control = true };
    public static WireMouseChord DefaultDeleteWireRun => new WireMouseChord { MouseButton = 0, Shift = true };

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
                key != Jump && key != FlyDown && key != Pause && key != Key.Escape && key != Key.E &&
                key != Key.Enter && key != Key.NumpadEnter && !(key >= Key.Digit1 && key <= Key.Digit9);
        }
        if (binding.MouseButton < 0 || binding.MouseButton > 2) return false;
        // Source interaction uses Shift + right mouse, including extra modifiers.
        if (binding.MouseButton == 1 && binding.Shift) return false;
        if ((binding.MouseButton == PlaceMouseButton || binding.MouseButton == BreakMouseButton) &&
            !binding.Control && !binding.Shift && !binding.Alt) return false;
        return true;
    }

    private static WireMouseChord LoadWireBinding(string preference, WireMouseChord fallback)
    {
        string json = PlayerPrefs.GetString(preference, "");
        if (string.IsNullOrEmpty(json)) return fallback;
        try
        {
            WireMouseChord binding = JsonUtility.FromJson<WireMouseChord>(json);
            return IsValidWireBinding(binding) ? binding : fallback;
        }
        catch (ArgumentException) { return fallback; }
    }

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
