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
    private ScrollRect loadScrollRect;
    private RectTransform loadViewport;

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
        ConfigureLoadScroll();
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

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)savedWorldContainer);
        if (loadScrollRect != null) loadScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ConfigureLoadScroll()
    {
        if (LoadPanel == null || savedWorldContainer == null) return;

        RectTransform panelTransform = LoadPanel.GetComponent<RectTransform>();
        RectTransform contentTransform = savedWorldContainer as RectTransform;
        if (panelTransform == null || contentTransform == null) return;

        loadScrollRect = LoadPanel.GetComponent<ScrollRect>();
        if (loadScrollRect == null) loadScrollRect = LoadPanel.AddComponent<ScrollRect>();

        loadScrollRect.horizontal = false;
        loadScrollRect.vertical = true;
        loadScrollRect.movementType = ScrollRect.MovementType.Clamped;
        loadScrollRect.scrollSensitivity = 30f;
        loadViewport = EnsureLoadViewport(panelTransform, contentTransform);
        loadScrollRect.viewport = loadViewport;
        loadScrollRect.content = contentTransform;

        ContentSizeFitter fitter = savedWorldContainer.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = savedWorldContainer.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup layout = savedWorldContainer.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
        }

        CreateLoadScrollbar(loadViewport);
    }

    private RectTransform EnsureLoadViewport(RectTransform panelTransform, RectTransform contentTransform)
    {
        if (loadViewport != null) return loadViewport;

        GameObject viewportObject = new GameObject("LoadViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(panelTransform, false);
        loadViewport = viewportObject.GetComponent<RectTransform>();

        loadViewport.anchorMin = new Vector2(0.5f, 0.5f);
        loadViewport.anchorMax = new Vector2(0.5f, 0.5f);
        loadViewport.pivot = new Vector2(0.5f, 0.5f);
        loadViewport.anchoredPosition = new Vector2(60f, 100f);
        loadViewport.sizeDelta = new Vector2(500f, 300f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        contentTransform.SetParent(loadViewport, false);
        contentTransform.anchorMin = new Vector2(0f, 1f);
        contentTransform.anchorMax = new Vector2(1f, 1f);
        contentTransform.pivot = new Vector2(0.5f, 1f);
        contentTransform.anchoredPosition = Vector2.zero;
        contentTransform.sizeDelta = new Vector2(0f, 0f);

        return loadViewport;
    }

    private void CreateLoadScrollbar(RectTransform viewportTransform)
    {
        Scrollbar scrollbar = LoadPanel.GetComponentInChildren<Scrollbar>(true);
        if (scrollbar == null)
        {
            GameObject scrollbarObject = new GameObject("LoadScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarObject.transform.SetParent(viewportTransform, false);
            RectTransform scrollbarTransform = (RectTransform)scrollbarObject.transform;
            scrollbarTransform.anchorMin = new Vector2(1f, 0f);
            scrollbarTransform.anchorMax = new Vector2(1f, 1f);
            scrollbarTransform.pivot = new Vector2(1f, 0.5f);
            scrollbarTransform.anchoredPosition = new Vector2(-12f, 0f);
            scrollbarTransform.sizeDelta = new Vector2(18f, -180f);

            Image track = scrollbarObject.GetComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.35f);

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(scrollbarObject.transform, false);
            RectTransform handleTransform = (RectTransform)handleObject.transform;
            handleTransform.anchorMin = Vector2.zero;
            handleTransform.anchorMax = Vector2.one;
            handleTransform.offsetMin = new Vector2(2f, 2f);
            handleTransform.offsetMax = new Vector2(-2f, -2f);
            handleObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.75f);

            scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.targetGraphic = handleObject.GetComponent<Image>();
            scrollbar.handleRect = handleTransform;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
        }

        loadScrollRect.verticalScrollbar = scrollbar;
        loadScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        loadScrollRect.verticalScrollbarSpacing = -3f;
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
