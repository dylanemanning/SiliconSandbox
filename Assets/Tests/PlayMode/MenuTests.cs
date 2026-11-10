using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class MenuTests : InputTestFixture
{
    private const string MainMenuScene = "MainMenu";
    private const string GameplayScene = "SampleScene";

    private Keyboard keyboard;
    private Mouse mouse;
    private bool hadFontSizePreference;
    private int originalFontSizePreference;

    public override void Setup()
    {
        base.Setup();

        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        hadFontSizePreference = PlayerPrefs.HasKey(AccessibilitySettings.FontSizeKey);
        originalFontSizePreference = PlayerPrefs.GetInt(AccessibilitySettings.FontSizeKey);

        PlayerPrefs.SetInt(AccessibilitySettings.FontSizeKey, 1);
        PlayerPrefs.DeleteKey("Keybind.Pause");
    }

    public override void TearDown()
    {        
        Time.timeScale = 1f;
        if (hadFontSizePreference)
        {
            PlayerPrefs.SetInt(AccessibilitySettings.FontSizeKey, originalFontSizePreference);
        }
        else
        {
            PlayerPrefs.DeleteKey(AccessibilitySettings.FontSizeKey);
        }

        base.TearDown();
    }

    private IEnumerator LoadScene(string scene)
    {
        SceneManager.LoadScene(scene);

        yield return null;
        yield return null;
    }

    // ---------------------------------------------------------
    // TC-9.1.1
    // Main menu is first user-facing menu
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_1_1_MainMenuAppears()
    {
        yield return LoadScene(MainMenuScene);

        Assert.AreEqual(
            MainMenuScene,
            SceneManager.GetActiveScene().name
        );

        MenuTraversal menu =
            Object.FindFirstObjectByType<MenuTraversal>();

        Assert.IsNotNull(menu);
        Assert.IsNotNull(menu.mainMenuPanel);

        Assert.IsTrue(
            menu.mainMenuPanel.activeSelf
        );
    }

    [UnityTest]
    public IEnumerator MenuStylerUsesConsistentCenteredLayoutAndPageColors()
    {
        yield return LoadScene(MainMenuScene);

        MenuTraversal menu = Object.FindFirstObjectByType<MenuTraversal>();
        Assert.IsNotNull(menu);
        Assert.IsNotNull(menu.menuStyler);

        menu.menuStyler.Apply();

        TMP_Text title = null;
        foreach (TMP_Text text in menu.mainMenuPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == "Title")
            {
                title = text;
                break;
            }
        }

        Assert.IsNotNull(title);
        Assert.AreEqual(TextAlignmentOptions.Center, title.alignment);
        Assert.AreEqual(0.5f, title.rectTransform.anchorMin.x);
        Assert.AreEqual(0.5f, title.rectTransform.anchorMax.x);
        Assert.AreEqual(0f, title.rectTransform.anchoredPosition.x);

        Button playButton = null;
        foreach (Button button in menu.mainMenuPanel.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "PlayButton")
            {
                playButton = button;
                break;
            }
        }

        Assert.IsNotNull(playButton);
        Assert.IsNotNull(playButton.transform.parent.GetComponent<VerticalLayoutGroup>());
        Assert.AreEqual(
            menu.menuStyler.buttonFontSize,
            playButton.GetComponentInChildren<TMP_Text>(true).fontSize
        );

        Assert.AreNotEqual(
            menu.mainMenuPanel.GetComponent<Image>().color,
            menu.LoadPanel.GetComponent<Image>().color
        );
        Assert.AreNotEqual(
            menu.LoadPanel.GetComponent<Image>().color,
            menu.CreatePanel.GetComponent<Image>().color
        );

        Transform backdrop = menu.mainMenuPanel.transform.Find("LogicGateBackdrop");
        Assert.IsNotNull(backdrop);
        Assert.AreEqual(0, backdrop.GetSiblingIndex());

        int gateCount = 0;
        foreach (Transform item in backdrop)
        {
            if (!item.name.StartsWith("Gate_")) continue;

            gateCount++;
            RectTransform gateRect = item.GetComponent<RectTransform>();
            Assert.IsTrue(gateRect.anchorMin.x == 0f || gateRect.anchorMin.x == 1f);
            Assert.Greater(Mathf.Abs(gateRect.anchoredPosition.x), 170f);
            Assert.Less(Mathf.Abs(gateRect.anchoredPosition.x), 200f);
            Image symbolImage = item.GetComponent<Image>();
            Assert.IsFalse(symbolImage.raycastTarget);
            Assert.IsNotNull(symbolImage.sprite);
            Assert.IsNull(item.GetComponentInChildren<TMP_Text>(true));
        }

        Assert.AreEqual(menu.menuStyler.gatesPerSide * 2, gateCount);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        Assert.IsNotNull(canvas);
        foreach (VerticalLayoutGroup buttonContainer in canvas.GetComponentsInChildren<VerticalLayoutGroup>(true))
        {
            if (buttonContainer.gameObject.name != "Button Container") continue;

            RectTransform containerRect = buttonContainer.GetComponent<RectTransform>();
            Assert.AreEqual(0.5f, containerRect.anchorMin.x);
            Assert.AreEqual(0.5f, containerRect.anchorMax.x);
            Assert.AreEqual(0f, containerRect.anchoredPosition.x);
            Assert.AreEqual(520f, containerRect.rect.width);
        }
    }

    // ---------------------------------------------------------
    // TC-9.1.2
    // Return from gameplay to main menu
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_1_2_ReturnToMainMenu()
    {
        yield return LoadScene(GameplayScene);

        PauseManager pause =
            Object.FindFirstObjectByType<PauseManager>();

        Assert.IsNotNull(pause);

        pause.ReturnToMainMenu();

        yield return null;
        yield return null;

        Assert.AreEqual(
            MainMenuScene,
            SceneManager.GetActiveScene().name
        );

        MenuTraversal menu =
            Object.FindFirstObjectByType<MenuTraversal>();

        Assert.IsNotNull(menu);

        Assert.IsTrue(
            menu.mainMenuPanel.activeSelf
        );
    }

    // ---------------------------------------------------------
    // TC-9.1 Integration
    // Main menu exists
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_1_MainMenuExists()
    {
        yield return LoadScene(MainMenuScene);

        MenuTraversal menu =
            Object.FindFirstObjectByType<MenuTraversal>();

        Assert.IsNotNull(
            menu,
            "MainMenu does not contain MenuTraversal."
        );

        Assert.IsNotNull(menu.mainMenuPanel);

        Assert.IsTrue(menu.mainMenuPanel.activeInHierarchy);
    }

    // ---------------------------------------------------------
    // TC-9.2.1
    // Escape opens pause menu
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_2_1_EscapeOpensPauseMenu()
    {
        WorldSaveSystem.PendingWorldName = "TC_PauseWorldLabel";
        yield return LoadScene(GameplayScene);

        PauseManager pause =
            Object.FindFirstObjectByType<PauseManager>();

        Assert.IsNotNull(pause);

        Assert.IsFalse(PauseManager.IsPaused);

        Press(keyboard.escapeKey);

        yield return null;

        Release(keyboard.escapeKey);

        yield return null;

        Assert.IsTrue(
            PauseManager.IsPaused,
            "Escape did not pause the game."
        );

        Assert.AreEqual(
            0f,
            Time.timeScale,
            "Game time did not stop when paused."
        );

        Assert.IsTrue(
            Cursor.visible,
            "Cursor should become visible while paused."
        );

        MainMenuStyler styler = Object.FindFirstObjectByType<MainMenuStyler>();
        Assert.IsNotNull(styler, "Gameplay scene should initialize the shared menu styler.");

        GameObject pausePanel = GameObject.Find("PausePanel");
        Assert.IsNotNull(pausePanel);
        Assert.AreEqual(styler.pauseBackground, pausePanel.GetComponent<Image>().color);
        Assert.IsTrue(pausePanel.GetComponent<Image>().raycastTarget);

        TMP_Text pauseTitle = null;
        foreach (TMP_Text text in pausePanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == "Title")
            {
                pauseTitle = text;
                break;
            }
        }

        Assert.IsNotNull(pauseTitle);
        Assert.AreEqual(TextAlignmentOptions.Center, pauseTitle.alignment);
        Assert.AreEqual(styler.fontAsset, pauseTitle.font);

        Button resumeButton = pausePanel.transform.Find("Button Container/ResumeButton").GetComponent<Button>();
        Assert.AreEqual(styler.buttonBackground, resumeButton.colors.normalColor);
        Assert.AreEqual(styler.fontAsset, resumeButton.GetComponentInChildren<TMP_Text>(true).font);

        // TMP_Text worldNameLabel = pausePanel.transform
        //     .Find("currentWorldName")
        //     .GetComponent<TMP_Text>();
        // Assert.AreEqual("TC_PauseWorldLabel", worldNameLabel.text);
    }

    // ---------------------------------------------------------
    // TC-9.2.2
    // Pause menu opens settings without resuming gameplay.
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_2_2_PauseMenuSettingsExists()
    {
        yield return LoadScene(GameplayScene);

        PauseManager pause =
            Object.FindFirstObjectByType<PauseManager>();

        Assert.IsNotNull(pause);

        pause.PauseGame();

        yield return null;

        GameObject pausePanel =
            GameObject.Find("PausePanel");

        Assert.IsNotNull(
            pausePanel,
            "PausePanel was not found."
        );

        Button[] buttons =
            pausePanel.GetComponentsInChildren<Button>(true);

        Button settingsButton = null;

        foreach (Button button in buttons)
        {
            if (button.name == "SettingsButton")
            {
                settingsButton = button;
                break;
            }
        }

        Assert.IsNotNull(
            settingsButton,
            "Pause menu does not contain SettingsButton."
        );

        settingsButton.onClick.Invoke();

        GameObject settingsPanel = GameObject.Find("settingsPanel");
        Assert.IsNotNull(settingsPanel);
        Assert.IsTrue(settingsPanel.activeSelf);
        Assert.IsFalse(pausePanel.activeSelf);
        Assert.IsTrue(PauseManager.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(settingsPanel.transform.Find("SettingsContent/ControlsPage").gameObject.activeSelf);
        Assert.IsFalse(settingsPanel.transform.Find("SettingsContent/VideoPage").gameObject.activeSelf);
        Assert.IsFalse(settingsPanel.transform.Find("SettingsContent/AccessibilityPage").gameObject.activeSelf);
    }

    // ---------------------------------------------------------
    // TC-9.2 Integration
    // Settings tabs and exit preserve the paused game state.
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_2_PauseMenuExistsAndNavigable()
    {
        yield return LoadScene(GameplayScene);

        PauseManager pause =
            Object.FindFirstObjectByType<PauseManager>();

        Assert.IsNotNull(pause);

        pause.PauseGame();

        yield return null;

        Assert.IsTrue(PauseManager.IsPaused);

        GameObject pausePanel =
            GameObject.Find("PausePanel");

        Assert.IsNotNull(pausePanel);

        Button[] buttons =
            pausePanel.GetComponentsInChildren<Button>(true);

        Assert.GreaterOrEqual(
            buttons.Length,
            3,
            "Pause menu should contain Resume, Settings, and Quit."
        );

        bool resumeFound = false;
        bool settingsFound = false;
        bool quitFound = false;

        foreach (Button button in buttons)
        {
            if (button.name == "ResumeButton")
                resumeFound = true;

            if (button.name == "SettingsButton")
                settingsFound = true;

            if (button.name == "QuitButton")
                quitFound = true;
        }

        Assert.IsTrue(resumeFound);
        Assert.IsTrue(settingsFound);
        Assert.IsTrue(quitFound);

        pausePanel.transform.Find("Button Container/SettingsButton")
            .GetComponent<Button>().onClick.Invoke();
        GameObject settingsPanel = GameObject.Find("settingsPanel");
        Assert.IsNotNull(settingsPanel);
        Assert.IsTrue(settingsPanel.activeSelf);

        GameObject.Find("VideoButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(settingsPanel.transform.Find("SettingsContent/VideoPage").gameObject.activeSelf);
        Assert.IsFalse(settingsPanel.transform.Find("SettingsContent/ControlsPage").gameObject.activeSelf);

        GameObject.Find("AccessibilityButton").GetComponent<Button>().onClick.Invoke();
        GameObject accessibilityPage =
            settingsPanel.transform.Find("SettingsContent/AccessibilityPage").gameObject;
        Assert.IsTrue(accessibilityPage.activeSelf);
        Assert.IsFalse(settingsPanel.transform.Find("SettingsContent/VideoPage").gameObject.activeSelf);
        AccessibilitySettingsMenu accessibilityMenu =
            accessibilityPage.GetComponent<AccessibilitySettingsMenu>();
        Assert.IsNotNull(accessibilityMenu);
        Assert.IsNotNull(accessibilityPage.transform.Find(
            "AccessibilitySettingsControls/Colorblind ModeRow/Colorblind ModeControl/colorblindModeToggle"));

        accessibilityPage.transform.Find("AccessibilitySettingsActions/ExitButton")
            .GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(settingsPanel.activeSelf);
        Assert.IsTrue(pausePanel.activeSelf);
        Assert.IsTrue(PauseManager.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);

        pause.ResumeGame();
        Assert.IsFalse(PauseManager.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator PauseSettingsKeepHotbarTextSizeAndRenderAboveHotbar()
    {
        PlayerPrefs.SetInt(AccessibilitySettings.FontSizeKey, 2);
        yield return LoadScene(GameplayScene);

        PauseManager pause = Object.FindFirstObjectByType<PauseManager>();
        GameObject hotbar = GameObject.Find("Hotbar");
        GameObject pauseCanvasObject = GameObject.Find("PauseCanvas");
        Assert.IsNotNull(pause);
        Assert.IsNotNull(hotbar);
        Assert.IsNotNull(pauseCanvasObject);

        Canvas hotbarCanvas = hotbar.GetComponent<Canvas>();
        Canvas pauseCanvas = pauseCanvasObject.GetComponent<Canvas>();
        Assert.IsNotNull(hotbarCanvas);
        Assert.IsNotNull(pauseCanvas);
        Assert.Greater(pauseCanvas.sortingOrder, hotbarCanvas.sortingOrder);

        TMP_Text[] hotbarTexts = hotbar.GetComponentsInChildren<TMP_Text>(true);
        Assert.IsNotEmpty(hotbarTexts);
        float[] initialFontSizes = new float[hotbarTexts.Length];
        for (int i = 0; i < hotbarTexts.Length; i++)
        {
            initialFontSizes[i] = hotbarTexts[i].fontSize;
        }

        for (int i = 0; i < 3; i++)
        {
            pause.PauseGame();
            pause.OpenSettings();
            pause.CloseSettings();

            for (int textIndex = 0; textIndex < hotbarTexts.Length; textIndex++)
            {
                Assert.AreEqual(
                    initialFontSizes[textIndex],
                    hotbarTexts[textIndex].fontSize,
                    0.01f);
            }
        }

        pause.ResumeGame();
    }
}