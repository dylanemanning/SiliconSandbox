using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class VideoSettingsMenu : MonoBehaviour
{
    private sealed class SettingsRow
    {
        public RectTransform controls;
        public TMP_Text value;
    }

    private const string BrightnessKey = "Video.Brightness";
    private const string ContrastKey = "Video.Contrast";
    private const string FullscreenKey = "Video.Fullscreen";
    private const string ShadowsKey = "Video.Shadows";
    private const string FieldOfViewKey = "Video.FieldOfView";

    private const float DefaultBrightness = 1f;
    private const float DefaultContrast = 1f;
    private const float DefaultFieldOfView = 60f;

    private static VolumeProfile baselineProfile;
    private static float baselineExposure;
    private static float baselineContrast;

    private bool initialized;
    private TMP_Dropdown displayModeDropdown;
    private Toggle shadowsToggle;
    private TMP_Text shadowsValue;

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
        ApplySettings();
    }

    public static void ApplySettings()
    {
        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
        FullScreenMode displayMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Screen.fullScreenMode != displayMode)
        {
            Screen.fullScreenMode = displayMode;
        }

        QualitySettings.shadows = PlayerPrefs.GetInt(ShadowsKey, 1) != 0
            ? UnityEngine.ShadowQuality.All
            : UnityEngine.ShadowQuality.Disable;

        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = FindFirstObjectByType<Camera>();
        }

        if (camera != null)
        {
            camera.fieldOfView = Mathf.Clamp(
                PlayerPrefs.GetFloat(FieldOfViewKey, DefaultFieldOfView),
                60f,
                120f
            );
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        Volume volume = null;
        foreach (Volume candidate in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (candidate.isGlobal)
            {
                volume = candidate;
                break;
            }
        }

        if (volume == null)
        {
            GameObject volumeObject = new GameObject("Video Settings Volume");
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        volume.isGlobal = true;
        VolumeProfile profile = volume.profile;
        if (!profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
        }

        if (baselineProfile != profile)
        {
            baselineProfile = profile;
            baselineExposure = colorAdjustments.postExposure.value;
            baselineContrast = colorAdjustments.contrast.value;
        }

        float brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness), 0f, 2f);
        float contrast = Mathf.Clamp(PlayerPrefs.GetFloat(ContrastKey, DefaultContrast), 0f, 2f);
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = baselineExposure
            + (brightness - DefaultBrightness) * 2f;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.contrast.value = Mathf.Clamp(
            baselineContrast + (contrast - DefaultContrast) * 100f,
            -100f,
            100f
        );
    }

    private void BuildControls()
    {
        RectTransform controls = CreateRectTransform("VideoSettingsControls", transform);
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

        CreateSliderRow(controls, "Brightness", 0f, 2f,
            PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness), "brightnessSlider",
            value => SetSliderPreference(BrightnessKey, value));
        CreateSliderRow(controls, "Contrast", 0f, 2f,
            PlayerPrefs.GetFloat(ContrastKey, DefaultContrast), "contrastSlider",
            value => SetSliderPreference(ContrastKey, value));
        CreateDisplayModeRow(controls, "Display Mode",
            PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0 ? "Fullscreen" : "Windowed",
            "displayModeDropdown");
        CreateShadowsRow(controls, "Shadows",
            PlayerPrefs.GetInt(ShadowsKey, 1) != 0 ? "On" : "Off",
            "shadowsToggle");
        CreateSliderRow(controls, "FOV", 60f, 120f,
            PlayerPrefs.GetFloat(FieldOfViewKey, DefaultFieldOfView), "fovSlider",
            value => SetSliderPreference(FieldOfViewKey, value), true);
        CreateActionButtons();
    }

    private void CreateActionButtons()
    {
        RectTransform actions = CreateRectTransform("VideoSettingsActions", transform);
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

        TMP_Text buttonText = CreateText("Text", button.transform, 0f, TextAlignmentOptions.Center);
        Stretch(buttonText.rectTransform);
        buttonText.text = label;
    }

    private static void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    private void ExitSettings()
    {
        PlayerPrefs.Save();
        if (PauseManager.TryCloseSettings()) return;

        MenuTraversal menuTraversal = FindFirstObjectByType<MenuTraversal>();
        if (menuTraversal == null)
        {
            Debug.LogError("Cannot exit video settings because MenuTraversal was not found.");
            return;
        }

        menuTraversal.closeSettings();
    }

    private void CreateSliderRow(
        Transform parent,
        string label,
        float minimum,
        float maximum,
        float initialValue,
        string sliderName,
        UnityEngine.Events.UnityAction<float> onValueChanged,
        bool wholeNumbers = false)
    {
        SettingsRow row = CreateRow(parent, label);
        Slider slider = CreateSlider(row.controls, sliderName, minimum, maximum, initialValue, wholeNumbers);
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        Stretch(sliderRect);

        slider.onValueChanged.AddListener(value =>
        {
            row.value.text = label == "FOV"
                ? Mathf.RoundToInt(value) + " deg"
                : Mathf.RoundToInt(value * 100f) + "%";
            onValueChanged(value);
        });

        row.value.text = label == "FOV"
            ? Mathf.RoundToInt(initialValue) + " deg"
            : Mathf.RoundToInt(initialValue * 100f) + "%";
    }

    private void CreateDisplayModeRow(Transform parent, string label, string initialValue, string dropdownName)
    {
        SettingsRow row = CreateRow(parent, label);
        RectTransform dropdownRect = CreateRectTransform(dropdownName, row.controls);
        Stretch(dropdownRect);
        Image background = dropdownRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.82f, 0.88f, 0.83f, 1f);

        TMP_Text caption = CreateText(dropdownName + "Value", dropdownRect, 0f, TextAlignmentOptions.Center);
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
        dropdown.options.Add(new TMP_Dropdown.OptionData("Windowed"));
        dropdown.options.Add(new TMP_Dropdown.OptionData("Fullscreen"));
        dropdown.value = initialValue == "Fullscreen" ? 1 : 0;
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetInt(FullscreenKey, value == 1 ? 1 : 0);
            ApplySettings();
        });
        displayModeDropdown = dropdown;
    }

    private void CreateShadowsRow(Transform parent, string label, string initialValue, string toggleName)
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
        toggle.isOn = initialValue == "On";
        row.value.text = initialValue;
        toggle.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetInt(ShadowsKey, value ? 1 : 0);
            row.value.text = value ? "On" : "Off";
            ApplySettings();
        });
        shadowsToggle = toggle;
        shadowsValue = row.value;
    }

    private RectTransform CreateDropdownTemplate(Transform parent, string dropdownName)
    {
        RectTransform template = CreateRectTransform("Template", parent);
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.sizeDelta = new Vector2(0f, 96f);

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
        content.sizeDelta = new Vector2(0f, 96f);
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

        TMP_Text itemText = CreateText(dropdownName + "Option", item, 0f, TextAlignmentOptions.Left);
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

        TMP_Text labelText = CreateText(label, row, 0f, TextAlignmentOptions.Left);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 230f;
        labelLayout.flexibleWidth = 0f;

        RectTransform controls = CreateRectTransform(label + "Control", row);
        LayoutElement controlsLayout = controls.gameObject.AddComponent<LayoutElement>();
        controlsLayout.flexibleWidth = 1f;
        controlsLayout.preferredHeight = 48f;

        TMP_Text value = CreateText(label + "Value", row, 0f, TextAlignmentOptions.Right);
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 112f;
        valueLayout.flexibleWidth = 0f;

        return new SettingsRow { controls = controls, value = value };
    }

    private Slider CreateSlider(Transform parent, string sliderName, float minimum, float maximum, float value, bool wholeNumbers)
    {
        RectTransform sliderRect = CreateRectTransform(sliderName, parent);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = wholeNumbers;
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

    private TMP_Text CreateText(string objectName, Transform parent, float preferredWidth, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRectTransform(objectName, parent);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = objectName;
        text.fontSize = 28f;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;

        if (preferredWidth > 0f)
        {
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
        }

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

    private static void SetSliderPreference(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        ApplySettings();
    }

}
