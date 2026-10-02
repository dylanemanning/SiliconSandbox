using System.Collections;
using NUnit.Framework;
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

    public override void Setup()
    {
        base.Setup();

        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();

        PlayerPrefs.DeleteKey("Keybind.Pause");
    }

    public override void TearDown()
    {        
        Time.timeScale = 1f;
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
    }

    // ---------------------------------------------------------
    // TC-9.2.2
    // Pause menu must contain navigable settings
    //
    // EXPECTED TO FAIL CURRENTLY.
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_2_2_PauseMenuSettingsExists()
    {
        Assert.Ignore(
            "Pause-menu settings navigation is not implemented yet."
        );

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

        // TODO: Implement Settings in pause menu
        // settingsButton.onClick.Invoke();

        // yield return null;

        // GameObject settingsPanel =
        //     GameObject.Find("SettingsPanel");

        // Assert.IsNotNull(
        //     settingsPanel,
        //     "TC-9.2.2 FAILED: SettingsButton currently " +
        //     "does not open a pause-menu SettingsPanel."
        // );

        // Assert.IsTrue(
        //     settingsPanel.activeInHierarchy,
        //     "Pause settings page exists but was not opened."
        // );
    }

    // ---------------------------------------------------------
    // TC-9.2 Integration
    // Pause menu exists and is navigable
    //
    // EXPECTED TO FAIL until TC-9.2.2 is implemented.
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_9_2_PauseMenuExistsAndNavigable()
    {
        Assert.Ignore(
            "Pause-menu settings navigation is not implemented yet."
        );

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

        // Requirement also says the menu is navigable.
        // Settings currently does nothing, therefore this
        // integration test must not pass yet.

        GameObject settingsPanel =
            GameObject.Find("SettingsPanel");

        Assert.IsNotNull(
            settingsPanel,
            "TC-9.2 FAILED: Pause menu exists, but its " +
            "required settings navigation is not implemented."
        );
    }
}