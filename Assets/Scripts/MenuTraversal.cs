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

    [Header("Scene")]
    public string gameplaySceneName = "SampleScene";

    private void Start()
    {
        mainMenuPanel.SetActive(true);
        LoadPanel.SetActive(false);
        CreatePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    public void startGame()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void openLoad()
    {
        mainMenuPanel.SetActive(false);
        LoadPanel.SetActive(true);
    }

    public void closeLoad()
    {
        LoadPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        
    }

    public void openNew()
    {
        LoadPanel.SetActive(false);
        CreatePanel.SetActive(true);
    }

    public void closeNew()
    {
        CreatePanel.SetActive(false);
        LoadPanel.SetActive(true);
    }

    public void openSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
        OpenSettingsTab("Controls");
    }

    public void closeSettings()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void OpenSettingsTab(string tabName)
    {
        settingsControlsPanel.SetActive(tabName == "Controls");
        settingsVideoPanel.SetActive(tabName == "Video");
        settingsAudioPanel.SetActive(tabName == "Audio");
        settingsAccessibilityPanel.SetActive(tabName == "Accessibility");
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
