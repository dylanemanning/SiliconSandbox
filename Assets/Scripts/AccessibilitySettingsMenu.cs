using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AccessibilitySettingsMenu : MonoBehaviour
{
    private sealed class SettingsRow
    {
        public RectTransform controls;
        public TMP_Text value;
    }

    private const float DefaultMouseSensitivity = 1f;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    public void Initialize()
    {
        if (initialized) return;

        initialized = true;
        BuildControls();
        AccessibilitySettings.ApplySettings();
    }

    private void BuildControls()
    {
        RectTransform controls = CreateRectTransform("AccessibilitySettingsControls", transform);
        controls.anchorMin = new Vector2(0.5f, 0.5f);
        controls.anchorMax = new Vector2(0.5f, 0.5f);
        controls.pivot = new Vector2(0.5f, 0.5f);
        controls.anchoredPosition = new Vector2(0f, -70f);
        controls.sizeDelta = new Vector2(900f, 560f);

        VerticalLayoutGroup layout = controls.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateToggleRow(controls, "Colorblind Mode", AccessibilitySettings.ColorblindModeKey,
            "colorblindModeToggle");
        CreateSliderRow(controls, "Mouse Sensitivity", 0.1f, 3f,
            PlayerPrefs.GetFloat(AccessibilitySettings.MouseSensitivityKey, DefaultMouseSensitivity),
            "mouseSensitivitySlider");
        CreateDropdownRow(controls, "Font Size", new[] { "Small", "Medium", "Large" },
            Mathf.Clamp(PlayerPrefs.GetInt(AccessibilitySettings.FontSizeKey, 1), 0, 2),
            "fontSizeDropdown");
        CreateToggleRow(controls, "Dyslexia Friendly Typeface",
            AccessibilitySettings.DyslexiaFriendlyTypefaceKey, "dyslexiaTypefaceToggle");

        CreateActionButtons();
    }

    private void CreateToggleRow(Transform parent, string label, string preferenceKey, string toggleName)
    {
        SettingsRow row = CreateRow(parent, label);
        RectTransform toggleRect = CreateRectTransform(toggleName, row.controls);
        toggleRect.sizeDelta = new Vector2(36f, 36f);
        toggleRect.anchorMin = new Vector2(0f, 0.5f);
        toggleRect.anchorMax = new Vector2(0f, 0.5f);
        toggleRect.pivot = new Vector2(0f, 0.5f);

        Image background = toggleRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.82f, 0.88f, 0.83f, 1f);
        Image checkmark = CreateImage("Checkmark", toggleRect, new Color(0.08f, 0.12f, 0.14f, 1f));
        Stretch(checkmark.rectTransform);
        checkmark.rectTransform.offsetMin = new Vector2(7f, 7f);
        checkmark.rectTransform.offsetMax = new Vector2(-7f, -7f);

        Toggle toggle = toggleRect.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.isOn = PlayerPrefs.GetInt(preferenceKey, 0) != 0;
        row.value.text = toggle.isOn ? "On" : "Off";
        toggle.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetInt(preferenceKey, value ? 1 : 0);
            row.value.text = value ? "On" : "Off";
            AccessibilitySettings.ApplySettings();
        });
    }

    private void CreateSliderRow(
        Transform parent,
        string label,
        float minimum,
        float maximum,
        float initialValue,
        string sliderName)
    {
        SettingsRow row = CreateRow(parent, label);
        Slider slider = CreateSlider(row.controls, sliderName, minimum, maximum, initialValue);
        Stretch(slider.GetComponent<RectTransform>());
        row.value.text = Mathf.RoundToInt(slider.value * 100f) + "%";
        slider.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetFloat(AccessibilitySettings.MouseSensitivityKey, value);
            row.value.text = Mathf.RoundToInt(value * 100f) + "%";
        });
    }

    private void CreateDropdownRow(Transform parent, string label, string[] options, int initialValue, string dropdownName)
    {
        SettingsRow row = CreateRow(parent, label);
        RectTransform dropdownRect = CreateRectTransform(dropdownName, row.controls);
        Stretch(dropdownRect);
        Image background = dropdownRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.82f, 0.88f, 0.83f, 1f);

        TMP_Text caption = CreateText(dropdownName + "Value", dropdownRect, TextAlignmentOptions.Center);
        Stretch(caption.rectTransform);
        caption.color = new Color(0.08f, 0.12f, 0.14f, 1f);
        row.value.text = string.Empty;

        RectTransform template = CreateDropdownTemplate(dropdownRect, dropdownName);
        TMP_Dropdown dropdown = dropdownRect.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.captionText = caption;
        dropdown.template = template;
        dropdown.itemText = template.Find("Viewport/Content/Item/" + dropdownName + "Option")
            .GetComponent<TMP_Text>();
        foreach (string option in options)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        }

        dropdown.value = initialValue;
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetInt(AccessibilitySettings.FontSizeKey, value);
            AccessibilitySettings.ApplySettings();
        });
    }

    private void CreateActionButtons()
    {
        RectTransform actions = CreateRectTransform("AccessibilitySettingsActions", transform);
        actions.anchorMin = new Vector2(0.5f, 0.5f);
        actions.anchorMax = new Vector2(0.5f, 0.5f);
        actions.pivot = new Vector2(0.5f, 0.5f);
        actions.anchoredPosition = Vector2.zero;
        actions.sizeDelta = new Vector2(1612f, 972f);

        CreateActionButton(actions, "SaveButton", "Save", SaveSettings, new Vector2(0.28f, 0.03f));
        CreateActionButton(actions, "ExitButton", "Exit", ExitSettings, new Vector2(0.72f, 0.03f));
    }

    private void CreateActionButton(
        Transform parent,
        string buttonName,
        string label,
        UnityEngine.Events.UnityAction action,
        Vector2 anchor)
    {
        Button button = CreateButton(buttonName, parent, action);
        LayoutElement buttonLayout = button.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 190f;
        buttonLayout.preferredHeight = 42f;
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchor;
        buttonRect.anchorMax = anchor;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(190f, 42f);

        TMP_Text buttonText = CreateText("Text", button.transform, TextAlignmentOptions.Center);
        Stretch(buttonText.rectTransform);
        buttonText.text = label;
    }

    private void ExitSettings()
    {
        PlayerPrefs.Save();
        if (PauseManager.TryCloseSettings()) return;

        MenuTraversal menuTraversal = FindFirstObjectByType<MenuTraversal>();
        if (menuTraversal == null)
        {
            Debug.LogError("Cannot exit accessibility settings because MenuTraversal was not found.");
            return;
        }

        menuTraversal.closeSettings();
    }

    private Slider CreateSlider(Transform parent, string sliderName, float minimum, float maximum, float value)
    {
        RectTransform sliderRect = CreateRectTransform(sliderName, parent);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.value = Mathf.Clamp(value, minimum, maximum);
        slider.direction = Slider.Direction.LeftToRight;

        Image background = CreateImage("Background", sliderRect, new Color(0.08f, 0.12f, 0.14f, 1f));
        Stretch(background.rectTransform);
        Image fill = CreateImage("Fill", sliderRect, new Color(0.48f, 0.82f, 0.72f, 1f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0.25f);
        fillRect.anchorMax = new Vector2(1f, 0.75f);
        fillRect.offsetMin = new Vector2(8f, 0f);
        fillRect.offsetMax = new Vector2(-8f, 0f);

        RectTransform fillArea = CreateRectTransform("Fill Area", sliderRect);
        Stretch(fillArea);
        fill.transform.SetParent(fillArea, false);

        Image handle = CreateImage("Handle", sliderRect, new Color(0.96f, 0.75f, 0.34f, 1f));
        RectTransform handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(24f, 32f);

        RectTransform handleArea = CreateRectTransform("Handle Slide Area", sliderRect);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);
        handle.transform.SetParent(handleArea, false);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.value = Mathf.Clamp(value, minimum, maximum);
        return slider;
    }

    private RectTransform CreateDropdownTemplate(Transform parent, string dropdownName)
    {
        RectTransform template = CreateRectTransform("Template", parent);
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.sizeDelta = new Vector2(0f, 144f);

        Image templateBackground = template.gameObject.AddComponent<Image>();
        templateBackground.color = new Color(0.82f, 0.88f, 0.83f, 1f);
        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = CreateRectTransform("Viewport", template);
        Stretch(viewport);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRectTransform("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 144f);
        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter contentSize = content.gameObject.AddComponent<ContentSizeFitter>();
        contentSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform item = CreateRectTransform("Item", content);
        LayoutElement itemLayout = item.gameObject.AddComponent<LayoutElement>();
        itemLayout.preferredHeight = 48f;
        Image itemBackground = item.gameObject.AddComponent<Image>();
        itemBackground.color = new Color(0.82f, 0.88f, 0.83f, 1f);
        Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBackground;

        Image itemCheckmark = CreateImage("Item Checkmark", item, new Color(0.08f, 0.12f, 0.14f, 1f));
        itemCheckmark.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        itemCheckmark.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        itemCheckmark.rectTransform.pivot = new Vector2(0f, 0.5f);
        itemCheckmark.rectTransform.anchoredPosition = new Vector2(12f, 0f);
        itemCheckmark.rectTransform.sizeDelta = new Vector2(18f, 18f);
        itemToggle.graphic = itemCheckmark;

        TMP_Text itemText = CreateText(dropdownName + "Option", item, TextAlignmentOptions.Left);
        itemText.color = new Color(0.08f, 0.12f, 0.14f, 1f);
        itemText.rectTransform.anchorMin = Vector2.zero;
        itemText.rectTransform.anchorMax = Vector2.one;
        itemText.rectTransform.offsetMin = new Vector2(42f, 0f);
        itemText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        scroll.viewport = viewport;
        scroll.content = content;
        template.gameObject.SetActive(false);
        return template;
    }

    private SettingsRow CreateRow(Transform parent, string label)
    {
        RectTransform row = CreateRectTransform(label + "Row", parent);
        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 76f;

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_Text labelText = CreateText(label, row, TextAlignmentOptions.Left);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 290f;
        labelLayout.flexibleWidth = 0f;

        RectTransform controls = CreateRectTransform(label + "Control", row);
        LayoutElement controlsLayout = controls.gameObject.AddComponent<LayoutElement>();
        controlsLayout.flexibleWidth = 1f;
        controlsLayout.preferredHeight = 48f;

        TMP_Text value = CreateText(label + "Value", row, TextAlignmentOptions.Right);
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 112f;
        valueLayout.flexibleWidth = 0f;

        return new SettingsRow { controls = controls, value = value };
    }

    private Button CreateButton(string buttonName, Transform parent, UnityEngine.Events.UnityAction action)
    {
        Image image = CreateImage(buttonName, parent, new Color(0.08f, 0.12f, 0.14f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = new ColorBlock
        {
            normalColor = new Color(0.08f, 0.12f, 0.14f, 1f),
            highlightedColor = new Color(0.18f, 0.31f, 0.31f, 1f),
            pressedColor = new Color(0.35f, 0.69f, 0.63f, 1f),
            selectedColor = new Color(0.18f, 0.31f, 0.31f, 1f),
            disabledColor = Color.gray,
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };
        button.onClick.AddListener(action);
        return button;
    }

    private TMP_Text CreateText(string objectName, Transform parent, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRectTransform(objectName, parent);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = objectName;
        text.fontSize = 28f;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        RectTransform rect = CreateRectTransform(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRectTransform(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SaveSettings()
    {
        PlayerPrefs.Save();
    }
}
