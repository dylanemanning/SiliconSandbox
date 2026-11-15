using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject settingsControlsPage;
    [SerializeField] private GameObject settingsVideoPage;
    [SerializeField] private GameObject settingsAccessibilityPage;
    private MainMenuStyler menuStyler;
    private TMP_Text currentWorldNameLabel;
    private TMP_InputField worldNameEditField;
    private TMP_Text worldNameEditError;
    private Button editWorldNameButton;
    private GameObject worldNameEditRoot;

    public static bool IsPaused { get; private set; }
    public bool IsSettingsOpen => settingsPanel != null && settingsPanel.activeSelf;

    private void Start()
    {
        IsPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        EnsureMenuStyler();
        if (pausePanel != null) TryApplyMenuStyle(pausePanel.transform);

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (InventoryGrid.ClosedThisFrame || Player.ConsumeWireCancelThisFrame() || PackagingWandController.IsConfirmationOpen) return;
        if (KeybindSettings.WasPressedThisFrame(KeybindSettings.Pause))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            if (IsSettingsOpen) CloseSettings();
            else ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        InventoryGrid.Close();
        //Debug.Log("Pause");
        IsPaused = true;

        EnsureMenuStyler();
        if (pausePanel != null)
        {
            TryApplyMenuStyle(pausePanel.transform);
            UpdateCurrentWorldNameLabel();
            pausePanel.SetActive(true);
        }

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenSettings()
    {
        if (pausePanel == null || settingsPanel == null ||
            settingsControlsPage == null || settingsVideoPage == null || settingsAccessibilityPage == null)
        {
            Debug.LogError("Cannot open pause settings because a settings panel or page reference is missing.");
            return;
        }

        if (!IsPaused) PauseGame();

        if (!InitializeSettingsMenus()) return;
        SetSettingsPage(settingsControlsPage);
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
        TryApplyMenuStyle(settingsPanel.transform);
    }

    public void ShowControlsSettings()
    {
        SetSettingsPage(settingsControlsPage);
    }

    public void ShowVideoSettings()
    {
        SetSettingsPage(settingsVideoPage);
    }

    public void ShowAccessibilitySettings()
    {
        SetSettingsPage(settingsAccessibilityPage);
    }

    public void CloseSettings()
    {
        if (settingsPanel == null)
        {
            Debug.LogError("Cannot close pause settings because a settings panel reference is missing.");
            return;
        }

        settingsPanel.SetActive(false);
        if (pausePanel == null)
        {
            Debug.LogError("Cannot restore pause menu because the pause panel reference is missing.");
            return;
        }

        pausePanel.SetActive(true);
        TryApplyMenuStyle(pausePanel.transform);
    }

    public static bool TryCloseSettings()
    {
        PauseManager pauseManager = FindFirstObjectByType<PauseManager>();
        if (pauseManager == null || !pauseManager.IsSettingsOpen) return false;

        pauseManager.CloseSettings();
        return true;
    }

    private bool InitializeSettingsMenus()
    {
        if (settingsControlsPage == null || settingsVideoPage == null || settingsAccessibilityPage == null)
        {
            Debug.LogError("Cannot initialize pause settings because one or more settings pages are not assigned.");
            return false;
        }

        ControlsKeybindsMenu controlsMenu = settingsControlsPage.GetComponent<ControlsKeybindsMenu>();
        if (controlsMenu == null) controlsMenu = settingsControlsPage.AddComponent<ControlsKeybindsMenu>();
        controlsMenu.Initialize();

        VideoSettingsMenu videoMenu = settingsVideoPage.GetComponent<VideoSettingsMenu>();
        if (videoMenu == null) videoMenu = settingsVideoPage.AddComponent<VideoSettingsMenu>();
        videoMenu.Initialize();

        AccessibilitySettingsMenu accessibilityMenu =
            settingsAccessibilityPage.GetComponent<AccessibilitySettingsMenu>();
        if (accessibilityMenu == null)
        {
            accessibilityMenu = settingsAccessibilityPage.AddComponent<AccessibilitySettingsMenu>();
        }

        accessibilityMenu.Initialize();
        return true;
    }

    private void SetSettingsPage(GameObject selectedPage)
    {
        if (selectedPage == null || settingsControlsPage == null ||
            settingsVideoPage == null || settingsAccessibilityPage == null)
        {
            Debug.LogError("Cannot change pause settings tab because one or more settings pages are not assigned.");
            return;
        }

        settingsControlsPage.SetActive(selectedPage == settingsControlsPage);
        settingsVideoPage.SetActive(selectedPage == settingsVideoPage);
        settingsAccessibilityPage.SetActive(selectedPage == settingsAccessibilityPage);
    }

    private void EnsureMenuStyler()
    {
        if (menuStyler != null) return;

        menuStyler = FindFirstObjectByType<MainMenuStyler>();
        if (menuStyler != null) return;

        if (pausePanel != null)
        {
            menuStyler = pausePanel.GetComponent<MainMenuStyler>();
            if (menuStyler == null) menuStyler = pausePanel.AddComponent<MainMenuStyler>();
            return;
        }

        menuStyler = gameObject.GetComponent<MainMenuStyler>();
        if (menuStyler == null) menuStyler = gameObject.AddComponent<MainMenuStyler>();
    }

    private bool TryApplyMenuStyle(Transform target)
    {
        if (target == null) return false;

        EnsureMenuStyler();
        if (menuStyler == null) return false;

        menuStyler.ApplyTo(target);
        return true;
    }

    private void UpdateCurrentWorldNameLabel()
    {
        if (currentWorldNameLabel == null && pausePanel != null)
        {
            Transform labelTransform = pausePanel.transform.Find("currentWorldName");
            if (labelTransform != null) currentWorldNameLabel = labelTransform.GetComponent<TMP_Text>();
        }

        if (currentWorldNameLabel == null) return;

        WorldSaveSystem saveSystem = WorldSaveSystem.Instance;
        string worldName = saveSystem != null ? saveSystem.WorldName : WorldSaveSystem.PendingWorldName;
        currentWorldNameLabel.text = string.IsNullOrWhiteSpace(worldName) ? "World" : worldName;
        EnsureWorldNameEditor();
        if (editWorldNameButton != null)
        {
            editWorldNameButton.gameObject.SetActive(
                !string.Equals(worldName, WorldSaveSystem.TutorialWorldName, System.StringComparison.OrdinalIgnoreCase)
            );
        }
    }

    private void EnsureWorldNameEditor()
    {
        if (worldNameEditField != null || currentWorldNameLabel == null) return;
        RectTransform labelRect = currentWorldNameLabel.rectTransform;

        editWorldNameButton = CreateWorldNameActionButton(
            currentWorldNameLabel.transform.parent,
            "EditWorldNameButton",
            "Edit",
            labelRect.anchorMin,
            labelRect.anchoredPosition + new Vector2(320f, 0f),
            new Vector2(100f, 56f),
            BeginWorldNameEdit,
            new Color(0.22f, 0.38f, 0.52f, 1f)
        );

        worldNameEditRoot = new GameObject(
            "WorldNameEditField",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField)
        );
        worldNameEditRoot.transform.SetParent(currentWorldNameLabel.transform.parent, false);
        RectTransform editRect = worldNameEditRoot.GetComponent<RectTransform>();
        editRect.anchorMin = labelRect.anchorMin;
        editRect.anchorMax = labelRect.anchorMax;
        editRect.pivot = labelRect.pivot;
        editRect.anchoredPosition = labelRect.anchoredPosition;
        editRect.sizeDelta = labelRect.sizeDelta;
        editRect.localRotation = Quaternion.identity;
        editRect.localScale = Vector3.one;

        Image editBackground = worldNameEditRoot.GetComponent<Image>();
        editBackground.color = new Color(0.12f, 0.22f, 0.31f, 1f);
        editBackground.raycastTarget = true;

        RectTransform viewport = CreateRectTransform("TextViewport", worldNameEditRoot.transform);
        viewport.anchorMin = new Vector2(0.03f, 0.12f);
        viewport.anchorMax = new Vector2(0.60f, 0.88f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();

        TMP_Text editText = Instantiate(currentWorldNameLabel, viewport);
        editText.gameObject.name = "Text";
        editText.raycastTarget = false;
        editText.alignment = TextAlignmentOptions.Left;
        RectTransform textRect = editText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        worldNameEditField = worldNameEditRoot.GetComponent<TMP_InputField>();
        worldNameEditField.targetGraphic = editBackground;
        worldNameEditField.textViewport = viewport;
        worldNameEditField.textComponent = editText;
        worldNameEditField.lineType = TMP_InputField.LineType.SingleLine;
        worldNameEditField.characterLimit = MenuTraversal.MaxWorldNameCharacters;

        Button confirmButton = CreateWorldNameActionButton(
            currentWorldNameLabel.transform.parent,
            "ConfirmWorldNameButton",
            "Save",
            labelRect.anchorMin,
            labelRect.anchoredPosition + new Vector2(170f, 0f),
            new Vector2(110f, 56f),
            ConfirmWorldNameEdit,
            new Color(0.2f, 0.52f, 0.34f, 1f)
        );
        Button cancelButton = CreateWorldNameActionButton(
            currentWorldNameLabel.transform.parent,
            "CancelWorldNameButton",
            "Cancel",
            labelRect.anchorMin,
            labelRect.anchoredPosition + new Vector2(290f, 0f),
            new Vector2(110f, 56f),
            CancelWorldNameEdit,
            new Color(0.58f, 0.28f, 0.27f, 1f)
        );
        confirmButton.gameObject.SetActive(false);
        cancelButton.gameObject.SetActive(false);

        worldNameEditError = Instantiate(currentWorldNameLabel, currentWorldNameLabel.transform.parent);
        worldNameEditError.gameObject.name = "WorldNameEditError";
        worldNameEditError.text = string.Empty;
        worldNameEditError.fontSize = 18f;
        worldNameEditError.color = new Color(1f, 0.72f, 0.62f, 1f);
        worldNameEditError.alignment = TextAlignmentOptions.Center;
        RectTransform errorRect = worldNameEditError.rectTransform;
        errorRect.anchoredPosition = labelRect.anchoredPosition + new Vector2(0f, -58f);
        errorRect.sizeDelta = new Vector2(labelRect.sizeDelta.x, 40f);
        worldNameEditError.gameObject.SetActive(false);
        worldNameEditRoot.SetActive(false);
    }

    private RectTransform CreateRectTransform(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private Button CreateWorldNameActionButton(
        Transform parent,
        string objectName,
        string caption,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size,
        System.Action action,
        Color backgroundColor)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchor;
        buttonRect.anchorMax = anchor;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        Image background = buttonObject.GetComponent<Image>();
        background.color = backgroundColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(() => action());

        GameObject captionObject = new GameObject(
            "Caption",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        captionObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI captionText = captionObject.GetComponent<TextMeshProUGUI>();
        captionText.font = currentWorldNameLabel.font;
        captionText.fontSize = 22f;
        captionText.color = Color.white;
        captionText.text = caption;
        captionText.alignment = TextAlignmentOptions.Center;
        captionText.textWrappingMode = TextWrappingModes.NoWrap;
        captionText.raycastTarget = false;
        RectTransform captionRect = captionText.rectTransform;
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.pivot = new Vector2(0.5f, 0.5f);
        captionRect.offsetMin = Vector2.zero;
        captionRect.offsetMax = Vector2.zero;
        return button;
    }

    private void BeginWorldNameEdit()
    {
        if (worldNameEditField == null || currentWorldNameLabel == null) return;
        worldNameEditError.gameObject.SetActive(false);
        worldNameEditField.text = currentWorldNameLabel.text;
        currentWorldNameLabel.enabled = false;
        editWorldNameButton.gameObject.SetActive(false);
        pausePanel.transform.Find("ConfirmWorldNameButton").gameObject.SetActive(true);
        pausePanel.transform.Find("CancelWorldNameButton").gameObject.SetActive(true);
        worldNameEditRoot.SetActive(true);
        worldNameEditField.Select();
        worldNameEditField.ActivateInputField();
    }

    private void ConfirmWorldNameEdit()
    {
        WorldSaveSystem saveSystem = WorldSaveSystem.Instance;
        if (saveSystem == null)
        {
            ShowWorldNameEditError("The world save system is unavailable.");
            return;
        }

        if (!saveSystem.TryRenameWorld(worldNameEditField.text, out string error))
        {
            ShowWorldNameEditError(error);
            return;
        }

        FinishWorldNameEdit();
        UpdateCurrentWorldNameLabel();
    }

    private void CancelWorldNameEdit()
    {
        FinishWorldNameEdit();
    }

    private void FinishWorldNameEdit()
    {
        currentWorldNameLabel.enabled = true;
        worldNameEditRoot.SetActive(false);
        worldNameEditError.gameObject.SetActive(false);
        editWorldNameButton.gameObject.SetActive(true);
        pausePanel.transform.Find("ConfirmWorldNameButton").gameObject.SetActive(false);
        pausePanel.transform.Find("CancelWorldNameButton").gameObject.SetActive(false);
    }

    private void ShowWorldNameEditError(string message)
    {
        worldNameEditError.text = message;
        worldNameEditError.gameObject.SetActive(true);
    }

    public void ResumeGame()
    {
        //Debug.Log("Resume");
        IsPaused = false;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void downloadWorld()
    {
        WorldSaveSystem saveSystem = WorldSaveSystem.Instance;
        if (saveSystem == null)
        {
            Debug.LogError("Cannot download the world because its save system is unavailable.");
            return;
        }

        try
        {
            string destinationPath = WorldFileDialog.SaveWorldFile(
                WorldSaveSystem.SanitizeExportFileName(saveSystem.WorldName)
            );
            if (string.IsNullOrWhiteSpace(destinationPath)) return;
            if (!destinationPath.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
            {
                destinationPath += ".json";
            }

            saveSystem.ExportWorld(destinationPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"Could not download world '{saveSystem.WorldName}': {exception.Message}");
        }
    }

    public void ReturnToMainMenu()
    {
        //Debug.Log("Main Menu");
        WorldSaveSystem.Instance?.SaveNow();
        IsPaused = false;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene("MainMenu");
    }
}