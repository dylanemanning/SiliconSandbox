using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuTraversal : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject LoadPanel;
    public GameObject CreatePanel;

    [Header("Scene")]
    public string gameplaySceneName = "GameScene";

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
        // mainMenuPanel.SetActive(false);
        // settingsPanel.SetActive(true);
    }

    public void closeSettings()
    {
        // settingsPanel.SetActive(false);
        // mainMenuPanel.SetActive(true);
    }

    public void quitGame()
    {
        Application.Quit();
        Debug.Log("Quit requested"); // shows in editor; quit works in build
    }
}
