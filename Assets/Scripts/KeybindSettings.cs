using UnityEngine;
using UnityEngine.InputSystem;

public static class KeybindSettings
{
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

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static Key GetKey(string settingKey, Key defaultKey)
    {
        return (Key)PlayerPrefs.GetInt(settingKey, (int)defaultKey);
    }
}