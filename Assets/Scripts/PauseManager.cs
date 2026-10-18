using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    private MainMenuStyler menuStyler;
    private TMP_Text currentWorldNameLabel;

    public static bool IsPaused { get; private set; }

    private void Start()
    {
        IsPaused = false;
        pausePanel.SetActive(false);

        EnsureMenuStyler();
        menuStyler.ApplyTo(pausePanel.transform);

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (InventoryGrid.ClosedThisFrame) return;
        if (KeybindSettings.WasPressedThisFrame(KeybindSettings.Pause))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        InventoryGrid.Close();
        Debug.Log("Pause");
        IsPaused = true;

        if (menuStyler == null) EnsureMenuStyler();
        menuStyler.ApplyTo(pausePanel.transform);
        UpdateCurrentWorldNameLabel();
        pausePanel.SetActive(true);

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

        menuStyler = gameObject.AddComponent<MainMenuStyler>();
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
        Debug.Log("Resume");
        IsPaused = false;

        pausePanel.SetActive(false);

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("Main Menu");
        IsPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene("MainMenu");
    }
}