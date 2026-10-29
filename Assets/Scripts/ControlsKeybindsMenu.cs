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
        FlyDown,
        ManualWire,
        DeleteWireRun,
        Interact
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
    private Button manualWireButton;
    private Button deleteWireRunButton;
    private TMP_Text bindingFeedback;
    private Button interactButton;
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

        if (IsWireRebinding() && KeybindSettings.WasPressedThisFrame(Key.Escape))
        {
            FinishRebinding();
            return;
        }

        if (TryGetPressedMouseButton(out int mouseButton))
        {
            if (IsWireRebinding())
            {
                CaptureWireBinding(new WireMouseChord { MouseButton = mouseButton });
            }
            else if (rebinding == BindingType.Place)
            {
                if (KeybindSettings.TrySetMouseButton("Keybind.PlaceMouseButton", mouseButton, out string error)) FinishRebinding();
                else ShowBindingFeedback(error);
            }
            else if (rebinding == BindingType.Break)
            {
                if (KeybindSettings.TrySetMouseButton("Keybind.BreakMouseButton", mouseButton, out string error)) FinishRebinding();
                else ShowBindingFeedback(error);
            }

            return;
        }

        if (!TryGetPressedKey(out Key key, IsWireRebinding())) return;
        if (IsWireRebinding())
        {
            if (!KeybindSettings.IsModifier(key)) CaptureWireBinding(new WireMouseChord { Key = key });
            return;
        }
        if (KeybindSettings.IsWirePrimaryKey(key))
        {
            ShowBindingFeedback("That key is assigned to a wire action.");
            return;
        }

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
            case BindingType.Interact:
                KeybindSettings.SetKey("Keybind.Interact", key);
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
        interactButton = FindButton("InteractRow/InteractButton");
        if (interactButton == null) interactButton = CreateInteractRow();
        manualWireButton = AddWireRow("ManualWireRow", "Manual wire (click or drag)", "ManualWireButton");
        deleteWireRunButton = AddWireRow("DeleteWireRunRow", "Delete wire run", "DeleteWireRunButton");
        Button resetWireButton = AddWireRow("ResetWireBindingsRow", "Wire controls", "ResetWireBindingsButton");
        foreach (Button bindingButton in new[] { moveForwardButton, moveBackwardButton, moveLeftButton,
            moveRightButton, placeButton, breakButton, pauseButton, jumpButton, downButton, interactButton,
            manualWireButton, deleteWireRunButton, resetWireButton })
            FitRowCaption(bindingButton);
        SetLabel(resetWireButton, "Restore defaults");
        AddListener(resetWireButton, () => { KeybindSettings.ResetWireBindings(); FinishRebinding(); });
        AddListener(manualWireButton, () => BeginRebinding(BindingType.ManualWire));
        AddListener(deleteWireRunButton, () => BeginRebinding(BindingType.DeleteWireRun));
        CreateBindingFeedback();
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
        AddListener(interactButton, () => BeginRebinding(BindingType.Interact));
        AddListener(saveButton, SaveBindings);
        AddListener(exitButton, ExitControls);
    }

    private Button CreateInteractRow()
    {
        if (downButton == null) return null;

        // Reuse the existing row styling and vertical layout in the Controls page.
        Transform template = downButton.transform.parent;
        GameObject row = Instantiate(template.gameObject, template.parent);
        row.name = "InteractRow";
        row.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);
        Button button = row.GetComponentInChildren<Button>(true);
        button.name = "InteractButton";
        button.onClick = new Button.ButtonClickedEvent();
        foreach (TMP_Text label in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!label.transform.IsChildOf(button.transform)) label.text = "Interact";
        }
        return button;
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
        ShowBindingFeedback(IsWireRebinding()
            ? "Press a key or mouse button; hold modifiers. Escape cancels."
            : "Press a key or mouse button to assign this control.");
        if (bindingType == BindingType.ManualWire) SetLabel(manualWireButton, "Press a binding...");
        if (bindingType == BindingType.DeleteWireRun) SetLabel(deleteWireRunButton, "Press a binding...");
    }

    private void FinishRebinding()
    {
        rebinding = null;
        ShowBindingFeedback("");
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
        SetLabel(interactButton, FormatKey(KeybindSettings.Interact));
        SetLabel(manualWireButton, KeybindSettings.ManualWireBinding.ToString());
        SetLabel(deleteWireRunButton, KeybindSettings.DeleteWireRunBinding.ToString());
    }

    private bool IsWireRebinding() => rebinding == BindingType.ManualWire || rebinding == BindingType.DeleteWireRun;

    private void CaptureWireBinding(WireMouseChord binding)
    {
        binding.Control = WireMouseChord.ControlHeld;
        binding.Shift = WireMouseChord.ShiftHeld;
        binding.Alt = WireMouseChord.AltHeld;
        if (KeybindSettings.TrySetWireBinding(rebinding == BindingType.ManualWire, binding, out string error))
            FinishRebinding();
        else ShowBindingFeedback(error);
    }

    private void OnDisable()
    {
        rebinding = null;
        ignoreInputThisFrame = false;
        ShowBindingFeedback("");
        if (visualsBound) RefreshLabels();
    }

    private Button AddWireRow(string rowName, string caption, string buttonName)
    {
        if (downButton == null) return null;
        Transform template = downButton.transform.parent;
        Transform row = template.parent.Find(rowName);
        if (row == null)
        {
            row = Instantiate(template.gameObject, template.parent).transform;
            row.name = rowName;
        }
        Button button = row.GetComponentInChildren<Button>(true);
        if (button == null) return null;
        button.name = buttonName;
        button.onClick = new Button.ButtonClickedEvent();
        foreach (TMP_Text label in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!label.transform.IsChildOf(button.transform)) label.text = caption;
        }
        TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel != null)
        {
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMin = 16;
            buttonLabel.fontSizeMax = 32;
        }
        return button;
    }

    private void CreateBindingFeedback()
    {
        Transform parent = transform.Find("RuntimeKeybindsPrototype");
        if (parent == null) parent = transform;
        var feedbackObject = new GameObject("WireBindingFeedback", typeof(RectTransform), typeof(TextMeshProUGUI));
        feedbackObject.transform.SetParent(parent, false);
        bindingFeedback = feedbackObject.GetComponent<TMP_Text>();
        TMP_Text source = downButton == null ? null : downButton.GetComponentInChildren<TMP_Text>(true);
        if (source != null) bindingFeedback.font = source.font;
        bindingFeedback.fontSize = 24;
        bindingFeedback.alignment = TextAlignmentOptions.Center;
        bindingFeedback.color = Color.white;
        bindingFeedback.raycastTarget = false;
        RectTransform rect = bindingFeedback.rectTransform;
        rect.anchorMin = new Vector2(.05f, .07f);
        rect.anchorMax = new Vector2(.95f, .12f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void FitRowCaption(Button button)
    {
        if (button == null) return;
        foreach (TMP_Text label in button.transform.parent.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.transform.IsChildOf(button.transform)) continue;
            // Use the available left column so additional rows do not wrap into one another.
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(.56f, 1);
            rect.offsetMin = new Vector2(12, 0);
            rect.offsetMax = new Vector2(-12, 0);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16;
            label.fontSizeMax = 24;
        }
    }

    private void ShowBindingFeedback(string message)
    {
        if (bindingFeedback != null) bindingFeedback.text = message;
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

    private static bool TryGetPressedKey(out Key key, bool skipModifiers = false)
    {
        key = Key.None;
        if (Keyboard.current == null) return false;

        foreach (var keyControl in Keyboard.current.allKeys)
        {
            if (skipModifiers && KeybindSettings.IsModifier(keyControl.keyCode)) continue;
            if (keyControl.wasPressedThisFrame)
            {
                key = keyControl.keyCode;
                return true;
            }
        }

        return false;
    }
}
