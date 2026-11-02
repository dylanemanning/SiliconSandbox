using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class VideoSettingsMenuTests
{
    private const string BrightnessKey = "Video.Brightness";
    private const string ContrastKey = "Video.Contrast";
    private const string FullscreenKey = "Video.Fullscreen";
    private const string ShadowsKey = "Video.Shadows";
    private const string FieldOfViewKey = "Video.FieldOfView";

    private readonly string[] preferenceKeys =
    {
        BrightnessKey,
        ContrastKey,
        FullscreenKey,
        ShadowsKey,
        FieldOfViewKey
    };

    private bool[] hadPreference;
    private float[] floatPreferences;
    private int[] intPreferences;
    private FullScreenMode originalDisplayMode;
    private UnityEngine.ShadowQuality originalShadowQuality;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        CapturePreferences();
        originalDisplayMode = Screen.fullScreenMode;
        originalShadowQuality = QualitySettings.shadows;

        PlayerPrefs.SetFloat(BrightnessKey, 1f);
        PlayerPrefs.SetFloat(ContrastKey, 1f);
        PlayerPrefs.SetInt(FullscreenKey, 0);
        PlayerPrefs.SetInt(ShadowsKey, 1);
        PlayerPrefs.SetFloat(FieldOfViewKey, 60f);

        SceneManager.LoadScene("MainMenu");
        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        RestorePreferences();
        Screen.fullScreenMode = originalDisplayMode;
        QualitySettings.shadows = originalShadowQuality;
        SceneManager.LoadScene("MainMenu");
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator VideoControlsApplyAndPersistTheirSettings()
    {
        MenuTraversal menu = Object.FindFirstObjectByType<MenuTraversal>();
        Assert.IsNotNull(menu);
        menu.openSettings();
        menu.openVideoTab();
        yield return null;

        VideoSettingsMenu settings = menu.settingsVideoPanel.GetComponent<VideoSettingsMenu>();
        Assert.IsNotNull(settings);
        Assert.AreEqual(3, settings.GetComponentsInChildren<Slider>(true).Length);

        Slider brightness = settings.transform.Find(
            "VideoSettingsControls/BrightnessRow/BrightnessControl/brightnessSlider"
        ).GetComponent<Slider>();
        Slider contrast = settings.transform.Find(
            "VideoSettingsControls/ContrastRow/ContrastControl/contrastSlider"
        ).GetComponent<Slider>();
        Slider fieldOfView = settings.transform.Find(
            "VideoSettingsControls/FOVRow/FOVControl/fovSlider"
        ).GetComponent<Slider>();
        Assert.AreEqual("Brightness", settings.transform.Find(
            "VideoSettingsControls/BrightnessRow/Brightness").GetComponent<TMP_Text>().text);
        Assert.AreEqual("FOV", settings.transform.Find(
            "VideoSettingsControls/FOVRow/FOV").GetComponent<TMP_Text>().text);
        Assert.AreEqual("60 deg", settings.transform.Find(
            "VideoSettingsControls/FOVRow/FOVValue").GetComponent<TMP_Text>().text);

        Volume volume = Object.FindFirstObjectByType<Volume>();
        Assert.IsNotNull(volume);
        Assert.IsTrue(volume.profile.TryGet(out ColorAdjustments colorAdjustments));
        float initialExposure = colorAdjustments.postExposure.value;
        float initialContrast = colorAdjustments.contrast.value;

        brightness.value = 1.5f;
        contrast.value = 1.25f;
        fieldOfView.value = 92f;

        Assert.AreEqual(1.5f, PlayerPrefs.GetFloat(BrightnessKey));
        Assert.AreEqual(1.25f, PlayerPrefs.GetFloat(ContrastKey));
        Assert.AreEqual(92f, PlayerPrefs.GetFloat(FieldOfViewKey));
        Assert.AreEqual(92f, Camera.main.fieldOfView);
        Assert.AreEqual(initialExposure + 1f, colorAdjustments.postExposure.value, 0.001f);
        Assert.AreEqual(initialContrast + 25f, colorAdjustments.contrast.value, 0.001f);

        TMP_Dropdown displayMode = settings.transform.Find(
            "VideoSettingsControls/Display ModeRow/Display ModeControl/displayModeDropdown"
        ).GetComponent<TMP_Dropdown>();
        Assert.AreEqual(0, PlayerPrefs.GetInt(FullscreenKey));
        displayMode.value = 1;
        Assert.AreEqual(1, PlayerPrefs.GetInt(FullscreenKey));
        Assert.AreEqual("Fullscreen", displayMode.captionText.text);
        Assert.AreEqual(menu.menuStyler.darkTextColor, displayMode.captionText.color);
        TMP_Text dropdownOptionText = settings.transform.Find(
            "VideoSettingsControls/Display ModeRow/Display ModeControl/displayModeDropdown/Template/Viewport/Content/Item/displayModeDropdownOption"
        ).GetComponent<TMP_Text>();
        Assert.AreEqual(menu.menuStyler.darkTextColor, dropdownOptionText.color);

        Toggle shadows = settings.transform.Find(
            "VideoSettingsControls/ShadowsRow/ShadowsControl/shadowsToggle"
        ).GetComponent<Toggle>();
        shadows.isOn = false;
        Assert.AreEqual(0, PlayerPrefs.GetInt(ShadowsKey));
        Assert.AreEqual(UnityEngine.ShadowQuality.Disable, QualitySettings.shadows);
        Assert.AreEqual("Off", settings.transform.Find(
            "VideoSettingsControls/ShadowsRow/ShadowsValue").GetComponent<TMP_Text>().text);

        Button saveButton = settings.transform.Find(
            "VideoSettingsActions/SaveButton").GetComponent<Button>();
        Button exitButton = settings.transform.Find(
            "VideoSettingsActions/ExitButton").GetComponent<Button>();
        Assert.AreEqual("Save", saveButton.GetComponentInChildren<TMP_Text>(true).text);
        Assert.AreEqual("Exit", exitButton.GetComponentInChildren<TMP_Text>(true).text);
        saveButton.onClick.Invoke();
        Assert.IsTrue(menu.settingsPanel.activeSelf);
        exitButton.onClick.Invoke();
        Assert.IsFalse(menu.settingsPanel.activeSelf);
        Assert.IsTrue(menu.mainMenuPanel.activeSelf);
    }

    private void CapturePreferences()
    {
        hadPreference = new bool[preferenceKeys.Length];
        floatPreferences = new float[preferenceKeys.Length];
        intPreferences = new int[preferenceKeys.Length];

        for (int index = 0; index < preferenceKeys.Length; index++)
        {
            hadPreference[index] = PlayerPrefs.HasKey(preferenceKeys[index]);
            if (!hadPreference[index]) continue;

            floatPreferences[index] = PlayerPrefs.GetFloat(preferenceKeys[index]);
            intPreferences[index] = PlayerPrefs.GetInt(preferenceKeys[index]);
        }
    }

    private void RestorePreferences()
    {
        for (int index = 0; index < preferenceKeys.Length; index++)
        {
            if (!hadPreference[index])
            {
                PlayerPrefs.DeleteKey(preferenceKeys[index]);
            }
            else if (index == 2 || index == 3)
            {
                PlayerPrefs.SetInt(preferenceKeys[index], intPreferences[index]);
            }
            else
            {
                PlayerPrefs.SetFloat(preferenceKeys[index], floatPreferences[index]);
            }
        }

        PlayerPrefs.Save();
    }
}
