using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject settingsControlsPage;
    [SerializeField] private GameObject settingsVideoPage;
    [SerializeField] private GameObject settingsAccessibilityPage;
    private MainMenuStyler menuStyler;
    private TMP_Text currentWorldNameLabel;

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