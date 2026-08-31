using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuTraversal : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject LoadPanel;
    public GameObject CreatePanel;
    public GameObject settingsPanel;
    public GameObject settingsControlsPanel;
    public GameObject settingsVideoPanel;
    public GameObject settingsAudioPanel;
    public GameObject settingsAccessibilityPanel;
    public GameObject settingsTabButtons;

    [Header("Styling")]
    public MainMenuStyler menuStyler;

    [Header("Scene")]
    public string gameplaySceneName = "SampleScene";

    private void Start()
    {
        EnsureStyler();

        mainMenuPanel.SetActive(true);
        LoadPanel.SetActive(false);
        CreatePanel.SetActive(false);
        settingsPanel.SetActive(false);

        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    private void EnsureStyler()
    {
        if (menuStyler == null)
        {
            menuStyler = GetComponentInChildren<MainMenuStyler>(true);
        }

        if (menuStyler == null)
        {
            menuStyler = FindFirstObjectByType<MainMenuStyler>();
        }
    }

    public void startGame()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void openLoad()
    {
        EnsureStyler();
        mainMenuPanel.SetActive(false);
        LoadPanel.SetActive(true);

        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    public void closeLoad()
    {
        LoadPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
        
    }

    public void openNew()
    {
        EnsureStyler();
        LoadPanel.SetActive(false);
        CreatePanel.SetActive(true);

        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    public void closeNew()
    {
        CreatePanel.SetActive(false);
        LoadPanel.SetActive(true);
        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    public void openSettings()
    {
        EnsureStyler();
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
        OpenSettingsTab("Controls");

        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    public void closeSettings()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    public void OpenSettingsTab(string tabName)
    {
        settingsControlsPanel.SetActive(tabName == "Controls");
        settingsVideoPanel.SetActive(tabName == "Video");
        settingsAudioPanel.SetActive(tabName == "Audio");
        settingsAccessibilityPanel.SetActive(tabName == "Accessibility");
        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    public void openControlsTab()
    {
        OpenSettingsTab("Controls");
    }

    public void openVideoTab()
    {
        OpenSettingsTab("Video");
    }

    public void openAudioTab()
    {
        OpenSettingsTab("Audio");
    }

    public void openAccessibilityTab()
    {
        OpenSettingsTab("Accessibility");
    }

    public void quitGame()
    {
        Application.Quit();
        Debug.Log("Quit requested"); // shows in editor; quit works in build
    }
}
