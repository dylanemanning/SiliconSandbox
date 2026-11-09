using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class AccessibilitySettingsMenuTests
{
    private static readonly string[] PreferenceKeys =
    {
        AccessibilitySettings.ColorblindModeKey,
        AccessibilitySettings.MouseSensitivityKey,
        AccessibilitySettings.FontSizeKey,
        AccessibilitySettings.DyslexiaFriendlyTypefaceKey
    };

    private bool[] hadPreference;
    private float[] floatPreferences;
    private int[] intPreferences;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        CapturePreferences();
        PlayerPrefs.DeleteKey(AccessibilitySettings.ColorblindModeKey);
        PlayerPrefs.DeleteKey(AccessibilitySettings.MouseSensitivityKey);
        PlayerPrefs.DeleteKey(AccessibilitySettings.FontSizeKey);
        PlayerPrefs.DeleteKey(AccessibilitySettings.DyslexiaFriendlyTypefaceKey);

        SceneManager.LoadScene("MainMenu");
        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        RestorePreferences();
        SceneManager.LoadScene("MainMenu");
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator AccessibilityControlsApplyAndPersistSettings()
    {
        MenuTraversal menu = Object.FindFirstObjectByType<MenuTraversal>();
        Assert.IsNotNull(menu);
        menu.openSettings();
        menu.openAccessibilityTab();
        yield return null;

        AccessibilitySettingsMenu settings =
            menu.settingsAccessibilityPanel.GetComponent<AccessibilitySettingsMenu>();
        Assert.IsNotNull(settings);
        Assert.AreEqual(3, settings.GetComponentsInChildren<Toggle>(true).Length);

        Toggle colorblind = settings.transform.Find(
            "AccessibilitySettingsControls/Colorblind ModeRow/Colorblind ModeControl/colorblindModeToggle"
        ).GetComponent<Toggle>();
        colorblind.isOn = true;
        Assert.AreEqual(1, PlayerPrefs.GetInt(AccessibilitySettings.ColorblindModeKey));

        Slider sensitivity = settings.transform.Find(
            "AccessibilitySettingsControls/Mouse SensitivityRow/Mouse SensitivityControl/mouseSensitivitySlider"
        ).GetComponent<Slider>();
        sensitivity.value = 2f;
        Assert.AreEqual(2f, AccessibilitySettings.MouseSensitivityMultiplier);

        TMP_FontAsset sansSerif = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        Assert.IsNotNull(sansSerif);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameObject textObject = new GameObject(
            "AccessibilityFontSizeTest",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(canvas.transform, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = sansSerif;
        text.fontSize = 20f;

        TMP_Dropdown fontSize = settings.transform.Find(
            "AccessibilitySettingsControls/Font SizeRow/Font SizeControl/fontSizeDropdown"
        ).GetComponent<TMP_Dropdown>();
        Assert.AreEqual("Medium", fontSize.options[fontSize.value].text);
        fontSize.value = 0;
        Assert.AreEqual(16f, text.fontSize, 0.001f);
        Assert.AreEqual("Small", fontSize.captionText.text);

        Toggle dyslexiaTypeface = settings.transform.Find(
            "AccessibilitySettingsControls/Dyslexia Friendly TypefaceRow/Dyslexia Friendly TypefaceControl/dyslexiaTypefaceToggle"
        ).GetComponent<Toggle>();
        dyslexiaTypeface.isOn = true;
        Assert.AreSame(sansSerif, text.font);

        GameObject rendererObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rendererObject.name = "AccessibilityPatternTest";
        MeshRenderer renderer = rendererObject.GetComponent<MeshRenderer>();
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        Assert.IsNotNull(litShader);
        Material originalMaterial = new Material(litShader);
        renderer.sharedMaterial = originalMaterial;
        Texture originalTexture = GetBaseTexture(originalMaterial);
        AccessibilitySettings.ApplySignalPattern(renderer, 0, false);
        Material lowOverlay = renderer.sharedMaterial;
        AccessibilitySettings.ApplySignalPattern(renderer, 1, false);
        Material highOverlay = renderer.sharedMaterial;
        AccessibilitySettings.ApplySignalPattern(renderer, 0, true);
        Material errorOverlay = renderer.sharedMaterial;

        Assert.AreSame(originalTexture, GetBaseTexture(lowOverlay));
        Assert.AreSame(originalTexture, GetBaseTexture(highOverlay));
        Assert.AreSame(originalTexture, GetBaseTexture(errorOverlay));
        Assert.AreNotSame(lowOverlay.GetTexture("_DetailAlbedoMap"), highOverlay.GetTexture("_DetailAlbedoMap"));
        Assert.AreNotSame(highOverlay.GetTexture("_DetailAlbedoMap"), errorOverlay.GetTexture("_DetailAlbedoMap"));
        Assert.IsTrue(lowOverlay.IsKeywordEnabled("_DETAIL_SCALED"));
        Assert.AreEqual(1f, lowOverlay.GetFloat("_DetailAlbedoMapScale"));

        PlayerPrefs.SetInt(AccessibilitySettings.ColorblindModeKey, 0);
        AccessibilitySettings.ApplySignalPattern(renderer, 0, false);
        Assert.AreSame(originalMaterial, renderer.sharedMaterial);

        PlayerPrefs.SetInt(AccessibilitySettings.ColorblindModeKey, 0);
        GameObject wireObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wireObject.name = "AccessibilityWireStateTest";
        LogicWireBlock wire = wireObject.AddComponent<LogicWireBlock>();
        WireModelVisual wireVisual = wireObject.AddComponent<WireModelVisual>();
        wireVisual.straightModel = CreateChild(wireObject.transform, "Straight");
        wireVisual.elbowModel = CreateChild(wireObject.transform, "Elbow");
        wireVisual.branches = new GameObject[6];
        for (int index = 0; index < wireVisual.branches.Length; index++)
        {
            wireVisual.branches[index] = CreateChild(wireObject.transform, "Branch " + index);
        }

        Material lowMaterial = new Material(litShader);
        lowMaterial.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        Material highMaterial = new Material(litShader);
        highMaterial.color = new Color(1f, 0.9f, 0.4f, 1f);
        wireVisual.lowMaterial = lowMaterial;
        wireVisual.highMaterial = highMaterial;

        wire.SetDisplayedState(0);
        wireVisual.Refresh();
        Material wireLow = wireObject.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreSame(lowMaterial, wireLow);

        wire.SetDisplayedState(1);
        wireVisual.Refresh();
        Material wireHigh = wireObject.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreSame(highMaterial, wireHigh);

        PlayerPrefs.SetInt(AccessibilitySettings.ColorblindModeKey, 1);
        wireVisual.RefreshAccessibilityVisuals();
        Material wireHighPattern = wireObject.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreEqual(highMaterial.color, wireHighPattern.color);
        Assert.IsNotNull(wireHighPattern.GetTexture("_DetailAlbedoMap"));

        wire.SetDisplayedState(0);
        wireVisual.Refresh();
        Material wireLowPattern = wireObject.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreEqual(lowMaterial.color, wireLowPattern.color);
        Assert.AreNotSame(
            wireLowPattern.GetTexture("_DetailAlbedoMap"),
            wireHighPattern.GetTexture("_DetailAlbedoMap"));

        wire.SetDisplayedState(1);
        wireVisual.Refresh();
        wireObject.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        wireVisual.RefreshAccessibilityVisuals();
        Material verticalWire = wireObject.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreEqual(highMaterial.color, verticalWire.color);
        Assert.AreSame(wireHighPattern.GetTexture("_DetailAlbedoMap"), verticalWire.GetTexture("_DetailAlbedoMap"));

        Object.Destroy(textObject);
        Object.Destroy(rendererObject);
        Object.Destroy(wireObject);
        Object.Destroy(originalMaterial);
        Object.Destroy(lowMaterial);
        Object.Destroy(highMaterial);
    }

    [UnityTest]
    public IEnumerator VideoAndAccessibilityActionButtonsMatchControlsLayout()
    {
        MenuTraversal menu = Object.FindFirstObjectByType<MenuTraversal>();
        Assert.IsNotNull(menu);
        menu.openSettings();
        menu.openVideoTab();
        menu.openAccessibilityTab();

        Transform controlsActions = menu.settingsControlsPanel.transform.Find("RuntimeKeybindsPrototype");
        Assert.IsNotNull(controlsActions);
        AssertButtonsMatchControls(
            controlsActions,
            menu.settingsVideoPanel.transform.Find("VideoSettingsActions"));
        AssertButtonsMatchControls(
            controlsActions,
            menu.settingsAccessibilityPanel.transform.Find("AccessibilitySettingsActions"));
        yield return null;
    }

    private static void AssertButtonsMatchControls(Transform controlsActions, Transform settingsActions)
    {
        Assert.IsNotNull(settingsActions);
        RectTransform controlsActionsRect = controlsActions.GetComponent<RectTransform>();
        RectTransform settingsActionsRect = settingsActions.GetComponent<RectTransform>();
        Assert.AreEqual(controlsActionsRect.sizeDelta, settingsActionsRect.sizeDelta);

        foreach (string buttonName in new[] { "SaveButton", "ExitButton" })
        {
            RectTransform controlsButton = controlsActions.Find(buttonName).GetComponent<RectTransform>();
            RectTransform settingsButton = settingsActions.Find(buttonName).GetComponent<RectTransform>();
            Assert.AreEqual(controlsButton.anchorMin, settingsButton.anchorMin, buttonName + " anchorMin");
            Assert.AreEqual(controlsButton.anchorMax, settingsButton.anchorMax, buttonName + " anchorMax");
            Assert.AreEqual(controlsButton.pivot, settingsButton.pivot, buttonName + " pivot");
            Assert.AreEqual(
                controlsButton.anchoredPosition,
                settingsButton.anchoredPosition,
                buttonName + " anchored position");
            Assert.AreEqual(controlsButton.sizeDelta, settingsButton.sizeDelta, buttonName + " size");
        }
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static Texture GetBaseTexture(Material material)
    {
        string textureProperty = material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        return material.GetTexture(textureProperty);
    }

    private void CapturePreferences()
    {
        hadPreference = new bool[PreferenceKeys.Length];
        floatPreferences = new float[PreferenceKeys.Length];
        intPreferences = new int[PreferenceKeys.Length];
        for (int index = 0; index < PreferenceKeys.Length; index++)
        {
            hadPreference[index] = PlayerPrefs.HasKey(PreferenceKeys[index]);
            if (!hadPreference[index]) continue;

            floatPreferences[index] = PlayerPrefs.GetFloat(PreferenceKeys[index]);
            intPreferences[index] = PlayerPrefs.GetInt(PreferenceKeys[index]);
        }
    }

    private void RestorePreferences()
    {
        for (int index = 0; index < PreferenceKeys.Length; index++)
        {
            if (!hadPreference[index])
            {
                PlayerPrefs.DeleteKey(PreferenceKeys[index]);
            }
            else if (index == 1)
            {
                PlayerPrefs.SetFloat(PreferenceKeys[index], floatPreferences[index]);
            }
            else
            {
                PlayerPrefs.SetInt(PreferenceKeys[index], intPreferences[index]);
            }
        }

        PlayerPrefs.Save();
    }
}
