using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ControlsKeybindsMenu : MonoBehaviour
{
    private enum BindingType
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Place,
        Break,
        Pause,
        Jump,
        FlyDown
    }

    private Button moveForwardButton;
    private Button moveBackwardButton;
    private Button moveLeftButton;
    private Button moveRightButton;
    private Button placeButton;
    private Button breakButton;
    private Button pauseButton;
    private Button jumpButton;
    private Button downButton;
    private Button saveButton;
    private Button exitButton;
    private MenuTraversal menuTraversal;
    private BindingType? rebinding;
    private bool ignoreInputThisFrame;
    private bool visualsBound;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (visualsBound)
        {
            RefreshLabels();
            return;
        }

        BindVisuals();
        visualsBound = true;
        RefreshLabels();
    }

    private void Update()
    {
        if (!rebinding.HasValue) return;
        if (ignoreInputThisFrame)
        {
            ignoreInputThisFrame = false;
            return;
        }

        if (TryGetPressedMouseButton(out int mouseButton))
        {
            if (rebinding == BindingType.Place)
            {
                KeybindSettings.SetMouseButton("Keybind.PlaceMouseButton", mouseButton);
                FinishRebinding();
            }
            else if (rebinding == BindingType.Break)
            {
                KeybindSettings.SetMouseButton("Keybind.BreakMouseButton", mouseButton);
                FinishRebinding();
            }

            return;
        }

        if (!TryGetPressedKey(out Key key)) return;

        switch (rebinding.Value)
        {
            case BindingType.MoveForward:
                KeybindSettings.SetKey("Keybind.MoveForward", key);
                FinishRebinding();
                break;
            case BindingType.MoveBackward:
                KeybindSettings.SetKey("Keybind.MoveBackward", key);
                FinishRebinding();
                break;
            case BindingType.MoveLeft:
                KeybindSettings.SetKey("Keybind.MoveLeft", key);
                FinishRebinding();
                break;
            case BindingType.MoveRight:
                KeybindSettings.SetKey("Keybind.MoveRight", key);
                FinishRebinding();
                break;
            case BindingType.Pause:
                KeybindSettings.SetKey("Keybind.Pause", key);
                FinishRebinding();
                break;
            case BindingType.Jump:
                KeybindSettings.SetKey("Keybind.Jump", key);
                FinishRebinding();
                break;
            case BindingType.FlyDown:
                KeybindSettings.SetKey("Keybind.FlyDown", key);
                FinishRebinding();
                break;
        }

        RefreshLabels();
    }

    private void BindVisuals()
    {
        moveForwardButton = FindButton("MoveForwardRow/W Button");
        moveBackwardButton = FindButton("MoveBackwardRow/S Button");
        moveLeftButton = FindButton("MoveLeftRow/A Button");
        moveRightButton = FindButton("MoveRightRow/D Button");
        placeButton = FindButton("PlaceRow/Right ClickButton");
        breakButton = FindButton("BreakRow/Left ClickButton");
        pauseButton = FindButton("PauseRow/EscapeButton");
        jumpButton = FindButton("JumpRow/SpaceButton");
        downButton = FindButton("FlyDownRow/ShiftButton");
        saveButton = FindButton("SaveButton");
        exitButton = FindButton("ExitButton");
        menuTraversal = FindFirstObjectByType<MenuTraversal>();

        AddListener(moveForwardButton, () => BeginRebinding(BindingType.MoveForward));
        AddListener(moveBackwardButton, () => BeginRebinding(BindingType.MoveBackward));
        AddListener(moveLeftButton, () => BeginRebinding(BindingType.MoveLeft));
        AddListener(moveRightButton, () => BeginRebinding(BindingType.MoveRight));
        AddListener(placeButton, () => BeginRebinding(BindingType.Place));
        AddListener(breakButton, () => BeginRebinding(BindingType.Break));
        AddListener(pauseButton, () => BeginRebinding(BindingType.Pause));
        AddListener(jumpButton, () => BeginRebinding(BindingType.Jump));
        AddListener(downButton, () => BeginRebinding(BindingType.FlyDown));
        AddListener(saveButton, SaveBindings);
        AddListener(exitButton, ExitControls);
    }

    private Button FindButton(string path)
    {
        Transform buttonTransform = transform.Find("RuntimeKeybindsPrototype/" + path);
        Button button = buttonTransform == null ? null : buttonTransform.GetComponent<Button>();
        if (button != null) return button;

        string buttonName = path.Substring(path.LastIndexOf('/') + 1);
        foreach (Button childButton in GetComponentsInChildren<Button>(true))
        {
            if (childButton.name == buttonName) return childButton;
        }

        return null;
    }

    private void SaveBindings()
    {
        KeybindSettings.Save();
    }

    private void ExitControls()
    {
        if (menuTraversal != null) menuTraversal.closeSettings();
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private void BeginRebinding(BindingType bindingType)
    {
        rebinding = bindingType;
        ignoreInputThisFrame = true;
    }

    private void FinishRebinding()
    {
        rebinding = null;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        SetLabel(moveForwardButton, FormatKey(KeybindSettings.MoveForward));
        SetLabel(moveBackwardButton, FormatKey(KeybindSettings.MoveBackward));
        SetLabel(moveLeftButton, FormatKey(KeybindSettings.MoveLeft));
        SetLabel(moveRightButton, FormatKey(KeybindSettings.MoveRight));
        SetLabel(placeButton, FormatMouseButton(KeybindSettings.PlaceMouseButton));
        SetLabel(breakButton, FormatMouseButton(KeybindSettings.BreakMouseButton));
        SetLabel(pauseButton, FormatKey(KeybindSettings.Pause));
        SetLabel(jumpButton, FormatKey(KeybindSettings.Jump));
        SetLabel(downButton, FormatKey(KeybindSettings.FlyDown));
    }

    private static void SetLabel(Button button, string value)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    private static string FormatKey(Key key)
    {
        return key == Key.Space ? "Space" : key.ToString().ToUpperInvariant();
    }

    private static string FormatMouseButton(int button)
    {
        if (button == 0) return "Left Click";
        if (button == 1) return "Right Click";
        return "Middle Click";
    }

    private static bool TryGetPressedMouseButton(out int button)
    {
        button = -1;
        if (Mouse.current == null) return false;
        if (Mouse.current.leftButton.wasPressedThisFrame) button = 0;
        else if (Mouse.current.rightButton.wasPressedThisFrame) button = 1;
        else if (Mouse.current.middleButton.wasPressedThisFrame) button = 2;
        return button >= 0;
    }

    private static bool TryGetPressedKey(out Key key)
    {
        key = Key.None;
        if (Keyboard.current == null) return false;

        foreach (var keyControl in Keyboard.current.allKeys)
        {
            if (keyControl.wasPressedThisFrame)
            {
                key = keyControl.keyCode;
                return true;
            }
        }

        return false;
    }
}