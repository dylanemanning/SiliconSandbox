using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
// using System.Drawing;

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
    public GameObject duplicateWorldNameMessage; 
    public Transform savedWorldContainer;
    public Button savedWorldButtonPrefab;
    public GameObject noSavedWorldMessage;
    private readonly List<GameObject> generatedWorldRows = new List<GameObject>();
    private ScrollRect loadScrollRect;
    private RectTransform loadViewport;
    private GameObject deleteConfirmationPanel;
    private string pendingDeleteWorldName;

    private void Start()
    {
        EnsureStyler();
        ResolveSettingsControlsPanel();

        mainMenuPanel.SetActive(true);
        LoadPanel.SetActive(false);
        CreatePanel.SetActive(false);
        settingsPanel.SetActive(false);

        duplicateWorldNameMessage.SetActive(false);

        if (settingsControlsPanel != null && settingsControlsPanel.GetComponent<ControlsKeybindsMenu>() == null)
        {
            settingsControlsPanel.AddComponent<ControlsKeybindsMenu>();
        }

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

    private void ResolveSettingsControlsPanel()
    {
        if (settingsControlsPanel != null) return;

        GameObject controlsPage = GameObject.Find("ControlsPage");
        if (controlsPage != null)
        {
            settingsControlsPanel = controlsPage;
        }
    }

    public void startGame()
    {
        WorldSaveSystem.PendingWorldName = "World";
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void createWorld()
    {
        string[] savedWorldNames = WorldSaveSystem.GetSavedWorldNames();
        
        string worldName = worldNameInput == null ? "World" : worldNameInput.text.Trim();
        worldName = string.IsNullOrWhiteSpace(worldName) ? "World" : worldName;
        if(savedWorldNames.Contains(worldName)) {
            worldNameInput.text = "";
            duplicateWorldNameMessage.SetActive(true);
            return;
        }
        WorldSaveSystem.PendingWorldName = worldName;
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
        // Color deleteColor = new Color(0.72f, 0.24f, 0.22f, 1f);

        foreach (GameObject row in generatedWorldRows)
        {
            if (row != null) Destroy(row);
        }
        generatedWorldRows.Clear();

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
            GameObject row = new GameObject(
                $"SavedWorldRow_{worldName}",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement)
            );
            row.transform.SetParent(savedWorldContainer, false);

            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            float rowHeight = Mathf.Max(
                75f,
                LayoutUtility.GetPreferredHeight(savedWorldButtonPrefab.GetComponent<RectTransform>())
            );
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, rowHeight);
            LayoutElement rowLayoutElement = row.GetComponent<LayoutElement>();
            rowLayoutElement.minHeight = rowHeight;
            rowLayoutElement.preferredHeight = rowHeight;
            rowLayoutElement.flexibleHeight = 0f;

            Button loadButton = Instantiate(savedWorldButtonPrefab, row.transform);
            loadButton.name = "LoadWorldButton";
            LayoutElement loadLayout = loadButton.GetComponent<LayoutElement>();
            if (loadLayout == null) loadLayout = loadButton.gameObject.AddComponent<LayoutElement>();
            loadLayout.minHeight = rowHeight;
            loadLayout.flexibleWidth = 1f;
            loadLayout.preferredHeight = rowHeight;
            loadLayout.flexibleHeight = 0f;

            TMP_Text label = loadButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = worldName;
            loadButton.onClick.AddListener(() => loadWorld(worldName));

            Button deleteButton = Instantiate(savedWorldButtonPrefab, row.transform);
            ColorBlock deleteColors = deleteButton.colors;
            deleteButton.name = "DeleteWorldButton";
            deleteColors.normalColor = Color.red;
            LayoutElement deleteLayout = deleteButton.GetComponent<LayoutElement>();
            if (deleteLayout == null) deleteLayout = deleteButton.gameObject.AddComponent<LayoutElement>();
            deleteLayout.minHeight = rowHeight;
            deleteLayout.minWidth = 52f;
            deleteLayout.preferredWidth = 52f;
            deleteLayout.flexibleWidth = 0f;
            deleteLayout.preferredHeight = rowHeight;
            deleteLayout.flexibleHeight = 0f;

            TMP_Text deleteLabel = deleteButton.GetComponentInChildren<TMP_Text>(true);
            if (deleteLabel != null) deleteLabel.text = "X";
            Image deleteBackground = deleteButton.GetComponent<Image>();
            if (deleteBackground != null) deleteBackground.color = new Color(0.72f, 0.24f, 0.22f, 1f);
            deleteButton.onClick.AddListener(() => ShowDeleteConfirmation(worldName));

            generatedWorldRows.Add(row);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)savedWorldContainer);
        if (loadScrollRect != null) loadScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ShowDeleteConfirmation(string worldName)
    {
        if (deleteConfirmationPanel != null) Destroy(deleteConfirmationPanel);
        pendingDeleteWorldName = worldName;

        deleteConfirmationPanel = new GameObject(
            "DeleteWorldConfirmation",
            typeof(RectTransform),
            typeof(Image)
        );
        deleteConfirmationPanel.transform.SetParent(LoadPanel.transform, false);
        deleteConfirmationPanel.transform.SetAsLastSibling();

        RectTransform overlayRect = deleteConfirmationPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = deleteConfirmationPanel.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.68f);
        overlayImage.raycastTarget = true;

        GameObject dialog = new GameObject("ConfirmationDialog", typeof(RectTransform), typeof(Image));
        dialog.transform.SetParent(deleteConfirmationPanel.transform, false);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(420f, 210f);
        dialog.GetComponent<Image>().color = menuStyler != null
            ? menuStyler.panelBackground
            : new Color(0.2f, 0.2f, 0.2f, 1f);

        TMP_Text textTemplate = savedWorldButtonPrefab.GetComponentInChildren<TMP_Text>(true);
        if (textTemplate != null)
        {
            TMP_Text message = Instantiate(textTemplate.gameObject, dialog.transform).GetComponent<TMP_Text>();
            message.gameObject.name = "ConfirmationMessage";
            message.text = $"Delete '{worldName}'?\nThis save cannot be restored.";
            message.alignment = TextAlignmentOptions.Center;
            message.enableAutoSizing = true;
            message.fontSize = 24f;
            RectTransform messageRect = message.rectTransform;
            messageRect.anchorMin = new Vector2(0.05f, 0.42f);
            messageRect.anchorMax = new Vector2(0.95f, 0.95f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;
        }

        GameObject buttonRow = new GameObject(
            "ConfirmationButtons",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup)
        );
        buttonRow.transform.SetParent(dialog.transform, false);
        RectTransform buttonRowRect = buttonRow.GetComponent<RectTransform>();
        buttonRowRect.anchorMin = new Vector2(0.12f, 0.08f);
        buttonRowRect.anchorMax = new Vector2(0.88f, 0.4f);
        buttonRowRect.offsetMin = Vector2.zero;
        buttonRowRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup buttonRowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonRowLayout.spacing = 12f;
        buttonRowLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonRowLayout.childControlWidth = true;
        buttonRowLayout.childControlHeight = true;
        buttonRowLayout.childForceExpandWidth = true;
        buttonRowLayout.childForceExpandHeight = true;

        Button cancelButton = CreateConfirmationButton(buttonRow.transform, "Cancel", "CancelDeleteButton");
        cancelButton.onClick.AddListener(CancelDeleteConfirmation);

        Button confirmButton = CreateConfirmationButton(buttonRow.transform, "Delete", "ConfirmDeleteButton");
        Image confirmBackground = confirmButton.GetComponent<Image>();
        if (confirmBackground != null) confirmBackground.color = new Color(0.72f, 0.24f, 0.22f, 1f);
        confirmButton.onClick.AddListener(ConfirmDeleteWorld);
    }

    private Button CreateConfirmationButton(Transform parent, string labelText, string objectName)
    {
        Button button = Instantiate(savedWorldButtonPrefab, parent);
        button.name = objectName;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = labelText;
        return button;
    }

    private void ConfirmDeleteWorld()
    {
        if (WorldSaveSystem.DeleteSavedWorld(pendingDeleteWorldName))
        {
            CloseDeleteConfirmation();
            PopulateSavedWorlds();
        }
    }

    private void CancelDeleteConfirmation()
    {
        CloseDeleteConfirmation();
    }

    private void CloseDeleteConfirmation()
    {
        if (deleteConfirmationPanel != null) Destroy(deleteConfirmationPanel);
        deleteConfirmationPanel = null;
        pendingDeleteWorldName = null;
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
        CloseDeleteConfirmation();
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
        CloseDeleteConfirmation();
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

        ResolveSettingsControlsPanel();
        if (settingsControlsPanel != null)
        {
            ControlsKeybindsMenu controlsMenu = settingsControlsPanel.GetComponent<ControlsKeybindsMenu>();
            if (controlsMenu == null) controlsMenu = settingsControlsPanel.AddComponent<ControlsKeybindsMenu>();
            controlsMenu.Initialize();
        }

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
