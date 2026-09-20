using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

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
    public TMP_InputField worldNameInput;
    public Transform savedWorldContainer;
    public Button savedWorldButtonPrefab;
    public GameObject noSavedWorldMessage;
    private readonly List<Button> generatedWorldButtons = new List<Button>();

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
        WorldSaveSystem.PendingWorldName = "World";
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void createWorld()
    {
        string worldName = worldNameInput == null ? "World" : worldNameInput.text.Trim();
        WorldSaveSystem.PendingWorldName = string.IsNullOrWhiteSpace(worldName) ? "World" : worldName;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void loadWorld(string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return;
        WorldSaveSystem.PendingWorldName = worldName;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void openLoad()
    {
        EnsureStyler();
        mainMenuPanel.SetActive(false);
        LoadPanel.SetActive(true);
        PopulateSavedWorlds();

        if (menuStyler != null)
        {
            menuStyler.Apply();
        }
    }

    private void PopulateSavedWorlds()
    {
        foreach (Button button in generatedWorldButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        generatedWorldButtons.Clear();

        string[] savedWorldNames = WorldSaveSystem.GetSavedWorldNames();
        GameObject emptyMessage = noSavedWorldMessage;
        if (emptyMessage == null && LoadPanel != null)
        {
            Transform messageTransform = LoadPanel.transform.Find("NoProjectError");
            if (messageTransform != null) emptyMessage = messageTransform.gameObject;
        }

        if (emptyMessage != null) emptyMessage.SetActive(savedWorldNames.Length == 0);
        if (savedWorldContainer == null || savedWorldButtonPrefab == null) return;

        foreach (string worldName in savedWorldNames)
        {
            Button button = Instantiate(savedWorldButtonPrefab, savedWorldContainer);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = worldName;
            button.onClick.AddListener(() => loadWorld(worldName));
            generatedWorldButtons.Add(button);
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
